using Fleet.Common;
using JasperFx.Events.Projections;
using JasperFx.Resources;
using Marten;
using OrderService;
using Wolverine;
using Wolverine.CritterWatch;
using Wolverine.CritterWatch.Http;
using Wolverine.Http;
using Wolverine.Http.Transport;
using Wolverine.Marten;

// =============================================================================================
// OrderService — a Wolverine HTTP web service (an event-sourced Orders API) monitored by CritterWatch,
// where the MONITORING LINK ITSELF RIDES WOLVERINE'S HTTP TRANSPORT (no broker).
//
// PUSH model. This service is the HTTP *client* of the monitoring link: AddCritterWatchMonitoring routes
// registration / heartbeat / telemetry envelopes to the console's HTTP-transport receive endpoint, and the
// Wolverine HTTP transport POSTs them there (InlineHttpSender → POST {console}/_wolverine/invoke). The
// console executes them inline against its CritterWatch handlers. Operator commands flow back the other way
// (console → this service's own /_wolverine/invoke), which this service receives by ALSO mapping the
// transport endpoints.
// =============================================================================================

var builder = WebApplication.CreateBuilder(args);

// The console's base URL (Aspire service discovery → services__critterwatch__http__0, else localhost). The
// monitoring telemetry is POSTed to this host's Wolverine HTTP-transport invoke route.
var consoleBaseUrl = SampleConnections.ConsoleBaseUrl();

// Wolverine's HTTP transport is registered under the "https" SCHEME (HttpTransport.Protocol == "https"; the
// "http" string the transport also carries is a descriptive TAG, not an alias scheme). Wolverine resolves a
// URI to a transport by scheme, so these monitoring URIs MUST be https:// — with http:// the monitoring call
// throws "Unknown Transport scheme 'http'" and the whole service fails to boot. The scheme is used ONLY for
// (a) transport resolution and (b) as the named-HttpClient KEY; the ACTUAL network address is that named
// client's BaseAddress, which we point at the console's real (plain-http, in local Aspire) invoke route
// below. So https here + an http BaseAddress is correct, not a mismatch.
//   telemetryUri  — where THIS service sends telemetry  → the console's BATCH route (same authority as the
//                   discovered console URL, but https scheme so the transport resolves). It has to be the
//                   batch route: CritterWatch 1.0 (#958; tracked as CritterWatch#1129) pins every telemetry route to BufferedInMemory, and
//                   for Wolverine's HTTP transport that means a BatchedSender POSTing "binary/wolverine-envelopes"
//                   — which /_wolverine/invoke (single envelope only) rejects with 415. The batch route hands
//                   the envelopes to the console's local queue named in the path, where CritterWatch's handlers
//                   run. (On rc.1 the http route still sent inline, so /_wolverine/invoke worked.)
//   controlUri    — where THIS service RECEIVES operator commands → its own inline-invoke route. The host is
//                   "localhost" because AddCritterWatchMonitoring only needs a stable local listener URI; the
//                   actual receive happens via MapWolverineHttpTransportEndpoints below. (The console learns
//                   the externally-reachable address from Aspire service discovery when it sends back.)
var telemetryUri = new Uri($"https://{new Uri(consoleBaseUrl).Authority}/_wolverine/batch/critterwatch");
var controlUri = new Uri("https://localhost/_wolverine/invoke");

