using Fleet.Common;
using Incidents.Domain;
using Incidents.Publisher;
using JasperFx;
using JasperFx.Core;
using JasperFx.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.CritterWatch;
using Wolverine.RabbitMQ;

return await IncidentPublisherProgram.CreateHostBuilder(args).RunJasperFxCommands(args);

/// <summary>
/// Console publisher that drives synthetic incident traffic into IncidentService over RabbitMQ. Mirrors
/// <c>TripPublisherProgram</c>: Solo durability, conventional routing, an <c>IHostedService</c> that walks
/// ~50 in-flight <see cref="IncidentStream"/>s.
/// </summary>
public static class IncidentPublisherProgram
{
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseWolverine(opts =>
            {
                opts.ServiceName = "IncidentPublisher";
                opts.Durability.Mode = DurabilityMode.Solo;
                opts.ApplicationAssembly = typeof(IncidentPublisherProgram).Assembly;
                opts.EnableAutomaticFailureAcks = false;

                opts.Services.AddSingleton<Publisher>();
                opts.UseRabbitMq(SampleConnections.RabbitMq())
                    .DisableDeadLetterQueueing()
                    .UseConventionalRouting()
                    .AutoProvision();

                opts.Policies.UseDurableInboxOnAllListeners();

                opts.AddCritterWatchMonitoring(
                    "rabbitmq://queue/critterwatch".ToUri(),
                    "rabbitmq://queue/incident_publisher".ToUri());
            })
            .ConfigureServices(services =>
            {
                services.AddHostedService<KickOffPublishing>();
                services.AddResourceSetupOnStartup();
            });
    }
}
