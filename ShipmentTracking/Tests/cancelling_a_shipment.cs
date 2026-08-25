using ShipmentTracking.Api;
using Wolverine.Persistence;

namespace Tests;

public class cancelling_a_shipment(AppFixture fixture) : IntegrationContext(fixture)
{
    [Fact]
    public async Task the_endpoint_answers_202_with_no_body_and_no_location()
    {
        var id = await BookShipment();

        var (_, http) = await TrackedHttpCall(x =>
        {
            x.Post.Json(new CancelShipmentRequest("customer changed their mind"))
                .ToUrl($"/shipments/{id}/cancel");
            x.StatusCodeShouldBe(202);
        });

        // AcceptResponse always stamps Location; this route deliberately uses
        // TypedResults.Accepted(null) instead, so there should not be one.
        http.Context.Response.Headers.Location.ToString().ShouldBeEmpty();
        (await http.ReadAsTextAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task cancelling_updates_the_document_and_publishes_the_event()
    {
        var id = await BookShipment();

        var tracked = await Track()
            .InvokeMessageAndWaitAsync(new CancelShipment(id, "customer changed their mind"));

        var shipment = await LoadShipment(id);
        shipment!.Status.ShouldBe("Cancelled");

        var cancelled = tracked.Sent.SingleMessage<ShipmentCancelled>();
        cancelled.ShipmentId.ShouldBe(id);
        cancelled.Reason.ShouldBe("customer changed their mind");
    }

    [Fact]
    public async Task a_delivered_shipment_cannot_be_cancelled()
    {
        var id = await BookShipment();

        // Get it to Delivered through the real path — a DELIVERED carrier scan
        await Track().InvokeMessageAndWaitAsync(
            new RecordCarrierScan(id, "Austin TX", "DELIVERED", DateTimeOffset.UtcNow));

        (await LoadShipment(id))!.Status.ShouldBe("Delivered");

        // InvokeMessageAndWaitAsync executes the handler INLINE, so a handler exception
        // propagates straight back to the caller rather than being captured on the
        // session. Assert the throw; DoNotAssertOnExceptionsDetected() would not help
        // here and would cost the timeout assertion as well.
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => Track().InvokeMessageAndWaitAsync(new CancelShipment(id, "too late")));

        ex.Message.ShouldBe("A delivered shipment cannot be cancelled");

        (await LoadShipment(id))!.Status.ShouldBe("Delivered");
    }

    [Fact]
    public async Task cancelling_an_unknown_shipment_raises_the_missing_data_exception()
    {
        var id = Guid.NewGuid();

        // [Entity(OnMissing = OnMissing.ThrowException)]. The default would be
        // "log it and stop", which is how an unknown shipment vanishes quietly.
        var ex = await Should.ThrowAsync<RequiredDataMissingException>(
            () => Track().InvokeMessageAndWaitAsync(new CancelShipment(id, "who?")));

        ex.Message.ShouldContain(id.ToString());
    }
}
