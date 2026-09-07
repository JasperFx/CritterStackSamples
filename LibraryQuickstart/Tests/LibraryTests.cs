using Alba;
using Api;
using JasperFx.Events;
using Library;
using Shouldly;
using Wolverine.Http;
using Wolverine.Tracking;

namespace Tests;

[Collection(nameof(AppCollection))]
public class LibraryTests(AppFixture fixture)
{
    private IAlbaHost Host => fixture.Host;

    private async Task<Guid> AddBook(string title = "The Pragmatic Programmer", string isbn = "978-0135957059")
    {
        var result = await Host.Scenario(x =>
        {
            x.Post.Json(new AddBook(title, isbn)).ToUrl("/books");
            x.StatusCodeShouldBe(201);
        });

        return result.ReadAsJson<CreationResponse<Guid>>()!.Value;
    }

    private async Task<Book> GetBook(Guid id)
    {
        var result = await Host.Scenario(x =>
        {
            x.Get.Url($"/books/{id}");
            x.StatusCodeShouldBe(200);
        });

        return result.ReadAsJson<Book>()!;
    }

    private async Task<IReadOnlyList<BorrowedBook>> OnLoan()
    {
        var result = await Host.Scenario(x => x.Get.Url("/books/on-loan"));
        return result.ReadAsJson<IReadOnlyList<BorrowedBook>>()!;
    }

    [Fact]
    public async Task the_whole_loop_add_borrow_return()
    {
        var bookId = await AddBook();

        var added = await GetBook(bookId);
        added.Title.ShouldBe("The Pragmatic Programmer");
        added.OnLoan.ShouldBeFalse();

        // Borrow: the Book read model flips and a BorrowedBook appears, in the same transaction
        var borrowResult = await Host.Scenario(x =>
        {
            x.Post.Json(new BorrowBook("Jane Doe")).ToUrl($"/books/{bookId}/borrow");
            x.StatusCodeShouldBe(200);
        });

        var borrowed = borrowResult.ReadAsJson<Book>()!;
        borrowed.OnLoan.ShouldBeTrue();
        borrowed.BorrowedBy.ShouldBe("Jane Doe");

        (await OnLoan()).ShouldContain(x => x.Id == bookId && x.MemberName == "Jane Doe");

        // Return: track the Wolverine activity so we can prove the "reactor" handler ran
        var tracked = await Host.ExecuteAndWaitAsync(() => Host.Scenario(x =>
        {
            x.Post.Url($"/books/{bookId}/return");
            x.StatusCodeShouldBe(200);
        }));

        var forwarded = tracked.Executed.SingleMessage<IEvent<BookReturned>>();
        forwarded.StreamId.ShouldBe(bookId);

        var returned = await GetBook(bookId);
        returned.OnLoan.ShouldBeFalse();
        returned.BorrowedBy.ShouldBeNull();

        (await OnLoan()).ShouldNotContain(x => x.Id == bookId);
    }

    [Fact]
    public async Task cannot_borrow_a_book_that_is_already_on_loan()
    {
        var bookId = await AddBook("Domain-Driven Design", "978-0321125215");

        await Host.Scenario(x =>
        {
            x.Post.Json(new BorrowBook("Jane Doe")).ToUrl($"/books/{bookId}/borrow");
            x.StatusCodeShouldBe(200);
        });

        var result = await Host.Scenario(x =>
        {
            x.Post.Json(new BorrowBook("John Smith")).ToUrl($"/books/{bookId}/borrow");
            x.StatusCodeShouldBe(400);
        });

        result.ReadAsText().ShouldContain("already on loan");

        // and the read model still says Jane has it
        (await GetBook(bookId)).BorrowedBy.ShouldBe("Jane Doe");
    }

    [Fact]
    public async Task borrowing_an_unknown_book_is_a_404()
    {
        await Host.Scenario(x =>
        {
            x.Post.Json(new BorrowBook("Jane Doe")).ToUrl($"/books/{Guid.NewGuid()}/borrow");
            x.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task a_returned_book_can_be_borrowed_again()
    {
        var bookId = await AddBook("Refactoring", "978-0134757599");

        await Host.Scenario(x => x.Post.Json(new BorrowBook("Jane Doe")).ToUrl($"/books/{bookId}/borrow"));
        await Host.Scenario(x => x.Post.Url($"/books/{bookId}/return"));
        await Host.Scenario(x => x.Post.Json(new BorrowBook("John Smith")).ToUrl($"/books/{bookId}/borrow"));

        (await GetBook(bookId)).BorrowedBy.ShouldBe("John Smith");
        (await OnLoan()).ShouldContain(x => x.Id == bookId && x.MemberName == "John Smith");
    }
}
