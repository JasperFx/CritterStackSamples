using Wolverine.Persistence.Sagas;

namespace ShipmentTracking.Messages;

// ShipmentId doubles as the saga identity on the events the delivery saga
// handles, which is what replaces NServiceBus' ConfigureHowToFindSaga mapper.

public record ShipmentBooked(Guid ShipmentId, string Carrier, DateTimeOffset BookedAt);

public record ShipmentCancelled([property: SagaIdentity] Guid ShipmentId, string Reason);

public record ShipmentLocationUpdated(
    Guid ShipmentId,
    string Location,
    DateTimeOffset OccurredAt);

public record LabelGenerated([property: SagaIdentity] Guid ShipmentId, string TrackingNumber);

public record ShipmentDelivered([property: SagaIdentity] Guid ShipmentId, DateTimeOffset DeliveredAt);
