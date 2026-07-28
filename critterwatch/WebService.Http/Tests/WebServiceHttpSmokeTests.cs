using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting.Testing;
using CritterWatch.Samples.Testing;
using Shouldly;
using Xunit;

namespace WebService.Http.Tests;

/// <summary>
/// Concrete collection definition binding the shared generic fixture to THIS solution's Aspire AppHost.
/// The harness can't name <c>Projects.AppHost</c> itself (that marker type only exists once a Tests project
/// references the AppHost), so each solution declares this one-liner. Carries the
/// <c>Category=DockerRequired</c> trait so the container battery can be filtered out where Docker is absent.
/// </summary>
[Trait("Category", DockerRequired.Category)]
[CollectionDefinition(Name)]
public sealed class WebServiceHttpAppHostCollection : ICollectionFixture<CritterWatchAppHostFixture<Projects.AppHost>>
{
    public const string Name = "WebService.Http AppHost";
}

/// <summary>
/// End-to-end smoke battery: boots the whole WebService.Http AppHost (Postgres container + the console + the
/// monitored OrderService) once, then asserts (1) the console is up, (2) the OrderService's OWN HTTP
/// endpoints respond, and (3) the OrderService self-registers with the console over the HTTP transport.
/// </summary>
[Collection(WebServiceHttpAppHostCollection.Name)]
public class WebServiceHttpSmokeTests
{
    private readonly CritterWatchAppHostFixture<Projects.AppHost> _fixture;

    public WebServiceHttpSmokeTests(CritterWatchAppHostFixture<Projects.AppHost> fixture) => _fixture = fixture;

    [Fact]
    public async Task console_is_up_and_about_returns_200()
    {
        using var client = _fixture.CreateCritterWatchClient();

        // Cheapest liveness check — depends only on the console's own wiring, not on the service registering.
        await client.CritterWatchAboutOk();
    }

    [Fact]
    public async Task order_service_own_http_endpoints_respond()
    {
        // Hit the MONITORED web service directly (not the console). Proves the HTTP service is actually up and
        // serving its own surface — both a bare ASP.NET endpoint and a Wolverine HTTP endpoint round-trip.
        using var service = _fixture.App.CreateHttpClient("OrderService", "http");

        // Bare ASP.NET GET /  (surfaced on CritterWatch's HTTP tab via AddCritterWatchHttp()).
        using var root = await service.GetAsync("/");
        root.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Wolverine HTTP POST /orders — starts an event stream and returns 201 Created + Location.
        using var placed = await service.PostAsJsonAsync(
            "/orders", new { customer = "Acme", items = new[] { "widget", "gadget" } });
        placed.StatusCode.ShouldBe(HttpStatusCode.Created);
        placed.Headers.Location.ShouldNotBeNull();
    }

    [Fact]
    public async Task order_service_registers_with_the_console_over_http_transport()
    {
        using var client = _fixture.CreateCritterWatchClient();

        // The OrderService self-registers ASYNCHRONOUSLY — it POSTs its registration/heartbeat over the HTTP
        // transport to the console's /_wolverine/invoke route after the console is up — so we poll /services
        // rather than asserting immediately.
        // Route through the fixture (not the bare HttpClient extension) so a timeout dumps every resource's
        // captured logs into the failure message — essential for diagnosing why OrderService doesn't register.
        var services = await _fixture.WaitForServicesAsync(
            client,
            ["OrderService"],
            timeout: TimeSpan.FromMinutes(2));

        services.Select(s => s.Id).ShouldContain("OrderService");
    }

    /// <summary>
    /// The CONTROL direction of the monitoring link — console → service — which this battery never
    /// covered. Everything above exercises telemetry (service → console), which is why ProductSupport#34
    /// shipped: the console's send side throws on every attempt, and nothing here noticed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asserted through the console's logs rather than by driving an operator command, because operator
    /// commands travel CritterWatch's SignalR hub and this harness has no SignalR client. It still pins
    /// the defect: the console pushes to a registered service on its own, so a broken send surfaces as
    /// <c>BufferedSendingAgent</c> failures in the console log with no operator action at all — exactly
    /// how the reporter found it.
    /// </para>
    /// <para>
    /// SKIPPED until this sample bumps past wolverine#3681 (GH-3690). It fails on the pinned WolverineFx
    /// 6.23.1 — legitimately, because the control channel really is broken there. Un-skip with the bump;
    /// see the warning in README.md.
    /// </para>
    /// </remarks>
    [Fact(Skip = "Control channel is broken on WolverineFx 6.23.1 — un-skip when this sample bumps past wolverine#3681 (GH-3690).")]
    public async Task console_control_channel_sends_without_transport_errors()
    {
        using var client = _fixture.CreateCritterWatchClient();

        // Registration first: the console has nothing to send to until OrderService reports its control URI.
        await _fixture.WaitForServicesAsync(client, ["OrderService"], timeout: TimeSpan.FromMinutes(2));

        // Give the console a window to actually push to the freshly-registered service.
        await Task.Delay(TimeSpan.FromSeconds(20));

        var consoleLogs = _fixture.DumpResourceLogs(CritterWatchAppHostFixture<Projects.AppHost>.CritterWatchResourceName);

        // The GH-3690 signature. Matched on the exception text rather than the sender type so the assertion
        // survives Wolverine renaming BufferedSendingAgent.
        consoleLogs.ShouldNotContain("An invalid request URI was provided");
        consoleLogs.ShouldNotContain("Failed to send outgoing envelopes batch");
    }
}
