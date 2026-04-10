namespace Mentorships;

// --- Domain Events ---

public record MentorshipRequested(Guid MentorshipId, Guid MentorId, string MentorName,
    Guid MenteeId, string MenteeName, MentorshipType Type,
    List<string> FocusAreas, string? RequestMessage, string? PreferredFrequency);
public record MentorshipAccepted(Guid MentorshipId, string? ResponseMessage);
public record MentorshipDeclined(Guid MentorshipId, string? ResponseMessage);
public record MentorshipCompleted(Guid MentorshipId);
public record MentorshipCancelled(Guid MentorshipId);

// --- Aggregate (event-sourced) ---

public class Mentorship
{
    public Guid Id { get; set; }
    public Guid MentorId { get; set; }
    public string MentorName { get; set; } = string.Empty;
    public Guid MenteeId { get; set; }
    public string MenteeName { get; set; } = string.Empty;
    public MentorshipStatus Status { get; set; }
    public MentorshipType Type { get; set; }
    public List<string> FocusAreas { get; set; } = [];
    public string? RequestMessage { get; set; }
    public string? ResponseMessage { get; set; }
    public string? PreferredFrequency { get; set; }

    public void Apply(MentorshipRequested e)
    {
        Id = e.MentorshipId;
        MentorId = e.MentorId;
        MentorName = e.MentorName;
        MenteeId = e.MenteeId;
        MenteeName = e.MenteeName;
        Type = e.Type;
        FocusAreas = e.FocusAreas;
        RequestMessage = e.RequestMessage;
        PreferredFrequency = e.PreferredFrequency;
        Status = MentorshipStatus.Pending;
    }

    public void Apply(MentorshipAccepted e)
    {
        Status = MentorshipStatus.Active;
        ResponseMessage = e.ResponseMessage;
    }

    public void Apply(MentorshipDeclined e)
    {
        Status = MentorshipStatus.Declined;
        ResponseMessage = e.ResponseMessage;
    }

    public void Apply(MentorshipCompleted _) => Status = MentorshipStatus.Completed;
    public void Apply(MentorshipCancelled _) => Status = MentorshipStatus.Cancelled;
}

public enum MentorshipStatus { Pending, Active, Completed, Cancelled, Declined }
public enum MentorshipType { NewToExperienced, ExperiencedToExperienced }
