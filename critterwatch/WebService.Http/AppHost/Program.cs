// =============================================================================================
// WebService.Http AppHost — clone, open WebService.Http.sln, press F5.
//
// Aspire provisions a single Postgres container, then launches the CritterWatch console and the monitored
// OrderService. There is NO broker: the OrderService reaches the console over Wolverine's HTTP transport,
// so the only infrastructure dependency is Postgres. The console's dashboard is the external HTTP endpoint.
// =============================================================================================

var builder = DistributedApplication.CreateBuilder(args);

// ---- Infrastructure: Postgres only ------------------------------------------------------------
// The postgres image defaults to max_connections=100. Every monitored service here runs Marten + a
// Wolverine durability store (and, for the DB-queue flavors, the transport store too), each with its own
// Npgsql pool — a whole fleet plus the console holds ~100 pooled connections at idle, which exhausts the
// default and silently starves the CritterWatch telemetry writes ("sorry, too many clients already").
// Raise the ceiling for the sample fleet.
var postgres = builder.AddPostgres("postgres")
    .WithArgs("-c", "max_connections=300");
// Resource name "critterstore" (NOT "critterwatch" — Aspire resource names are unique case-insensitive
// across types, and the console PROJECT below owns the name "critterwatch"). The 2nd arg keeps the actual
// Postgres database named "critterwatch" so the non-Aspire localhost fallback string still matches.
var db = postgres.AddDatabase("critterstore", "critterwatch");

// ---- The CritterWatch console -----------------------------------------------------------------
// Resource name MUST be "critterwatch": the shared test harness (CritterWatchAppHostFixture) waits on a
// resource by that exact name and builds its HttpClient against it. WithExternalHttpEndpoints surfaces the
// dashboard — AND the HTTP-transport receive routes — outside the Aspire proxy.
var console = builder.AddProject<Projects.CritterWatchConsole>("critterwatch")
    .WithReference(db).WaitFor(db)
    .WithExternalHttpEndpoints();

// ---- The monitored OrderService ----------------------------------------------------------------
// .WithReference(console) injects Aspire service discovery for the console's http endpoint
// (services__critterwatch__http__0) — that's the base URL the OrderService POSTs its telemetry to over the
// HTTP transport. .WaitFor(console) so the console's receive routes exist before the service registers.
builder.AddProject<Projects.OrderService>("OrderService")
    .WithReference(db).WaitFor(db)
    .WithReference(console).WaitFor(console)
    .WithExternalHttpEndpoints();   // expose the Orders API so the Tests battery can hit it directly.

// ---- License propagation ----------------------------------------------------------------------
// CritterWatch's license-gated operator handlers execute ON the monitored service. Aspire child processes
// don't inherit the AppHost's env, so push JASPERFX__LICENSEKEY onto every project resource. Absent a key
// (e.g. CI) the fleet still boots and registers; only paid operator actions are gated.
var licenseKey = builder.Configuration["JASPERFX__LICENSEKEY"];
if (!string.IsNullOrWhiteSpace(licenseKey))
{
    foreach (var project in builder.Resources.OfType<Aspire.Hosting.ApplicationModel.ProjectResource>().ToList())
    {
        builder.CreateResourceBuilder(project).WithEnvironment("JASPERFX__LICENSEKEY", licenseKey);
    }
}

builder.Build().Run();
