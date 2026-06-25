using JasperFx.Core;
using TripMessages;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Marten;
using Wolverine.Runtime.Handlers;

namespace TripService;

/// <summary>
/// The <c>[AggregateHandler]</c> for every command that targets an existing Trip stream. Wolverine
/// codegen wires each method to <c>FetchForWriting&lt;Trip&gt;</c>, applies the returned event(s), and
/// commits in one transaction. The <see cref="Configure"/> error policies (retry-with-cooldown, requeue,
/// move-to-error-queue) are exactly the kind of resilience CritterWatch is built to observe.
/// </summary>
[AggregateHandler]
public static class TripMessageHandler
{
    // begin-snippet: trip-error-handling
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

    public static Traveled Handle(RecordTravel message, Trip trip) => message.Event;

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

    public static Departure Handle(Depart command, Trip trip) => new(command.Day, command.State);

    public static TripEnded Handle(EndTrip command, Trip trip) => new(command.Day, command.State);

    public static TripResumed Handle(RepairsCompleted e, Trip trip) => new();
}
