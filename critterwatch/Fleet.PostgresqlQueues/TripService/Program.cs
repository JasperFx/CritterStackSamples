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
using Wolverine.Persistence.Durability;
using Wolverine.Postgresql;

// TripService is a headless Wolverine worker (RunJasperFxCommands gives it the JasperFx CLI:
// `dotnet run -- check-env`, `codegen`, etc.). Aspire launches it as a project resource; standalone,
// it falls back to the localhost docker-compose Postgres.
return await TripServiceProgram.CreateHostBuilder(args).RunJasperFxCommands(args);

public static class TripServiceProgram
{
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseWolverine(ConfigureWolverine);
    }

    // begin-snippet: trip-service-wolverine-postgres-queue
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

        var connectionString = SampleConnections.Postgres();

        // ---- Wolverine PostgreSQL DB-backed queue transport (the CritterWatch control channel) --------
        // There is NO broker. This call stands up Wolverine's database-backed queues IN THE SAME POSTGRES
        // that backs the event store. Two schema choices are load-bearing for the fleet to work:
        //
        //   * role: MessageStoreRole.Ancillary — a DB transport otherwise registers itself as a second
        //     "Main" message store, which collides with the app's own Marten/Wolverine durability store
        //     and throws at startup. Ancillary tells Wolverine "this is just my transport, not my node
        //     store" so the Marten IntegrateWithWolverine store stays Main.
        //
        //   * transportSchema: "critterwatch_wolverine" — the console's transport schema. Since CritterWatch
        //     1.0 (#1025, see CritterWatch#1126) AddCritterWatch pins the console's Wolverine transport AND durability tables to
        //     "{schema}_wolverine" (= critterwatch_wolverine for the default "critterwatch" schema), and it
        //     cannot be overridden from the console side. EVERY monitored service must therefore point its
        //     DB-queue transport at that same schema, so they ALL resolve "postgresql://critterwatch" to the
        //     SAME queue table the console drains. Leave this at Wolverine's default ("wolverine_queues") and
        //     this service writes to its own private "critterwatch" table the console never reads — the silent
        //     schema-isolation failure mode plan 04 warns about. (Each service still keeps a DISTINCT Marten
        //     event-store schema — see DatabaseSchemaName below — only the transport/queue schema must coincide.)
        opts.UsePostgresqlPersistenceAndTransport(connectionString, transportSchema: "critterwatch_wolverine", role: MessageStoreRole.Ancillary)
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
                m.Connection(connectionString);
                // This service's OWN event-store schema — distinct from every other service's. ONLY the
                // transport/queue schema is shared (critterwatch_wolverine); the event data is isolated.
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

        // ---- Cross-app message routing (explicit — the Postgres transport has NO conventional routing) --
        // RabbitMQ has UseConventionalRouting() (auto-route each message to a type-named queue); the
        // Wolverine PostgreSQL transport does NOT, so every cross-app route is wired explicitly here.
        //  * Business commands from TripPublisher (StartTrip, Depart, …) and replies from RepairShop
        //    (RepairsCompleted) land on this service's "trip_commands" queue.
        //  * A critical breakdown emits RepairRequested → RepairShop's "repair_commands" queue.
        //  * The projection's ContinueTrip side effect goes back to TripPublisher's "trip_callbacks" queue.
        opts.ListenToPostgresqlQueue("trip_commands");
        opts.PublishMessage<RepairRequested>().ToPostgresqlQueue("repair_commands");
        opts.PublishMessage<ContinueTrip>().ToPostgresqlQueue("trip_callbacks");

        // This service's own inbound CritterWatch control queue — another Wolverine PostgreSQL queue (same
        // shared transport schema). AddCritterWatchMonitoring's second URI points here so CritterWatch can
        // call operator commands back to this service. Kept distinct from the business "trip_commands"
        // queue so operator control traffic and domain traffic don't share a drain.
        opts.ListenToPostgresqlQueue("trip_service_control");

        // ---- CritterWatch monitoring -------------------------------------------------------------
        // First URI  = the console's shared control/telemetry queue (postgresql://critterwatch). The
        //              console listens there via ListenToPostgresqlQueue("critterwatch"). CritterWatch
        //              auto-pins DB-queue routes to BufferedInMemory (a DB queue can't run Inline).
        // Second URI = this service's own control queue, so operator commands route back here.
        opts.AddCritterWatchMonitoring(
            "postgresql://critterwatch".ToUri(),
            "postgresql://trip_service_control".ToUri()).EnableEventStoreExplorer = true;

        // Build Marten schema + Wolverine transport resources on startup (dev convenience).
        opts.Services.AddResourceSetupOnStartup();
    }
    // end-snippet
}
