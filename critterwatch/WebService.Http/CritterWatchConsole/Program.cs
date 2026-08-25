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
// the OrderService over the same HTTP transport. AddWolverineHttp() above registers the transport client for
// that; since wolverine#3681 (GH-3690, WolverineFx 6.24.0) the transport posts to the absolute outbound URI
// it learns from the service's registration, so no per-service named HttpClient has to be pre-registered.
// (On 6.23.1 and earlier that send threw "An invalid request URI was provided…" — ProductSupport#34.)

builder.Services.AddHealthChecks();

var app = builder.Build();

// Maps CritterWatch's HTTP API (/api/critterwatch/*), the SignalR hub (/api/messages), and the SPA. The
// license check is skipped in Development (Aspire's default environment).
app.UseCritterWatch();

// THE receive side of the monitoring link. Maps:
//   POST /_wolverine/invoke          — single inline envelope, executed against this console's handlers.
//   POST /_wolverine/batch/{queue}   — batched envelopes, delivered to the named local queue — THIS is what
//                                       the monitored service's telemetry uses: CritterWatch 1.0 (#958) sends
//                                       all telemetry BufferedInMemory, i.e. as batches, and the invoke route
//                                       answers a batch with 415.
// The OrderService's AddCritterWatchMonitoring(...) points its first URI at /_wolverine/batch/critterwatch.
app.MapWolverineHttpTransportEndpoints();

app.MapHealthChecks("/health");

app.Run();
