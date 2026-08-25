namespace Tests;

public class recording_carrier_scans(AppFixture fixture) : IntegrationContext(fixture)
{
    private static readonly DateTimeOffset Ten = new(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Nine = new(2026, 8, 25, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Eleven = new(2026, 8, 25, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task the_webhook_answers_202_and_cascades_the_scan()
    {
        var id = await BookShipment();

        var (tracked, http) = await TrackedHttpCall(x =>
        {
            x.Post.Json(new RecordCarrierScan(id, "Waco TX", "IN_TRANSIT", Ten))
                .ToUrl("/webhooks/carrier-scan");
            x.StatusCodeShouldBe(202);
        });

        http.Context.Response.Headers.Location.ToString().ShouldBeEmpty();

        // Routed by the global partitioned topology, not by an explicit PublishMessage rule
        tracked.Executed.SingleMessage<RecordCarrierScan>().Location.ShouldBe("Waco TX");
        ShouldHavePublishedLocationUpdate(tracked, "Waco TX");
    }

    [Fact]
    public async Task a_newer_scan_advances_the_shipment()
    {
        var id = await BookShipment();

        await Track().InvokeMessageAndWaitAsync(new RecordCarrierScan(id, "Waco TX", "IN_TRANSIT", Ten));

        var shipment = await LoadShipment(id);
        shipment!.LastLocation.ShouldBe("Waco TX");
        shipment.LastScanAt.ShouldBe(Ten);
    }

    [Fact]
    public async Task an_older_scan_is_a_no_op_and_writes_nothing()
    {
        var id = await BookShipment();

        await Track().InvokeMessageAndWaitAsync(new RecordCarrierScan(id, "Waco TX", "IN_TRANSIT", Ten));
        var afterFirst = (await LoadShipment(id))!;

        // A redelivery, or a scan that overtook a newer one on the wire. Under NativeAck
        // both are expected rather than exceptional.
        var tracked = await Track()
            .InvokeMessageAndWaitAsync(new RecordCarrierScan(id, "Hillsboro TX", "IN_TRANSIT", Nine));

        var afterSecond = (await LoadShipment(id))!;
        afterSecond.LastLocation.ShouldBe("Waco TX");
        afterSecond.LastScanAt.ShouldBe(Ten);

        // Storage.Nothing<T>() means no write at all, and the revision proves it:
        // an update that merely wrote the same values back would still increment it.
        afterSecond.Version.ShouldBe(afterFirst.Version);

        // ...and no event was published at all for a scan that changed nothing
        tracked.AllRecordsInOrder()
            .ShouldNotContain(r => r.Envelope.Message is ShipmentLocationUpdated);
    }

    [Fact]
    public async Task scans_apply_in_order_and_the_latest_wins()
    {
        var id = await BookShipment();

        foreach (var (location, at) in new[] { ("Waco TX", Ten), ("Hillsboro TX", Nine), ("Round Rock TX", Eleven) })
        {
            await Track().InvokeMessageAndWaitAsync(new RecordCarrierScan(id, location, "IN_TRANSIT", at));
        }

        var shipment = (await LoadShipment(id))!;
        shipment.LastLocation.ShouldBe("Round Rock TX");
        shipment.LastScanAt.ShouldBe(Eleven);
    }

    [Fact]
    public async Task a_delivered_scan_also_publishes_shipment_delivered()
    {
        var id = await BookShipment();

        var tracked = await Track()
            .InvokeMessageAndWaitAsync(new RecordCarrierScan(id, "Austin TX", "DELIVERED", Ten));

        ShouldHavePublishedLocationUpdate(tracked, "Austin TX");

        // ShipmentDelivered, by contrast, HAS a subscriber: the delivery saga handles it,
        // so it routes locally and shows up in Sent as normal.
        tracked.Sent.SingleMessage<ShipmentDelivered>().ShipmentId.ShouldBe(id);

        (await LoadShipment(id))!.Status.ShouldBe("Delivered");
    }

    [Fact]
    public async Task a_scan_for_an_unknown_shipment_raises_the_missing_data_exception()
    {
        var id = Guid.NewGuid();

        // The Dapper version updated no rows and published ShipmentLocationUpdated
        // anyway. Making this loud is a deliberate phase 3 change.
        var ex = await Should.ThrowAsync<Wolverine.Persistence.RequiredDataMissingException>(
            () => Track().InvokeMessageAndWaitAsync(
                new RecordCarrierScan(id, "Nowhere", "IN_TRANSIT", Ten)));

        ex.Message.ShouldContain(id.ToString());
    }

    /// <summary>
    /// ⚠️ A FINDING, not a design decision, and the test says so rather than hiding it.
    ///
    /// <para>
    /// ShipmentLocationUpdated is published by the handler but this application declares
    /// no subscriber and no route for it, so Wolverine records <c>NoRoutes</c> and the
    /// event is dropped. It never appears in <c>Sent</c>. Carried over from the
    /// NServiceBus original, where a subscriber elsewhere would have picked it up.
    /// </para>
    ///
    /// <para>
    /// Asserting on the NoRoutes record keeps the handler's behaviour covered — it did
    /// produce the event — while making the gap visible in a failing-if-changed way. If
    /// a route is ever added, this assertion breaks and someone has to look at it. That
    /// is the point.
    /// </para>
    /// </summary>
    private static void ShouldHavePublishedLocationUpdate(ITrackedSession tracked, string location)
    {
        var record = tracked.AllRecordsInOrder()
            .SingleOrDefault(r => r.Envelope.Message is ShipmentLocationUpdated);

        record.ShouldNotBeNull("The handler did not publish a ShipmentLocationUpdated at all");
        record.MessageEventType.ShouldBe(MessageEventType.NoRoutes);
        ((ShipmentLocationUpdated)record.Envelope.Message!).Location.ShouldBe(location);
    }
}
