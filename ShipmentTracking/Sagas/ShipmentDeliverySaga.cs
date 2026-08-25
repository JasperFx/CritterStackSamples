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
    /// Set the state on the saga and return only what should happen next. Do
    /// NOT return `this` alongside it — that shape is for an immutable saga
    /// that returns a new instance, and while it compiles for a mutable one it
    /// is not how the model is meant to work.
    ///
    /// The delay lives on DeliverySlaExpired, which subclasses TimeoutMessage,
    /// so there is no schedule call here at all.
    ///
    /// <para>
    /// GenerateLabel is cascaded from HERE rather than from BookShipmentHandler, and
    /// that is an ordering guarantee rather than a preference. Wolverine inserts the
    /// saga and flushes this method's outgoing messages in one transaction, so
    /// GenerateLabel cannot leave before the saga row exists — and therefore
    /// LabelGenerated cannot arrive before there is a saga to receive it. Cascading
    /// both from the booking handler raced, and only the carrier's 30-90 second
    /// latency was hiding it.
    /// </para>
    /// </summary>
    public (DeliverySlaExpired, GenerateLabel) Start(ShipmentBooked booked)
    {
        Id = booked.ShipmentId;
        BookedAt = booked.BookedAt;

        return (
            new DeliverySlaExpired(booked.ShipmentId),
            new GenerateLabel(booked.ShipmentId, booked.Carrier));
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
