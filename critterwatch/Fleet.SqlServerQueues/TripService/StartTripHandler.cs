using Microsoft.Extensions.Logging;
using TripMessages;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Polecat;
using Wolverine.Runtime.Handlers;

namespace TripService;

/// <summary>
/// Creation handler for a new trip. <see cref="StartTrip"/> opens a fresh Polecat event stream and replies
/// <see cref="ContinueTrip"/> to the publisher so the next scripted command for the trip flows.
///
/// <para>
/// The only store-specific change versus the Marten flagship is <c>PolecatOps.StartStream&lt;T&gt;</c>
/// (from <c>Wolverine.Polecat</c>) in place of <c>MartenOps.StartStream&lt;T&gt;</c>; both return an
/// <see cref="IStartStream"/> Wolverine commits in the same transaction.
/// </para>
/// </summary>
public static class StartTripHandler
{
    public static void Configure(HandlerChain chain) => chain.OnAnyException().MoveToErrorQueue();

    public static (IStartStream, OutgoingMessages) Handle(StartTrip command, ILogger logger)
    {
        logger.LogInformation("Starting a new trip {Id}", command.TripId);

        var startStream = PolecatOps.StartStream<Trip>(command.TripId, new TripStarted(command.StartDay, command.State));

        var outgoing = new OutgoingMessages { new ContinueTrip(command.TripId) };
        return (startStream, outgoing);
    }
}
