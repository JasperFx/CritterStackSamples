using CritterWatch.Samples.Testing;
using Shouldly;
using Xunit;

namespace Fleet.PostgresqlQueues.Tests;

/// <summary>
/// Concrete collection definition binding the shared generic fixture to THIS solution's Aspire AppHost.
/// The harness can't name <c>Projects.AppHost</c> itself (that marker type only exists once a Tests
/// project references the AppHost), so each solution declares this one-liner. Carries the
/// <c>Category=DockerRequired</c> trait so the container battery can be filtered out where Docker is absent.
/// </summary>
[Trait("Category", DockerRequired.Category)]
[CollectionDefinition(Name)]
public sealed class FleetAppHostCollection : ICollectionFixture<CritterWatchAppHostFixture<Projects.AppHost>>
{
    public const string Name = "Fleet.PostgresqlQueues AppHost";
}

/// <summary>
/// End-to-end smoke battery: boots the whole Fleet.PostgresqlQueues AppHost (ONE Postgres container — the
/// transport IS the database, no broker — plus the console + the monitored fleet) once, then asserts the
/// console is up and the fleet self-registers.
///
/// <para>
/// The <see cref="fleet_services_register_with_the_console"/> assertion is the load-bearing test for THIS
/// transport: a Wolverine DB-backed queue is a TABLE in a transport schema, so if the console and the
/// services didn't all share the SAME transport schema for the <c>critterwatch</c> control queue, each
/// service would write telemetry to its own private <c>critterwatch</c> table and NONE would appear in
/// <c>/services</c>. Asserting that all five monitored services (Trip trio + Incidents group) register
/// proves the shared-schema control channel actually delivers across hosts — the schema-isolation failure
/// mode plan 04 warns about. (A single-host test could never catch it.)
/// </para>
/// </summary>
[Collection(FleetAppHostCollection.Name)]
public class FleetSmokeTests
{
    private readonly CritterWatchAppHostFixture<Projects.AppHost> _fixture;

    public FleetSmokeTests(CritterWatchAppHostFixture<Projects.AppHost> fixture) => _fixture = fixture;

    [Fact]
    public async Task console_is_up_and_about_returns_200()
    {
        using var client = _fixture.CreateCritterWatchClient();

        // Cheapest liveness check — depends only on the console's own wiring, not on any service registering.
        await client.CritterWatchAboutOk();
    }

    [Fact]
    public async Task fleet_services_register_with_the_console()
    {
        using var client = _fixture.CreateCritterWatchClient();

        // Services self-register ASYNCHRONOUSLY over the shared Postgres control queue after the console is
        // up, so we poll /services rather than asserting immediately. ALL five monitored services must
        // appear — including the Incidents group, which this fleet carries — to prove the shared-schema
        // DB-queue control channel actually delivers cross-host (the schema-isolation catch). The names are
        // each service's ServiceName.
        var services = await client.WaitForServicesAsync(
            ["TripService", "RepairShop", "TripPublisher", "IncidentService", "IncidentPublisher"],
            timeout: TimeSpan.FromMinutes(2));

        var ids = services.Select(s => s.Id).ToList();
        ids.ShouldContain("TripService");
        ids.ShouldContain("RepairShop");
        ids.ShouldContain("TripPublisher");
        ids.ShouldContain("IncidentService");
        ids.ShouldContain("IncidentPublisher");
    }
}
