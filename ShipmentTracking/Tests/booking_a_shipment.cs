using ShipmentTracking.Api;

namespace Tests;

public class booking_a_shipment(AppFixture fixture) : IntegrationContext(fixture)
{
    [Fact]
    public async Task the_endpoint_answers_202_with_a_location_header()
    {
        var (_, http) = await TrackedHttpCall(x =>
        {
            x.Post.Json(new BookShipmentRequest("Dallas", "Austin", "acme", 12.5m)).ToUrl("/shipments");
            x.StatusCodeShouldBe(202);
        });

        var accepted = await http.ReadAsJsonAsync<ShipmentAccepted>();
        accepted.ShouldNotBeNull();
        accepted.ShipmentId.ShouldNotBe(Guid.Empty);

        http.Context.Response.Headers.Location
            .ToString().ShouldBe($"/shipments/{accepted.ShipmentId}");
    }

    [Fact]
    public async Task the_endpoint_cascades_the_command_rather_than_handling_it()
    {
        var (tracked, http) = await TrackedHttpCall(x =>
        {
            x.Post.Json(new BookShipmentRequest("Dallas", "Austin", "acme", 12.5m)).ToUrl("/shipments");
            x.StatusCodeShouldBe(202);
        });

        var accepted = (await http.ReadAsJsonAsync<ShipmentAccepted>())!;

        // The endpoint returns a tuple; the second element goes out through the outbox.
        // It is SENT to the shipment-commands queue, not invoked inline.
        var command = tracked.Sent.SingleMessage<BookShipment>();
        command.ShipmentId.ShouldBe(accepted.ShipmentId);
        command.Origin.ShouldBe("Dallas");
    }

    [Fact]
    public async Task the_handler_inserts_the_document_and_cascades_two_messages()
    {
        var id = Guid.NewGuid();

        var tracked = await Track()
            .InvokeMessageAndWaitAsync(new BookShipment(id, "Dallas", "Austin", "acme", 12.5m));

        // Storage.Insert() actually wrote, through the same transaction as the outbox
        var shipment = await LoadShipment(id);
        shipment.ShouldNotBeNull();
        shipment.Origin.ShouldBe("Dallas");
        shipment.Destination.ShouldBe("Austin");
        shipment.Carrier.ShouldBe("acme");
        shipment.WeightKg.ShouldBe(12.5m);

        // NOT Status == "Booked", and the reason is worth stating: the tracked session
        // waits for ALL cascading work, so by the time it returns the label chain has
        // also run and the status is "Labelled". That is not a gap in the test — it is
        // proof of the insert, because RecordTrackingNumberHandler only advances a
        // shipment whose status is exactly "Booked". Reaching "Labelled" is only
        // possible if this handler wrote "Booked" first.
        shipment.Status.ShouldBe("Labelled");

        tracked.Sent.SingleMessage<ShipmentBooked>().Carrier.ShouldBe("acme");
        tracked.Sent.SingleMessage<GenerateLabel>().ShipmentId.ShouldBe(id);
    }

    [Fact]
    public async Task the_whole_chain_runs_end_to_end_from_one_http_call()
    {
        // One HTTP call, five message types, three queues, a saga and two document
        // writes. Nothing here sleeps: the tracked session returns when the last
        // handler finishes, which on this machine is well under a second.
        var (tracked, http) = await TrackedHttpCall(x =>
        {
            x.Post.Json(new BookShipmentRequest("Dallas", "Austin", "acme", 12.5m)).ToUrl("/shipments");
            x.StatusCodeShouldBe(202);
        });

        var id = (await http.ReadAsJsonAsync<ShipmentAccepted>())!.ShipmentId;

        tracked.Executed.SingleMessage<BookShipment>().ShouldNotBeNull();
        tracked.Executed.SingleMessage<GenerateLabel>().ShouldNotBeNull();
        tracked.Executed.SingleMessage<RecordTrackingNumber>().ShouldNotBeNull();

        var shipment = await LoadShipment(id);
        shipment.ShouldNotBeNull();
        shipment.Status.ShouldBe("Labelled");
        shipment.TrackingNumber.ShouldStartWith("ACME-");
    }
}