builder.Host.UseWolverine(opts =>
{
    // The identity CritterWatch registers this service under — the value the Tests battery asserts shows up
    // in GET /api/critterwatch/services.
    opts.ServiceName = "OrderService";
    opts.ApplicationAssembly = typeof(OrderEndpoints).Assembly;

    // CritterWatch requires Balanced durability even on a single node (projection pause/restart are
    // leader-owned agent-assignment changes; Solo has no leader so those commands silently no-op). A single
    // Balanced node elects itself leader and runs every projection agent locally.
    opts.Durability.Mode = DurabilityMode.Balanced;
    opts.EnableAutomaticFailureAcks = false;

    // ---- Marten event store + an async projection -------------------------------------------------
    opts.Services.AddMarten(m =>
        {
            m.Connection(SampleConnections.Postgres());
            m.DatabaseSchemaName = "orders";          // the service's OWN schema, distinct from the console's.
            m.DisableNpgsqlLogging = true;

            // Self-aggregate the Order from its event stream, and run a customer rollup async so CritterWatch
            // has a projection-agent + rebuild target to show off.
            m.Projections.Snapshot<Order>(SnapshotLifecycle.Inline);
            m.Projections.Add<OrdersByCustomerProjection>(ProjectionLifecycle.Async);
        })
        .IntegrateWithWolverine(o =>
        {
            // Let Wolverine distribute the async projection agent across the cluster (here: the single leader
            // node) — that's what CritterWatch's agent view reports on.
            o.UseWolverineManagedEventSubscriptionDistribution = true;
        });

    opts.Policies.AutoApplyTransactions();

    // Register Wolverine's HTTP transport in the options BEFORE AddCritterWatchMonitoring. The monitoring
    // call eagerly does ListenForMessagesFrom(controlUri) for the http:// control URI, and Wolverine resolves
    // a URI to a transport by SCHEME — with no http/https transport registered yet it throws
    // "Unknown Transport scheme 'http'" and the whole service fails to boot. Unlike a broker (where
    // UseRabbitMq()/UseAmazonSqs() registers the transport first), the HTTP transport otherwise only
    // materializes lazily via ToHttpEndpoint()/the receive routes — too late for the monitoring listener — so
    // add it explicitly here. (This is the documented opts.Transports.Add(new HttpTransport()) pattern from
    // Wolverine's own external-HTTP-server sample; GetOrCreate<HttpTransport> later returns this same instance.)
    opts.Transports.Add(new HttpTransport());

    // begin-snippet: orderservice-http-transport-monitoring
    // ---- CritterWatch monitoring over the HTTP transport -----------------------------------------
    // First URI  = the console's HTTP-transport invoke route — where telemetry/heartbeats are POSTed.
    // Second URI = this service's own control route, where operator commands are received.
    // The http(s):// scheme makes AddCritterWatchMonitoring route over Wolverine's HTTP transport with
    // SendInline() — each envelope is a POST to the target's /_wolverine/invoke. No broker involved.
    opts.AddCritterWatchMonitoring(telemetryUri, controlUri).EnableEventStoreExplorer = true;
    // end-snippet

    // Build Marten schema + Wolverine transport resources on startup (dev convenience).
    opts.Services.AddResourceSetupOnStartup();
});

// ---- WolverineFx.Http: the Orders API + the HTTP transport ------------------------------------
builder.Services.AddWolverineHttp();

// #538 — discover the non-Wolverine ASP.NET endpoints (the bare app.MapGet below) so they show up on
// CritterWatch's HTTP tab alongside the Wolverine HTTP endpoints. AddCritterWatchHttp() opts this host in.
builder.Services.AddCritterWatchHttp();

// begin-snippet: orderservice-http-transport-client
// The HTTP transport's SENDER needs IWolverineHttpTransportClient plus a named HttpClient. The client name
// MUST equal the telemetry endpoint's outbound URI string (the transport does
// IHttpClientFactory.CreateClient(outboundUri) == telemetryUri.ToString()), and that client's BaseAddress is
// where it actually POSTs. BaseAddress is the console's REAL batch route (plain http in local Aspire) — NOT
// the https telemetryUri, whose https scheme exists only so Wolverine resolves the HTTP transport (see the
// telemetryUri comment above). So the client is keyed by the https URI but posts to the http console.
builder.Services.AddScoped<IWolverineHttpTransportClient, WolverineHttpTransportClient>();
builder.Services.AddHttpClient(
    telemetryUri.ToString(),
    c => c.BaseAddress = new Uri($"{consoleBaseUrl}/_wolverine/batch/critterwatch"));
// end-snippet

var app = builder.Build();

// A couple of bare ASP.NET endpoints — these are NON-Wolverine, so they only appear on CritterWatch's HTTP
// tab because of AddCritterWatchHttp() above.
app.MapGet("/", () => Results.Ok(new { service = "OrderService", monitoredOver = "wolverine-http-transport" }));
app.MapGet("/health", () => Results.Ok("healthy"));

// The Orders API ([WolverinePost]/[WolverineGet] in OrderEndpoints.cs).
app.MapWolverineEndpoints();

// THE receive side of the monitoring link on this service: lets the console POST operator commands back to
// this service over the HTTP transport (POST /_wolverine/invoke + /_wolverine/batch/{queue}). Coexists with
// MapWolverineEndpoints above — they register separate route data sources.
app.MapWolverineHttpTransportEndpoints();

app.Run();
