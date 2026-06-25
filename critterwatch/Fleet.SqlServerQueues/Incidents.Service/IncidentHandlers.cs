using Incidents.Domain;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Polecat;
using Wolverine.Runtime.Handlers;

namespace Incidents.Service;

/// <summary>
/// Creation handler — opens a new <see cref="Incident"/> stream and replies <see cref="ContinueIncident"/>
/// to the publisher. Lives outside the <c>[AggregateHandler]</c> class because it creates the aggregate.
///
/// <para>
/// Versus the Marten flagship, the only store-specific change is <c>PolecatOps.StartStream&lt;T&gt;</c>
/// (from <c>Wolverine.Polecat</c>) in place of <c>MartenOps.StartStream&lt;T&gt;</c>.
/// </para>
/// </summary>
public static class LogIncidentHandler
{
    public static void Configure(HandlerChain chain) => chain.OnAnyException().MoveToErrorQueue();

    public static (IStartStream, OutgoingMessages) Handle(LogIncident command, ILogger logger)
    {
        logger.LogInformation("Logging incident {IncidentId} for customer {CustomerId}",
            command.IncidentId, command.CustomerId);

        var startStream = PolecatOps.StartStream<Incident>(
            command.IncidentId,
            new IncidentLogged(command.CustomerId, command.Contact, command.Description, command.LoggedBy));

        var outgoing = new OutgoingMessages { new ContinueIncident(command.IncidentId) };
        return (startStream, outgoing);
    }
}

/// <summary>
/// Every command targeting an existing <see cref="Incident"/> stream. Wolverine's
/// <c>[AggregateHandler]</c> wires each to <c>FetchForWriting&lt;Incident&gt;</c> (Polecat), applies the
/// event(s), and forwards the <see cref="OutgoingMessages"/> through the outbox in the same transaction.
/// </summary>
[AggregateHandler]
public static class IncidentHandlers
{
    public static void Configure(HandlerChain chain) => chain.OnAnyException().MoveToErrorQueue();

    public static (IncidentCategorised, OutgoingMessages) Handle(CategoriseIncident command, Incident incident)
    {
        var ev = new IncidentCategorised(command.Category, command.CategorisedBy);
        return (ev, new OutgoingMessages { new ContinueIncident(command.IncidentId) });
    }

    public static (IncidentPrioritised, OutgoingMessages) Handle(PrioritiseIncident command, Incident incident)
    {
        var ev = new IncidentPrioritised(command.Priority, command.PrioritisedBy);
        return (ev, new OutgoingMessages { new ContinueIncident(command.IncidentId) });
    }

    public static (AgentAssignedToIncident, OutgoingMessages) Handle(AssignAgentToIncident command, Incident incident)
    {
        var ev = new AgentAssignedToIncident(command.AgentId, DateTimeOffset.UtcNow);
        return (ev, new OutgoingMessages { new ContinueIncident(command.IncidentId) });
    }

    public static (AgentRespondedToIncident, OutgoingMessages) Handle(RecordAgentResponseToIncident command, Incident incident)
    {
        var ev = new AgentRespondedToIncident(command.Response);
        return (ev, new OutgoingMessages { new ContinueIncident(command.IncidentId) });
    }

    public static (CustomerRespondedToIncident, OutgoingMessages) Handle(RecordCustomerResponseToIncident command, Incident incident)
    {
        var ev = new CustomerRespondedToIncident(command.Response);
        return (ev, new OutgoingMessages { new ContinueIncident(command.IncidentId) });
    }

    public static (IncidentResolved, OutgoingMessages) Handle(ResolveIncident command, Incident incident)
    {
        // Embed the category so the projection can attribute the Resolved bucket without round-tripping.
        var ev = new IncidentResolved(command.Resolution, command.ResolvedBy, incident.Category);
        return (ev, new OutgoingMessages { new ContinueIncident(command.IncidentId) });
    }

    public static (ResolutionAcknowledgedByCustomer, OutgoingMessages) Handle(AcknowledgeResolution command, Incident incident)
    {
        var ev = new ResolutionAcknowledgedByCustomer(command.AcknowledgedBy);
        return (ev, new OutgoingMessages { new ContinueIncident(command.IncidentId) });
    }

    public static (IncidentClosed, OutgoingMessages) Handle(CloseIncident command, Incident incident)
    {
        var ev = new IncidentClosed(command.ClosedBy, incident.Category);
        return (ev, new OutgoingMessages { new ContinueIncident(command.IncidentId) });
    }

    /// <summary>Terminal step — archives the stream; no <see cref="ContinueIncident"/> as the stream has drained.</summary>
    public static IncidentArchived Handle(ArchiveIncident command, Incident incident) => new();
}
