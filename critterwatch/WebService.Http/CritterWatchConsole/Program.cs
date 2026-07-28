using CritterWatch.Services.Hosting;
using Fleet.Common;
using Wolverine;
using Wolverine.CritterWatch;
using Wolverine.Http;
using Wolverine.Http.Transport;

// =============================================================================================
// CritterWatchConsole — the standalone monitoring dashboard for the WebService.Http sample.
//
// What's different from the broker fleets: here the monitored OrderService reaches this console over
// WOLVERINE'S HTTP TRANSPORT, not a message broker. So instead of listening on a RabbitMQ/Postgres queue,
// the console maps Wolverine's HTTP-transport receive routes (MapWolverineHttpTransportEndpoints) — the
// OrderService POSTs its registration/heartbeat/telemetry envelopes to /_wolverine/invoke, and those
// envelopes are executed inline against the console's CritterWatch handlers.
//
// PUSH model: the monitored service is the HTTP client; the console is the HTTP server. (Confirmed from
// Wolverine.Http.Transport: ToHttpEndpoint() is the publish side, MapWolverineHttpTransportEndpoints() is
// the receive side — the sender POSTs envelopes to the receiver's HTTP endpoint.)
// =============================================================================================

var builder = WebApplication.CreateBuilder(args);

// The console's own Postgres store. Under Aspire this is the `critterwatch` database; standalone it falls
// back to the localhost docker-compose Postgres. NOTE: this is the *console's* store, entirely separate
// from the OrderService's own event store (the OrderService keeps its events in its own `orders` schema).
var consoleConnectionString = SampleConnections.Postgres();

// begin-snippet: console-http-transport-receive
builder.AddCritterWatch(
    consoleConnectionString,
    configureWolverine: opts =>
    {
        // PIN the CritterWatch wire-format serializer (System.Text.Json + Brotli framing) as this host's
        // default Wolverine serializer. This is the HTTP-transport equivalent of the broker fleets'
        // `.ListenToRabbitQueue("critterwatch").UseCritterWatchSerializer()`:
        //   - On a broker the inbound queue gets the serializer pinned per-endpoint.
        //   - On the HTTP transport, inbound envelopes arrive at /_wolverine/invoke and are decoded by the
        //     serializer the runtime resolves for the envelope's content-type. The CritterWatch serializer
        //     reports content-type "application/json" (its inner STJ type) but frames the body with a
        //     Brotli prefix, so the console must resolve "application/json" to the CritterWatch serializer —
        //     not the stock JSON one — or the framed bytes won't decode. Setting it as the default does
        //     exactly that. Safe here because this host is dedicated to CritterWatch traffic.
        opts.DefaultSerializer = Wolverine.CritterWatch.WolverineOptionsExtensions.BuildCritterWatchSerializer();
    },
    // Single-node sample → no sharded external topology to wire, so cluster partitioning stays off.
    enableClusterPartitioning: false);
// end-snippet

// WolverineFx.Http hosts the HTTP-transport receive routes. AddWolverineHttp wires the executor those
// routes resolve; MapWolverineHttpTransportEndpoints (below, after Build) maps the actual POST endpoints.
builder.Services.AddWolverineHttp();

// The console can also push operator commands (PauseProjection / RebuildProjection / DLQ ops / …) BACK to
// the OrderService over the same HTTP transport. Sending over the transport needs the transport client.
//
// ⚠️ On WolverineFx 6.23.1 (the pin below) this registration is NOT sufficient, and the control channel
// does not work. An earlier version of this comment claimed the named HttpClient for a service's control
// URL is "created on demand from the service's reported control URI" — it is not, and nothing creates it.
// WolverineHttpTransportClient uses the outbound URI purely as an IHttpClientFactory client NAME and then
// posts to that client's BaseAddress; CreateClient returns a default client with a null BaseAddress for an
// unknown name, so the send throws "An invalid request URI was provided…". The console cannot pre-register
// a named client either, because it only learns a service's control URL at runtime from that service's own
// registration. Reported as ProductSupport#34, fixed in wolverine#3681 (GH-3690): the transport falls back
// to its own isolated client and posts to the absolute outbound URI, and AddWolverineHttp() registers both
// of these lines for you. DELETE THEM when this sample bumps past that Wolverine release.
builder.Services.AddScoped<IWolverineHttpTransportClient, WolverineHttpTransportClient>();
builder.Services.AddHttpClient();

builder.Services.AddHealthChecks();

var app = builder.Build();

// Maps CritterWatch's HTTP API (/api/critterwatch/*), the SignalR hub (/api/messages), and the SPA. The
// license check is skipped in Development (Aspire's default environment).
app.UseCritterWatch();

// THE receive side of the monitoring link. Maps:
//   POST /_wolverine/invoke          — single inline envelope (what AddCritterWatchMonitoring's https route
//                                       uses); executed against this console's CritterWatch handlers.
//   POST /_wolverine/batch/{queue}   — batched envelopes, delivered to the named local queue.
// The OrderService's AddCritterWatchMonitoring(...) points its first URI at this console's /_wolverine/invoke.
app.MapWolverineHttpTransportEndpoints();

app.MapHealthChecks("/health");

app.Run();
