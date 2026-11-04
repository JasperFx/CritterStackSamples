namespace JasperFxCrm.Features.Leads;

/// <summary>
/// Who hears about new leads and new messages on them. Configuration, because today it is
/// one person and tomorrow it is a short list.
/// </summary>
public sealed class LeadNotificationSettings
{
    public List<string> LeadRecipients { get; set; } = [];
}
