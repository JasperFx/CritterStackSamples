using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.Core;
using Marten;
using SagaWorkflows;
using SagaWorkflows.AgentLoop;
using SagaWorkflows.MoneyTransfer;
using SagaWorkflows.TripBooking;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Http;
using Wolverine.Marten;
using Wolverine.Postgresql;

var builder = WebApplication.CreateBuilder(args);

// Every saga in this sample is kept in Marten, as a document or as an event stream. See README.md for the database
var connectionString = builder.Configuration.GetConnectionString("Marten")
                       ?? throw new InvalidOperationException("No ConnectionStrings:Marten configured");

// The money transfer and the trip are written twice, on the same messages: as document sagas (the default) and as
// event-sourced sagas. One application runs one or the other, so Sagas:EventSourced=true swaps them, and
// Marten:Schema keeps the two apart in one database
var eventSourced = builder.Configuration.GetValue<bool>("Sagas:EventSourced");

builder.Services.AddMarten(opts =>
    {
        opts.Connection(connectionString);
        opts.DatabaseSchemaName = builder.Configuration.GetValue("Marten:Schema", "saga_workflows")!;
        opts.DisableNpgsqlLogging = true;

        // Saga.LastModified is Marten's mt_last_modified column. Index it to find idle agent runs without a scan
        opts.Schema.For<AgentRun>().IndexLastModified();
    })
    // Marten stores the sagas, and is the transactional inbox and outbox. Wolverine registers each event-sourced saga
    // as an Inline snapshot of its own stream
    .IntegrateWithWolverine();

builder.Host.UseWolverine(opts =>
{
    opts.ServiceName = "saga-workflows";

    // Under the test runner the entry assembly is the test project, so say where the handlers are
    opts.ApplicationAssembly = typeof(Program).Assembly;

    Type[] otherStyle = eventSourced ? [typeof(Transfer), typeof(Trip)] : [typeof(EventSourcedTransfer), typeof(EventSourcedTrip)];
    opts.Discovery.CustomizeHandlerDiscovery(types =>
        types.Excludes.WithCondition("the other style of the transfer and trip sagas", otherStyle.Contains));

    opts.Policies.AutoApplyTransactions();

    // Every local queue is backed by the durable inbox, so a message, a reply or a scheduled timeout survives a crash
    // just like a workflow tool's history does
    opts.Policies.UseDurableLocalQueues();

    // A message for a saga that is gone fails with UnknownSagaException. That's the default, said out loud here: a
    // saga, a message type or a Handle method opts into Discard where a late message is normal
    opts.Policies.WhenSagaIsMissing(SagaMissingBehavior.Throw);

    // Fan-in. Many replies for one saga at once fight over the saga document. Send every IFanInReply through global
    // partitioning instead, grouped by saga id: the replies for one saga are handled one at a time, and different
    // sagas still run in parallel, across the whole cluster. The shards are PostgreSQL queues in the database we
    // already have; on a single node they shortcut straight to the companion local queues
    opts.MessagePartitioning
        .UseInferredMessageGrouping()
        .GlobalPartitioned(topology =>
        {
            topology.UseShardedPostgresqlQueues("fan_in", 4);
            topology.MessagesImplementing<IFanInReply>();
        });

    // A Fault<T> or a timeout can still land beside a reply. The loser of the optimistic concurrency check on an
    // IRevisioned saga simply runs again against the fresh saga
    opts.OnException<ConcurrencyException>()
        .RetryWithCooldown(50.Milliseconds(), 100.Milliseconds(), 250.Milliseconds(), 500.Milliseconds());

    // Anything else: try three times in all, then dead letter. A dead lettered step sends its Fault<T> back to the
    // saga that sent it, when that saga declared a Compensate or Handle(Fault<T>) for it. An exception marked
    // [TerminalFailure] skips the retries
    opts.OnException<Exception>().RetryTimes(2);
});

// A sample: compile the generated handler code at runtime in every environment, rather than ship it pre-generated
builder.Services.CritterStackDefaults(x => x.Production.GeneratedCodeMode = TypeLoadMode.Auto);

builder.Services.AddWolverineHttp();
builder.Services.AddFakeExternalSystems();

var app = builder.Build();
app.MapWolverineEndpoints();

return await app.RunJasperFxCommands(args);

public partial class Program;
