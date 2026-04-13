using CritterWatch.Services.Hosting;
using JasperFx.Core;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// Aspire injects the connection string as "critterwatch" via WithReference(db, "critterwatch")
var postgresConnection = builder.Configuration.GetConnectionString("critterwatch")
    ?? "Host=localhost;Port=5432;Database=critterwatch;Username=postgres;Password=postgres";

builder.AddCritterWatch(postgresConnection, opts =>
{
    // Aspire injects RabbitMQ connection as "rabbitmq"
    var rabbitUri = builder.Configuration.GetConnectionString("rabbitmq") ?? "amqp://localhost";
    opts.UseRabbitMq(new Uri(rabbitUri))
        .DisableDeadLetterQueueing()
        .AutoProvision();

    opts.ListenToRabbitQueue("critterwatch").Sequential();
});

var app = builder.Build();

app.UseCritterWatch();

await app.RunAsync();
