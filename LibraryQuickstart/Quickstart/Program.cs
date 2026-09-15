using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Library;
using Marten;
using Marten.Events.Projections;

// A plain console app, no host and no IoC container. This mirrors the Chronicle
// quickstart at https://blog.cratis.io/blog/event-sourcing-in-dotnet-with-chronicle/
// but with Marten + PostgreSQL. `docker compose up -d` at the repo root starts Postgres on 5433.
var connectionString = Environment.GetEnvironmentVariable("MARTEN_CONNECTION")
    ?? "Host=localhost;Port=5433;Database=postgres;Username=postgres;Password=postgres";

await using var store = DocumentStore.For(opts =>
{
    opts.Connection(connectionString);

    // Marten builds every table it needs inside this schema the first time it's used
    opts.DatabaseSchemaName = "library_quickstart";
    opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.All;

    // Let this process run the async daemon for the subscription below
    opts.Projections.AsyncMode = DaemonMode.Solo;

    // Read models. "Inline" means they are updated in the same transaction as the events,
    // so they're queryable the instant SaveChangesAsync() returns.
    opts.Projections.Snapshot<Book>(SnapshotLifecycle.Inline);
    opts.Projections.Snapshot<BorrowedBook>(SnapshotLifecycle.Inline);

    // The "reactor": a subscription that runs in the async daemon after events are committed
    opts.Events.Subscribe(new BookReturnedNotifier(), s =>
    {
        s.Name = "BookReturnedNotifier";
        s.IncludeType<BookReturned>();
    });
});

// Start the async daemon in-process so the subscription runs. Nothing else to deploy.
using var daemon = await store.BuildProjectionDaemonAsync();
await daemon.StartAllAsync();

Console.WriteLine($"Connected to PostgreSQL, schema '{store.Options.DatabaseSchemaName}'");

var bookId = Guid.NewGuid();

await using (var session = store.LightweightSession())
{
    session.Events.StartStream<Book>(bookId, new BookAdded("The Pragmatic Programmer", "978-0135957059"));
    await session.SaveChangesAsync();
    Console.WriteLine("Appended BookAdded");
}

// Appending a BookBorrowed with a guard: this is the raw Marten "command handler" we'll
// hand over to Wolverine in the Api project.
await BorrowBook(store, bookId, "Jane Doe");

// No Task.Delay() here. Inline projections are already committed.
await using (var query = store.QuerySession())
{
    var book = await query.LoadAsync<Book>(bookId);
    Console.WriteLine($"Book read model: {book!.Title} ({book.Isbn}) OnLoan={book.OnLoan} BorrowedBy={book.BorrowedBy}");

    // Read models are ordinary documents, so LINQ works against them
    var onLoan = await query.Query<BorrowedBook>().Where(x => x.MemberName == "Jane Doe").ToListAsync();
    foreach (var loan in onLoan)
    {
        Console.WriteLine($"BorrowedBook read model: {loan.Id} borrowed by {loan.MemberName}");
    }
}

await using (var session = store.LightweightSession())
{
    session.Events.Append(bookId, new BookReturned());
    await session.SaveChangesAsync();
    Console.WriteLine("Appended BookReturned");
}

// The subscription is asynchronous, so *this* is the one place we wait -- and we wait
// for a fact ("the daemon has caught up"), not for a guessed number of seconds.
await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(15));

await using (var query = store.QuerySession())
{
    var book = await query.LoadAsync<Book>(bookId);
    Console.WriteLine($"Book read model after return: {book!.Title} OnLoan={book.OnLoan}");

    var count = await query.Query<BorrowedBook>().CountAsync();
    Console.WriteLine($"BorrowedBook read models after return: {count}");

    // "See the history": the stream is right there in PostgreSQL
    var events = await query.Events.FetchStreamAsync(bookId);
    foreach (var e in events)
    {
        Console.WriteLine($"  v{e.Version} {e.EventTypeName} at {e.Timestamp:HH:mm:ss.fff}");
    }
}

await daemon.StopAllAsync();
return;

// Before Wolverine: load the stream for writing (with optimistic concurrency),
// decide, append, save. This is the code Wolverine's [WriteAggregate] generates for you.
static async Task BorrowBook(IDocumentStore store, Guid bookId, string memberName)
{
    await using var session = store.LightweightSession();

    var stream = await session.Events.FetchForWriting<Book>(bookId);
    if (stream.Aggregate is null) throw new InvalidOperationException($"Unknown book {bookId}");
    if (stream.Aggregate.OnLoan) throw new InvalidOperationException($"{stream.Aggregate.Title} is already on loan");

    stream.AppendOne(new BookBorrowed(memberName));
    await session.SaveChangesAsync();
    Console.WriteLine("Appended BookBorrowed");
}
