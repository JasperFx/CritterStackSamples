using JasperFx.Events;

namespace Library;

/// <summary>
/// The "what is this book right now?" read model. One document per book stream.
/// The Evolve() method is the whole projection: current snapshot + one event => next snapshot.
/// </summary>
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
