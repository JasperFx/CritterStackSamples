using ShipmentTracking.Data;
using ShipmentTracking.Messages;
using Wolverine.Persistence;

namespace ShipmentTracking.Handlers;

public static class CancelShipmentHandler
{
    /// <summary>
    /// The LoadAsync + null check is gone. [Entity] resolves the id from the command by
    /// convention — CancelShipment.ShipmentId matches the {EntityType}Id rule for
    /// Shipment — and loads it before the handler runs.
    ///
    /// OnMissing.ThrowException is deliberate rather than the Simple404 default. In a
    /// message handler the default is "log it and stop", which would make an unknown
    /// shipment vanish quietly; the NServiceBus version threw and the message went to
    /// the error queue, and that is worth keeping. RequiredDataMissingException has its
    /// own MoveToErrorQueue policy in Program.cs.
    /// </summary>
    public static (Update<Shipment>, ShipmentCancelled) Handle(
        CancelShipment command,
        [Entity(Required = true, OnMissing = OnMissing.ThrowException)] Shipment shipment)
    {
        if (shipment.Status == "Delivered")
            throw new InvalidOperationException("A delivered shipment cannot be cancelled");

        shipment.Status = "Cancelled";

        return (Storage.Update(shipment), new ShipmentCancelled(command.ShipmentId, command.Reason));
    }
}
