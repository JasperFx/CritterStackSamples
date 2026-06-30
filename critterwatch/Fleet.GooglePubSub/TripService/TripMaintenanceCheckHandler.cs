using Marten;
using Microsoft.Extensions.Logging;
using TripMessages;
using Wolverine;

namespace TripService;

/// <summary>
/// Handles the delayed <see cref="TripMaintenanceCheck"/>. If the trip is still active it re-schedules
/// itself 15 s out; otherwise it stops. A small but realistic recurring-scheduled-work pattern that keeps
/// the Scheduled Messages view populated.
/// </summary>
public static class TripMaintenanceCheckHandler
{
    public static async Task<DeliveryMessage<TripMaintenanceCheck>?> Handle(
        TripMaintenanceCheck message,
        IDocumentSession session,
        ILogger logger)
    {
        var trip = await session.Events.AggregateStreamAsync<Trip>(message.TripId);

        if (trip is { Active: true })
        {
            logger.LogInformation("Maintenance check for trip {TripId}: still active, scheduling next check",
                message.TripId);

            return new TripMaintenanceCheck(message.TripId).DelayedFor(TimeSpan.FromSeconds(15));
        }

        logger.LogInformation("Maintenance check for trip {TripId}: no longer active, stopping checks",
            message.TripId);

        return null;
    }
}
