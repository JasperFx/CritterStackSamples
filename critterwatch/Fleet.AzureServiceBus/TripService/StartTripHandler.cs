using Microsoft.Extensions.Logging;
using TripMessages;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Marten;
using Wolverine.Runtime.Handlers;

namespace TripService;

/// <summary>
/// Creation handler for a new trip. <see cref="StartTrip"/> opens a fresh event stream and schedules a
/// delayed maintenance-check message — a nice demo of Wolverine's <c>DelayedFor</c> scheduling, which
/// CritterWatch surfaces on its Scheduled Messages page.
/// </summary>
public static class StartTripHandler
{
    public static void Configure(HandlerChain chain) => chain.OnAnyException().MoveToErrorQueue();

    public static (IStartStream, DeliveryMessage<TripMaintenanceCheck>) Handle(StartTrip command, ILogger logger)
    {
        logger.LogInformation("Starting a new trip {Id}", command.TripId);

        var startStream = MartenOps.StartStream<Trip>(command.TripId, new TripStarted(command.StartDay, command.State));
        var maintenanceCheck = new TripMaintenanceCheck(command.TripId)
            .DelayedFor(TimeSpan.FromSeconds(15));

        return (startStream, maintenanceCheck);
    }
}
