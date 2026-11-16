using JasperFx;
using JasperFx.Documents;
using Microsoft.Data.SqlClient;
using Polecat;
using Polecat.Linq;
using PolecatIndexing;

const string connectionString =
    "Server=localhost,14455;Database=master;User Id=sa;Password=P@55w0rd;TrustServerCertificate=true;Command Timeout=120";

// ---------------------------------------------------------------------------------------------
// Stage 1: no indexes at all, just the clustered primary key Polecat always gives you
// ---------------------------------------------------------------------------------------------
await using (var bare = DocumentStore.For(opts =>
             {
                 opts.ConnectionString = connectionString;
                 opts.DatabaseSchemaName = "indexing";
                 opts.Schema.For<Order>();
             }))
{
    await DropSchema();

    // Build everything else Polecat owns now, so that the migration in
    // stage 2 is about nothing but the indexes
    await bare.Database.ApplyAllConfiguredChangesToDatabaseAsync();

    await bare.Advanced.BulkInsertAsync(SampleData.Orders(100_000));

    Banner("Stage 1: 100,000 orders and no indexes");

    await using var session = bare.QuerySession();
    Console.WriteLine(session.ToSql(session.Query<Order>().Where(x => x.CustomerEmail == "Customer42@Example.com")));
    await Explain(bare, "Orders for one customer", "CustomerEmail = @0", ["Customer42@Example.com"]);
}

// ---------------------------------------------------------------------------------------------
// Stage 2: the same document type, now with indexes declared
// ---------------------------------------------------------------------------------------------
await using (var store = DocumentStore.For(opts =>
             {
                 opts.ConnectionString = connectionString;
                 opts.DatabaseSchemaName = "indexing";
                 opts.AutoCreateSchemaObjects = AutoCreate.None;

                 OrderIndexes.Configure(opts);
             }))
{
    Banner("Stage 2: the migration Polecat detects after indexes are declared");
    await PrintMigration(store);

    await store.Database.ApplyAllConfiguredChangesToDatabaseAsync();
    await store.Database.AssertDatabaseMatchesConfigurationAsync();
    Console.WriteLine("The database matches the configuration.");

    await using var session = store.QuerySession();

    Banner("A basic index on one property");
    Console.WriteLine(session.ToSql(session.Query<Order>().Where(x => x.CustomerEmail == "Customer42@Example.com")));
    await Explain(store, "Orders for one customer", "CustomerEmail = @0", ["Customer42@Example.com"]);

    Banner("A nested property");
    Console.WriteLine(session.ToSql(session.Query<Order>().Where(x => x.ShippingAddress.PostalCode == "40213")));
    await Explain(store, "Orders shipped to one postal code", "ShippingAddress.PostalCode = @0", ["40213"]);

    Banner("A multi-property index");
    Console.WriteLine(session.ToSql(session.Query<Order>().Where(x => x.Region == "EMEA" && x.Status == OrderStatus.Open)));
    await Explain(store, "Open orders in EMEA", "Region = @0 and Status = @1", ["EMEA", OrderStatus.Open]);

    Banner("A descending index on a DateTimeOffset");
    Console.WriteLine(session.ToSql(session.Query<Order>().OrderByDescending(x => x.PlacedAt).Take(10)));
    await Explain(store, "The ten newest orders", orderBy: "PlacedAt desc", pageSize: 10);

    Banner("A unique index");
    await TryToStoreADuplicateOrderNumber(store);

    Banner("Arrays of simple values");
    Console.WriteLine(session.ToSql(session.Query<Order>().Where(x => x.Tags.Contains("wholesale"))));
    await Explain(store, "Orders tagged as wholesale", "Tags.Contains(@0)", ["wholesale"]);
}

// ---------------------------------------------------------------------------------------------
// Stage 3: change one index definition
// ---------------------------------------------------------------------------------------------
await using (var store = DocumentStore.For(opts =>
             {
                 opts.ConnectionString = connectionString;
                 opts.DatabaseSchemaName = "indexing";
                 opts.AutoCreateSchemaObjects = AutoCreate.None;

                 OrderIndexes.Configure(opts, unicodeEmail: true);
             }))
{
    Banner("Stage 3: one index definition changed");
    await PrintMigration(store);
    await store.Database.ApplyAllConfiguredChangesToDatabaseAsync();

    await using var session = store.QuerySession();
    Console.WriteLine(session.ToSql(session.Query<Order>().Where(x => x.CustomerEmail == "Customer42@Example.com")));
    await Explain(store, "Orders for one customer", "CustomerEmail = @0", ["Customer42@Example.com"]);
}

return;

// ---------------------------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------------------------

// Polecat reads SQL Server's actual execution plan for a document query and boils it down to
// the questions you would ask of it: which indexes, how many rows read, and was it a scan?
async Task Explain(DocumentStore store, string title, string? where = null, object?[]? arguments = null,
    string? orderBy = null, int pageSize = 1000)
{
    var plan = await ((IDocumentStoreDiagnostics)store).ExplainDocumentQueryAsync(
        typeof(Order).FullName!,
        new DocumentQueryOptions(1, pageSize) { Where = where, Arguments = arguments, OrderBy = orderBy },
        CancellationToken.None);

    Console.WriteLine($"-- {title}");
    Console.WriteLine($"   Indexes used:  {string.Join(", ", plan!.IndexesUsed ?? [])}");
    Console.WriteLine($"   Scans table:   {plan.ScansTable}");
    Console.WriteLine($"   Rows examined: {plan.RowsExamined}");
    Console.WriteLine($"   Rows returned: {plan.RowsReturned}");
    Console.WriteLine();
}

async Task PrintMigration(DocumentStore store)
{
    var migration = await store.Database.CreateMigrationAsync();
    Console.WriteLine($"Difference: {migration.Difference}");

    var writer = new StringWriter();
    migration.WriteAllUpdates(writer, store.Database.Migrator, AutoCreate.CreateOrUpdate);
    Console.WriteLine(writer.ToString().Trim());
}

async Task TryToStoreADuplicateOrderNumber(DocumentStore store)
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

// Start from nothing on every run
async Task DropSchema()
{
    await using var conn = new SqlConnection(connectionString);
    await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "DROP TABLE IF EXISTS indexing.pc_doc_order";
    await cmd.ExecuteNonQueryAsync();
}

static void Banner(string text)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 100));
    Console.WriteLine(text);
    Console.WriteLine(new string('=', 100));
}
