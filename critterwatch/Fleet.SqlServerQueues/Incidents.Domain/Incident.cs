namespace Incidents.Domain;

/// <summary>
/// Incident aggregate — single-stream snapshot ported from the upstream Wolverine IncidentService sample.
/// Polecat snapshots this inline (see Incidents.Service Program.cs); the multi-stream
/// <see cref="IncidentsByCategoryProjection"/> runs async alongside it.
///
/// <para>
/// The self-applying aggregate (<c>Apply</c> / <c>ShouldDelete</c> methods on the document) is
/// store-agnostic — Polecat and Marten both honor the same convention, so this file is identical to the
/// Marten flagship.
/// </para>
/// </summary>
public class Incident
{
    public Guid Id { get; set; }

    public IncidentStatus Status { get; set; } = IncidentStatus.Pending;

    public IncidentCategory? Category { get; set; }

    public IncidentPriority? Priority { get; set; }

    public Guid? AssignedAgentId { get; set; }

    /// <summary>True while the agent owes the customer a reply. Flipped back the next time the agent responds.</summary>
    public bool HasOutstandingResponseToCustomer { get; set; }

    public bool Archived { get; set; }

    public void Apply(IncidentLogged @event) => Status = IncidentStatus.Pending;

    public void Apply(IncidentCategorised @event) => Category = @event.Category;

    public void Apply(IncidentPrioritised @event) => Priority = @event.Priority;

    public void Apply(AgentAssignedToIncident @event) => AssignedAgentId = @event.AgentId;

    public void Apply(AgentRespondedToIncident @event) => HasOutstandingResponseToCustomer = false;

    public void Apply(CustomerRespondedToIncident @event) => HasOutstandingResponseToCustomer = true;

    public void Apply(IncidentResolved @event) => Status = IncidentStatus.Resolved;

    public void Apply(ResolutionAcknowledgedByCustomer @event) =>
        Status = IncidentStatus.ResolutionAcknowledgedByCustomer;

    public void Apply(IncidentClosed @event) => Status = IncidentStatus.Closed;

    public void Apply(IncidentArchived @event) => Archived = true;

    /// <summary>Archives the stream once <see cref="IncidentArchived"/> is appended.</summary>
    public bool ShouldDelete(IncidentArchived @event) => true;
}
