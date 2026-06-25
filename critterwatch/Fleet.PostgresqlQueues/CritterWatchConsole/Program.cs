using CritterWatch.Services.Hosting;
using Fleet.Common;
using Wolverine.Postgresql;

// =============================================================================================
// CritterWatchConsole — the standalone monitoring dashboard (Postgres DB-queue flavor).
//
// This is the minimal app an operator writes to run CritterWatch as its own dedicated backend over a
// BROKER-FREE Wolverine PostgreSQL transport: call AddCritterWatch (consumes the packed NuGet) to
// register the console's store + Wolverine + SignalR + HTTP endpoints + SPA, configure the DB-backed
// control queue inside `configureWolverine`, then UseCritterWatch to map everything. The console's own
// storage is Postgres; monitored services reach it over a Wolverine PostgreSQL queue in that same DB.
//
// This mirrors CritterWatch's own src/Smoke boot host almost verbatim — the smoke host is the canonical
// "broker-free Postgres transport" console, and a fleet console is just that plus the AppHost provisioning
// the monitored services around it.
// =============================================================================================

var builder = WebApplication.CreateBuilder(args);

// The console's own Postgres store — and the host of the Wolverine DB-backed queue tables. Under Aspire
// this is the `critterwatch` database; standalone it falls back to the localhost docker-compose Postgres.
// NOTE: this is the *console's* event store, kept in its own Marten schema, separate from each monitored
// service's event store schema. The Wolverine queue/transport tables live in the DEFAULT Wolverine
// transport schema — the one every monitored service ALSO uses (none of them passes a per-service
// transportSchema), which is exactly why they all resolve "postgresql://critterwatch" to the SAME queue
// table and the console actually sees their telemetry. See plan 04's schema-coupling note.
var consoleConnectionString = SampleConnections.Postgres();

// begin-snippet: console-postgres-db-queue-control-channel
builder.AddCritterWatch(
    consoleConnectionString,
    configureWolverine: opts =>
    {
        // THE control channel — a broker-free inbound listener on the Wolverine PostgreSQL transport,
        // backed by the same Postgres the console's Marten store uses. The queue name is the URI host:
        // every monitored service points the FIRST URI of its AddCritterWatchMonitoring(...) at
        // "postgresql://critterwatch" — i.e. THIS queue table — and the console drains it here.
        //
        // CritterWatch's AddCritterWatch reconciles the DB-transport's "Main store" requirement against
        // its own durability store automatically (it keeps the console's store as Main and demotes the
        // transport store to Ancillary — the #531 / wolverine#3248 reconciliation), and it pins the
        // DB-queue route to BufferedInMemory (a DB queue can't run Inline). Nothing to wire here beyond
        // naming the queue.
        //
        // Single-node sample → no .ListenOnlyAtLeader() incantation is needed (the one node elects itself
        // leader and owns the queue automatically); CritterWatch applies its serializer to the
        // critterwatch-named queue via its serializer policy.
        opts.ListenToPostgresqlQueue("critterwatch");
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
