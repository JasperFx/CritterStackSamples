namespace JasperFxCrm.Features.Leads;

public sealed record DisqualifyLead(Guid LeadId, string Reason, string By);

public sealed record ReopenLead(Guid LeadId, string By);
