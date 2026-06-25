namespace Incidents.Domain;

/// <summary>Lifecycle status of an <see cref="Incident"/>. Powers-of-two layout inherited from the upstream sample.</summary>
public enum IncidentStatus
{
    Pending = 1,
    Resolved = 8,
    ResolutionAcknowledgedByCustomer = 16,
    Closed = 32,
}

public enum IncidentCategory
{
    Software,
    Hardware,
    Network,
    Database,
}

public enum IncidentPriority
{
    Critical,
    High,
    Medium,
    Low,
}

public enum ContactChannel
{
    Email,
    Phone,
    InPerson,
    Generative,
}

public enum ResolutionType
{
    Temporary,
    Permanent,
    NotAnIncident,
}

public record Contact(
    ContactChannel Channel,
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber);

public abstract record IncidentResponse(string Content);

public record AgentResponse(Guid AgentId, string Content, bool VisibleToCustomer) : IncidentResponse(Content);

public record CustomerResponse(Guid CustomerId, string Content) : IncidentResponse(Content);

// --- Events ---------------------------------------------------------------
// Past-tense immutable records, shared through Incidents.Domain so the projection + handlers + publisher
// agree on the wire shape.

public record IncidentLogged(
    Guid CustomerId,
    Contact Contact,
    string Description,
    Guid LoggedBy);

public record IncidentCategorised(IncidentCategory Category, Guid CategorisedBy);

public record IncidentPrioritised(IncidentPriority Priority, Guid PrioritisedBy);

public record AgentAssignedToIncident(Guid AgentId, DateTimeOffset AssignedAt);

public record AgentRespondedToIncident(AgentResponse Response);

public record CustomerRespondedToIncident(CustomerResponse Response);

/// <summary>
/// Carries the <see cref="IncidentCategory"/> on the event itself so the IncidentsByCategory projection
/// can attribute Resolved counts to the correct bucket without re-fetching the aggregate.
/// </summary>
public record IncidentResolved(
    ResolutionType Resolution,
    Guid ResolvedBy,
    IncidentCategory? Category);

public record ResolutionAcknowledgedByCustomer(Guid AcknowledgedBy);

/// <summary>Carries the category for the same projection reason as <see cref="IncidentResolved"/>.</summary>
public record IncidentClosed(Guid ClosedBy, IncidentCategory? Category);

/// <summary>Terminal archival event — <see cref="Incident.ShouldDelete"/> returns true on it so Marten archives the stream.</summary>
public record IncidentArchived;
