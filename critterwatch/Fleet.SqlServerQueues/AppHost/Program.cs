// =============================================================================================
// Fleet.SqlServerQueues AppHost — clone, open Fleet.SqlServerQueues.sln, press F5.
//
// The simplest AppHost of the set: ONE container, NO broker. A single SQL Server 2025 is simultaneously
// the Polecat event store AND the Wolverine database-backed QUEUE transport that carries the CritterWatch
// control channel. Aspire provisions that one container, then launches the CritterWatch console (Polecat
// flavor) and the monitored fleet (Trip trio + Incidents group). Every service reaches the console over
// the SQL Server database-queue control channel; the console's dashboard is the external HTTP endpoint.
// =============================================================================================

var builder = DistributedApplication.CreateBuilder(args);

// ---- Infrastructure: a single SQL Server 2025 container ----------------------------------------
// Polecat stores event payloads in the native `json` column type, which only exists in SQL Server 2025
// (v17). Older engines (incl. Azure SQL Edge) fail Polecat schema creation with SqlException 2715
// "Cannot find data type json", so we PIN the 2025 image tag — the default Aspire SQL Server image is an
// older engine. AddSqlServer pulls the image and exposes a connection string to any resource that
// .WithReference()s the database below (injected as ConnectionStrings__<db-resource-name>).
var sql = builder.AddSqlServer("sqlserver")
    .WithImageTag("2025-latest");          // mcr.microsoft.com/mssql/server:2025-latest — Polecat requires 2025+.

// Resource name "critterstore" (NOT "critterwatch" — Aspire resource names are unique case-insensitive
// across types, and the console PROJECT below owns the name "critterwatch"). The actual database is named
// "critterwatch" so the non-Aspire localhost fallback string still matches.
var db = sql.AddDatabase("critterstore", "critterwatch");   // the CONSOLE's own store database.

// ---- The CritterWatch console ----------------------------------------------------------------
// Resource name MUST be "critterwatch": the shared test harness (CritterWatchAppHostFixture) waits on a
// resource by that exact name and builds its HttpClient against it. WithExternalHttpEndpoints surfaces the
// dashboard outside the Aspire proxy.
var console = builder.AddProject<Projects.CritterWatchConsole>("critterwatch")
    .WithReference(db).WaitFor(db)
    .WithExternalHttpEndpoints();

// ---- Monitored fleet: Trip trio ---------------------------------------------------------------
// Each service references the SAME SQL Server (its event store AND the shared DB-queue control channel
// live there). The monitored services keep their OWN Polecat event stores in their OWN schemas
// (trips / repair_shop / incidents) on the same container — only the Wolverine TRANSPORT schema is shared
// fleet-wide so the one "critterwatch" control queue is visible to the console. They wait for the console
// so the shared control queue exists first.
var tripService = builder.AddProject<Projects.TripService>("TripService")
    .WithReference(db).WaitFor(db)
    .WaitFor(console);

builder.AddProject<Projects.RepairShop>("RepairShop")
    .WithReference(db).WaitFor(db)
    .WaitFor(console);

// The publisher drives traffic into TripService — wait for it so the first burst isn't dropped.
builder.AddProject<Projects.TripPublisher>("TripPublisher")
    .WithReference(db).WaitFor(db)
    .WaitFor(tripService);

// ---- Monitored fleet: Incidents group ---------------------------------------------------------
var incidentService = builder.AddProject<Projects.Incidents_Service>("IncidentService")
    .WithReference(db).WaitFor(db)
    .WaitFor(console);

builder.AddProject<Projects.Incidents_Publisher>("IncidentPublisher")
    .WithReference(db).WaitFor(db)
    .WaitFor(incidentService);

// ---- License propagation ----------------------------------------------------------------------
// CritterWatch's license-gated operator handlers (PauseProjection / RebuildProjection / DLQ ops / …)
// execute ON the monitored services. The AppHost may have JASPERFX__LICENSEKEY in its environment, but
// Aspire child processes don't inherit it — push it onto every project resource so operator actions work.
// Absent a key (e.g. CI), the fleet still boots and registers; only paid operator actions are gated.
var licenseKey = builder.Configuration["JASPERFX__LICENSEKEY"];
if (!string.IsNullOrWhiteSpace(licenseKey))
{
    foreach (var project in builder.Resources.OfType<Aspire.Hosting.ApplicationModel.ProjectResource>().ToList())
    {
        builder.CreateResourceBuilder(project).WithEnvironment("JASPERFX__LICENSEKEY", licenseKey);
    }
}

builder.Build().Run();
