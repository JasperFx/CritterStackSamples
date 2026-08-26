using CritterWatch.Services.Hosting;
using JasperFx.Resources;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// Aspire injects this as ConnectionStrings__critterstore. The fallback keeps the
// console runnable under plain `docker compose` too.
var critterWatchConnection = builder.Configuration.GetConnectionString("critterstore")
    ?? "Server=localhost,1433;Database=CritterWatch;User Id=sa;Password=P@ssw0rd!;TrustServerCertificate=true";

var rabbitConnection = builder.Configuration.GetConnectionString("rabbitmq")
    ?? "amqp://guest:guest@localhost:5672";

// AddCritterWatch registers Wolverine, Polecat, SignalR, AddWolverineHttp() and every
// CritterWatch service. The console owns its own SQL Server database — deliberately a
// different one from ShipmentTracking's, because a monitoring console that dies with
// the thing it monitors is not a monitoring console.
builder.AddCritterWatch(
    critterWatchConnection,
    configureWolverine: opts =>
    {
        opts.ServiceName = "CritterWatch";

        opts.UseRabbitMq(new Uri(rabbitConnection))
            .AutoProvision();

        opts.ListenToRabbitQueue("critterwatch").Sequential();
    });
// enableClusterPartitioning defaults to false on this flavor — single node, single
// `critterwatch` queue. A second BFF node would need it true, a Redis connection
// string for the SignalR backplane, and a sharded topology matching the monitored
// services' own.

// NOTE: no .DisableDeadLetterQueueing() here, on purpose.
//
// The console and ShipmentTracking share one broker, and BOTH declare the well-known
// `critterwatch` queue. RabbitMQ rejects an inequivalent redeclare with
// PRECONDITION_FAILED (406), so the queue's dead-letter arguments have to be identical
// on every side. ShipmentTracking leaves DLQ at the Wolverine default, so the console
// must too. Disabling it on one side only is the trap: whichever process starts second
// dies at startup.

// The console needs its Polecat schema before the first telemetry message lands, and
// it never gets a CLI invocation to do it — under Aspire it is just started. Polecat
// creates DOCUMENT tables on demand, but the event tables (pc_streams, pc_events) come
// from the resource model, so without this the console starts, listens, and then fails
// every inbound message with "Invalid object name 'critterwatch.pc_streams'".
builder.Services.AddResourceSetupOnStartup();

var app = builder.Build();

app.UseCritterWatch();

await app.RunAsync();
