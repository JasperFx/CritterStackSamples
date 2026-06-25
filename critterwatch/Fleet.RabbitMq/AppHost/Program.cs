// =============================================================================================
// Fleet.RabbitMq AppHost — clone, open Fleet.RabbitMq.sln, press F5.
//
// Aspire provisions the RabbitMQ + Postgres containers, then launches the CritterWatch console and the
// monitored fleet (Trip trio + Incidents group). Every service reaches the console over the RabbitMQ
// control channel; the console's dashboard is the external HTTP endpoint.
// =============================================================================================

var builder = DistributedApplication.CreateBuilder(args);

// ---- Infrastructure containers ----------------------------------------------------------------
// AddRabbitMQ / AddPostgres pull official container images and expose a connection string to any
// resource that .WithReference()s them (injected as ConnectionStrings__<name>).
var rabbit = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();          // RabbitMQ management UI, handy for inspecting the control queue.

var postgres = builder.AddPostgres("postgres");
// Resource name "critterstore" (NOT "critterwatch" — Aspire resource names are unique case-insensitive
// across types, and the console PROJECT below owns the name "critterwatch"). The 2nd arg keeps the actual
// Postgres database named "critterwatch" so the non-Aspire localhost fallback string still matches.
var db = postgres.AddDatabase("critterstore", "critterwatch");   // the CONSOLE's own store database.

// ---- The CritterWatch console ----------------------------------------------------------------
// Resource name MUST be "critterwatch": the shared test harness (CritterWatchAppHostFixture) waits on a
// resource by that exact name and builds its HttpClient against it. WithExternalHttpEndpoints surfaces
// the dashboard outside the Aspire proxy.
var console = builder.AddProject<Projects.CritterWatchConsole>("critterwatch")
    .WithReference(rabbit).WaitFor(rabbit)
    .WithReference(db).WaitFor(db)
    .WithExternalHttpEndpoints();

// ---- Monitored fleet: Trip trio ---------------------------------------------------------------
// Each service references the broker (for its control channel) and the console's database server (the
// monitored services keep their OWN event stores in their OWN schemas on the same Postgres container —
// see DatabaseSchemaName in each Program.cs). They wait for the console so the control queue exists first.
var tripService = builder.AddProject<Projects.TripService>("TripService")
    .WithReference(rabbit).WaitFor(rabbit)
    .WithReference(db).WaitFor(db)
    .WaitFor(console);

builder.AddProject<Projects.RepairShop>("RepairShop")
    .WithReference(rabbit).WaitFor(rabbit)
    .WithReference(db).WaitFor(db)
    .WaitFor(console);

// The publisher drives traffic into TripService — wait for it so the first burst isn't dropped.
builder.AddProject<Projects.TripPublisher>("TripPublisher")
    .WithReference(rabbit).WaitFor(rabbit)
    .WaitFor(tripService);

// ---- Monitored fleet: Incidents group ---------------------------------------------------------
var incidentService = builder.AddProject<Projects.Incidents_Service>("IncidentService")
    .WithReference(rabbit).WaitFor(rabbit)
    .WithReference(db).WaitFor(db)
    .WaitFor(console);

builder.AddProject<Projects.Incidents_Publisher>("IncidentPublisher")
    .WithReference(rabbit).WaitFor(rabbit)
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
