using CritterWatch.Services.Hosting;
using Fleet.Common;
using Wolverine.CritterWatch;
using Wolverine.RabbitMQ;

// =============================================================================================
// CritterWatchConsole — the standalone monitoring dashboard.
//
// This is the minimal app an operator writes to run CritterWatch as its own dedicated backend: call
// AddCritterWatch (consumes the packed NuGet) to register the console's store + Wolverine + SignalR +
// HTTP endpoints + SPA, configure the transport's control channel inside `configureWolverine`, then
// UseCritterWatch to map everything. The console's own storage is Postgres; monitored services reach it
// over the RabbitMQ control channel.
// =============================================================================================

var builder = WebApplication.CreateBuilder(args);

// The console's own Postgres store. Under Aspire this is the `critterwatch` database; standalone it falls
// back to the localhost docker-compose Postgres. NOTE: this is the *console's* store, entirely separate
// from each monitored service's event store.
var consoleConnectionString = SampleConnections.Postgres();

// begin-snippet: console-rabbitmq-control-channel
builder.AddCritterWatch(
    consoleConnectionString,
    configureWolverine: opts =>
    {
        // Stand up the RabbitMQ transport the monitored fleet publishes telemetry on. Same broker the
        // services use (Aspire `rabbitmq` reference, else localhost). AutoProvision declares the queue.
        opts.UseRabbitMq(SampleConnections.RabbitMq())
            .DisableDeadLetterQueueing()
            .AutoProvision();

        // THE control channel. Every monitored service points the FIRST URI of its
        // AddCritterWatchMonitoring(...) at "rabbitmq://queue/critterwatch" — i.e. this queue. The console
        // listens here for their telemetry + registration:
        //   - ListenOnlyAtLeader()      : in a multi-node console cluster, exactly one node owns this
        //                                 shared queue (no split-brain). On this single-node sample the
        //                                 one node elects itself leader and owns it automatically.
        // No serializer call needed: AddCritterWatch registers the CritterWatch wire-format serializer
        // globally (by a unique content-type), so the console decodes telemetry on any transport with
        // zero per-endpoint serializer config.
        opts.ListenToRabbitQueue("critterwatch")
            .ListenOnlyAtLeader()
            // CritterWatch 1.0 applies PartitionProcessingByGroupId() to its ingest listener (CritterWatch#1127), and Wolverine
            // (>= 6.24, GH-3708) refuses that on an Inline endpoint — the broker default. Native acks +
            // parallel processing is the mode that exists for exactly this combination.
            .ProcessInParallelWithNativeAcks();
    },
    // Single-node sample → no sharded external topology to wire, so cluster partitioning stays off.
    // Production multi-node consoles pass enableClusterPartitioning: true plus a
    // configureClusterShardedTopology that declares UseShardedRabbitQueues("critterwatch", N).
    enableClusterPartitioning: false);
// end-snippet

builder.Services.AddHealthChecks();

var app = builder.Build();

// Maps CritterWatch's HTTP endpoints (/api/critterwatch/*), the SignalR hub (/api/messages), and serves
// the embedded SPA. The license check is skipped in the Development environment (Aspire's default).
app.UseCritterWatch();
app.MapHealthChecks("/health");

app.Run();
