using JasperFx;
using Marten;
using Marten.Linq;
using Marten.Schema;
using MartenIndexing;
using Npgsql;
using Weasel.Postgresql.Tables;

const string connectionString =
    "Host=localhost;Port=5455;Database=postgres;Username=postgres;Password=postgres";

// ---------------------------------------------------------------------------------------------
// Stage 1: no indexes at all, just the primary key Marten always gives you
// ---------------------------------------------------------------------------------------------
await using (var bare = DocumentStore.For(opts =>
             {
                 opts.Connection(connectionString);
                 opts.DatabaseSchemaName = "indexing";
                 opts.RegisterDocumentType<Order>();
             }))
{
    await bare.Advanced.Clean.CompletelyRemoveAllAsync();
    await bare.BulkInsertAsync(SampleData.Orders(100_000), batchSize: 5000);
    await Analyze();

    Banner("Stage 1: 100,000 orders and no indexes");

    await using var session = bare.QuerySession();
    await Explain("Orders for one customer",
        session.Query<Order>().Where(x => x.CustomerEmail == "Customer42@Example.com"));
}

// ---------------------------------------------------------------------------------------------
// Stage 2: the same document type, now with indexes declared. Nothing has touched the database
// yet, so first ask Marten what it would change.
// ---------------------------------------------------------------------------------------------
await using (var store = DocumentStore.For(opts =>
             {
                 opts.Connection(connectionString);
                 opts.DatabaseSchemaName = "indexing";

                 // The migration is run explicitly below, so we can look at it first
                 opts.AutoCreateSchemaObjects = AutoCreate.None;

                 OrderIndexes.Configure(opts);
             }))
{
    Banner("Stage 2: the migration Marten detects after indexes are declared");
    await PrintMigration(store);

    await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    await Analyze();

    // Now there is nothing left to do, and this would throw if there were
    await store.Storage.Database.AssertDatabaseMatchesConfigurationAsync();
    Console.WriteLine("The database matches the configuration.");

    await using var session = store.QuerySession();

    Banner("A basic index on one property");
    await Explain("Orders for one customer",
        session.Query<Order>().Where(x => x.CustomerEmail == "Customer42@Example.com"));

    Banner("A nested property");
    await Explain("Orders shipped to one postal code",
        session.Query<Order>().Where(x => x.ShippingAddress.PostalCode == "40213"));

    Banner("A multi-property index");
    await Explain("Shipped orders in EMEA",
        session.Query<Order>().Where(x => x.Region == "EMEA" && x.Status == OrderStatus.Shipped));

    Banner("A partial index");
    await Explain("Open orders bound for Oslo",
        session.Query<Order>().Where(x => x.Status == OrderStatus.Open && x.ShippingAddress.City == "Oslo"));

    Banner("A duplicated field for a DateTimeOffset, sorted descending");
    await Explain("The ten newest orders",
        session.Query<Order>().OrderByDescending(x => x.PlacedAt).Take(10));

    Banner("A unique index");
    await TryToStoreADuplicateOrderNumber(store);

    Banner("Child collections: equality becomes JSONB containment, and the GIN index serves it");
    await Explain("Orders with a line for SKU-0042",
        session.Query<Order>().Where(x => x.Lines.Any(l => l.Sku == "SKU-0042")));

    await Explain("Orders with a line for exactly 12 of SKU-0042",
        session.Query<Order>().Where(x => x.Lines.Any(l => l.Sku == "SKU-0042" && l.Quantity == 12)));

    await Explain("Orders with a line for SKU-0042 or SKU-0043",
        session.Query<Order>().Where(x => x.Lines.Any(l => l.Sku == "SKU-0042" || l.Sku == "SKU-0043")));

    Banner("Child collections: a range comparison becomes JSONPath");
    await Explain("Orders with a line for 10 or more of SKU-0042",
        session.Query<Order>().Where(x => x.Lines.Any(l => l.Sku == "SKU-0042" && l.Quantity >= 10)));

    Banner("Full text search over a GiST index");
    await Explain("Orders to be left with a neighbor",
        session.Query<Order>().Where(x => x.PlainTextSearch("neighbor")));

    Banner("Arrays of simple values use the whole-document GIN index");
    await Explain("Orders tagged as backorder and international",
        session.Query<Order>().Where(x => x.Tags.Contains("backorder") && x.Tags.Contains("international")));
}

