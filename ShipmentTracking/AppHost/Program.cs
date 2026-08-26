using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Infrastructure. Aspire pulls the containers, generates the credentials and
// injects the connection strings — docker-compose.yml is now the fallback for
// people who would rather not run Aspire, not the primary path.
// ---------------------------------------------------------------------------

// SQL Server 2025 explicitly. Aspire's default image tag is NOT 2025, and Polecat
// requires v17+ for the native `json` column type — on an older image it fails at
// schema creation with an error that does not mention the version.
var sql = builder.AddSqlServer("sql")
    .WithImageTag("2025-latest")
    .WithLifetime(ContainerLifetime.Persistent);

// Two databases on one server, deliberately.
//
// The console must not share a database with the thing it monitors: a monitoring
// console that dies alongside its subject is not a monitoring console, and
// CritterWatch's metrics table is by far the largest table in either system.
var shipmentsDb = sql.AddDatabase("shipments", "ShipmentTracking");

// The DB resource is "critterstore", NOT "critterwatch" — the console PROJECT below
// owns that name, and Aspire resource names are unique case-insensitively ACROSS
// types. The second argument keeps the real database named "CritterWatch" so the
// non-Aspire fallback string in CritterWatchHost still points somewhere sensible.
var critterStore = sql.AddDatabase("critterstore", "CritterWatch");

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

// ---------------------------------------------------------------------------
// The console, first. Monitored services WaitFor it so the shared `critterwatch`
// queue exists before they publish their first registration message.
// ---------------------------------------------------------------------------
// WithHttpEndpoint is not optional here: neither project ships a launchSettings.json,
// and without a launch profile Aspire has no endpoint to assign, bind or health-check.
// WithExternalHttpEndpoints() then surfaces the dashboard outside the Aspire proxy.
var critterwatch = builder.AddProject<Projects.CritterWatchHost>("critterwatch")
    .WithHttpEndpoint(env: "ASPNETCORE_HTTP_PORTS")
    .WithReference(critterStore).WaitFor(critterStore)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WithExternalHttpEndpoints();

// ---------------------------------------------------------------------------
// The monitored service.
//
// WaitFor on both resources is what makes AutoProvision() reliable: Wolverine
// declares its exchanges and queues, and Polecat creates its tables, the moment the
// host boots. Without WaitFor the service can start before the container accepts
// connections and provisioning fails intermittently.
// ---------------------------------------------------------------------------
builder.AddProject<Projects.ShipmentTracking>("shipment-tracking")
    .WithHttpEndpoint(env: "ASPNETCORE_HTTP_PORTS")
    .WithReference(shipmentsDb).WaitFor(shipmentsDb)
    .WithReference(rabbitmq).WaitFor(rabbitmq)
    .WaitFor(critterwatch)
    // CritterWatch monitoring is off by default in appsettings.json so a plain
    // `dotnet run` does not publish telemetry nobody is listening to. Aspire is the
    // environment where a console IS listening, so turn it on here.
    .WithEnvironment("CritterWatch__Enabled", "true");

// ---------------------------------------------------------------------------
// License propagation.
//
// Aspire child processes do NOT inherit JASPERFX__LICENSEKEY from the AppHost's
// environment, and CritterWatch's license-gated operator handlers (PauseProjection,
// RebuildProjection, the DLQ actions) execute ON the monitored services. Without
// this those actions silently no-op; the fleet still boots and registers.
// ---------------------------------------------------------------------------
// NOT Configuration["JASPERFX__LICENSEKEY"] — that is ALWAYS null. .NET's
// environment-variable configuration provider translates "__" into ":", so the
// variable JASPERFX__LICENSEKEY arrives as the configuration key JasperFx:LicenseKey.
// Reading it back with the double underscore silently finds nothing, and the
// propagation below never runs — the exact failure this block exists to prevent.
// CritterWatch itself reads config["JasperFx:LicenseKey"].
var licenseKey = builder.Configuration["JasperFx:LicenseKey"];
if (!string.IsNullOrWhiteSpace(licenseKey))
{
    foreach (var project in builder.Resources.OfType<ProjectResource>().ToList())
    {
        builder.CreateResourceBuilder(project)
            .WithEnvironment("JASPERFX__LICENSEKEY", licenseKey);
    }
}

builder.Build().Run();
