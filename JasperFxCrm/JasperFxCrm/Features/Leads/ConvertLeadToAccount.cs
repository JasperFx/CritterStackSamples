using JasperFxCrm.Features.Accounts;

namespace JasperFxCrm.Features.Leads;

/// <summary>
/// Turns an open lead into a customer: a new Account and a Contact for every person on the
/// lead, the first of them primary. Always a new Account; a lead from an existing
/// customer's domain was routed to that account at capture and never became a lead.
/// </summary>
public sealed record ConvertLeadToAccount(
    Guid LeadId,
    AccountKind Kind,
    string? AccountName,
    string By);
