namespace Speakers;

// --- Domain Events ---

public record SpeakerRegistered(Guid SpeakerId, string Email, string FirstName, string LastName, SpeakerType Type);
public record SpeakerProfileUpdated(Guid SpeakerId, string FirstName, string LastName, string? Bio, string? Goals,
    string? HeadshotUrl, List<string> Expertise, List<SocialLink> SocialLinks);
public record MentoringAvailabilityChanged(Guid SpeakerId, bool IsAvailable, int MaxMentees, string? Focus);

// --- Aggregate (event-sourced) ---

public class Speaker
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string? Bio { get; set; }
    public string? Goals { get; set; }
    public string? HeadshotUrl { get; set; }
    public SpeakerType Type { get; set; }
    public List<string> Expertise { get; set; } = [];
    public List<SocialLink> SocialLinks { get; set; } = [];
    public bool IsAvailableForMentoring { get; set; }
    public int MaxMentees { get; set; }
    public string? MentorshipFocus { get; set; }

    public void Apply(SpeakerRegistered e)
    {
        Id = e.SpeakerId;
        Email = e.Email;
        FirstName = e.FirstName;
        LastName = e.LastName;
        Type = e.Type;
    }

    public void Apply(SpeakerProfileUpdated e)
    {
        FirstName = e.FirstName;
        LastName = e.LastName;
        Bio = e.Bio;
        Goals = e.Goals;
        HeadshotUrl = e.HeadshotUrl;
        Expertise = e.Expertise;
        SocialLinks = e.SocialLinks;
    }

    public void Apply(MentoringAvailabilityChanged e)
    {
        IsAvailableForMentoring = e.IsAvailable;
        MaxMentees = e.MaxMentees;
        MentorshipFocus = e.Focus;
    }
}

public enum SpeakerType { New, Experienced }

public class SocialLink
{
    public string Platform { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
