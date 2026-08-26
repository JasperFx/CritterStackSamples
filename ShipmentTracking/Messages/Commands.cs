using JasperFx.Core;
using Wolverine;
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
/// Applies a carrier tracking number to the shipment document.
///
/// Phase 3 introduced this. GenerateLabel's handler used to make the 30-90 second
/// carrier call AND write the result; with a document store that would mean holding a
/// loaded document — and its revision — across the whole call. Splitting the write out
/// keeps the slow handler free of the database entirely.
/// </summary>
public record RecordTrackingNumber(Guid ShipmentId, string TrackingNumber);

/// <summary>
/// Raised by the delivery saga when a shipment blows its SLA. Routed to the
/// operations service rather than handled here.
/// </summary>
public record EscalateLateShipment(Guid ShipmentId, DateTimeOffset BookedAt);

/// <summary>
/// The saga's SLA timer. NServiceBus expressed this as IHandleTimeouts plus a
/// RequestTimeout call that carried the delay at the call site. Wolverine puts
/// the delay on the message type: subclassing TimeoutMessage means every
/// DeliverySlaExpired is scheduled five days out wherever it is returned, so the
/// saga method stays a pure function that names an outcome rather than
/// scheduling one.
/// </summary>
public record DeliverySlaExpired([property: SagaIdentity] Guid ShipmentId)
    : TimeoutMessage(5.Days());
