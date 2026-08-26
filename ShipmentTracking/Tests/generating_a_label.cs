namespace Tests;

public class generating_a_label(AppFixture fixture) : IntegrationContext(fixture)
{
    [Fact]
    public async Task the_slow_handler_cascades_two_messages_and_writes_nothing()
    {
        var id = await BookShipment(carrier: "acme");
        var before = (await LoadShipment(id))!;

        var tracked = await Track().InvokeMessageAndWaitAsync(new GenerateLabel(id, "acme"));

        // The split from phase 3: the carrier call produces messages, not a write.
        tracked.Sent.SingleMessage<RecordTrackingNumber>().TrackingNumber.ShouldStartWith("ACME-");
        tracked.Sent.SingleMessage<LabelGenerated>().ShipmentId.ShouldBe(id);

        // Re-running GenerateLabel changes the document only through
        // RecordTrackingNumberHandler, and that write is idempotent here because the
        // status is already past "Booked" -- so only the tracking number is rewritten.
        var after = (await LoadShipment(id))!;
        after.TrackingNumber.ShouldBe(before.TrackingNumber);
    }

    [Fact]
    public async Task recording_the_tracking_number_advances_a_booked_shipment()
    {
        var id = Guid.NewGuid();
        await Track().InvokeMessageAndWaitAsync(new BookShipment(id, "Dallas", "Austin", "acme", 1m));

        var shipment = (await LoadShipment(id))!;
        shipment.Status.ShouldBe("Labelled");
        shipment.TrackingNumber.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task a_late_label_does_not_resurrect_a_cancelled_shipment()
    {
        var id = await BookShipment();

        await Track().InvokeMessageAndWaitAsync(new CancelShipment(id, "changed my mind"));
        (await LoadShipment(id))!.Status.ShouldBe("Cancelled");

        // The carrier call was in flight when the cancellation landed. The tracking
        // number is still worth recording; the status must not move.
        await Track().InvokeMessageAndWaitAsync(new RecordTrackingNumber(id, "ACME-LATE12345"));

        var shipment = (await LoadShipment(id))!;
        shipment.Status.ShouldBe("Cancelled");
        shipment.TrackingNumber.ShouldBe("ACME-LATE12345");
    }

    [Fact]
    public async Task recording_a_tracking_number_for_an_unknown_shipment_throws()
    {
        var id = Guid.NewGuid();

        var ex = await Should.ThrowAsync<Wolverine.Persistence.RequiredDataMissingException>(
            () => Track().InvokeMessageAndWaitAsync(new RecordTrackingNumber(id, "ACME-NOBODY99")));

        ex.Message.ShouldContain(id.ToString());
    }
}
