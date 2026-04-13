using JasperFx.Core;
using Marten;
using Wolverine;
using Wolverine.CritterWatch;
using Wolverine.RabbitMQ;
using Wolverine.Http;
using Wolverine.Marten;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddWolverineHttp();

builder.Services.AddMarten(opts =>
{
    var connectionString = builder.Configuration.GetConnectionString("Marten")
        ?? "Host=localhost;Port=5433;Database=outbox_demo;Username=postgres;Password=postgres";

    opts.Connection(connectionString);
    opts.DatabaseSchemaName = "outbox_demo";

    opts.Schema.For<OutboxDemo.Registration>()
        // Watch this syntax. Marten does this by making you specify a separate expression for each property
        // rather than using an anonymous type
        .UniqueIndex(r => r.MemberId, r => r.EventId);
})
// IntegrateWithWolverine() enables the Marten outbox — messages published during
// a handler or HTTP request are stored in the same Marten transaction and delivered
// after commit. This replaces MassTransit's EntityFramework outbox entirely.
.IntegrateWithWolverine()
.UseLightweightSessions();

builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
    opts.Policies.AutoApplyTransactions();
    opts.ServiceName = "OutboxDemo";

    // Wolverine's durable inbox/outbox with Marten persistence
    opts.Durability.Mode = DurabilityMode.Solo;

    // CritterWatch monitoring — Aspire injects RabbitMQ as ConnectionStrings:rabbitmq
    opts.UseRabbitMq(new Uri(builder.Configuration.GetConnectionString("rabbitmq") ?? "amqp://localhost"))
        .DisableDeadLetterQueueing().AutoProvision();

    opts.AddCritterWatchMonitoring(
        "rabbitmq://queue/critterwatch".ToUri(),
        "rabbitmq://queue/outbox_demo".ToUri());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapWolverineEndpoints();

await app.RunAsync();
