namespace Incidents.Domain;

/// <summary>
/// Logs a new incident. The publisher pre-generates <see cref="IncidentId"/> (version-7 GUID) so the
/// StartStream call on the service can use it as the stream key — mirrors the <c>StartTrip</c> shape.
/// </summary>
public record LogIncident(
    Guid IncidentId,
    Guid CustomerId,
    Contact Contact,
    string Description,
    Guid LoggedBy);

public record CategoriseIncident(Guid IncidentId, IncidentCategory Category, Guid CategorisedBy);

public record PrioritiseIncident(Guid IncidentId, IncidentPriority Priority, Guid PrioritisedBy);

public record AssignAgentToIncident(Guid IncidentId, Guid AgentId);

public record RecordAgentResponseToIncident(Guid IncidentId, AgentResponse Response);

public record RecordCustomerResponseToIncident(Guid IncidentId, CustomerResponse Response);

public record ResolveIncident(Guid IncidentId, ResolutionType Resolution, Guid ResolvedBy);

public record AcknowledgeResolution(Guid IncidentId, Guid AcknowledgedBy);

public record CloseIncident(Guid IncidentId, Guid ClosedBy);

public record ArchiveIncident(Guid IncidentId);

/// <summary>
/// Callback the service publishes back to the publisher after each command is handled — the publisher
/// dequeues the next message for that incident and publishes it. Mirrors <c>ContinueTrip</c>.
/// </summary>
public record ContinueIncident(Guid IncidentId);

/// <summary>
/// A lightweight, future-dated demo message. IncidentService continuously schedules these a few minutes
/// out (see <c>ReminderScheduler</c>) so the CritterWatch Scheduled Messages page always has realistic
/// pending rows to display. Its handler is a no-op.
/// </summary>
public record IncidentReminder(Guid IncidentId, string Note);
