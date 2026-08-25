using ShipmentTracking.Data;
using ShipmentTracking.Messages;
using Wolverine;
using Wolverine.Persistence;

namespace ShipmentTracking.Handlers;

/// <summary>
/// The hot path. Carrier webhooks push several thousand scans a minute at peak, and
/// scans for one shipment must be applied in the order they were recorded.
///
/// This endpoint runs NativeAck with global partitioning by shipment id, so the
/// ordering guarantee comes from the partitioning rather than from the mode — see
/// Program.cs. NativeAck also means redelivery is expected, so this handler has to be
/// safe to run twice.
/// </summary>
public static class CarrierScanHandler
{
    /// <summary>
    /// The idempotency guard used to be a SQL WHERE clause
    /// (<c>and (LastScanAt is null or LastScanAt &lt; @occurredAt)</c>). It is an
    /// ordinary <c>if</c> now, which reads better and — more to the point — is
    /// testable without a database.
    ///
    /// <para>
    /// It is also still atomic, and that is not an accident. A read-modify-write is
    /// only safe if nothing else is applying scans to this shipment at the same time,
    /// and that is exactly what phase 1's <c>GlobalPartitioned</c> topology bought:
    /// every scan for a shipment lands on the same shard and is processed one at a
    /// time, cluster-wide. Had the ordering been left to a per-listener
    /// PartitionProcessingByGroupId call, this <c>if</c> would race across nodes.
    /// </para>
    ///
    /// <para>
    /// Handlers on <i>other</i> queues do write this same document, and those the
    /// partitioning does not cover — see Shipment.Version.
    /// </para>
    /// </summary>
    public static (IStorageAction<Shipment>, OutgoingMessages) Handle(
        RecordCarrierScan command,
        [Entity(Required = true, OnMissing = OnMissing.ThrowException)] Shipment shipment,
        Envelope envelope)
    {
        // The carrier's own id, echoed back on the wire. Envelope replaces
        // NServiceBus' IMessageHandlerContext.MessageHeaders.
        envelope.Headers.TryGetValue("Carrier.ScanId", out var carrierScanId);

        if (shipment.LastScanAt is not null && shipment.LastScanAt >= command.OccurredAt)
        {
            // A redelivery, or a scan that arrived out of order. Storage.Nothing<T>() is
            // the declarative "and no write" — the handler still names an outcome
            // rather than falling off the end having done nothing.
            return (Storage.Nothing<Shipment>(), []);
        }

        shipment.LastLocation = command.Location;
        shipment.LastScanAt = command.OccurredAt;

        var messages = new OutgoingMessages
        {
            new ShipmentLocationUpdated(command.ShipmentId, command.Location, command.OccurredAt)
        };

        if (command.ScanType == "DELIVERED")
        {
            messages.Add(new ShipmentDelivered(command.ShipmentId, command.OccurredAt));
        }

        return (Storage.Update(shipment), messages);
    }
}
