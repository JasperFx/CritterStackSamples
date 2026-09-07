using JasperFx.Events;

namespace Library;

/// <summary>
/// The "what is out on loan right now?" read model. It exists only while a loan is active:
/// created by BookBorrowed, deleted by BookReturned (returning null from Evolve deletes the document).
/// </summary>
public record BorrowedBook(Guid Id, string MemberName)
{
    public BorrowedBook? Evolve(IEvent e) => e.Data switch
    {
        BookBorrowed borrowed => new BorrowedBook(e.StreamId, borrowed.MemberName),
        BookReturned returned => null,
        _ => this
    };
}
