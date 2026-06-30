using Fleet.Common;
using Google.Api.Gax;
using JasperFx;
using JasperFx.Events.Projections;
using JasperFx.Resources;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TripMessages;
using TripService;
using Wolverine;
using Wolverine.CritterWatch;
using Wolverine.Marten;
using Wolverine.Pubsub;

// TripService is a headless Wolverine worker (RunJasperFxCommands gives it the JasperFx CLI:
// `dotnet run -- check-env`, `codegen`, etc.). Aspire launches it as a project resource; standalone,
// it falls back to the localhost docker-compose Postgres + a manually-started Pub/Sub emulator.
return await TripServiceProgram.CreateHostBuilder(args).RunJasperFxCommands(args);

public static class TripServiceProgram
{
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseWolverine(ConfigureWolverine);
    }

    // begin-snippet: trip-service-wolverine-pubsub
    private static void ConfigureWolverine(WolverineOptions opts)
    {
        // The ServiceName is the identity CritterWatch registers this node under — it's the value the
        // Tests battery asserts shows up in GET /api/critterwatch/services.
        opts.ServiceName = "TripService";
        opts.ApplicationAssembly = typeof(StartTripHandler).Assembly;

        // CritterWatch requires Balanced durability even on a single node: projection pause/restart are
        // leader-owned agent-assignment changes, and Solo has no leader/agent distribution (the command
        // silently no-ops). A single Balanced node elects itself leader and runs every agent locally.
        opts.Durability.Mode = DurabilityMode.Balanced;
        opts.EnableAutomaticFailureAcks = false;

        var projectId = SampleConnections.PubsubProjectId();

        // ---- Google Cloud Pub/Sub transport (the ONLY transport swap vs. the RabbitMQ flagship) --------
        // UseEmulatorDetection: when PUBSUB_EMULATOR_HOST is set (the AppHost injects it, pointing at the
        // emulator container) the Google client talks to the emulator; in production the same code reaches
        // real Pub/Sub. AutoProvision: the emulator starts EMPTY, so Wolverine creates the topics + this
        // node's pull subscriptions at startup (in real GCP you'd usually pre-provision and drop this).
        opts.UsePubsub(projectId)
            .UseEmulatorDetection(EmulatorDetection.EmulatorOrProduction)
            .AutoProvision();

        // EXPLICIT routing (no UseConventionalRouting): the service receives commands on its app-inbox topic
        // and cascades the two cross-service messages it produces to the publisher / repair shop's app-inbox
        // topics. The matching listeners are declared in TripPublisher / RepairShop. (Pub/Sub has no tight
        // entity cap like the ASB emulator, but explicit routing keeps the fleet's topology legible and 1:1
        // with the flagship.)
        opts.ListenToPubsubTopic("trip_service_app");
        opts.PublishMessage<ContinueTrip>().ToPubsubTopic("trip_publisher_app");
        opts.PublishMessage<RepairRequested>().ToPubsubTopic("repair_shop_app");

        // Durable inbox/outbox so in-flight messages survive a restart — the realistic production posture
        // CritterWatch is built to observe (it surfaces the inbox/outbox + dead-letter state per endpoint).
        opts.Policies.UseDurableInboxOnAllListeners();
        opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
        opts.Policies.UseDurableLocalQueues();
        opts.Policies.AutoApplyTransactions();

        // ContinueTrip is published BY TripService's projection and handled by TripPublisher — declare it
        // so Wolverine knows the message type even though this service has no handler for it.
        opts.Discovery.IncludeType<ContinueTrip>();

        // ---- Marten event store + projections ----------------------------------------------------
        opts.Services.AddMarten(m =>
            {
                m.Connection(SampleConnections.Postgres());
                m.DatabaseSchemaName = "trips";
                m.DisableNpgsqlLogging = true;

                // Three async projections give CritterWatch's Projections / rebuild UI real targets:
                //  - TripProjection      : single-stream snapshot, also publishes ContinueTrip side effects
                //  - DayProjection       : multi-stream rollup with FanOut
                //  - DistanceProjection  : event projection
                m.Projections.Add<TripProjection>(ProjectionLifecycle.Async);
                m.Projections.Add<DayProjection>(ProjectionLifecycle.Async);
                m.Projections.Add<DistanceProjection>(ProjectionLifecycle.Async);

                // A no-op subscription so this service also exposes a Subscription-typed shard — that's
                // what lights up the Rewind action in the CritterWatch UI (gated on isSubscription).
                m.Projections.Subscribe(new TripNotificationSubscription());
            })
            .IntegrateWithWolverine(o =>
            {
                // Let Wolverine distribute the async projection/subscription agents across the cluster's
                // nodes (here: the single leader node), which is what CritterWatch's agent view reports on.
                o.UseWolverineManagedEventSubscriptionDistribution = true;
            });

        // The service's own inbound control topic. AddCritterWatchMonitoring's second URI must point here
        // (pubsub://{projectId}/trip_service) so CritterWatch can call operator commands back to this service.
        opts.ListenToPubsubTopic("trip_service");

        // ---- CritterWatch monitoring -------------------------------------------------------------
        // First URI  = the console's shared control/telemetry topic (pubsub://{projectId}/critterwatch). The
        //              console listens there with .ListenOnlyAtLeader().
        // Second URI = this service's own topic, so commands route back here.
        // GcpPubsubEndpointUri.Topic builds the canonical "pubsub://{projectId}/{topic}" form Wolverine's
        // Pub/Sub transport resolves.
        opts.AddCritterWatchMonitoring(
            GcpPubsubEndpointUri.Topic(projectId, "critterwatch"),
            GcpPubsubEndpointUri.Topic(projectId, "trip_service")).EnableEventStoreExplorer = true;

        // Build Marten schema on startup (dev convenience). AutoProvision above owns the Pub/Sub topology, so
        // AddResourceSetupOnStartup here is just for the Marten event-store schema.
        opts.Services.AddResourceSetupOnStartup();
    }
    // end-snippet
}
