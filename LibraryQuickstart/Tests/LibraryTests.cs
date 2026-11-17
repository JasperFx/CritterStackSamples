using Alba;
using Api;
using Library;
using Shouldly;
using Wolverine.Http;

namespace Tests;

// The original tests, on xUnit v3. The add/borrow/return loop and the two refusals now live in LibrarySpecs as
// Bobcat specifications; this one stays plain because it is a walk through four requests in a row, not one act.
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
