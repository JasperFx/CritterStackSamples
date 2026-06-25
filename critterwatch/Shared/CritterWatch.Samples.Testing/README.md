# CritterWatch.Samples.Testing

Shared `Aspire.Hosting.Testing` harness consumed by every CritterWatch sample solution's `Tests/`
project. It launches a sample's Aspire AppHost end-to-end and gives the test a tidy way to assert that
the CritterWatch console came up and the fleet registered. Keeping it here means the "boot the AppHost
and smoke the routes" battery is written once, not re-implemented per solution.

## What it provides

| Type | Purpose |
|------|---------|
| `CritterWatchAppHostFixture<TAppHost>` | xUnit `IAsyncLifetime` fixture. Boots `DistributedApplicationTestingBuilder.CreateAsync<TAppHost>()` → `BuildAsync()` → `StartAsync()`, waits for the **`critterwatch`** resource to reach `Running` (5-min ceiling), and exposes `CreateCritterWatchClient()` → `app.CreateHttpClient("critterwatch")`. Generic over the AppHost so each solution passes its own `Projects.*AppHost`. |
| `CritterWatchAssertions` | `HttpClient` extensions: `CritterWatchAboutOk()` (asserts `GET /api/critterwatch/about` is 200) and `WaitForServicesAsync(expectedNames, timeout)` which **polls** `GET /api/critterwatch/services` until every expected service name appears, then returns the parsed list. |
| `ServiceSummaryDto` | Thin parse target for `/api/critterwatch/services` (camelCase wire). `Id` holds the service name. |
| `DockerRequired` / `DockerRequiredCollection` | Trait category + collection so the container-heavy tests can be filtered and serialized. |

## Using it from a solution's `Tests/` project

The fixture is generic, so it has **no** concrete AppHost reference and compiles standalone. A solution's
`Tests/` project references both this library **and** its own AppHost project (which makes Aspire generate
the `Projects.*AppHost` marker type), then:

```csharp
[Collection(DockerRequiredCollection.Name)]
public class fleet_smoke : IClassFixture<CritterWatchAppHostFixture<Projects.Fleet_RabbitMq_AppHost>>
{
    private readonly CritterWatchAppHostFixture<Projects.Fleet_RabbitMq_AppHost> _fixture;

    public fleet_smoke(CritterWatchAppHostFixture<Projects.Fleet_RabbitMq_AppHost> fixture)
        => _fixture = fixture;

    [Fact]
    public async Task console_is_up_and_fleet_registers()
    {
        using var client = _fixture.CreateCritterWatchClient();

        await client.CritterWatchAboutOk();

        var services = await client.WaitForServicesAsync(
            ["TripService", "TripPublisher", "RepairShop"],
            timeout: TimeSpan.FromMinutes(2));

        services.Select(s => s.Id).ShouldContain("TripService");
    }
}
```

Keep solution `Tests/` projects thin: one fixture + a handful of asserts via the helpers.

## Docker requirement

These tests **start real containers** through Aspire, so they need a running **Docker** daemon. They are
tagged `Category=DockerRequired`. To skip them where Docker is unavailable:

```bash
dotnet test --filter "Category!=DockerRequired"
```

**CI must run Docker-in-CI** (a Docker daemon available to the build agent) for this battery to execute;
without it, filter the category out.
