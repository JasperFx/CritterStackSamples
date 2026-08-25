using CritterWatch.Services.Hosting;
using Fleet.Common;
using JasperFx.Resources;
using Wolverine.CritterWatch;
using Wolverine.Persistence.Durability;
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
        // backs the console's Polecat store. The queue TABLES (transport) land in "critterwatch_wolverine":
        // CritterWatch 1.0 (#1025) pins the console's Wolverine transport schema there (its IntegrateWithWolverine
        // sets TransportSchemaName explicitly, which overrides this call's default at host build), and that is
        // the ONE shared transport schema every monitored service also passes as transportSchema.
        //
        // role: Ancillary — deliberately, and this differs from CritterWatch's own docs/tests. Wolverine.Polecat's
        // IntegrateWithWolverine (>= 6.30) also stamps the transport's MessageStorageSchemaName with the console's
        // "critterwatch_wolverine", so with the default Main role there are TWO Main stores in that one schema and
        // AddCritterWatch's ResolveMainStoreOnConflict (which picks "the Main store NOT in critterwatch_wolverine",
        // #531) resolves to nothing — every request then dies with "Wolverine.Polecat requires a SQL Server-backed
        // message store. The configured store was NullMessageStore" (CritterWatch#1130). Ancillary sidesteps the
        // conflict: the console's Polecat store stays Main and the transport is just a transport — the same posture
        // every monitored service in this fleet takes.
        opts.UseSqlServerPersistenceAndTransport(consoleConnectionString, role: MessageStoreRole.Ancillary)
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

        // Provision EVERY registered IStatefulResource at startup — here that's the console's two Polecat
        // stores (the primary "critterwatch_wolverine" durability/machinery store AND the ancillary
        // ICritterWatchStore in the "critterwatch" schema, where CritterWatch's own events + ServiceSummary
        // projection live) plus the SQL Server queue transport tables. Unlike Marten, Polecat does NOT yet
        // create event-store schema lazily on first use, so without this the ancillary store's pc_events /
        // pc_streams tables never exist and the FIRST telemetry the console receives dead-letters with
        // SqlException 208 "Invalid object name 'critterwatch.pc_events'" — so no service ever registers.
        // Polecat now contributes its stores to JasperFx's resource model (polecat#187), so the idiomatic
        // AddResourceSetupOnStartup() discovers and builds them; the monitored services already do the same.
        opts.Services.AddResourceSetupOnStartup();
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
