using JasperFxCrm.Features.Catalog;

namespace JasperFxCrm.Features.Leads;

#region sample_capture_lead_command
/// <summary>
/// The website's contact form, and the admin's "enter a lead" form. Replaces the
/// Netlify form: the submission lands in the CRM first, then JasperFx is emailed.
/// </summary>
public sealed record CaptureLead(
    LeadSource Source,
    string Name,
    string Email,
    string? CompanyName,
    string? Message,
    IReadOnlyList<Offering> Interests,
    /// <summary>Cloudflare Turnstile token; required on the public form, absent from the admin one.</summary>
    string? TurnstileToken = null,
    /// <summary>The admin who keyed the lead in; null from the public form.</summary>
    string? By = null);

/// <summary>
/// Somebody from an existing customer used the contact form. That is not a lead; it is a
/// touchpoint on their account, and the Engagement chapter records it.
/// </summary>
public sealed record ExistingCustomerWroteIn(
    Guid AccountId,
    string Name,
    string Email,
    string? Message,
    IReadOnlyList<Offering> Interests,
    DateTimeOffset ReceivedAt);
#endregion

#region sample_lead_intake_decider
/// <summary>
/// The dynamic consistency boundary CaptureLead decides against. It spans every open lead
/// and every account: which open lead, if any, already holds this email or this company
/// domain, and which account already has a contact there.
/// </summary>
public sealed class LeadIntake
{
    public Guid? OpenLeadWithEmail { get; set; }
    public Guid? OpenLeadWithDomain { get; set; }
    public Guid? AccountWithDomain { get; set; }
    public Guid? AccountWithEmail { get; set; }
}
#endregion
