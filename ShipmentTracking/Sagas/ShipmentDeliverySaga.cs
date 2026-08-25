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
    /// Replaces IHandleTimeouts&lt;DeliverySlaExpired&gt;. The timeout arrives as an
    /// ordinary message.
    ///
    /// <para>
    /// The <c>!Delivered</c> guard is belt-and-braces rather than load-bearing: a
    /// delivered or cancelled shipment has already completed this saga, so by the time
    /// the five-day timeout lands there is nothing here to escalate. See the NotFound
    /// methods below for what actually happens in that case.
    /// </para>
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

    // =======================================================================
    // NotFound — messages that arrive after this saga has completed itself.
    //
    // Wolverine throws UnknownSagaException for a saga message whose saga cannot be
    // loaded, UNLESS the saga declares a NotFound method for that message type. An
    // empty one is a legitimate implementation: the point is to say "yes, I know this
    // races, and dropping it is the right answer."
    //
    // All three of these are reachable, and none of them were until the code review
    // went looking:
    //
    //   LabelGenerated    cancel a shipment while the 30-90 second carrier call is
    //                     still in flight -> the label lands on a completed saga.
    //   ShipmentDelivered a carrier sends a second DELIVERED scan with a newer
    //                     timestamp -> the second one passes the staleness guard and
    //                     publishes the event again.
    //   ShipmentCancelled cancel twice. The handler only refuses a DELIVERED shipment,
    //                     so a second cancel of an already-cancelled one goes through.
    //
    // DeliverySlaExpired deliberately has NO NotFound method, and that is not an
    // oversight: it subclasses TimeoutMessage, and Wolverine special-cases those --
    // SagaChain emits `if (saga == null) return;` for a timeout instead of the throw.
    // You can see it for yourself:
    //
    //   dotnet run -- wolverine-diagnostics codegen-preview --handler DeliverySlaExpired
    //   dotnet run -- wolverine-diagnostics codegen-preview --handler LabelGenerated
    //
    // NotFound methods may be static -- only Start and NotFound may be, because both
    // assume the saga does not exist yet.
    // =======================================================================

    public static void NotFound(LabelGenerated message, ILogger logger) =>
        logger.LogInformation(
            "Label {TrackingNumber} arrived for shipment {ShipmentId} after its delivery saga completed; " +
            "the tracking number is still recorded on the document",
            message.TrackingNumber, message.ShipmentId);

    public static void NotFound(ShipmentDelivered message, ILogger logger) =>
        logger.LogInformation(
            "Duplicate delivery notification for shipment {ShipmentId}; its saga is already complete",
            message.ShipmentId);

    public static void NotFound(ShipmentCancelled message, ILogger logger) =>
        logger.LogInformation(
            "Cancellation for shipment {ShipmentId} arrived after its saga completed",
            message.ShipmentId);
}
