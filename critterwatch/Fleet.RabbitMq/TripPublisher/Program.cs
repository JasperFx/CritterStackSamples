using Fleet.Common;
using JasperFx;
using JasperFx.Core;
using JasperFx.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TripPublisher;
using Wolverine;
using Wolverine.CritterWatch;
using Wolverine.RabbitMQ;

return await TripPublisherProgram.CreateHostBuilder(args).RunJasperFxCommands(args);

public static class TripPublisherProgram
{
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseWolverine(opts =>
            {
                opts.ServiceName = "TripPublisher";

                // A pure driver — Solo durability is fine, it owns no projections/agents.
                opts.Durability.Mode = DurabilityMode.Solo;
                opts.ApplicationAssembly = typeof(TripPublisherProgram).Assembly;
                opts.EnableAutomaticFailureAcks = false;

                opts.Services.AddSingleton<Publisher>();

                // Conventional routing sends each command to the queue named after its message type; the
                // matching listeners on TripService pick them up. Same broker resolution as TripService.
                opts.UseRabbitMq(SampleConnections.RabbitMq())
                    .DisableDeadLetterQueueing()
                    .UseConventionalRouting()
                    .AutoProvision();

                opts.Policies.UseDurableInboxOnAllListeners();

                // Report to CritterWatch over the shared control queue; callbacks come back to trip_publisher.
                opts.AddCritterWatchMonitoring(
                    "rabbitmq://queue/critterwatch".ToUri(),
                    "rabbitmq://queue/trip_publisher".ToUri());
            })
            .ConfigureServices(services =>
            {
                // The hosted service kicks off the first burst; ContinueTrip drives the rest.
                services.AddHostedService<KickOffPublishing>();
                services.AddResourceSetupOnStartup();
            });
    }
}
