using JasperFx;
using JasperFx.Core;
using Microsoft.Data.SqlClient;
using ShipmentTracking.Api;
using ShipmentTracking.Data;
using ShipmentTracking.Handlers;
using ShipmentTracking.Messages;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Http;
using Wolverine.RabbitMQ;
using Wolverine.SqlServer;

var builder = WebApplication.CreateBuilder(args);

var shipmentsConnection = builder.Configuration.GetConnectionString("Shipments")!;
var rabbitConnection = builder.Configuration.GetConnectionString("RabbitMq")!;

builder.Services.AddSingleton(new ShipmentRepository(shipmentsConnection));
builder.Services.AddSingleton<ICarrierLabelClient, FakeCarrierLabelClient>();

builder.UseWolverine(opts =>
{
    opts.ServiceName = "ShipmentTracking";

    opts.UseRabbitMq(factory => factory.Uri = new Uri($"amqp://{rabbitConnection}"))
        .AutoProvision();

    // Envelope storage: saga persistence plus the transactional outbox, the
    // direct equivalent of UsePersistence<SqlPersistence>() + EnableOutbox().
    opts.PersistMessagesWithSqlServer(shipmentsConnection);

    // -----------------------------------------------------------------------
    // Routing. Commands are SENT and need a destination; events are published.
    // -----------------------------------------------------------------------
    opts.PublishMessage<BookShipment>().ToRabbitQueue("shipment-commands");
    opts.PublishMessage<CancelShipment>().ToRabbitQueue("shipment-commands");
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
    // endpoint; Wolverine configures it per exception type.
    // -----------------------------------------------------------------------
    opts.OnException<SqlException>()
        .RetryWithCooldown(50.Milliseconds(), 250.Milliseconds(), 1.Seconds())
        .Then.MoveToErrorQueue();

    opts.OnException<TimeoutException>()
        .RetryTimes(3)
        .Then.Requeue();

    opts.OnException<InvalidOperationException>()
        .MoveToErrorQueue();
});

var app = builder.Build();

app.MapWolverineEndpoints();

// Replaces EnableInstallers() — creates the envelope storage on startup.
return await app.RunJasperFxCommands(args);
