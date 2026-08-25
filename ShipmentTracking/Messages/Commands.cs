using Wolverine.Persistence.Sagas;

namespace ShipmentTracking.Messages;

// Wolverine has no ICommand / IEvent marker interfaces. The distinction is in
// how you dispatch: SendAsync goes to one destination, PublishAsync goes to
// every subscriber. Message contracts are ordinary records.

public record BookShipment(
    Guid ShipmentId,
    string Origin,
    string Destination,
    string Carrier,
    decimal WeightKg);

public record CancelShipment(Guid ShipmentId, string Reason);

public record GenerateLabel(Guid ShipmentId, string Carrier);

/// <summary>
/// Recorded by the carrier webhook receiver. The high-volume path — several
/// thousand a minute at peak, and scans for one shipment must be applied in the
/// order they were recorded.
/// </summary>
public record RecordCarrierScan(
    Guid ShipmentId,
    string Location,
    string ScanType,
    DateTimeOffset OccurredAt);

/// <summary>
/// Raised by the delivery saga when a shipment blows its SLA. Routed to the
/// operations service rather than handled here.
/// </summary>
public record EscalateLateShipment(Guid ShipmentId, DateTimeOffset BookedAt);

/// <summary>
/// The saga's SLA timer. In NServiceBus this was an IHandleTimeouts message
/// requested through RequestTimeout; in Wolverine a timeout is just a scheduled
/// message, so it is an ordinary contract carrying the saga identity.
/// </summary>
public record DeliverySlaExpired([property: SagaIdentity] Guid ShipmentId);
