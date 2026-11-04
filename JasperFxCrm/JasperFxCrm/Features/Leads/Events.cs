using JasperFxCrm.Features.Catalog;

namespace JasperFxCrm.Features.Leads;

#region sample_lead_events
/// <summary>Somebody from a company (or person) we had no open lead for wrote in.</summary>
public sealed record LeadCaptured(
    Guid LeadId,
    LeadSource Source,
    LeadContact Contact,
    string? CompanyName,
    string? CompanyDomain,
    string? Message,
    IReadOnlyList<Offering> Interests,
    DateTimeOffset CapturedAt,
    string? By);

/// <summary>A second person from the same company wrote in while the lead was open.</summary>
public sealed record LeadContactAdded(
    Guid LeadId,
    LeadContact Contact,
    string? Message,
    IReadOnlyList<Offering> Interests,
    DateTimeOffset AddedAt);

/// <summary>Somebody already on the lead wrote in again.</summary>
public sealed record LeadMessageReceived(
    Guid LeadId,
    string FromNormalizedEmail,
    string Message,
    IReadOnlyList<Offering> Interests,
    DateTimeOffset ReceivedAt);

public sealed record LeadDisqualified(Guid LeadId, string Reason, DateTimeOffset DisqualifiedAt, string By);

public sealed record LeadReopened(Guid LeadId, DateTimeOffset ReopenedAt, string By);

/// <summary>
/// The lead became a customer: one Account, and one Contact per person on the lead.
/// </summary>
public sealed record LeadConvertedToAccount(
    Guid LeadId,
    Guid AccountId,
    IReadOnlyList<Guid> ContactIds,
    DateTimeOffset ConvertedAt,
    string By);

/// <summary>JasperFx was told about the lead (or about a new message on it).</summary>
public sealed record LeadNotificationSent(Guid LeadId, IReadOnlyList<string> To, DateTimeOffset SentAt);
#endregion
