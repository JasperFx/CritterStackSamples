using Fleet.Common;
using Google.Api.Gax;
using JasperFx;
using JasperFx.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TripMessages;
using TripPublisher;
using Wolverine;
using Wolverine.CritterWatch;
using Wolverine.Pubsub;

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

                var projectId = SampleConnections.PubsubProjectId();

                // ---- Google Cloud Pub/Sub transport (transport swap vs. the RabbitMQ flagship) ---------
                // Emulator detection + AutoProvision: see TripService for the full rationale.
                opts.UsePubsub(projectId)
                    .UseEmulatorDetection(EmulatorDetection.EmulatorOrProduction)
                    .AutoProvision();

                // EXPLICIT routing. Every command the driver scripts a trip through goes to TripService's
                // app-inbox topic; the publisher listens on its own app-inbox topic for the ContinueTrip
                // cascade back.
                opts.PublishMessage<StartTrip>().ToPubsubTopic("trip_service_app");
                opts.PublishMessage<RecordTravel>().ToPubsubTopic("trip_service_app");
                opts.PublishMessage<Depart>().ToPubsubTopic("trip_service_app");
                opts.PublishMessage<Arrive>().ToPubsubTopic("trip_service_app");
                opts.PublishMessage<RecordBreakdown>().ToPubsubTopic("trip_service_app");
                opts.PublishMessage<AbortTrip>().ToPubsubTopic("trip_service_app");
                opts.PublishMessage<EndTrip>().ToPubsubTopic("trip_service_app");
                opts.ListenToPubsubTopic("trip_publisher_app");

                opts.Policies.UseDurableInboxOnAllListeners();

                // Report to CritterWatch over the shared control topic; callbacks come back to trip_publisher.
                opts.AddCritterWatchMonitoring(
                    GcpPubsubEndpointUri.Topic(projectId, "critterwatch"),
                    GcpPubsubEndpointUri.Topic(projectId, "trip_publisher"));
            })
            .ConfigureServices(services =>
            {
                // The hosted service kicks off the first burst; ContinueTrip drives the rest.
                services.AddHostedService<KickOffPublishing>();
                services.AddResourceSetupOnStartup();
            });
    }
}
