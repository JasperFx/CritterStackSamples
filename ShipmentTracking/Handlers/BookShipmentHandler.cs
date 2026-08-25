using ShipmentTracking.Data;
using ShipmentTracking.Messages;
using Wolverine.Persistence;

namespace ShipmentTracking.Handlers;

public static class BookShipmentHandler
{
    // Two declarative returns now, not one. The cascading messages were already
    // cascading; phase 3 makes the *write* declarative too, so the handler is a
    // synchronous pure function with no session, no repository and no await.
    //
    // Returning an IStorageAction<T> turns transactional middleware on by itself, and
    // the insert and the two outgoing messages commit in one transaction with the
    // outbox — which the Dapper version could not do, because it opened its own
    // connection outside Wolverine's.
    public static (Insert<Shipment>, ShipmentBooked) Handle(
        BookShipment command,
        ILogger logger)
    {
        logger.LogInformation("Booking shipment {ShipmentId} with {Carrier}",
            command.ShipmentId, command.Carrier);

        var shipment = new Shipment
        {
            Id = command.ShipmentId,
            Origin = command.Origin,
            Destination = command.Destination,
            Carrier = command.Carrier,
            WeightKg = command.WeightKg,
            Status = "Booked"
        };

        // GenerateLabel used to be cascaded from here alongside ShipmentBooked. Phase 4
        // moved it into the saga's Start, and the reason is in the README: cascading
        // both in parallel let LabelGenerated reach the saga before ShipmentBooked had
        // created it. The only thing preventing that was the carrier taking 30-90
        // seconds — a timing accident standing in for a correctness argument.
        return (
            Storage.Insert(shipment),
            new ShipmentBooked(command.ShipmentId, command.Carrier, DateTimeOffset.UtcNow));
    }
}
