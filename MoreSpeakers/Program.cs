using JasperFx.Core;
using Marten;
using Wolverine;
using Wolverine.CritterWatch;
using Wolverine.RabbitMQ;
using Wolverine.FluentValidation;
using Wolverine.Http;
using Wolverine.Http.FluentValidation;
using Wolverine.Marten;
using OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// OpenTelemetry — export traces to Jaeger via OTLP
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Wolverine:MoreSpeakers");
        metrics.AddMeter("Marten");
    })
    .WithTracing(tracing =>
    {
        tracing.AddSource("Wolverine");
        tracing.AddSource("Marten");
    })
    .UseOtlpExporter();

builder.Services.AddWolverineHttp();

builder.Services.AddMarten(opts =>
{
    var connectionString = builder.Configuration.GetConnectionString("Marten")
        ?? "Host=localhost;Port=5433;Database=more_speakers;Username=postgres;Password=postgres";

    opts.Connection(connectionString);
    opts.DatabaseSchemaName = "speakers";

    opts.Schema.For<Speakers.Speaker>().Index(x => x.Email);
    opts.Schema.For<Speakers.Speaker>().Index(x => x.Expertise);
    opts.Schema.For<Mentorships.Mentorship>().Index(x => x.MentorId);
    opts.Schema.For<Mentorships.Mentorship>().Index(x => x.MenteeId);
    opts.Schema.For<Mentorships.Mentorship>().Index(x => x.Status);
})
.IntegrateWithWolverine()
.UseLightweightSessions();

builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
    opts.Policies.AutoApplyTransactions();
    opts.UseFluentValidation();
    opts.ServiceName = "MoreSpeakers";

    // CritterWatch monitoring — Aspire injects RabbitMQ as ConnectionStrings:rabbitmq
    opts.UseRabbitMq(new Uri(builder.Configuration.GetConnectionString("rabbitmq") ?? "amqp://localhost"))
        .DisableDeadLetterQueueing().AutoProvision();

    opts.AddCritterWatchMonitoring(
        "rabbitmq://queue/critterwatch".ToUri(),
        "rabbitmq://queue/more_speakers".ToUri());
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
