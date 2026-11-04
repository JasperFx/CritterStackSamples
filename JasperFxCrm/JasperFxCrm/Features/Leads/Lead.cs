using JasperFxCrm.Features.Catalog;

namespace JasperFxCrm.Features.Leads;

public enum LeadStatus
{
    /// <summary>Somebody wrote in and nobody has decided yet.</summary>
    Open,
    Disqualified,
    Converted
}

public enum LeadSource
{
    WebForm,
    Conference,
    Referral,
    Inbound,
    Manual
}

/// <summary>One person on a lead. The first is whoever wrote in first.</summary>
public sealed record LeadContact(string Name, string Email, string NormalizedEmail);

/// <summary>A message somebody on the lead sent, kept in the order received.</summary>
public sealed record LeadMessage(string FromNormalizedEmail, string Text, DateTimeOffset ReceivedAt);

#region sample_lead_aggregate
/// <summary>
/// A company, or a person on free-mail, that has shown interest and is not a customer yet.
/// Several people from one company are one lead; the lead holds all of them.
/// </summary>
public sealed class Lead
{
    public Guid Id { get; set; }
    public LeadStatus Status { get; set; } = LeadStatus.Open;
    public LeadSource Source { get; set; }
    public string? CompanyName { get; set; }

    /// <summary>The email domain when it identifies a company; null for free-mail leads.</summary>
    public string? CompanyDomain { get; set; }

    public List<LeadContact> Contacts { get; set; } = [];
    public List<LeadMessage> Messages { get; set; } = [];
    public HashSet<Offering> Interests { get; set; } = [];

    public DateTimeOffset CapturedAt { get; set; }
    public DateTimeOffset? DisqualifiedAt { get; set; }
    public string? DisqualifyReason { get; set; }
    public DateTimeOffset? ConvertedAt { get; set; }
    public Guid? AccountId { get; set; }

    public static Lead Create(LeadCaptured e) => new()
    {
        Id = e.LeadId,
        Source = e.Source,
        CompanyName = e.CompanyName,
        CompanyDomain = e.CompanyDomain,
        Contacts = [e.Contact],
        Messages = e.Message is null ? [] : [new LeadMessage(e.Contact.NormalizedEmail, e.Message, e.CapturedAt)],
        Interests = [..e.Interests],
        CapturedAt = e.CapturedAt
    };

    public void Apply(LeadContactAdded e)
    {
        Contacts.Add(e.Contact);
        if (e.Message is not null) Messages.Add(new LeadMessage(e.Contact.NormalizedEmail, e.Message, e.AddedAt));
        Interests.UnionWith(e.Interests);
    }

    public void Apply(LeadMessageReceived e)
    {
        Messages.Add(new LeadMessage(e.FromNormalizedEmail, e.Message, e.ReceivedAt));
        Interests.UnionWith(e.Interests);
    }

    public void Apply(LeadDisqualified e)
    {
        Status = LeadStatus.Disqualified;
        DisqualifiedAt = e.DisqualifiedAt;
        DisqualifyReason = e.Reason;
    }

    public void Apply(LeadReopened e)
    {
        Status = LeadStatus.Open;
        DisqualifiedAt = null;
        DisqualifyReason = null;
    }

    public void Apply(LeadConvertedToAccount e)
    {
        Status = LeadStatus.Converted;
        ConvertedAt = e.ConvertedAt;
        AccountId = e.AccountId;
    }

    public void Apply(LeadNotificationSent e)
    {
        // Recorded on the stream for the timeline; nothing to fold.
    }
}
#endregion
