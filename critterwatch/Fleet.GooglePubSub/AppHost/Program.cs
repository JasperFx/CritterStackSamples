// =============================================================================================
// Fleet.GooglePubSub AppHost — clone, open Fleet.GooglePubSub.sln, press F5.
//
// Aspire provisions the Google Cloud Pub/Sub EMULATOR (as a plain container — GCP Pub/Sub has no
// first-class Aspire resource) and a Postgres container, then launches the CritterWatch console and the
// monitored Trip trio. Every service reaches the console over the Pub/Sub control channel; the console's
// dashboard is the external HTTP endpoint.
//
// This is the RabbitMQ flagship with the transport swapped to Google Cloud Pub/Sub. The transport swap
// forces two AppHost-side specifics that Rabbit doesn't need:
//   1. No first-class Aspire resource → run the emulator with AddContainer + the google-cloud-cli image's
//      `gcloud beta emulators pubsub start`. Every project gets PUBSUB_EMULATOR_HOST pointed at it, which is
//      the single switch the Google client library reads to talk to the emulator instead of real Pub/Sub.
//   2. The emulator starts EMPTY (no topics/subscriptions). Each service calls Wolverine `.AutoProvision()`
//      so the topics + pull subscriptions are created at startup — there is no pre-declared topology like
//      the ASB emulator's AddServiceBusQueue.
// =============================================================================================

var builder = DistributedApplication.CreateBuilder(args);

// The GCP project id the whole fleet shares (emulator + console + services all address topics under it).
// Kept in sync with Fleet.Common.SampleConnections.ProjectId (the services' default), repeated here as a
// literal so the AppHost needs no reference to the shared library.
const string projectId = "critterwatch-sample";

// The emulator's container port. Bound to a FIXED host port so the project resources (which run on the host,
// not in the container network) can reach it at a stable localhost:<port> — that's what PUBSUB_EMULATOR_HOST
// must be. 8085 is the Pub/Sub emulator's conventional port and doesn't collide with the CritterWatch
// docker-compose dev stack (which runs no Pub/Sub emulator).
const int emulatorPort = 8085;

// ---- Infrastructure: Google Cloud Pub/Sub emulator --------------------------------------------
// The `emulators` tag of the official google-cloud-cli image bundles the Java-based Pub/Sub emulator. We
// override the entrypoint to start it bound to all interfaces on the container so the host can reach it.
var pubsub = builder.AddContainer("pubsub", "gcr.io/google.com/cloudsdktool/google-cloud-cli", "emulators")
    .WithEntrypoint("gcloud")
    .WithArgs("beta", "emulators", "pubsub", "start",
        $"--project={projectId}",
        $"--host-port=0.0.0.0:{emulatorPort}")
    // isProxied: false publishes the container port DIRECTLY to the host (docker -p 8085:8085) instead of
    // behind Aspire's reverse proxy. The Pub/Sub client talks gRPC (HTTP/2) to the emulator, which the proxy
    // doesn't pass through for a raw "tcp" endpoint — so the host-process services must reach the container
    // directly at localhost:8085 (== PUBSUB_EMULATOR_HOST below).
    .WithEndpoint(port: emulatorPort, targetPort: emulatorPort, scheme: "tcp", name: "emulator", isProxied: false);

// PUBSUB_EMULATOR_HOST (host:port the Google client connects to) + PUBSUB_PROJECT_ID, applied to every
// project resource below. Local helper so each service wires identically.
var emulatorHost = $"localhost:{emulatorPort}";

// ---- Infrastructure: Postgres -----------------------------------------------------------------
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
var db = postgres.AddDatabase("critterstore", "critterwatch");   // the CONSOLE's own store database.

// ---- The CritterWatch console ----------------------------------------------------------------
// Resource name MUST be "critterwatch": the shared test harness (CritterWatchAppHostFixture) waits on a
// resource by that exact name and builds its HttpClient against it. WithExternalHttpEndpoints surfaces the
// dashboard outside the Aspire proxy.
var console = builder.AddProject<Projects.CritterWatchConsole>("critterwatch")
    .WithEnvironment("PUBSUB_EMULATOR_HOST", emulatorHost)
    .WithEnvironment("PUBSUB_PROJECT_ID", projectId)
    .WithReference(db).WaitFor(db)
    .WaitFor(pubsub)
    .WithExternalHttpEndpoints();

// ---- Monitored fleet: Trip trio ---------------------------------------------------------------
// Each service reaches the Pub/Sub emulator (its control channel) and the console's database server (the
// monitored services keep their OWN event stores in their OWN schemas on the same Postgres container —
// see DatabaseSchemaName in each Program.cs). They wait for the console so the control topic exists first.
var tripService = builder.AddProject<Projects.TripService>("TripService")
    .WithEnvironment("PUBSUB_EMULATOR_HOST", emulatorHost)
    .WithEnvironment("PUBSUB_PROJECT_ID", projectId)
    .WithReference(db).WaitFor(db)
    .WaitFor(pubsub)
    .WaitFor(console);

builder.AddProject<Projects.RepairShop>("RepairShop")
    .WithEnvironment("PUBSUB_EMULATOR_HOST", emulatorHost)
    .WithEnvironment("PUBSUB_PROJECT_ID", projectId)
    .WithReference(db).WaitFor(db)
    .WaitFor(pubsub)
    .WaitFor(console);

// The publisher drives traffic into TripService — wait for it so the first burst isn't dropped.
builder.AddProject<Projects.TripPublisher>("TripPublisher")
    .WithEnvironment("PUBSUB_EMULATOR_HOST", emulatorHost)
    .WithEnvironment("PUBSUB_PROJECT_ID", projectId)
    .WaitFor(pubsub)
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
