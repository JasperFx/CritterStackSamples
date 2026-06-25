using CritterWatch.Services.Hosting;
using Fleet.Common;
using Wolverine.CritterWatch;
using Wolverine.SqlServer;

// =============================================================================================
// CritterWatchConsole — the standalone monitoring dashboard (Polecat / SQL Server flavor).
//
// The minimal app an operator writes to run CritterWatch as its own dedicated backend on SQL Server:
// call AddCritterWatch from the CritterWatch.SqlServer package (consumes the packed NuGet) to register the
// console's Polecat ancillary store + Wolverine + SignalR + HTTP endpoints + SPA, configure the SQL Server
// DB-QUEUE control channel inside `configureWolverine`, then UseCritterWatch to map everything. The
// console's own storage is SQL Server (Polecat); monitored services reach it over the SAME SQL Server's
// database-queue control channel — there is NO broker in this fleet.
// =============================================================================================

var builder = WebApplication.CreateBuilder(args);

// The console's own SQL Server store. Under Aspire this is the `critterstore` database; standalone it
// falls back to the localhost docker-compose SQL Server (port 1443). NOTE: this is the *console's* store,
// entirely separate from each monitored service's Polecat event store (which live in their own schemas in
// the same SQL Server).
var consoleConnectionString = SampleConnections.SqlServer();

// begin-snippet: sqlserver-console-db-queue-control-channel
builder.AddCritterWatch(
    consoleConnectionString,
    configureWolverine: opts =>
    {
        // Stand up the Wolverine SQL Server database-backed queue transport — the SAME SQL Server that
        // backs the console's Polecat store. The queue TABLES (transport) live in the default "dbo" schema:
        // we deliberately pass NO schema/transportSchema so the "critterwatch" control queue lands in the
        // ONE shared transport schema every monitored service also uses. (Each service keeps its own
        // distinct Polecat event-store schema; ONLY the transport/queue schema must coincide — this is the
        // #1 DB-queue failure mode, see this solution's README + plan 04's CRITICAL note.)
        //
        // No role: argument here — the SQL Server transport store takes the Main role. CritterWatch's
        // AddCritterWatch already wired ResolveMainStoreOnConflict (#531): it keeps the transport store as
        // Main and demotes CritterWatch's own "critterwatch_wolverine" durability store to Ancillary, so
        // the two SQL-Server Main claims reconcile instead of throwing at startup.
        opts.UseSqlServerPersistenceAndTransport(consoleConnectionString)
            .AutoProvision();

        // THE control channel. Every monitored service points the FIRST URI of its
        // AddCritterWatchMonitoring(...) at "sqlserver://critterwatch" — i.e. this queue. The console
        // listens here for their telemetry + registration:
        //   - ListenOnlyAtLeader()       : in a multi-node console cluster, exactly one node owns this
        //                                  shared queue (no split-brain). On this single-node sample the
        //                                  one node elects itself leader and owns it automatically.
        //   - UseCritterWatchSerializer(): pins CritterWatch's wire-format so the encode/decode contract
        //                                  matches what AddCritterWatchMonitoring configures on the
        //                                  publisher side.
        // CritterWatch automatically pins this DB-queue listener to BufferedInMemory (a DB queue can't run
        // Inline, and Durable would couple it to the demoted ancillary store).
        // No serializer call needed: AddCritterWatch registers the CritterWatch wire-format serializer
        // globally (by a unique content-type), so the console decodes telemetry with zero per-endpoint config.
        opts.ListenToSqlServerQueue("critterwatch")
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
