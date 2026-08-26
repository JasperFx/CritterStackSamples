using JasperFx;
using Polecat;

namespace Tests;

/// <summary>
/// Not a handler and not an endpoint, but it verifies the claim phase 3 rests on: a
/// document store writes the WHOLE document, so the lost-update protection that
/// column-scoped SQL gave away for free has to be put back deliberately.
/// </summary>
public class optimistic_concurrency(AppFixture fixture) : IntegrationContext(fixture)
{
    [Fact]
    public async Task the_losing_write_throws_instead_of_discarding_the_winner()
    {
        var id = await BookShipment();

        await using var first = Store.LightweightSession();
        await using var second = Store.LightweightSession();

        // Both read the same revision, the way a scan handler and a cancel handler on
        // two different queues would.
        var forScan = (await first.LoadAsync<Shipment>(id))!;
        var forCancel = (await second.LoadAsync<Shipment>(id))!;

        forScan.Version.ShouldBe(forCancel.Version);

        forScan.LastLocation = "Waco TX";
        first.Update(forScan);
        await first.SaveChangesAsync();

        forCancel.Status = "Cancelled";
        second.Update(forCancel);

        // Without IRevisioned this would silently overwrite the scan. Program.cs retries
        // ConcurrencyException with a short cooldown, which is why the handler can be a
        // plain read-modify-write.
        await Should.ThrowAsync<ConcurrencyException>(() => second.SaveChangesAsync());

        // The winner's change survived
        (await LoadShipment(id))!.LastLocation.ShouldBe("Waco TX");
    }

    [Fact]
    public async Task every_write_advances_the_revision()
    {
        var id = Guid.NewGuid();

        await Track().InvokeMessageAndWaitAsync(new BookShipment(id, "Dallas", "Austin", "acme", 1m));
        var afterBooking = (await LoadShipment(id))!.Version;

        await Track().InvokeMessageAndWaitAsync(
            new RecordCarrierScan(id, "Waco TX", "IN_TRANSIT", DateTimeOffset.UtcNow));

        (await LoadShipment(id))!.Version.ShouldBe(afterBooking + 1);
    }
}
