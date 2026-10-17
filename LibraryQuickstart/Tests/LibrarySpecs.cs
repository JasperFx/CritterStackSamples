using Api;
using Bobcat;
using Bobcat.Xunit;
using JasperFx.Events;
using Library;
using Wolverine.Tracking;

namespace Tests;

/// <summary>
/// The quickstart's loop as Bobcat specifications: the original end-to-end "add, borrow, return" test split
/// into one specification per command, each arranging the book's history as events instead of through earlier
/// requests, plus the two refusals. The one remaining original test is in <see cref="LibraryTests" />.
/// </summary>
[BobcatFeature("Library")]
public class LibrarySpecs(AppFixture app) : LibrarySpec(app)
{
    [BobcatSpec(typeof(AddBook))]
    public async Task adding_a_book_starts_its_stream()
    {
        await WhenPosted(new AddBook("The Pragmatic Programmer", "978-0135957059"), "/books");

        ThenTheResponseStatusIs(201);
        await ThenStreamIsStartedWithEvents<Book>(new BookAdded("The Pragmatic Programmer", "978-0135957059"));
        await ThenReadModel<Book>(TheStartedStream<Book>(), Specify<Book>()
            .With(x => x.Title, "The Pragmatic Programmer")
            .With(x => x.OnLoan, false));
    }

    [BobcatSpec(typeof(BorrowBook))]
    public async Task borrowing_a_book_puts_it_on_loan()
    {
        var bookId = Guid.NewGuid();
        await GivenEvents<Book>(bookId, new BookAdded("The Pragmatic Programmer", "978-0135957059"));

        await WhenPosted(new BorrowBook("Jane Doe"), $"/books/{bookId}/borrow");

        // The Book read model flips and a BorrowedBook appears, in the same transaction
        ThenTheResponseStatusIs(200);
        ThenEvents(new BookBorrowed("Jane Doe"));
        await ThenReadModel<Book>(bookId, Specify<Book>().With(x => x.OnLoan, true).With(x => x.BorrowedBy, "Jane Doe"));
        await ThenReadModel<BorrowedBook>(bookId, Specify<BorrowedBook>().With(x => x.MemberName, "Jane Doe"));
    }

    [BobcatSpec]
    public async Task returning_a_book_takes_it_off_loan_and_tells_the_reactor()
    {
        var bookId = Guid.NewGuid();
        await GivenEvents<Book>(bookId,
            new BookAdded("The Pragmatic Programmer", "978-0135957059"),
            new BookBorrowed("Jane Doe"));

        await WhenPostedTo($"/books/{bookId}/return");

        ThenTheResponseStatusIs(200);
        ThenEvents(new BookReturned());

        // Fast event forwarding hands the committed event to BookReturnedHandler, the "reactor"
        ThenMessageSent<IEvent<BookReturned>>();
        ThenTheReactorHeardTheReturnOf(bookId);

        await ThenReadModel<Book>(bookId, Specify<Book>().With(x => x.OnLoan, false).With(x => x.BorrowedBy, null));
        await ThenNoReadModel<BorrowedBook>(bookId);
    }

    [BobcatSpec(typeof(BorrowBook))]
    public async Task cannot_borrow_a_book_that_is_already_on_loan()
    {
        var bookId = Guid.NewGuid();
        await GivenEvents<Book>(bookId,
            new BookAdded("Domain-Driven Design", "978-0321125215"),
            new BookBorrowed("Jane Doe"));

        await WhenPosted(new BorrowBook("John Smith"), $"/books/{bookId}/borrow");

        ThenTheResponseStatusIs(400);
        ThenTheResponseSays("already on loan");
        ThenNoEvents();

        // and the read model still says Jane has it
        await ThenReadModel<Book>(bookId, Specify<Book>().With(x => x.BorrowedBy, "Jane Doe"));
    }

    [BobcatSpec(typeof(BorrowBook))]
    public async Task borrowing_an_unknown_book_is_a_404()
    {
        await WhenPosted(new BorrowBook("Jane Doe"), $"/books/{Guid.NewGuid()}/borrow");

        ThenTheResponseStatusIs(404);
        ThenNoEvents();
    }

    [Then("the reactor heard that book {bookId} was returned")]
    public void ThenTheReactorHeardTheReturnOf(Guid bookId)
    {
        // No vocabulary step reads what a handler executed, or an IEvent<T> envelope's metadata
        var executed = LastAct.Session!.Executed.MessagesOf<IEvent<BookReturned>>().ToArray();
        SpecAssert.Check("reactor runs", executed.Length, 1);
        SpecAssert.Check("stream", executed.FirstOrDefault()?.StreamId, bookId);
    }
}
