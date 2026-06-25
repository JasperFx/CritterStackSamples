using Fleet.Common;
using JasperFx;
using JasperFx.Core;
using JasperFx.Events.Projections;
using JasperFx.Resources;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TripMessages;
using TripService;
using Wolverine;
using Wolverine.AzureServiceBus;
using Wolverine.CritterWatch;
using Wolverine.Marten;

// TripService is a headless Wolverine worker (RunJasperFxCommands gives it the JasperFx CLI:
// `dotnet run -- check-env`, `codegen`, etc.). Aspire launches it as a project resource; standalone,
// it falls back to the localhost docker-compose Postgres + a manually-started ASB emulator.
return await TripServiceProgram.CreateHostBuilder(args).RunJasperFxCommands(args);

public static class TripServiceProgram
{
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseWolverine(ConfigureWolverine);
    }

    // begin-snippet: trip-service-wolverine-asb
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

        // ---- Azure Service Bus transport (the ONLY transport swap vs. the RabbitMQ flagship) ----------
        // Aspire injects ConnectionStrings__messaging from `builder.AddAzureServiceBus("messaging")
        // .RunAsEmulator()`; standalone we fall back to the well-known development-emulator literal.
        opts.UseAzureServiceBus(SampleConnections.AzureServiceBus());

        // #356 emulator constraint — drop Wolverine's per-node response/retry/control SYSTEM queues. The
        // ASB emulator caps a namespace at 50 entities; the system queues plus a per-message-type queue set
        // would blow past it. With them off, the fleet's entity count is just the handful of explicit
        // queues declared below (mirrored 1:1 by the AppHost's AddServiceBusQueue calls).
        var asb = opts.Transports.GetOrCreate<AzureServiceBusTransport>();
        asb.SystemQueuesEnabled = false;

        // NOTE vs. the docker-compose Trips2 sample: we do NOT set asb.ManagementConnectionString and do
        // NOT call .AutoProvision(). Under Aspire's `.RunAsEmulator()`, the AppHost pre-declares every queue
        // via AddServiceBusQueue (loaded into the emulator's config at container start), so the entities
        // already exist before this service connects — there is nothing for Wolverine to provision, and the
        // emulator exposes no separate management port to provision against. Treat the topology as
        // externally owned. (The standalone-emulator path likewise expects entities created out-of-band.)

        // #356 emulator constraint — EXPLICIT routing replaces UseConventionalRouting(). Conventional
        // routing mints one ASB queue per message type (~30), which would exceed the 50-entity cap. Instead
        // the service receives commands on its app inbox and cascades the two cross-service messages it
        // produces to the publisher / repair shop's app inboxes. The matching listeners are declared in
        // TripPublisher / RepairShop. Every queue named here has a 1:1 AddServiceBusQueue in the AppHost.
        opts.ListenToAzureServiceBusQueue("trip_service_app");
        opts.PublishMessage<ContinueTrip>().ToAzureServiceBusQueue("trip_publisher_app");
        opts.PublishMessage<RepairRequested>().ToAzureServiceBusQueue("repair_shop_app");

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

        // The service's own inbound control queue. AddCritterWatchMonitoring's second URI must point here
        // (asb://queue/trip_service) so CritterWatch can call operator commands back to this service.
        opts.ListenToAzureServiceBusQueue("trip_service");

        // ---- CritterWatch monitoring -------------------------------------------------------------
        // First URI  = the console's shared control/telemetry queue (asb://queue/critterwatch). The
        //              console listens there with .ListenOnlyAtLeader().UseCritterWatchSerializer().
        // Second URI = this service's own queue, so commands route back here.
        //
        // #345: must be `asb://queue/{name}` — Wolverine's AzureServiceBusTransport.findEndpointByUri
        // switches on uri.Host ("queue" or "topic"), so the bare `asb://{name}` form throws
        // ArgumentOutOfRangeException at host build.
        opts.AddCritterWatchMonitoring(
            "asb://queue/critterwatch".ToUri(),
            "asb://queue/trip_service".ToUri()).EnableEventStoreExplorer = true;

        // Build Marten schema on startup (dev convenience). The ASB entities are owned by the emulator
        // config (declared in the AppHost), so there is no transport topology to set up here.
        opts.Services.AddResourceSetupOnStartup();
    }
    // end-snippet
}
