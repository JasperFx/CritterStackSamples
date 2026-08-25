using ShipmentTracking.Data;
using ShipmentTracking.Messages;

namespace ShipmentTracking.Handlers;

public static class CancelShipmentHandler
{
    public static async Task<ShipmentCancelled> Handle(
        CancelShipment command,
        ShipmentRepository repository)
    {
        var shipment = await repository.LoadAsync(command.ShipmentId);
        if (shipment is null)
            throw new InvalidOperationException($"Unknown shipment {command.ShipmentId}");

        if (shipment.Status == "Delivered")
            throw new InvalidOperationException("A delivered shipment cannot be cancelled");

        await repository.UpdateStatusAsync(command.ShipmentId, "Cancelled");

        return new ShipmentCancelled(command.ShipmentId, command.Reason);
    }
}
