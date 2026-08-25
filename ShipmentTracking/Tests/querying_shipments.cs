namespace Tests;

public class querying_shipments(AppFixture fixture) : IntegrationContext(fixture)
{
    [Fact]
    public async Task get_by_id_returns_the_shipment()
    {
        var id = await BookShipment("Dallas", "Austin", "acme", 12.5m);

        var result = await Host.Scenario(x =>
        {
            x.Get.Url($"/shipments/{id}");
            x.StatusCodeShouldBeOk();
        });

        var shipment = await result.ReadAsJsonAsync<Shipment>();
        shipment.ShouldNotBeNull();
        shipment.Id.ShouldBe(id);
        shipment.Origin.ShouldBe("Dallas");
        shipment.Destination.ShouldBe("Austin");
    }

    [Fact]
    public async Task get_by_id_answers_404_for_an_unknown_shipment()
    {
        // [Entity]'s OnMissing.Simple404 short-circuits BEFORE the endpoint body runs,
        // and still contributes Produces(404) to the OpenAPI document.
        await Host.Scenario(x =>
        {
            x.Get.Url($"/shipments/{Guid.NewGuid()}");
            x.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task get_all_returns_every_shipment()
    {
        var first = await BookShipment("Dallas", "Austin", "acme", 1m);
        var second = await BookShipment("Houston", "El Paso", "globex", 2m);

        var result = await Host.Scenario(x =>
        {
            x.Get.Url("/shipments");
            x.StatusCodeShouldBeOk();
        });

        var all = await result.ReadAsJsonAsync<List<Shipment>>();
        all.ShouldNotBeNull();
        all.Select(x => x.Id).ShouldBe(new[] { first, second }, ignoreOrder: true);
    }

    [Fact]
    public async Task get_all_returns_an_empty_list_rather_than_404_when_there_is_nothing()
    {
        // [All] has no Required / OnMissing, because an empty table is an empty list
        // rather than a miss. Each test starts from a reset store, so this is that case.
        var result = await Host.Scenario(x =>
        {
            x.Get.Url("/shipments");
            x.StatusCodeShouldBeOk();
        });

        (await result.ReadAsJsonAsync<List<Shipment>>()).ShouldBeEmpty();
    }
}
