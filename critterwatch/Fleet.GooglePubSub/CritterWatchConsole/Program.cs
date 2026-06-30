using CritterWatch.Services.Hosting;
using Fleet.Common;
using Google.Api.Gax;
using Wolverine.CritterWatch;
using Wolverine.Pubsub;

// =============================================================================================
// CritterWatchConsole — the standalone monitoring dashboard (Google Cloud Pub/Sub flavor).
//
// This is the minimal app an operator writes to run CritterWatch as its own dedicated backend: call
// AddCritterWatch (consumes the packed NuGet) to register the console's store + Wolverine + SignalR +
// HTTP endpoints + SPA, configure the transport's control channel inside `configureWolverine`, then
// UseCritterWatch to map everything. The console's own storage is Postgres; monitored services reach it
// over the Google Cloud Pub/Sub control channel.
// =============================================================================================

var builder = WebApplication.CreateBuilder(args);

// The console's own Postgres store. Under Aspire this is the `critterwatch` database; standalone it falls
// back to the localhost docker-compose Postgres. NOTE: this is the *console's* store, entirely separate
// from each monitored service's event store.
var consoleConnectionString = SampleConnections.Postgres();
var projectId = SampleConnections.PubsubProjectId();

// begin-snippet: console-pubsub-control-channel
builder.AddCritterWatch(
    consoleConnectionString,
    configureWolverine: opts =>
    {
        // Stand up the Google Cloud Pub/Sub transport the monitored fleet publishes telemetry on, against
        // the SAME project id every service uses.
        //   - UseEmulatorDetection(EmulatorOrProduction): when PUBSUB_EMULATOR_HOST is set (the AppHost
        //     injects it, pointing at the emulator container), the Google client talks to the emulator over
        //     an insecure channel; in production the same code talks to real Pub/Sub. The env var is the
        //     only switch — no code change between emulator and cloud.
        //   - AutoProvision(): the emulator starts EMPTY, so Wolverine must create the "critterwatch" topic
        //     and this console's pull subscription on it at startup (in real GCP you'd usually pre-provision
        //     with IaC and drop this).
        opts.UsePubsub(projectId)
            .UseEmulatorDetection(EmulatorDetection.EmulatorOrProduction)
            .AutoProvision();

        // THE control channel. Every monitored service points the FIRST URI of its
        // AddCritterWatchMonitoring(...) at "pubsub://{projectId}/critterwatch" — i.e. this topic. The
        // console listens here for their telemetry + registration:
        //   - ListenOnlyAtLeader(): in a multi-node console cluster, exactly one node owns this shared
        //     subscription so Pub/Sub delivers each telemetry message once (no split-brain). On this
        //     single-node sample the one node elects itself leader and owns it automatically.
        //     (Relies on wolverine#3258: leader-pinned Pub/Sub listeners must read from ONE shared,
        //     cluster-stable subscription — otherwise each node would create its own subscription and
        //     Pub/Sub would fan a copy of every message to all of them. That fix shipped in WolverineFx 6.16.0.)
        // No serializer call needed: AddCritterWatch registers the CritterWatch wire-format serializer
        // globally (by a unique content-type), so the console decodes telemetry with zero per-endpoint config.
        opts.ListenToPubsubTopic("critterwatch")
            .ListenOnlyAtLeader();
    },
    // Single-node sample → no sharded external topology to wire, so cluster partitioning stays off.
    enableClusterPartitioning: false);
// end-snippet

builder.Services.AddHealthChecks();

var app = builder.Build();

// Maps CritterWatch's HTTP endpoints (/api/critterwatch/*), the SignalR hub (/api/messages), and serves
// the embedded SPA. The license check is skipped in the Development environment (Aspire's default).
app.UseCritterWatch();
app.MapHealthChecks("/health");

app.Run();
