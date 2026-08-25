using JasperFx;
using JasperFx.Core;
using Microsoft.Data.SqlClient;
using Polecat;
using ShipmentTracking.Api;
using ShipmentTracking.Data;
using ShipmentTracking.Handlers;
using ShipmentTracking.Messages;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Http;
using Wolverine.Persistence;
using Wolverine.Polecat;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

var shipmentsConnection = builder.Configuration.GetConnectionString("Shipments")!;
var rabbitConnection = builder.Configuration.GetConnectionString("RabbitMq")!;

builder.Services.AddSingleton<ICarrierLabelClient, FakeCarrierLabelClient>();

// ===========================================================================
// Polecat replaces ShipmentRepository AND PersistMessagesWithSqlServer.
//
// There is no mapping, no DDL and no repository: Shipment is stored as native
// SQL Server 2025 `json` and the table is created on first use.
//
// IntegrateWithWolverine() is the part that pays off the debt phase 1 left
// behind. It registers Wolverine's message store over the SAME SQL Server
// connection Polecat is using, and it inserts Polecat's persistence frame
// provider ahead of the others — so a handler's document writes, its saga
// state and the outbox rows for its cascading messages all commit in one
// transaction on one connection. The Dapper repository opened its own
// connection outside that transaction and could not.
// ===========================================================================
builder.Services.AddPolecat(opts =>
    {
        opts.Connection(shipmentsConnection);

        // Documents, events and the Wolverine envelope tables all land here:
        // IntegrateWithWolverine() inherits the store's schema name for message
        // storage when none is configured explicitly.
        opts.DatabaseSchemaName = "shipments";
    })
    .IntegrateWithWolverine();

