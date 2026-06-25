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
using Wolverine.CritterWatch;
using Wolverine.Marten;
using Wolverine.RabbitMQ;

// TripService is a headless Wolverine worker (RunJasperFxCommands gives it the JasperFx CLI:
// `dotnet run -- check-env`, `codegen`, etc.). Aspire launches it as a project resource; standalone,
// it falls back to the localhost docker-compose ports.
return await TripServiceProgram.CreateHostBuilder(args).RunJasperFxCommands(args);

public static class TripServiceProgram
{
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseWolverine(ConfigureWolverine);
    }

    // begin-snippet: trip-service-wolverine
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

        // ---- RabbitMQ transport -------------------------------------------------------------------
        // Aspire injects ConnectionStrings__rabbitmq (a full amqp:// URI with credentials); standalone
        // we fall back to the docker-compose broker. AutoProvision declares the queues/exchanges on boot.
        opts.UseRabbitMq(SampleConnections.RabbitMq())
            .DisableDeadLetterQueueing()
            .UseConventionalRouting()
            .AutoProvision();

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
        // (rabbitmq://queue/trip_service) so CritterWatch can call operator commands back to this service.
        opts.ListenToRabbitQueue("trip_service");

        // ---- CritterWatch monitoring -------------------------------------------------------------
        // First URI  = the console's shared control/telemetry queue (rabbitmq://queue/critterwatch).
        //              The console listens there with .ListenOnlyAtLeader().UseCritterWatchSerializer().
        // Second URI = this service's own queue, so commands route back here.
        opts.AddCritterWatchMonitoring(
            "rabbitmq://queue/critterwatch".ToUri(),
            "rabbitmq://queue/trip_service".ToUri()).EnableEventStoreExplorer = true;

        // Build Marten schema + Wolverine transport resources on startup (dev convenience).
        opts.Services.AddResourceSetupOnStartup();
    }
    // end-snippet
}
