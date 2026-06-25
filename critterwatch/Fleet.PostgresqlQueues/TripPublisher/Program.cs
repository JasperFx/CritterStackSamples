using Fleet.Common;
using JasperFx;
using JasperFx.Core;
using JasperFx.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TripMessages;
using TripPublisher;
using Wolverine;
using Wolverine.CritterWatch;
using Wolverine.Postgresql;

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

                // ---- Wolverine PostgreSQL DB-backed queue transport (no broker) ----------------------
                // This publisher has NO event store, so the DB transport itself is its (Main) message
                // store — no role override needed and nothing to reconcile. It still uses the SAME default
                // transport schema (wolverine_queues) as everyone else, so "postgresql://critterwatch"
                // resolves to the one shared control queue table the console drains.
                opts.UsePostgresqlPersistenceAndTransport(SampleConnections.Postgres())
                    .AutoProvision();

                opts.Policies.UseDurableInboxOnAllListeners();

                // ---- Cross-app routing (explicit — Postgres transport has no conventional routing) -------
                // Every Trip command the publisher synthesizes is routed to TripService's "trip_commands"
                // queue. The projection on TripService replies ContinueTrip to "trip_callbacks", which this
                // publisher listens on (its ContinueTripHandler dequeues the next scripted command).
                opts.PublishMessage<StartTrip>().ToPostgresqlQueue("trip_commands");
                opts.PublishMessage<Depart>().ToPostgresqlQueue("trip_commands");
                opts.PublishMessage<Arrive>().ToPostgresqlQueue("trip_commands");
                opts.PublishMessage<RecordTravel>().ToPostgresqlQueue("trip_commands");
                opts.PublishMessage<RecordBreakdown>().ToPostgresqlQueue("trip_commands");
                opts.PublishMessage<MarkVacationOver>().ToPostgresqlQueue("trip_commands");
                opts.PublishMessage<EndTrip>().ToPostgresqlQueue("trip_commands");
                opts.PublishMessage<AbortTrip>().ToPostgresqlQueue("trip_commands");
                opts.ListenToPostgresqlQueue("trip_callbacks");

                // Report to CritterWatch over the shared control queue; operator commands come back to
                // this publisher's own control queue.
                opts.AddCritterWatchMonitoring(
                    "postgresql://critterwatch".ToUri(),
                    "postgresql://trip_publisher_control".ToUri());
            })
            .ConfigureServices(services =>
            {
                // The hosted service kicks off the first burst; ContinueTrip drives the rest.
                services.AddHostedService<KickOffPublishing>();
                services.AddResourceSetupOnStartup();
            });
    }
}
