using JasperFx.Core;
using TripMessages;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Polecat;
using Wolverine.Runtime.Handlers;

namespace TripService;

/// <summary>
/// The <c>[AggregateHandler]</c> for every command that targets an existing Trip stream. Wolverine
/// codegen wires each method to <c>FetchForWriting&lt;Trip&gt;</c> (Polecat), applies the returned
/// event(s), and commits in one transaction. The <see cref="Configure"/> error policies
/// (retry-with-cooldown, requeue, move-to-error-queue) are exactly the kind of resilience CritterWatch is
/// built to observe — and because this fleet's durable store IS the SQL Server queue database, the
/// resulting dead-letters fully populate CritterWatch's DLQ panel.
///
/// <para>
/// Several handlers also reply <see cref="ContinueTrip"/> via <c>OutgoingMessages</c> to keep the
/// self-driving traffic loop turning. (The Marten flagship raised that side effect from the projection's
/// <c>RaiseSideEffects</c>; the Polecat fleet drives it from the command handlers, matching the upstream
/// PolecatTrips sample.)
/// </para>
/// </summary>
[AggregateHandler]
public static class TripMessageHandler
{
    // begin-snippet: sqlserver-trip-error-handling
    public static void Configure(HandlerChain chain)
    {
        chain.OnException<TransientException>()
            .RetryWithCooldown(50.Milliseconds(), 100.Milliseconds(), 250.Milliseconds())
            .Then.MoveToErrorQueue();

        chain.OnException<OtherTransientException>()
            .Requeue(3).Then.MoveToErrorQueue();

        chain.OnAnyException().MoveToErrorQueue();
    }
    // end-snippet

    public static (Traveled, OutgoingMessages) Handle(RecordTravel message, Trip trip)
    {
        var outgoing = new OutgoingMessages();
        if (!trip.WaitingRepairs) outgoing.Add(new ContinueTrip(message.TripId));
        return (message.Event, outgoing);
    }

    public static TripAborted Handle(AbortTrip command, Trip trip) => new();

    public static (BrokeDown, OutgoingMessages) Handle(RecordBreakdown command, Trip trip)
    {
        var e = new BrokeDown(command.IsCritical);
        return command.IsCritical
            ? (e, [new RepairRequested(command.TripId, trip.State)])
            : (e, []);
    }

    public static VacationOver Handle(MarkVacationOver command, Trip trip) => new();

    public static Arrival Handle(Arrive command, Trip trip) => new(command.Day, command.State);

    public static (Departure, OutgoingMessages) Handle(Depart command, Trip trip)
    {
        var outgoing = new OutgoingMessages();
        if (!trip.WaitingRepairs) outgoing.Add(new ContinueTrip(command.TripId));
        return (new Departure(command.Day, command.State), outgoing);
    }

    public static TripEnded Handle(EndTrip command, Trip trip) => new(command.Day, command.State);

    public static TripResumed Handle(RepairsCompleted e, Trip trip) => new();
}
