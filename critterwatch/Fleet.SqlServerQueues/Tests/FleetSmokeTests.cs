using CritterWatch.Samples.Testing;
using Shouldly;
using Xunit;

namespace Fleet.SqlServerQueues.Tests;

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
    public const string Name = "Fleet.SqlServerQueues AppHost";
}

/// <summary>
/// End-to-end smoke battery: boots the whole Fleet.SqlServerQueues AppHost (the single SQL Server 2025
/// container + the Polecat console + the monitored fleet) once, then asserts the console is up and the
/// fleet self-registers over the SQL Server database-queue control channel.
///
/// <para>
/// This is the load-bearing test for the DB-queue schema-coupling failure mode (plan 04's CRITICAL note):
/// if any service used a different Wolverine transport schema for the "critterwatch" control queue, its
/// telemetry would land in its own private queue table and it would NEVER appear in <c>/services</c>. A
/// single-host control-channel test wouldn't catch that; this multi-host boot does.
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

        // Services self-register ASYNCHRONOUSLY over the SQL Server DB-queue control channel after the
        // console is up, so we poll /services rather than asserting immediately. The names are each
        // service's ServiceName. Incidents.Service is asserted alongside the Trip trio because this fleet
        // (per the locked decision) carries the Incidents group.
        // Route through the fixture (not the bare HttpClient extension) so a timeout dumps every resource's
        // captured logs into the failure message — essential for diagnosing why the fleet doesn't register.
        var services = await _fixture.WaitForServicesAsync(
            client,
            ["TripService", "RepairShop", "IncidentService"],
            timeout: TimeSpan.FromMinutes(3));

        var ids = services.Select(s => s.Id).ToList();
        ids.ShouldContain("TripService");
        ids.ShouldContain("RepairShop");
        ids.ShouldContain("IncidentService");
    }
}