// ---------------------------------------------------------------------------------------------
// Stage 3: change one index definition and add another. Marten compares the configuration to the
// database again and finds only the difference.
// ---------------------------------------------------------------------------------------------
await using (var store = DocumentStore.For(opts =>
             {
                 opts.Connection(connectionString);
                 opts.DatabaseSchemaName = "indexing";
                 opts.AutoCreateSchemaObjects = AutoCreate.None;

                 // The same indexes as before, except that the one on
                 // CustomerEmail is now on lower(CustomerEmail)
                 OrderIndexes.Configure(opts, caseInsensitiveEmail: true);
             }))
{
    Banner("Stage 3: one index definition changed");
    await PrintMigration(store);

    await store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    await Analyze();

    await using var session = store.QuerySession();
    await Explain("Orders for one customer, whatever the casing",
        session.Query<Order>().Where(x => x.CustomerEmail.ToLower() == "customer42@example.com"));
}

return;

// ---------------------------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------------------------

async Task Explain<T>(string title, IQueryable<T> query) where T : notnull
{
    Console.WriteLine();
    Console.WriteLine($"-- {title}");

    // 1. The SQL Marten will send, without sending it
    var command = query.ToCommand(FetchType.FetchMany);
    Console.WriteLine(command.CommandText);
    foreach (NpgsqlParameter parameter in command.Parameters)
    {
        Console.WriteLine($"   {parameter.ParameterName} = {parameter.Value}");
    }

    // 2. Marten's own ExplainAsync(), here with ANALYZE so that the query is actually run
    var plan = await query.ExplainAsync(configureExplain: x => x.Analyze());
    Console.WriteLine(
        $"   => {Describe(plan)}, {plan.ActualRows} rows, cost {plan.TotalCost}, {plan.ExecutionTime} ms");

    // 3. The plan as PostgreSQL prints it, which names the index
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();
    command.Connection = conn;
    command.CommandText = "explain (costs off) " + command.CommandText;
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        Console.WriteLine("   " + reader.GetString(0));
    }
}

// The node types of the plan, outermost first: "Bitmap Heap Scan > Bitmap Index Scan"
static string Describe(QueryPlan plan)
{
    return plan.Plans is { Length: > 0 }
        ? $"{plan.NodeType} > {string.Join(" + ", plan.Plans.Select(Describe))}"
        : plan.NodeType;
}

async Task PrintMigration(IDocumentStore store)
{
    var migration = await store.Storage.Database.CreateMigrationAsync();
    Console.WriteLine($"Difference: {migration.Difference}");

    var writer = new StringWriter();
    migration.WriteAllUpdates(writer, store.Storage.Database.Migrator, AutoCreate.CreateOrUpdate);
    Console.WriteLine(writer.ToString().Trim());
}

async Task TryToStoreADuplicateOrderNumber(IDocumentStore store)
{
    await using var session = store.LightweightSession();
    session.Store(new Order { Id = Guid.NewGuid(), OrderNumber = "ORD-000042" });

    try
    {
        await session.SaveChangesAsync();
    }
    catch (Exception e)
    {
        Console.WriteLine($"{e.GetType().Name}: {e.Message}");
    }
}

// Fresh planner statistics, so that the plans do not depend on when autovacuum last ran
async Task Analyze()
{
    await using var conn = new NpgsqlConnection(connectionString);
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand("vacuum analyze indexing.mt_doc_order", conn);
    await cmd.ExecuteNonQueryAsync();
}

static void Banner(string text)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 100));
    Console.WriteLine(text);
    Console.WriteLine(new string('=', 100));
}