builder.UseWolverine(opts =>
{
    opts.ServiceName = "ShipmentTracking";

    // Handlers that return IStorageAction<T> turn transactional middleware on by
    // themselves; this covers the rest, including the saga chains.
    opts.Policies.AutoApplyTransactions();

    // The connection string is a real AMQP URI. Phase 1 carried across the
    // NServiceBus-shaped "host=localhost" and interpolated it into "amqp://{...}",
    // which produces "amqp://host=localhost" and throws UriFormatException at
    // startup. Two phases of clean compiles never saw it; the first `dotnet run`
    // did, before a single line of Polecat code was reached.
    opts.UseRabbitMq(factory => factory.Uri = new Uri(rabbitConnection))
        .AutoProvision();

    // -----------------------------------------------------------------------
    // Routing. Commands are SENT and need a destination; events are published.
    // -----------------------------------------------------------------------
    opts.PublishMessage<BookShipment>().ToRabbitQueue("shipment-commands");
    opts.PublishMessage<CancelShipment>().ToRabbitQueue("shipment-commands");
    opts.PublishMessage<RecordTrackingNumber>().ToRabbitQueue("shipment-commands");
    opts.PublishMessage<GenerateLabel>().ToRabbitQueue("label-generation");
    opts.PublishMessage<EscalateLateShipment>().ToRabbitQueue("shipment-operations");

    // RecordCarrierScan is deliberately absent: the global partitioned topology
    // below owns its routing and sends it to the right shard.

    // =======================================================================
    // Listening endpoints.
    //
    // The NServiceBus version had ONE endpoint and therefore one concurrency
    // setting for all three workloads. Raising it to clear a carrier-scan
    // backlog also raised the number of concurrent 45-second label calls;
    // lowering it to protect the carrier API starved the scans.
    //
    // Wolverine asks the question per listener, and the three answers differ
    // for three different reasons.
    // =======================================================================

    // -- carrier-events -----------------------------------------------------
    // Decided by THROUGHPUT COST. Thousands a minute, and Durable's inbox
    // insert plus mark-handled per message is the ceiling. NativeAck settles
    // natively, with no database involvement at all.
    //
    // Ordering is a SEPARATE concern, and the naive answer is wrong here. A
    // single carrier-events queue with competing consumers across nodes has no
    // per-shipment ordering at all, and PartitionProcessingByGroupId on the
    // listener would only order each node's own work while two nodes processed
    // the same shipment simultaneously.
    //
    // So the topology is global: the queue is sharded, grouping is inferred
    // from ShipmentId, and one shipment always lands on the same shard. Only
    // then does per-shipment ordering hold across the cluster.
    //
    // Phase 3 leans on this harder than phase 1 did. CarrierScanHandler's
    // "is this scan newer?" guard used to be a SQL WHERE clause and is now a
    // read-modify-write in C#; it is safe only because this topology means one
    // shipment's scans are never in flight on two nodes at once.
    opts.MessagePartitioning
        .UseInferredMessageGrouping()
        .ByPropertyNamed("ShipmentId")
        .GlobalPartitioned(topology =>
        {
            // Opts the partition slots out of the durable inbox — this is the
            // NativeAck half of the decision.
            topology.ProcessInParallelWithNativeAcks();

            var sharded = topology.UseShardedRabbitQueues("carrier-events", 5);
            sharded.Message<RecordCarrierScan>();
        });

    // -- shipment-commands --------------------------------------------------
    // Decided by DELIVERY GUARANTEE. Low volume, but each one writes the
    // database and publishes follow-on events that must not be lost if the
    // process dies mid-handler. That is what the outbox is for, and the outbox
    // needs a durable endpoint.
    opts.ListenToRabbitQueue("shipment-commands")
        .UseDurableInbox();

    // -- label-generation ---------------------------------------------------
    // Decided by HANDLER DURATION, and this is the one that looks like
    // carrier-events and is not.
    //
    // Under NativeAck the broker's clock runs for the whole handler. A 30-90
    // second carrier API call outlives the delivery lease, the broker redelivers
    // while the original is still running, and the duplicate executes
    // CONCURRENTLY with it. Durable acks after the inbox insert, so the clock
    // stops before the slow work starts.
    //
    // Do not "optimise" this one onto NativeAck to match its noisy neighbour.
    opts.ListenToRabbitQueue("label-generation")
        .UseDurableInbox()
        .MaximumParallelMessages(4);

    // -----------------------------------------------------------------------
    // Error handling. NServiceBus configured recoverability once for the whole
    // endpoint; Wolverine configures it per exception type, and the first rule
    // that matches an exception wins.
    // -----------------------------------------------------------------------

    // Whole-document writes need a concurrency policy that column-scoped SQL
    // updates never did. Shipment is IRevisioned, so a losing write throws
    // rather than silently discarding the winner's change; reloading and
    // retrying is the whole point, and it is cheap because no handler holds a
    // document across slow work any more.
    opts.OnException<ConcurrencyException>()
        .RetryWithCooldown(50.Milliseconds(), 200.Milliseconds(), 500.Milliseconds());

    // [Entity(OnMissing = OnMissing.ThrowException)] raises this. A command for a
    // shipment that does not exist is not retryable and should be visible.
    opts.OnException<RequiredDataMissingException>()
        .MoveToErrorQueue();

    // Narrowed in phase 3: Polecat's integration registers its own Discard rule
    // for the unique-constraint violations a duplicate incoming envelope causes
    // (2627 / 2601). Excluding them here keeps this broad transient-fault retry
    // from claiming those first and turning a benign duplicate into three
    // retries and a dead letter.
    opts.OnException<SqlException>(e => e.Number is not (2627 or 2601))
        .RetryWithCooldown(50.Milliseconds(), 250.Milliseconds(), 1.Seconds())
        .Then.MoveToErrorQueue();

    opts.OnException<TimeoutException>()
        .RetryTimes(3)
        .Then.Requeue();

    opts.OnException<InvalidOperationException>()
        .MoveToErrorQueue();
});

builder.Services.AddWolverineHttp();

var app = builder.Build();

app.MapWolverineEndpoints();

// Replaces EnableInstallers() — creates the Polecat schema and the envelope
// storage on startup, and adds the JasperFx `resources` and `codegen` commands.
return await app.RunJasperFxCommands(args);

// Makes the implicit Program class reachable from the test project so Alba can
// bootstrap the real application rather than a hand-assembled imitation of it.
public partial class Program;
