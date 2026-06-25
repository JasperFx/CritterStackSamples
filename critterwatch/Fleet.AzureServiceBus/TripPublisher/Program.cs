using Fleet.Common;
using JasperFx;
using JasperFx.Core;
using JasperFx.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TripMessages;
using TripPublisher;
using Wolverine;
using Wolverine.AzureServiceBus;
using Wolverine.CritterWatch;

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

                // ---- Azure Service Bus transport (transport swap vs. the RabbitMQ flagship) -----------
                opts.UseAzureServiceBus(SampleConnections.AzureServiceBus());

                // #356 emulator constraint — drop the per-node system queues to stay under the 50-entity
                // cap. As in TripService, the entities are emulator-owned (declared in the AppHost), so we
                // neither set ManagementConnectionString nor call AutoProvision.
                var asb = opts.Transports.GetOrCreate<AzureServiceBusTransport>();
                asb.SystemQueuesEnabled = false;

                // #356 emulator constraint — EXPLICIT routing replaces UseConventionalRouting() (which
                // would mint ~one ASB queue per message type and exceed the cap). Every command the driver
                // scripts a trip through goes to TripService's app inbox; the publisher listens on its own
                // app inbox for the ContinueTrip cascade back. Each queue maps 1:1 to an AppHost
                // AddServiceBusQueue.
                opts.PublishMessage<StartTrip>().ToAzureServiceBusQueue("trip_service_app");
                opts.PublishMessage<RecordTravel>().ToAzureServiceBusQueue("trip_service_app");
                opts.PublishMessage<Depart>().ToAzureServiceBusQueue("trip_service_app");
                opts.PublishMessage<Arrive>().ToAzureServiceBusQueue("trip_service_app");
                opts.PublishMessage<RecordBreakdown>().ToAzureServiceBusQueue("trip_service_app");
                opts.PublishMessage<AbortTrip>().ToAzureServiceBusQueue("trip_service_app");
                opts.PublishMessage<EndTrip>().ToAzureServiceBusQueue("trip_service_app");
                opts.ListenToAzureServiceBusQueue("trip_publisher_app");

                opts.Policies.UseDurableInboxOnAllListeners();

                // Report to CritterWatch over the shared control queue; callbacks come back to trip_publisher.
                // #345: must be the `asb://queue/{name}` form (see TripService for the why).
                opts.AddCritterWatchMonitoring(
                    "asb://queue/critterwatch".ToUri(),
                    "asb://queue/trip_publisher".ToUri());
            })
            .ConfigureServices(services =>
            {
                // The hosted service kicks off the first burst; ContinueTrip drives the rest.
                services.AddHostedService<KickOffPublishing>();
                services.AddResourceSetupOnStartup();
            });
    }
}
