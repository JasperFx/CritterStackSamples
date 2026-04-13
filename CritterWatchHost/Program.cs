using CritterWatch.Services.Hosting;
using JasperFx.Core;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

var postgresConnection = builder.Configuration.GetConnectionString("critterwatch")
    ?? "Host=localhost;Port=5433;Database=critterwatch;Username=postgres;Password=postgres";

builder.AddCritterWatch(postgresConnection, opts =>
{
    opts.UseRabbitMq(new Uri(
        builder.Configuration.GetConnectionString("rabbitmq")
        ?? "amqp://localhost"))
        .DisableDeadLetterQueueing()
        .AutoProvision();

    opts.ListenToRabbitQueue("critterwatch").Sequential();
});

var app = builder.Build();

app.UseCritterWatch();

await app.RunAsync();
