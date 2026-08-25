using Alba;
using JasperFx.CommandLine;
using JasperFx.Core;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Polecat;
using ShipmentTracking.Handlers;
using Wolverine;
using Wolverine.CritterWatch;
using Wolverine.Polecat;
using Wolverine.RabbitMQ;
using Wolverine.Tracking;
using Xunit;

namespace Tests;

/// <summary>
/// Bootstraps the real application once for the whole suite.
///
/// "Real database, real Wolverine, no mocks" is meant literally: this is
/// ShipmentTracking's own Program.cs, its own Polecat store against a real SQL
/// Server 2025, its own handlers, its own generated code. Two things are
/// substituted, and both are substitutions of things that are not the system
/// under test.
/// </summary>
public class AppFixture : IAsyncLifetime
{
    /// <summary>
    /// A database of its own, so running the suite cannot wipe whatever you were
    /// looking at in the dev database.
    /// </summary>
    public const string TestingDatabase = "ShipmentTracking_Testing";

    public IAlbaHost Host { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Required when the application uses JasperFx for command line processing
        // and you want WebApplicationFactory / Alba to host it. Without it,
        // RunJasperFxCommands(args) takes over and the host never starts.
        JasperFxEnvironment.AutoStartHost = true;

        // Polecat creates its tables on demand but never the database itself, so
        // the test database has to exist before the first connection. Six lines of
        // ADO.NET rather than a fixture library, because that is genuinely all it is.
        await CreateTestDatabaseAsync();

        Host = await AlbaHost.For<Program>(
            x => x.ConfigureServices(services =>
            {
                // One node, no leader election, no agent assignment churn.
                services.RunWolverineInSoloMode();

                // Belt and braces. CritterWatch:Enabled defaults to false, so nothing
                // is registered in the first place — but this is the documented,
                // order-independent off switch, and it is what keeps the suite honest
                // if someone ever flips the default or an environment variable leaks
                // in. Running the monitoring pipeline under Alba is pure overhead:
                // nothing consumes the telemetry, and AddCritterWatchMonitoring also
                // turns on message-causation and event-append tracking, which do
                // per-envelope work in the hot path.
                //
                // Do NOT try to do this by branching on IHostEnvironment. Alba runs as
                // Development by default, so the branch would not fire here — and a
                // developer running this service against a real local console IS a
                // normal Development activity, so disabling there would break the setup
                // they most want working.
                services.DisableCritterWatch();

                // Stands in for the downstream operations service. EscalateLateShipment
                // is routed to shipment-operations and nothing in THIS application
                // consumes it, so with IncludeExternalTransports() on, a tracked session
                // that produces one waits forever for a delivery that never lands.
                // Listening to the queue makes the escalation observable; Wolverine
                // records NoHandlers for it, which is the honest answer — this service
                // really does not handle escalations.
                services.ConfigureWolverine(opts =>
                    opts.ListenToRabbitQueue("shipment-operations"));

                // The one service double in the suite, and it stands in for a THIRD
                // PARTY, not for anything this application owns. FakeCarrierLabelClient
                // sleeps 45 seconds on purpose — that duration is the entire reason
                // label-generation is a Durable listener — and a test may not sleep.
                services.AddSingleton<ICarrierLabelClient, InstantCarrierLabelClient>();
            }),
            ConfigurationOverride.Create(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Shipments"] = TestingConnectionString
            }));
    }

    public async Task DisposeAsync()
    {
        if (Host is not null) await Host.DisposeAsync();
    }

    public static string TestingConnectionString =>
        $"Server=localhost,1433;Database={TestingDatabase};User Id=sa;Password=P@ssw0rd!;TrustServerCertificate=true";

    private static async Task CreateTestDatabaseAsync()
    {
        await using var connection = new SqlConnection(
            "Server=localhost,1433;Database=master;User Id=sa;Password=P@ssw0rd!;TrustServerCertificate=true");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"if db_id('{TestingDatabase}') is null create database [{TestingDatabase}]";
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// Returns immediately. The production stand-in sleeps 45 seconds to model the
/// carrier's real latency; that belongs in the running application, not in a test.
/// </summary>
public class InstantCarrierLabelClient : ICarrierLabelClient
{
    public Task<string> CreateLabelAsync(Guid shipmentId, string carrier, CancellationToken token)
        => Task.FromResult($"{carrier.ToUpperInvariant()}-{shipmentId.ToString("N")[..10].ToUpperInvariant()}");
}

[CollectionDefinition("integration")]
public class IntegrationCollection : ICollectionFixture<AppFixture>;

[Collection("integration")]
public abstract class IntegrationContext(AppFixture fixture) : IAsyncLifetime
{
    public IAlbaHost Host => fixture.Host;

    public IDocumentStore Store => Host.DocumentStore();

    /// <summary>
    /// A clean slate per test. Polecat's own reset, not a hand-rolled TRUNCATE.
    /// </summary>
    public async Task InitializeAsync() => await Host.ResetAllPolecatDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// The one place the tracked-session configuration lives.
    ///
    /// <para>
    /// <b>IncludeExternalTransports() is not optional here, and leaving it off is the
    /// trap this suite was nearly built on.</b> Every command in this application is
    /// routed to a RabbitMQ queue by Program.cs. Without this call, a Sent record whose
    /// destination is not <c>local://</c> is marked complete the instant the send is
    /// made — so the session goes quiet immediately, the assertions run before the
    /// handler on the other side of the broker has started, and every test passes while
    /// proving nothing.
    /// </para>
    ///
    /// <para>
    /// The first draft of this fixture called
    /// <c>DisableAllExternalWolverineTransports()</c>, which is the usual advice for a
    /// Wolverine test host. That is worse than it looks for this application: stubbing
    /// replaces each external sender with a <c>NullSender</c>, so a command routed
    /// <c>ToRabbitQueue(...)</c> is dropped rather than rerouted. The tracked session
    /// showed <c>Sent: BookShipment -&gt; rabbitmq://queue/shipment-commands</c> and the
    /// database held zero shipments — a green test over a system that had done nothing.
    /// Stubbing is right for an application whose messages route locally by convention;
    /// it is wrong for one whose topology is the point.
    /// </para>
    /// </summary>
    protected TrackedSessionConfiguration Track() =>
        Host.TrackActivity()
            .IncludeExternalTransports()
            .Timeout(30.Seconds());

    /// <summary>
    /// An in-memory HTTP call (Alba) wrapped in a Wolverine tracked session, so the
    /// test does not continue until every cascading message the request spawned has
    /// been handled. This is the shape that replaces a sleep.
    ///
    /// Not a shipped API — it composes Host.Scenario and Host.ExecuteAndWaitAsync.
    /// </summary>
    protected async Task<(ITrackedSession Tracked, IScenarioResult Http)> TrackedHttpCall(
        Action<Scenario> configure)
    {
        IScenarioResult result = null!;

        // A local function rather than an inline lambda: ExecuteAndWaitAsync has both a
        // Task and a ValueTask overload, and an `async _ => { }` lambda is ambiguous
        // between them.
        async Task callTheEndpoint() => result = await Host.Scenario(configure);

        var tracked = await Track().ExecuteAndWaitAsync(_ => callTheEndpoint());

        return (tracked, result);
    }

    /// <summary>Book a shipment and return its id once all cascading work has settled.</summary>
    protected async Task<Guid> BookShipment(
        string origin = "Dallas", string destination = "Austin",
        string carrier = "acme", decimal weightKg = 12.5m)
    {
        var id = Guid.NewGuid();

        await Track().InvokeMessageAndWaitAsync(
            new BookShipment(id, origin, destination, carrier, weightKg));

        return id;
    }

    protected async Task<Shipment?> LoadShipment(Guid id)
    {
        await using var session = Store.QuerySession();
        return await session.LoadAsync<Shipment>(id);
    }
}
