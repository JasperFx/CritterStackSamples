using CritterWatch.Samples.Testing;
using Bobcat;
using Bobcat.Xunit;
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
/// transport IS the database, no broker — plus the console + the monitored fleet) once, then specifies that
/// the console is up and the fleet self-registers. Written as Bobcat specifications, so a run renders each
/// one as Given/When/Then steps.
///
/// <para>
/// The <see cref="fleet_services_register_with_the_console"/> spec is the load-bearing one for THIS
/// transport: a Wolverine DB-backed queue is a TABLE in a transport schema, so if the console and the
/// services didn't all share the SAME transport schema for the <c>critterwatch</c> control queue, each
/// service would write telemetry to its own private <c>critterwatch</c> table and NONE would appear in
/// <c>/services</c>. Seeing all five monitored services (Trip trio + Incidents group) register proves the
/// shared-schema control channel actually delivers across hosts — the schema-isolation failure mode plan 04
/// warns about. (A single-host test could never catch it.)
/// </para>
/// <para>
/// The fleet runs out of process, so there is no in-process host for Bobcat's store or Wolverine vocabulary
/// to stand on: the steps are this class's own helpers, and they judge with <see cref="SpecAssert"/>.
/// </para>
/// </summary>
[BobcatFeature("Fleet over PostgreSQL queues")]
[Collection(FleetAppHostCollection.Name)]
public class FleetSmokeTests(CritterWatchAppHostFixture<Projects.AppHost> fixture)
{
    private int _aboutStatus;
    private IReadOnlyList<ServiceSummaryDto> _services = [];

    [BobcatSpec]
    public async Task console_is_up_and_about_returns_200()
    {
        // The collection fixture has already booted the Fleet.PostgresqlQueues AppHost

        // Cheapest liveness check — depends only on the console's own wiring, not on any service registering.
        await WhenTheConsoleAboutPageIsRead();

        ThenTheConsoleAnswers(200);
    }

    [BobcatSpec]
    public async Task fleet_services_register_with_the_console()
    {
        // The collection fixture has already booted the Fleet.PostgresqlQueues AppHost

        // Services self-register ASYNCHRONOUSLY over the shared Postgres control queue after the console is
        // up, so the step polls /services rather than reading it once. The names are each service's ServiceName.
        await WhenTheConsoleHasHeardFrom("TripService", "RepairShop", "TripPublisher", "IncidentService", "IncidentPublisher");

        ThenTheRegisteredServicesInclude("TripService", "RepairShop", "TripPublisher", "IncidentService", "IncidentPublisher");
    }

    [When("the console's about page is read")]
    public async Task WhenTheConsoleAboutPageIsRead()
    {
        using var client = fixture.CreateCritterWatchClient();
        using var response = await client.GetAsync("/api/critterwatch/about");
        _aboutStatus = (int)response.StatusCode;
    }

    [Then("the console answers {status}")]
    public void ThenTheConsoleAnswers(int status) => SpecAssert.Check("status", _aboutStatus, status);

    [When("the console has heard from {services}")]
    public async Task WhenTheConsoleHasHeardFrom(params string[] services)
    {
        using var client = fixture.CreateCritterWatchClient();

        // Times out (with every resource's log tail in the message) if they never all register
        _services = await fixture.WaitForServicesAsync(client, services, timeout: TimeSpan.FromMinutes(2));
    }

    [Then("the registered services include {services}")]
    public void ThenTheRegisteredServicesInclude(params string[] services)
    {
        var ids = _services.Select(s => s.Id).ToList();
        foreach (var service in services)
        {
            SpecAssert.Fact(ids.Contains(service), $"{service} is registered (registered: {string.Join(", ", ids)})");
        }
    }
}
