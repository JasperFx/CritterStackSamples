using Bobcat;
using Bobcat.Xunit;
using JasperFx.Core;
using ShipmentTracking.Sagas;

namespace Tests;

/// <summary>
/// The saga is persisted as a Polecat document — Wolverine's Polecat integration puts
/// its persistence frame provider first, so a Saga is stored through IDocumentSession
/// rather than in Wolverine's own lightweight saga table. You can see the table it
/// creates: shipments.pc_doc_shipmentdeliverysaga.
/// </summary>
/// <remarks>
/// The original saga tests as Bobcat specifications. A shipment is arranged through the real handlers
/// (Given steps), and "five days later" is the booking's own scheduled <c>DeliverySlaExpired</c>, delivered
/// inline as the act (WhenTheTimeoutComesDue).
/// </remarks>
[BobcatFeature("The delivery saga")]
public class the_delivery_saga(AppFixture fixture) : ShipmentSpec(fixture)
{
    [BobcatSpec(typeof(BookShipment))]
    public async Task booking_starts_the_saga_and_schedules_the_sla_timeout()
    {
        var id = Guid.NewGuid();

        await WhenReceived(new BookShipment(id, "Dallas", "Austin", "acme", 1m));

        // Start(ShipmentBooked) returns only the timeout, and the delay lives on the
        // message type, so nothing in the saga schedules anything explicitly.
        ThenMessageSent(Specify<DeliverySlaExpired>().With(x => x.ShipmentId, id));

        // BookShipment cascaded GenerateLabel, whose chain ends in LabelGenerated,
        // which the saga also handles.
        await ThenDocument<ShipmentDeliverySaga>(id, Specify<ShipmentDeliverySaga>()
            .With(x => x.Delivered, false)
            .With(x => x.LabelGenerated, true));
    }

    [BobcatSpec(typeof(BookShipment))]
    public async Task the_sla_timeout_is_scheduled_five_days_out_not_executed()
    {
        var id = Guid.NewGuid();

        await WhenReceived(new BookShipment(id, "Dallas", "Austin", "acme", 1m));

        ThenMessageScheduled<DeliverySlaExpired>(5.Days());

        // It has NOT run -- the saga is still open and nothing escalated
        ThenNotHandled(typeof(DeliverySlaExpired));
        await ThenDocument<ShipmentDeliverySaga>(id);
    }

    [BobcatSpec(typeof(DeliverySlaExpired))]
    public async Task an_undelivered_shipment_escalates_when_the_sla_expires()
    {
        var id = Guid.NewGuid();
        await GivenABookedShipment(id);

        // Five days, in no time at all: the timeout the booking scheduled, delivered now
        await WhenTheTimeoutComesDue(TheScheduledSlaTimeout());

        ThenHandled(typeof(DeliverySlaExpired));
        ThenMessageSent(Specify<EscalateLateShipment>().With(x => x.ShipmentId, id));

        // MarkCompleted() ran, so the saga document is gone
        await ThenNoReadModel<ShipmentDeliverySaga>(id);
    }

    [BobcatSpec(typeof(DeliverySlaExpired))]
    public async Task a_delivered_shipment_does_not_escalate()
    {
        var id = Guid.NewGuid();
        await GivenABookedShipment(id);
        await GivenTheShipmentIsDelivered(id, DateTimeOffset.UtcNow);

        // Handle(ShipmentDelivered) marks the saga complete, so the timeout arrives to
        // no saga at all and there is nothing to escalate.
        await GivenTheSagaHasCompleted(id);

        await WhenTheTimeoutComesDue(TheScheduledSlaTimeout());

        ThenHandled(typeof(DeliverySlaExpired));
        ThenNoMessageSent<EscalateLateShipment>();
    }

    [BobcatSpec(typeof(CancelShipment))]
    public async Task cancelling_completes_the_saga()
    {
        var id = Guid.NewGuid();
        await GivenABookedShipment(id);
        await GivenTheSagaIsOpen(id);

        await WhenReceived(new CancelShipment(id, "changed my mind"));

        await ThenNoReadModel<ShipmentDeliverySaga>(id);
    }

    // =======================================================================
    // Messages that arrive after the saga has completed itself.
    //
    // Every one of these threw UnknownSagaException before the NotFound methods were
    // added, and every one of them is reachable in production. The phase 4 suite missed
    // all three because it tested the DOCUMENT side of these races and never delivered
    // the saga-bound event.
    // =======================================================================

    [BobcatSpec(typeof(LabelGenerated))]
    public async Task a_label_landing_after_cancellation_does_not_blow_up_the_saga()
    {
        var id = Guid.NewGuid();
        await GivenABookedShipment(id);
        await GivenTheShipmentIsCancelled(id, "changed my mind");
        await GivenTheSagaHasCompleted(id);

        // The carrier call was in flight when the cancellation landed.
        await WhenReceived(new LabelGenerated(id, "ACME-LATE12345"));

        ThenHandled(typeof(LabelGenerated));
        ThenTheActSucceeded();
    }

    [BobcatSpec(typeof(RecordCarrierScan))]
    public async Task a_duplicate_delivery_notification_does_not_blow_up_the_saga()
    {
        var id = Guid.NewGuid();
        var first = new DateTimeOffset(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
        await GivenABookedShipment(id);
        await GivenTheShipmentIsDelivered(id, first);
        await GivenTheSagaHasCompleted(id);

        // A newer DELIVERED scan passes the staleness guard and publishes the event again.
        await WhenReceived(new RecordCarrierScan(id, "Austin TX", "DELIVERED", first.AddHours(1)));

        ThenHandled(typeof(ShipmentDelivered));
        ThenTheActSucceeded();
    }

    [BobcatSpec(typeof(CancelShipment))]
    public async Task cancelling_twice_does_not_blow_up_the_saga()
    {
        var id = Guid.NewGuid();
        await GivenABookedShipment(id);
        await GivenTheShipmentIsCancelled(id, "first");

        // CancelShipmentHandler only refuses a DELIVERED shipment, so a second cancel of
        // an already-cancelled one goes through and publishes ShipmentCancelled again.
        await WhenReceived(new CancelShipment(id, "second"));

        ThenHandled(typeof(ShipmentCancelled));
        ThenTheActSucceeded();
    }

    [BobcatSpec(typeof(DeliverySlaExpired))]
    public async Task the_sla_timeout_needs_no_NotFound_because_it_is_a_TimeoutMessage()
    {
        var id = Guid.NewGuid();
        await GivenABookedShipment(id);
        await GivenTheShipmentIsDelivered(id, DateTimeOffset.UtcNow);
        await GivenTheSagaHasCompleted(id);

        // DeliverySlaExpired subclasses TimeoutMessage, and Wolverine emits
        //     if (saga == null) return;
        // for those instead of the throw it emits for every other saga message.
        // ShipmentDeliverySaga deliberately declares no NotFound(DeliverySlaExpired).
        await WhenTheTimeoutComesDue(new DeliverySlaExpired(id));

        ThenHandled(typeof(DeliverySlaExpired));
        ThenTheActSucceeded();
        ThenNoMessageSent<EscalateLateShipment>();
    }
}
