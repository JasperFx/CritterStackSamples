using ShipmentTracking.Messages;
using Wolverine;

namespace ShipmentTracking.Sagas;

/// <summary>
/// Tracks a shipment from booking to delivery and escalates one that has not
/// been delivered within the SLA.
///
/// Converted from an NServiceBus Saga&lt;ShipmentDeliveryData&gt;. The separate
/// data class is gone — in Wolverine the saga *is* the state — and the
/// ConfigureHowToFindSaga mapper is replaced by [SagaIdentity] on the message
/// properties that carry the shipment id.
/// </summary>
public class ShipmentDeliverySaga : Saga
{
    public Guid Id { get; set; }
    public bool LabelGenerated { get; set; }
    public bool Delivered { get; set; }
    public DateTimeOffset BookedAt { get; set; }

    /// <summary>
    /// Replaces IAmStartedByMessages&lt;ShipmentBooked&gt;. Wolverine starts a saga
    /// on a method named Start / Starts.
    ///
    /// The NServiceBus version called RequestTimeout&lt;DeliverySlaExpired&gt;. A
    /// Wolverine timeout is just a scheduled message, so the SLA timer is
    /// returned as a cascading message with a delay.
    /// </summary>
    public (ShipmentDeliverySaga, DeliveryMessage<DeliverySlaExpired>) Start(ShipmentBooked booked)
    {
        Id = booked.ShipmentId;
        BookedAt = booked.BookedAt;

        return (this, new DeliverySlaExpired(booked.ShipmentId).DelayedFor(TimeSpan.FromDays(5)));
    }

    public void Handle(LabelGenerated _) => LabelGenerated = true;

    public void Handle(ShipmentDelivered _)
    {
        Delivered = true;
        MarkCompleted();
    }

    public void Handle(ShipmentCancelled _) => MarkCompleted();

    /// <summary>
    /// Replaces IHandleTimeouts&lt;DeliverySlaExpired&gt;. Nothing special about it
    /// in Wolverine — the timeout arrives as an ordinary message.
    /// </summary>
    public OutgoingMessages Handle(DeliverySlaExpired _)
    {
        var messages = new OutgoingMessages();

        if (!Delivered)
        {
            messages.Add(new EscalateLateShipment(Id, BookedAt));
        }

        MarkCompleted();
        return messages;
    }
}
