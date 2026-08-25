// =============================================================================================
// Fleet.PostgresqlQueues AppHost — clone, open Fleet.PostgresqlQueues.sln, press F5.
//
// The "one container, no broker" fleet. The transport IS the database: Wolverine's PostgreSQL
// DB-backed queues live as tables in the same Postgres that backs every event store. So Aspire
// provisions exactly ONE Postgres container, then launches the CritterWatch console and the
// monitored fleet (Trip trio + Incidents group). Every service reaches the console over a Wolverine
// PostgreSQL queue named "critterwatch" — no RabbitMQ/SQS/ASB anywhere.
//
// SCHEMA COORDINATION (the load-bearing detail): a DB-backed queue is a TABLE in a specific schema,
// not a broker destination. For the whole multi-host fleet to share the one "critterwatch" control
// queue, the console AND every monitored service must resolve that queue to the SAME table — i.e.
// they must all use the SAME Wolverine transport schema. CritterWatch 1.0 pins the console's to
// "critterwatch_wolverine", so every monitored service passes transportSchema: "critterwatch_wolverine". Each service still keeps its OWN distinct Marten event-store schema (trips,
// repair_shop, incidents, …) — only the queue/transport schema must coincide. See each Program.cs.
// =============================================================================================

var builder = DistributedApplication.CreateBuilder(args);

// ---- Infrastructure: ONE Postgres container, no broker ---------------------------------------
// AddPostgres pulls the official Postgres image and exposes a connection string to any resource that
// .WithReference()s it (injected as ConnectionStrings__<name>).
// The postgres image defaults to max_connections=100. Every monitored service here runs Marten + a
// Wolverine durability store (and, for the DB-queue flavors, the transport store too), each with its own
// Npgsql pool, and Wolverine's durable inbox/outbox work fans out across that pool: under this sample's
// steady trip-simulation traffic a DB-queue host settles at ~20 pooled connections and the whole fleet at
// ~70-90 (measured on both the July 2026 and August 2026 stacks — it is not a regression). That leaves no
// headroom: anything that makes a host retry (e.g. an undrained telemetry queue) tips it past 100, and
// the only symptom is "FATAL: sorry, too many clients already" in the Postgres log while telemetry
// silently stops. Raise the ceiling for the sample fleet.
var postgres = builder.AddPostgres("postgres")
    .WithArgs("-c", "max_connections=300");

// Resource name "critterstore" (NOT "critterwatch" — Aspire resource names are unique case-insensitive
// across types, and the console PROJECT below owns the name "critterwatch"). The 2nd arg keeps the actual
// Postgres database named "critterwatch" so the non-Aspire localhost fallback string still matches. This
// single database is BOTH the console's event store AND the shared Wolverine queue host for the fleet.
var db = postgres.AddDatabase("critterstore", "critterwatch");

// ---- The CritterWatch console ----------------------------------------------------------------
// Resource name MUST be "critterwatch": the shared test harness (CritterWatchAppHostFixture) waits on a
// resource by that exact name and builds its HttpClient against it. WithExternalHttpEndpoints surfaces
// the dashboard outside the Aspire proxy. The console listens on the Postgres queue "critterwatch".
var console = builder.AddProject<Projects.CritterWatchConsole>("critterwatch")
    .WithReference(db).WaitFor(db)
    .WithExternalHttpEndpoints();

// ---- Monitored fleet: Trip trio ---------------------------------------------------------------
// Each service references the SAME database (its control queue table AND its own event store live there;
// the event store stays in its own schema). They wait for the console so the control queue exists first.
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
