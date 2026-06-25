using CritterWatch.Services.Hosting;
using Fleet.Common;
using Wolverine.AzureServiceBus;
using Wolverine.CritterWatch;

// =============================================================================================
// CritterWatchConsole — the standalone monitoring dashboard.
//
// This is the minimal app an operator writes to run CritterWatch as its own dedicated backend: call
// AddCritterWatch (consumes the packed NuGet) to register the console's store + Wolverine + SignalR +
// HTTP endpoints + SPA, configure the transport's control channel inside `configureWolverine`, then
// UseCritterWatch to map everything. The console's own storage is Postgres; monitored services reach it
// over the Azure Service Bus control channel.
// =============================================================================================

var builder = WebApplication.CreateBuilder(args);

// The console's own Postgres store. Under Aspire this is the `critterwatch` database; standalone it falls
// back to the localhost docker-compose Postgres. NOTE: this is the *console's* store, entirely separate
// from each monitored service's event store.
var consoleConnectionString = SampleConnections.Postgres();

// begin-snippet: console-asb-control-channel
builder.AddCritterWatch(
    consoleConnectionString,
    configureWolverine: opts =>
    {
        // Stand up the Azure Service Bus transport the monitored fleet publishes telemetry on. Same
        // namespace the services use (Aspire `messaging` reference → the emulator, else the well-known
        // development-emulator literal).
        opts.UseAzureServiceBus(SampleConnections.AzureServiceBus());

        // #356 emulator constraint — drop Wolverine's per-node response/retry/control SYSTEM queues so the
        // console + the Trip trio stay under the emulator's hard 50-entity-per-namespace cap. Mirrors the
        // services. Under Aspire's `.RunAsEmulator()` every queue is pre-declared by the AppHost
        // (AddServiceBusQueue), so — unlike the docker-compose Trips2 sample — we set NO
        // ManagementConnectionString and call NO AutoProvision: the `critterwatch` queue already exists.
        var asb = opts.Transports.GetOrCreate<AzureServiceBusTransport>();
        asb.SystemQueuesEnabled = false;

        // THE control channel. Every monitored service points the FIRST URI of its
        // AddCritterWatchMonitoring(...) at "asb://queue/critterwatch" — i.e. this queue. The console
        // listens here for their telemetry + registration:
        //   - ListenOnlyAtLeader()      : in a multi-node console cluster, exactly one node owns this
        //                                 shared queue (no split-brain). On this single-node sample the
        //                                 one node elects itself leader and owns it automatically.
        //   - UseCritterWatchSerializer(): pins CritterWatch's wire-format (the brotli-framed STJ
        //                                 serializer) so the encode/decode contract matches what
        //                                 AddCritterWatchMonitoring configures on the publisher side. (The
        //                                 brotli payload-shrink path keeps DDL-heavy ServiceUpdates under
        //                                 the ASB Standard tier's 256 KB body limit.)
        // No serializer call needed: AddCritterWatch registers the CritterWatch wire-format serializer
        // globally (by a unique content-type), so the console decodes telemetry with zero per-endpoint config.
        opts.ListenToAzureServiceBusQueue("critterwatch")
            .ListenOnlyAtLeader();
    },
    // Single-node sample → no sharded external topology to wire, so cluster partitioning stays off.
    // Production multi-node consoles pass enableClusterPartitioning: true plus a
    // configureClusterShardedTopology that declares sharded ASB queues for "critterwatch".
    enableClusterPartitioning: false);
// end-snippet

builder.Services.AddHealthChecks();

var app = builder.Build();

// Maps CritterWatch's HTTP endpoints (/api/critterwatch/*), the SignalR hub (/api/messages), and serves
// the embedded SPA. The license check is skipped in the Development environment (Aspire's default).
app.UseCritterWatch();
app.MapHealthChecks("/health");

app.Run();
