namespace CqrsMinimalApi;

/// <summary>
/// Student document stored in Marten. Uses int identity with HiLo sequence.
/// </summary>
public class Student
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Email { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public bool Active { get; set; } = true;
}
