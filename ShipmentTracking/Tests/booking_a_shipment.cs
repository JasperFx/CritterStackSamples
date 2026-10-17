using Bobcat;
using Bobcat.Xunit;
using ShipmentTracking.Api;

namespace Tests;

/// <summary>
/// The original booking tests, as Bobcat specifications: the endpoint, the handler, and the whole chain from
/// one HTTP call.
/// </summary>
[BobcatFeature("Booking a shipment")]
public class booking_a_shipment(AppFixture fixture) : ShipmentSpec(fixture)
{
    [BobcatSpec(typeof(BookShipmentRequest))]
    public async Task the_endpoint_answers_202_with_a_location_header()
    {
        await WhenPosted(new BookShipmentRequest("Dallas", "Austin", "acme", 12.5m), "/shipments");

        ThenTheResponseStatusIs(202);
        var accepted = TheResponse<ShipmentAccepted>();
        ThenMatches(accepted, Specify<ShipmentAccepted>().Check(x => x.ShipmentId.ShouldNotBe(Guid.Empty, null)));
        ThenTheLocationIs($"/shipments/{accepted.ShipmentId}");
    }

    [BobcatSpec(typeof(BookShipmentRequest))]
    public async Task the_endpoint_cascades_the_command_rather_than_handling_it()
    {
        await WhenPosted(new BookShipmentRequest("Dallas", "Austin", "acme", 12.5m), "/shipments");

        ThenTheResponseStatusIs(202);
        var accepted = TheResponse<ShipmentAccepted>();

        // The endpoint returns a tuple; the second element goes out through the outbox.
        // It is SENT to the shipment-commands queue, not invoked inline.
        ThenMessageSent<BookShipment>("rabbitmq://queue/shipment-commands");
        ThenMessageSent(Specify<BookShipment>()
            .With(x => x.ShipmentId, accepted.ShipmentId)
            .With(x => x.Origin, "Dallas"));
    }

    [BobcatSpec(typeof(BookShipment))]
    public async Task the_handler_inserts_the_document_and_cascades_two_messages()
    {
        var id = Guid.NewGuid();

        await WhenReceived(new BookShipment(id, "Dallas", "Austin", "acme", 12.5m));

        // Storage.Insert() actually wrote, through the same transaction as the outbox.
        // NOT Status == "Booked": the act waits for ALL cascading work, so the label chain has also
        // run. That is proof of the insert, because RecordTrackingNumberHandler only advances a
        // shipment whose status is exactly "Booked".
        await ThenDocument<Shipment>(id, Specify<Shipment>()
            .With(x => x.Origin, "Dallas")
            .With(x => x.Destination, "Austin")
            .With(x => x.Carrier, "acme")
            .With(x => x.WeightKg, 12.5m)
            .With(x => x.Status, "Labelled"));

        ThenMessageSent(Specify<ShipmentBooked>().With(x => x.ShipmentId, id).With(x => x.Carrier, "acme"));
        ThenMessageSent(new GenerateLabel(id, "acme"));
    }

    [BobcatSpec(typeof(BookShipmentRequest))]
    public async Task the_whole_chain_runs_end_to_end_from_one_http_call()
    {
        // One HTTP call, five message types, three queues, a saga and two document writes.
        // Nothing here sleeps: the act returns when the last handler finishes.
        await WhenPosted(new BookShipmentRequest("Dallas", "Austin", "acme", 12.5m), "/shipments");

        ThenTheResponseStatusIs(202);
        var id = TheResponse<ShipmentAccepted>().ShipmentId;

        ThenHandled(typeof(BookShipment));
        ThenHandled(typeof(GenerateLabel));
        ThenHandled(typeof(RecordTrackingNumber));

        await ThenDocument<Shipment>(id, Specify<Shipment>()
            .With(x => x.Status, "Labelled")
            .Check(x => x.TrackingNumber.ShouldStartWith("ACME-", Case.Sensitive, null)));
    }
}
