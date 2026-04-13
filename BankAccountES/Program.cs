using BankAccountES;
using JasperFx.Events.Projections;
using JasperFx.Core;
using Marten;
using Marten.Events.Projections;
using Wolverine.CritterWatch;
using Wolverine.RabbitMQ;
using Wolverine;
using Wolverine.FluentValidation;
using Wolverine.Http;
using Wolverine.Http.FluentValidation;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddWolverineHttp();

builder.Services.AddMarten(opts =>
{
    var connectionString = builder.Configuration.GetConnectionString("Marten")
        ?? "Host=localhost;Port=5433;Database=bank_account;Username=postgres;Password=postgres";

    opts.Connection(connectionString);
    opts.DatabaseSchemaName = "bank";

    // Inline snapshot projections — aggregates are always up-to-date
    opts.Projections.Snapshot<Account>(SnapshotLifecycle.Inline);
    opts.Projections.Snapshot<Client>(SnapshotLifecycle.Inline);

    // Transaction history projection — builds read model from deposit/withdrawal events
    opts.Projections.Add<AccountTransactionsProjection>(ProjectionLifecycle.Inline);
})
.IntegrateWithWolverine()
.UseLightweightSessions();

builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
    opts.Policies.AutoApplyTransactions();
    opts.UseFluentValidation();
    opts.ServiceName = "BankAccount";

    // CritterWatch monitoring
    opts.UseRabbitMq(rabbit =>
    {
        rabbit.HostName = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
    }).DisableDeadLetterQueueing().AutoProvision();

    opts.AddCritterWatchMonitoring(
        "rabbitmq://queue/critterwatch".ToUri(),
        "rabbitmq://queue/bank_account".ToUri());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapWolverineEndpoints(opts =>
{
    opts.UseFluentValidationProblemDetailMiddleware();
});

await app.RunAsync();
