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
using Wolverine.Persistence.Durability;
using Wolverine.SqlServer;

return await TripPublisherProgram.CreateHostBuilder(args).RunJasperFxCommands(args);

public static class TripPublisherProgram
{
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseWolverine(opts =>
            {
                opts.ServiceName = "TripPublisher";

                // A pure driver — Solo durability is fine, it owns no projections/agents/event store.
                opts.Durability.Mode = DurabilityMode.Solo;
                opts.ApplicationAssembly = typeof(TripPublisherProgram).Assembly;
                opts.EnableAutomaticFailureAcks = false;

                opts.Services.AddSingleton<Publisher>();

                var sqlServer = SampleConnections.SqlServer();

                // Shared SQL Server DB-queue transport (no broker). role: Ancillary — this driver still
                // needs a Wolverine durability store for the inbox; it isn't the Main of anything.
                opts.UseSqlServerPersistenceAndTransport(sqlServer, role: MessageStoreRole.Ancillary)
                    .AutoProvision();

                // The SQL Server transport has no convention-based routing (unlike RabbitMQ), so route the
                // trip COMMANDS explicitly to TripService's work queue. Every trip command type goes to the
                // one "trip_commands" SQL Server queue that TripService listens on.
                opts.PublishMessage<StartTrip>().ToSqlServerQueue("trip_commands");
                opts.PublishMessage<Depart>().ToSqlServerQueue("trip_commands");
                opts.PublishMessage<Arrive>().ToSqlServerQueue("trip_commands");
                opts.PublishMessage<RecordTravel>().ToSqlServerQueue("trip_commands");
                opts.PublishMessage<RecordBreakdown>().ToSqlServerQueue("trip_commands");
                opts.PublishMessage<EndTrip>().ToSqlServerQueue("trip_commands");
                opts.PublishMessage<AbortTrip>().ToSqlServerQueue("trip_commands");
                opts.PublishMessage<MarkVacationOver>().ToSqlServerQueue("trip_commands");

                // The publisher's own inbound queue — it RECEIVES ContinueTrip back from TripService here.
                opts.ListenToSqlServerQueue("trip_publisher");

                opts.Policies.UseDurableInboxOnAllListeners();

                // Report to CritterWatch over the shared control queue; callbacks come back to trip_publisher.
                opts.AddCritterWatchMonitoring(
                    "sqlserver://critterwatch".ToUri(),
                    "sqlserver://trip_publisher".ToUri());
            })
            .ConfigureServices(services =>
            {
                // The hosted service kicks off the first burst; ContinueTrip drives the rest.
                services.AddHostedService<KickOffPublishing>();
                services.AddResourceSetupOnStartup();
            });
    }
}
