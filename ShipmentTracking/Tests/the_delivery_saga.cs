using JasperFx.Core;
using Microsoft.Extensions.DependencyInjection;
using Polecat;
using ShipmentTracking.Sagas;

namespace Tests;

/// <summary>
/// The saga is persisted as a Polecat document — Wolverine's Polecat integration puts
/// its persistence frame provider first, so a Saga is stored through IDocumentSession
/// rather than in Wolverine's own lightweight saga table. You can see the table it
/// creates: shipments.pc_doc_shipmentdeliverysaga.
/// </summary>
public class the_delivery_saga(AppFixture fixture) : IntegrationContext(fixture)
{
    [Fact]
    public async Task booking_starts_the_saga_and_schedules_the_sla_timeout()
    {
        var id = Guid.NewGuid();

        var tracked = await Track()
            .InvokeMessageAndWaitAsync(new BookShipment(id, "Dallas", "Austin", "acme", 1m));

        // Start(ShipmentBooked) returns only the timeout, and the delay lives on the
        // message type, so nothing in the saga schedules anything explicitly.
        var timeout = tracked.Sent.SingleMessage<DeliverySlaExpired>();
        timeout.ShipmentId.ShouldBe(id);

        var saga = await LoadSaga(id);
        saga.ShouldNotBeNull();
        saga.Delivered.ShouldBeFalse();

        // BookShipment cascaded GenerateLabel, whose chain ends in LabelGenerated,
        // which the saga also handles.
        saga.LabelGenerated.ShouldBeTrue();
    }

    [Fact]
    public async Task the_sla_timeout_is_scheduled_five_days_out_not_executed()
    {
        var id = Guid.NewGuid();

        var tracked = await Track()
            .InvokeMessageAndWaitAsync(new BookShipment(id, "Dallas", "Austin", "acme", 1m));

        var scheduled = tracked.Scheduled.SingleMessage<DeliverySlaExpired>();
        scheduled.ShipmentId.ShouldBe(id);

        // It has NOT run -- the saga is still open and nothing escalated
        tracked.Executed.MessagesOf<DeliverySlaExpired>().ShouldBeEmpty();
        (await LoadSaga(id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task an_undelivered_shipment_escalates_when_the_sla_expires()
    {
        var id = Guid.NewGuid();

        var initial = await Track()
            .InvokeMessageAndWaitAsync(new BookShipment(id, "Dallas", "Austin", "acme", 1m));

        // Five days, in no time at all. PlayScheduledMessagesAsync replays the captured
        // scheduled envelopes immediately and hands back a fresh tracked session. This
        // is the alternative to a sleep that could not work anyway -- no test is going
        // to wait out a five day delay.
        var played = await initial.PlayScheduledMessagesAsync(30.Seconds());

        played.Executed.SingleMessage<DeliverySlaExpired>().ShipmentId.ShouldBe(id);

        var escalation = played.Sent.SingleMessage<EscalateLateShipment>();
        escalation.ShipmentId.ShouldBe(id);

        // MarkCompleted() ran, so the saga document is gone
        (await LoadSaga(id)).ShouldBeNull();
    }

    [Fact]
    public async Task a_delivered_shipment_does_not_escalate()
    {
        var id = Guid.NewGuid();

        var initial = await Track()
            .InvokeMessageAndWaitAsync(new BookShipment(id, "Dallas", "Austin", "acme", 1m));

        await Track().InvokeMessageAndWaitAsync(
            new RecordCarrierScan(id, "Austin TX", "DELIVERED", DateTimeOffset.UtcNow));

        // Handle(ShipmentDelivered) marks the saga complete, so the timeout arrives to
        // no saga at all and there is nothing to escalate.
        (await LoadSaga(id)).ShouldBeNull();

        var played = await initial.PlayScheduledMessagesAsync(30.Seconds());
        played.Sent.MessagesOf<EscalateLateShipment>().ShouldBeEmpty();
    }

    [Fact]
    public async Task cancelling_completes_the_saga()
    {
        var id = Guid.NewGuid();

        await Track().InvokeMessageAndWaitAsync(new BookShipment(id, "Dallas", "Austin", "acme", 1m));
        (await LoadSaga(id)).ShouldNotBeNull();

        await Track().InvokeMessageAndWaitAsync(new CancelShipment(id, "changed my mind"));

        (await LoadSaga(id)).ShouldBeNull();
    }

    private async Task<ShipmentDeliverySaga?> LoadSaga(Guid id)
    {
        await using var session = Store.QuerySession();
        return await session.LoadAsync<ShipmentDeliverySaga>(id);
    }
}
