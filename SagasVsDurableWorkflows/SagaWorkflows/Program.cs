using JasperFx;
using JasperFx.Core;
using Marten;
using SagaWorkflows;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Http;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

// Every saga in this sample is a Marten document. See README.md for the database
var connectionString = builder.Configuration.GetConnectionString("Marten")
                       ?? throw new InvalidOperationException("No ConnectionStrings:Marten configured");

builder.Services.AddMarten(opts =>
    {
        opts.Connection(connectionString);
        opts.DatabaseSchemaName = "saga_workflows";
        opts.DisableNpgsqlLogging = true;
    })
    // Marten stores the sagas, and is the transactional inbox and outbox
    .IntegrateWithWolverine();

builder.Host.UseWolverine(opts =>
{
    // Under the test runner the entry assembly is the test project, so say where the handlers are
    opts.ApplicationAssembly = typeof(Program).Assembly;

    opts.Policies.AutoApplyTransactions();

    // Every local queue is backed by the durable inbox, so a message, a reply or a scheduled
    // timeout survives a crash just like a workflow tool's history does
    opts.Policies.UseDurableLocalQueues();

    // Two replies for one saga at the same moment (fan-in) lose the optimistic concurrency
    // check on IRevisioned sagas. The loser simply runs again against the fresh saga
    opts.OnException<ConcurrencyException>()
        .RetryWithCooldown(50.Milliseconds(), 100.Milliseconds(), 250.Milliseconds(), 500.Milliseconds());

    // Anything else: try three times in all, then dead letter. A dead lettered step sends its
    // Fault<T> back to a saga that declared a Compensate or Handle(Fault<T>) for it
    opts.OnException<Exception>().RetryTimes(2);
});

builder.Services.AddWolverineHttp();
builder.Services.AddFakeExternalSystems();

var app = builder.Build();
app.MapWolverineEndpoints();

return await app.RunJasperFxCommands(args);

public partial class Program;
