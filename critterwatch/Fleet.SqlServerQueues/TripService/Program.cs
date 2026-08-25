using Fleet.Common;
using JasperFx;
using JasperFx.Core;
using JasperFx.Events.Projections;
using JasperFx.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polecat;
using TripMessages;
using TripService;
using Wolverine;
using Wolverine.CritterWatch;
using Wolverine.Persistence.Durability;
using Wolverine.Polecat;
using Wolverine.SqlServer;

// TripService is a headless Wolverine worker (RunJasperFxCommands gives it the JasperFx CLI:
// `dotnet run -- check-env`, `codegen`, etc.). Aspire launches it as a project resource; standalone,
// it falls back to the localhost docker-compose SQL Server.
return await TripServiceProgram.CreateHostBuilder(args).RunJasperFxCommands(args);

public static class TripServiceProgram
{
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseWolverine(ConfigureWolverine);
    }

    // begin-snippet: sqlserver-trip-service-wolverine
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

        var sqlServer = SampleConnections.SqlServer();

        // ---- SQL Server database-backed queue transport (the CritterWatch control channel) ------------
        // There is NO broker in this fleet. The transport is a set of QUEUE TABLES in the SAME SQL Server
        // that backs the event store. UseSqlServerPersistenceAndTransport stands up both the Wolverine
        // durability tables and the queue tables.
        //
        // SCHEMA COUPLING — the #1 failure mode (see plan 04's CRITICAL note): a DB-backed queue is a
        // table in a specific schema. AddCritterWatchMonitoring routes telemetry to sqlserver://critterwatch
        // — a queue named "critterwatch" resolved to a queue TABLE in THIS node's transport schema. For the
        // console to actually receive this service's telemetry, console + EVERY monitored service must use
        // the SAME Wolverine transport schema for that control queue. Since CritterWatch 1.0 (#1025, CritterWatch#1126) the
        // console's AddCritterWatch pins its transport schema to "critterwatch_wolverine" (not overridable
        // console-side), so every participant passes transportSchema: "critterwatch_wolverine" and the one
        // shared "critterwatch" queue table there is what the console drains. (Each service still keeps its
        // OWN distinct Polecat EVENT-STORE schema below — only the transport/queue schema must coincide.)
        //
        // role: Ancillary — the SQL Server transport otherwise registers a second Main store and Wolverine
        // refuses to start. The app's Polecat event store stays the Main durability store.
        opts.UseSqlServerPersistenceAndTransport(sqlServer, transportSchema: "critterwatch_wolverine", role: MessageStoreRole.Ancillary)
            .AutoProvision();

        // Durable inbox/outbox so in-flight messages survive a restart — the realistic production posture
        // CritterWatch is built to observe (it surfaces the inbox/outbox + dead-letter state per endpoint).
        opts.Policies.UseDurableInboxOnAllListeners();
        opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
        opts.Policies.UseDurableLocalQueues();
        opts.Policies.AutoApplyTransactions();

        // ContinueTrip is published BY TripService and handled by TripPublisher — declare it so Wolverine
        // knows the message type even though this service has no handler for it.
        opts.Discovery.IncludeType<ContinueTrip>();

        // ---- Polecat event store + projections (the standalone-Polecat storage showcase) -------------
        opts.Services.AddPolecat(m =>
            {
                m.ConnectionString = sqlServer;
                // This service's OWN event-store schema — distinct from every other service's. Only the
                // Wolverine TRANSPORT schema (above) is shared across the fleet for the control queue.
                m.DatabaseSchemaName = "trips";

                // Three async projections give CritterWatch's Projections / rebuild UI real targets:
                //  - TripProjection      : single-stream snapshot
                //  - DayProjection       : multi-stream rollup with FanOut
                //  - DistanceProjection  : event projection
                m.Projections.Add<TripProjection>(ProjectionLifecycle.Async);
                m.Projections.Add<DayProjection>(ProjectionLifecycle.Async);
                m.Projections.Add<DistanceProjection>(ProjectionLifecycle.Async);
            })
            .IntegrateWithWolverine(o =>
            {
                // Let Wolverine distribute the async projection agents across the cluster's nodes (here:
                // the single leader node), which is what CritterWatch's agent view reports on.
                o.UseWolverineManagedEventSubscriptionDistribution = true;
            })
            // JasperFx/polecat#187: Polecat doesn't yet register an IStatefulResource the way Marten does,
            // so AddResourceSetupOnStartup() never discovers the Polecat store and its event tables
            // (trips.pc_streams, pc_events, …) are never created — every StartTrip then dead-letters with
            // SqlException 208 "Invalid object name 'trips.pc_streams'". Polecat's own startup schema
            // applier provisions the schema; call it explicitly until the SystemPart lands upstream.
            .ApplyAllDatabaseChangesOnStartup();

        // ---- Domain message routing (no convention-based routing on the SQL Server transport) ---------
        // RabbitMQ's UseConventionalRouting() auto-wires queues by message type; the SQL Server transport
        // has no such convention, so route explicitly. TripService:
        //   - LISTENS on "trip_commands" for all the trip commands the publisher sends, AND for the
        //     RepairsCompleted reply RepairShop routes back here.
        //   - SENDS RepairRequested to RepairShop's work queue, and ContinueTrip back to the publisher.
        opts.ListenToSqlServerQueue("trip_commands");
        opts.PublishMessage<RepairRequested>().ToSqlServerQueue("repair_shop_work");
        opts.PublishMessage<ContinueTrip>().ToSqlServerQueue("trip_publisher");

        // The service's own inbound control queue (a SQL Server queue table). AddCritterWatchMonitoring's
        // second URI points here so CritterWatch can call operator commands back to this service.
        opts.ListenToSqlServerQueue("trip_service");

        // ---- CritterWatch monitoring -------------------------------------------------------------
        // First URI  = the console's shared control/telemetry queue (sqlserver://critterwatch). The console
        //              listens there with .ListenOnlyAtLeader().UseCritterWatchSerializer(). CritterWatch
        //              automatically pins these DB-queue routes to BufferedInMemory (a DB queue can't run
        //              Inline, and Durable would couple delivery to the demoted ancillary store).
        // Second URI = this service's own queue, so commands route back here.
        opts.AddCritterWatchMonitoring(
            "sqlserver://critterwatch".ToUri(),
            "sqlserver://trip_service".ToUri()).EnableEventStoreExplorer = true;

        // Build the Wolverine transport + durability resources on startup (dev convenience). The Polecat
        // event-store schema is provisioned by the explicit ApplyAllDatabaseChangesOnStartup() above.
        opts.Services.AddResourceSetupOnStartup();
    }
    // end-snippet
}
