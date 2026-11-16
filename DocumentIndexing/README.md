# Document Indexing — indexes inside the JSON, on Marten and Polecat

## Reference

**Original.** Two console apps that store the same `Order` document in Marten (PostgreSQL) and in
Polecat (SQL Server 2025), declare indexes on members of the JSON, and print what the database
actually does with them: the SQL from a LINQ query, the query plan, and the migration each tool
computes when the index configuration changes.

Blog post: [Indexing JSON Documents in Marten and Polecat](https://jasperfx.net/news/indexing-json-documents-marten-polecat) (jasperfx.net)

## Projects

| Project | What it is |
|---|---|
| `MartenIndexing/` | Marten 9.61 against PostgreSQL 17. Computed, nested, multi-property, unique and partial indexes, a duplicated field, GIN indexes over the document and over one child collection, and a GiST full text index |
| `PolecatIndexing/` | Polecat 5.50 against SQL Server 2025. Computed column indexes: single, nested, multi-property, unique, covering (`INCLUDE`) and descending |

Each project has the same three stages in `Program.cs`:

1. Store 100,000 orders with no indexes and explain a query. It scans the table.
2. Build a second store with `OrderIndexes.Configure()`, print the migration it detects, apply it,
   and explain a query for each index.
3. Build a third store with one index definition changed and print that migration too.

## Running

```bash
docker compose up -d

dotnet run --project MartenIndexing
dotnet run --project PolecatIndexing
```

PostgreSQL is on port 5455 and SQL Server on 14455, so neither collides with a database you
already have running. Every run starts from nothing, and the seed data uses a fixed random seed,
so the row counts are the same every time.

The Polecat run takes a few minutes on Apple Silicon, where the SQL Server image runs under
emulation and most of the time goes to inserting the 100,000 documents.

## Patterns Demonstrated

### Reading the SQL and the plan from a LINQ query (Marten)

```csharp
var query = session.Query<Order>().Where(x => x.Lines.Any(l => l.Sku == "SKU-0042"));

// The SQL, without running it
var command = query.ToCommand(FetchType.FetchMany);

// The plan, here with ANALYZE so that the query is run
var plan = await query.ExplainAsync(configureExplain: x => x.Analyze());
```

`QueryPlan` does not carry the index name yet ([marten#6018](https://github.com/JasperFx/marten/issues/6018)),
so the sample also runs a plain `explain` over the same command and prints PostgreSQL's own text.

### Reading the SQL and the plan (Polecat)

```csharp
var sql = session.ToSql(session.Query<Order>().Where(x => x.CustomerEmail == email));

var plan = await ((IDocumentStoreDiagnostics)store).ExplainDocumentQueryAsync(
    typeof(Order).FullName!,
    new DocumentQueryOptions(1, 1000) { Where = "CustomerEmail = @0", Arguments = [email] },
    CancellationToken.None);

// plan.IndexesUsed, plan.ScansTable, plan.RowsExamined, plan.RowsReturned
```

### Seeing the migration before applying it

```csharp
var migration = await store.Storage.Database.CreateMigrationAsync();   // store.Database in Polecat

var writer = new StringWriter();
migration.WriteAllUpdates(writer, store.Storage.Database.Migrator, AutoCreate.CreateOrUpdate);
```

In an application you would use `dotnet run -- db-patch` instead, which writes the same thing to a
file with a rollback script beside it.

## Things the sample ran into

- In Marten, declare `GinIndexJsonData()` before `GinIndexJsonDataMember()`. The other order loses
  the member index ([marten#6017](https://github.com/JasperFx/marten/issues/6017)).
- In Polecat, a `Casing = IndexCasing.Lower` index is not used by a LINQ `ToLower()` comparison
  ([polecat#1030](https://github.com/JasperFx/polecat/issues/1030)), so stage 3 changes the SQL
  type of the index instead.
- Polecat's docs show `queryable.ToSql()`; the method is `session.ToSql(queryable)`
  ([polecat#1029](https://github.com/JasperFx/polecat/issues/1029)).
