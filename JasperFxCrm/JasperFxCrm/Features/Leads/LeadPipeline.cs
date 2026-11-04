using JasperFxCrm.Features.Catalog;

namespace JasperFxCrm.Features.Leads;

/// <summary>One row of the pipeline board: a lead as the admin scans it.</summary>
public sealed class LeadPipelineRow
{
    public Guid Id { get; set; }
    public LeadStatus Status { get; set; }
    public LeadSource Source { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyDomain { get; set; }
    public string PrimaryContactName { get; set; } = "";
    public string PrimaryContactEmail { get; set; } = "";
    public int ContactCount { get; set; }
    public int MessageCount { get; set; }
    public List<Offering> Interests { get; set; } = [];
    public DateTimeOffset CapturedAt { get; set; }
    public DateTimeOffset LastMessageAt { get; set; }
    public Guid? AccountId { get; set; }
}
