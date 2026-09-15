# Library Quickstart — Chronicle's "zero to first projection", on Marten

## Reference

**Adapted from:** [Event sourcing in .NET with Chronicle: from zero to first projection](https://blog.cratis.io/blog/event-sourcing-in-dotnet-with-chronicle/) (Cratis blog, Aug 2026)

The Cratis team's quickstart is a tiny library domain: a book arrives, gets borrowed, and comes
back. This sample is the same domain, the same three events, the same two read models, and the
same "reactor" side effect — written against Marten and PostgreSQL instead of Chronicle. Then it
adds a Wolverine.HTTP API to show the command handler before and after Wolverine.

Blog post: [Chronicle's quickstart, on Marten](https://jasperfx.net/news/chronicle-quickstart-on-marten) (jasperfx.net)

## Projects

| Project | What it is |
|---|---|
| `Library/` | Events, the two read models with their `Evolve()` methods, and an `ISubscription` "reactor" |
| `Quickstart/` | A plain console app, no host and no IoC, mirroring the Chronicle post step for step |
| `Api/` | The same domain behind Wolverine.HTTP: `[Aggregate]` command handlers, `[ReadAggregate]` reads, LINQ queries, and the reactor as a Wolverine handler via fast event forwarding |
| `Tests/` | Alba + xUnit integration tests against a throwaway PostgreSQL started by Testcontainers |

## Patterns Demonstrated

### Read models as one `Evolve()` method

```csharp
public record Book(Guid Id, string Title, string Isbn, bool OnLoan, string? BorrowedBy)
{
    public Book Evolve(IEvent e) => e.Data switch
    {
        BookAdded added => new Book(e.StreamId, added.Title, added.Isbn, OnLoan: false, BorrowedBy: null),
        BookBorrowed borrowed => this with { OnLoan = true, BorrowedBy = borrowed.MemberName },
        BookReturned returned => this with { OnLoan = false, BorrowedBy = null },
        _ => this
    };
}
```

`BorrowedBook` returns `null` from `Evolve()` on `BookReturned`, which deletes the document. Both
are registered as `SnapshotLifecycle.Inline`, so they are updated in the same transaction as the
events and are queryable the moment `SaveChangesAsync()` returns — no `Task.Delay()`.

### Command handler, before and after Wolverine

Before (`Quickstart/Program.cs`):

```csharp
var stream = await session.Events.FetchForWriting<Book>(bookId);
if (stream.Aggregate is null) throw ...;
if (stream.Aggregate.OnLoan) throw ...;
stream.AppendOne(new BookBorrowed(memberName));
await session.SaveChangesAsync();
```

After (`Api/Books.cs`):

```csharp
public static ProblemDetails Validate(Book book)
    => book.OnLoan ? new ProblemDetails { Detail = "...", Status = 400 } : WolverineContinue.NoProblems;

[WolverinePost("/books/{bookId}/borrow")]
public static (UpdatedAggregate, BookBorrowed) Borrow(BorrowBook command, [Aggregate] Book book)
    => (new UpdatedAggregate(), new BookBorrowed(command.MemberName));
```

Run `dotnet run -- codegen preview` in `Api/` to see the code Wolverine writes for that endpoint.

### The "reactor" two ways

- `Library/BookReturnedNotifier.cs` — a Marten `ISubscription` run by the async daemon (console app)
- `Api/Books.cs` `BookReturnedHandler` — an ordinary Wolverine handler receiving `IEvent<BookReturned>`
  through `IntegrateWithWolverine(x => x.UseFastEventForwarding = true)`, no daemon required

## Running

Start PostgreSQL from the repo root (`docker compose up -d`, port 5433). Both apps use the
`postgres` database from that container and create their own schema in it.

```bash
cd LibraryQuickstart
dotnet run --project Quickstart      # the console walkthrough
dotnet run --project Api             # the HTTP API on http://localhost:5000
dotnet test                          # Testcontainers starts its own postgres:17
```

The tests do not use the compose container at all — `Testcontainers.PostgreSql` starts a fresh
`postgres:17`, Marten builds its schema in it, and the container is thrown away afterwards.
