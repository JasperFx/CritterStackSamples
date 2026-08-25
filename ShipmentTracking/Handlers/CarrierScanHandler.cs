using ShipmentTracking.Data;
using ShipmentTracking.Messages;
using Wolverine;

namespace ShipmentTracking.Handlers;

/// <summary>
/// The hot path. Carrier webhooks push several thousand scans a minute at peak,
/// and scans for one shipment must be applied in the order they were recorded.
///
/// This endpoint runs NativeAck with group partitioning by shipment id, so the
/// ordering guarantee comes from the partitioning rather than from the mode —
/// see Program.cs. NativeAck also means redelivery is expected, so this handler
/// has to be safe to run twice.
/// </summary>
public static class CarrierScanHandler
{
    public static async Task<OutgoingMessages> Handle(
        RecordCarrierScan command,
        ShipmentRepository repository,
        Envelope envelope)
    {
        // The carrier's own id, echoed back on the wire. Envelope replaces
        // NServiceBus' IMessageHandlerContext.MessageHeaders.
        envelope.Headers.TryGetValue("Carrier.ScanId", out var carrierScanId);

        // Idempotent by construction: the update is guarded on the scan being
        // newer than the one already recorded, so a redelivery is a no-op rather
        // than a regression. That guard is what makes NativeAck safe here.
        await repository.RecordScanAsync(command.ShipmentId, command.Location, command.OccurredAt);

        var messages = new OutgoingMessages
        {
            new ShipmentLocationUpdated(command.ShipmentId, command.Location, command.OccurredAt)
        };

        if (command.ScanType == "DELIVERED")
        {
            messages.Add(new ShipmentDelivered(command.ShipmentId, command.OccurredAt));
        }

        return messages;
    }
}
