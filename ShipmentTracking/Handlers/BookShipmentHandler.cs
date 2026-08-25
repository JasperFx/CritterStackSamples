using ShipmentTracking.Data;
using ShipmentTracking.Messages;
using Wolverine;

namespace ShipmentTracking.Handlers;

public static class BookShipmentHandler
{
    // Returning an OutgoingMessages is Wolverine's cascading-message pattern:
    // the handler decides what should happen next and hands it back, rather
    // than reaching for context.Publish / context.Send itself. That keeps the
    // decision testable without a bus.
    public static async Task<OutgoingMessages> Handle(
        BookShipment command,
        ShipmentRepository repository,
        ILogger logger)
    {
        logger.LogInformation("Booking shipment {ShipmentId} with {Carrier}",
            command.ShipmentId, command.Carrier);

        await repository.InsertAsync(new Shipment
        {
            Id = command.ShipmentId,
            Origin = command.Origin,
            Destination = command.Destination,
            Carrier = command.Carrier,
            WeightKg = command.WeightKg,
            Status = "Booked"
        });

        return
        [
            new ShipmentBooked(command.ShipmentId, command.Carrier, DateTimeOffset.UtcNow),

            // Label generation is slow, so it stays a separate command. Routing
            // sends it to the label queue.
            new GenerateLabel(command.ShipmentId, command.Carrier)
        ];
    }
}
