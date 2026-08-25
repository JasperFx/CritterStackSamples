// =============================================================================================
// Fleet.AzureServiceBus AppHost — clone, open Fleet.AzureServiceBus.sln, press F5.
//
// Aspire provisions the Azure Service Bus EMULATOR (plus the backing SQL container Aspire wires for it)
// and a Postgres container, then launches the CritterWatch console and the monitored Trip trio. Every
// service reaches the console over the ASB control channel; the console's dashboard is the external HTTP
// endpoint.
//
// This is the RabbitMQ flagship with the transport swapped to Azure Service Bus. The transport swap forces
// two AppHost-side specifics that Rabbit doesn't need:
//   1. The ASB emulator (mcr.microsoft.com/azure-messaging/servicebus-emulator) requires its entities to
//      be declared UP FRONT — it loads a config of queues/topics at container start and does NOT support
//      runtime AutoProvision via the management API. So every queue the fleet uses is declared here via
//      AddServiceBusQueue (1:1 with the ListenTo.../PublishMessage(...).To... calls in each service).
//   2. The emulator caps a namespace at 50 entities; combined with SystemQueuesEnabled=false + explicit
//      routing on the Wolverine side, the fleet's footprint is the handful of queues below — well under it.
// =============================================================================================

var builder = DistributedApplication.CreateBuilder(args);

// ---- Infrastructure: Azure Service Bus emulator -----------------------------------------------
// AddAzureServiceBus declares the namespace; RunAsEmulator() runs it as the local emulator container and
// auto-provisions the SQL Server/SQL Edge container the emulator depends on. .WithReference(serviceBus) on
// a project injects ConnectionStrings__messaging (Endpoint=sb://...;UseDevelopmentEmulator=true;...).
var serviceBus = builder.AddAzureServiceBus("messaging")
    .RunAsEmulator();

// Pre-declare every queue the fleet uses (the emulator needs them declared before any service connects):
//   critterwatch     — the console's shared telemetry/control queue (asb://queue/critterwatch)
//   {service}        — each service's inbound CritterWatch control queue (operator commands route here)
//   {service}_app    — each service's application inbox (explicit cross-service routing replaces
//                      conventional routing to stay under the 50-entity cap)
// Aspire RESOURCE names must be DNS-style (letters/digits/hyphens — NO underscores) and unique
// case-insensitive across ALL resource types (so the control queue's resource name must NOT be
// "critterwatch", which the console PROJECT owns). The actual ASB QUEUE name (2nd arg, what Wolverine
// addresses as asb://queue/<name>) DOES allow underscores. So: hyphenated resource name, real queue name.
foreach (var (resourceName, queueName) in new[]
         {
             ("cw-control",        "critterwatch"),
             ("trip-service",      "trip_service"),   ("trip-service-app",   "trip_service_app"),
             ("trip-publisher",    "trip_publisher"), ("trip-publisher-app", "trip_publisher_app"),
             ("repair-shop",       "repair_shop"),    ("repair-shop-app",    "repair_shop_app")
         })
{
    serviceBus.AddServiceBusQueue(resourceName, queueName);
}

// ---- Infrastructure: Postgres -----------------------------------------------------------------
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
// Postgres database named "critterwatch" so the non-Aspire localhost fallback string still matches.
var db = postgres.AddDatabase("critterstore", "critterwatch");   // the CONSOLE's own store database.

// ---- The CritterWatch console ----------------------------------------------------------------
// Resource name MUST be "critterwatch": the shared test harness (CritterWatchAppHostFixture) waits on a
// resource by that exact name and builds its HttpClient against it. WithExternalHttpEndpoints surfaces
// the dashboard outside the Aspire proxy.
var console = builder.AddProject<Projects.CritterWatchConsole>("critterwatch")
    .WithReference(serviceBus).WaitFor(serviceBus)
    .WithReference(db).WaitFor(db)
    .WithExternalHttpEndpoints();

// ---- Monitored fleet: Trip trio ---------------------------------------------------------------
// Each service references the ASB namespace (its control channel) and the console's database server (the
// monitored services keep their OWN event stores in their OWN schemas on the same Postgres container —
// see DatabaseSchemaName in each Program.cs). They wait for the console so the control queue exists first.
var tripService = builder.AddProject<Projects.TripService>("TripService")
    .WithReference(serviceBus).WaitFor(serviceBus)
    .WithReference(db).WaitFor(db)
    .WaitFor(console);

builder.AddProject<Projects.RepairShop>("RepairShop")
    .WithReference(serviceBus).WaitFor(serviceBus)
    .WithReference(db).WaitFor(db)
    .WaitFor(console);

// The publisher drives traffic into TripService — wait for it so the first burst isn't dropped.
builder.AddProject<Projects.TripPublisher>("TripPublisher")
    .WithReference(serviceBus).WaitFor(serviceBus)
    .WaitFor(tripService);

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
