using CritterWatch.Samples.Testing;
using Shouldly;
using Xunit;

namespace Fleet.RabbitMq.Tests;

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
    public const string Name = "Fleet.RabbitMq AppHost";
}

/// <summary>
/// End-to-end smoke battery: boots the whole Fleet.RabbitMq AppHost (RabbitMQ + Postgres containers + the
/// console + the monitored fleet) once, then asserts the console is up and the fleet self-registers.
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

        // Services self-register ASYNCHRONOUSLY over the RabbitMQ control channel after the console is up,
        // so we poll /services rather than asserting immediately. The names are each service's ServiceName.
        var services = await client.WaitForServicesAsync(
            ["TripService", "RepairShop", "IncidentService"],
            timeout: TimeSpan.FromMinutes(2));

        var ids = services.Select(s => s.Id).ToList();
        ids.ShouldContain("TripService");
        ids.ShouldContain("RepairShop");
        ids.ShouldContain("IncidentService");
    }
}
