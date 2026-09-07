namespace Library;

// Events are plain records. Marten discovers the type from the append
// and uses the type name as the event type identity -- no attribute needed.
public record BookAdded(string Title, string Isbn);

public record BookBorrowed(string MemberName);

public record BookReturned;
