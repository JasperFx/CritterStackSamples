using JasperFx.Events;
using Library;
using Marten;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;
using Wolverine.Http.Marten;
using Wolverine.Marten;

namespace Api;

public record AddBook(string Title, string Isbn);

public record BorrowBook(string MemberName);

public static class AddBookEndpoint
{
    // Starting a stream. The return tuple is "201 Created with the new id" + "start this stream".
    [WolverinePost("/books")]
    public static (CreationResponse<Guid>, IStartStream) Add(AddBook command)
    {
        var start = MartenOps.StartStream<Book>(new BookAdded(command.Title, command.Isbn));
        return (new CreationResponse<Guid>($"/books/{start.StreamId}", start.StreamId), start);
    }
}

public static class BorrowBookEndpoint
{
    // After Wolverine: [Aggregate] loads the Book from its stream (FetchForWriting, with
    // optimistic concurrency), Validate() runs against the loaded state, and the returned
    // event is appended and committed by the transactional middleware.
    public static ProblemDetails Validate(Book book)
    {
        if (book.OnLoan)
        {
            return new ProblemDetails { Detail = $"{book.Title} is already on loan", Status = 400 };
        }

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/books/{bookId}/borrow")]
    public static (UpdatedAggregate, BookBorrowed) Borrow(BorrowBook command, [Aggregate] Book book)
        => (new UpdatedAggregate(), new BookBorrowed(command.MemberName));
}

public static class ReturnBookEndpoint
{
    [WolverinePost("/books/{bookId}/return")]
    public static (UpdatedAggregate, BookReturned) Return([Aggregate] Book book)
        => (new UpdatedAggregate(), new BookReturned());
}

public static class BookQueries
{
    // [ReadAggregate] uses FetchLatest, so this is current even if there are events
    // that an async projection hasn't caught up with yet.
    [WolverineGet("/books/{bookId}")]
    public static Book Get([ReadAggregate] Book book) => book;

    // Read models are plain documents, so the query side is just LINQ
    [WolverineGet("/books/on-loan")]
    public static Task<IReadOnlyList<BorrowedBook>> OnLoan(IQuerySession session)
        => session.Query<BorrowedBook>().OrderBy(x => x.MemberName).ToListAsync();

    [WolverineGet("/books")]
    public static Task<IReadOnlyList<Book>> All(IQuerySession session)
        => session.Query<Book>().OrderBy(x => x.Title).ToListAsync();
}

// The "reactor", now as an ordinary Wolverine message handler. Fast event forwarding
// delivers the IEvent<BookReturned> when the session that appended it commits.
// (Wolverine discovers handlers by the *Handler suffix, so the name matters here.)
public static class BookReturnedHandler
{
    public static void Handle(IEvent<BookReturned> e, ILogger<BookReturned> logger)
    {
        logger.LogInformation("Reactor: book {BookId} was returned -- notify the next member in line.", e.StreamId);
    }
}
