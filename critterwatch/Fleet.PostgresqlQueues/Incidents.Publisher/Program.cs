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
using Wolverine.Postgresql;

return await IncidentPublisherProgram.CreateHostBuilder(args).RunJasperFxCommands(args);

/// <summary>
/// Console publisher that drives synthetic incident traffic into IncidentService over the Wolverine
/// PostgreSQL queue transport. Mirrors <c>TripPublisherProgram</c>: Solo durability, an
/// <c>IHostedService</c> that walks ~50 in-flight <see cref="IncidentStream"/>s, and explicit routing
/// (the Postgres transport has no conventional routing).
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

                // Wolverine PostgreSQL DB-backed queue transport (no broker). No event store here, so the
                // DB transport is this publisher's (Main) message store — nothing to reconcile. Shared
                // default transport schema (wolverine_queues) so postgresql://critterwatch is the one queue.
                opts.UsePostgresqlPersistenceAndTransport(SampleConnections.Postgres())
                    .AutoProvision();

                opts.Policies.UseDurableInboxOnAllListeners();

                // ---- Cross-app routing (explicit — Postgres transport has no conventional routing) -------
                // Route every incident command to IncidentService's "incident_commands" queue; listen on
                // "incident_callbacks" for the ContinueIncident ping-pong that drives each stream forward.
                opts.PublishMessage<LogIncident>().ToPostgresqlQueue("incident_commands");
                opts.PublishMessage<CategoriseIncident>().ToPostgresqlQueue("incident_commands");
                opts.PublishMessage<PrioritiseIncident>().ToPostgresqlQueue("incident_commands");
                opts.PublishMessage<AssignAgentToIncident>().ToPostgresqlQueue("incident_commands");
                opts.PublishMessage<RecordAgentResponseToIncident>().ToPostgresqlQueue("incident_commands");
                opts.PublishMessage<RecordCustomerResponseToIncident>().ToPostgresqlQueue("incident_commands");
                opts.PublishMessage<ResolveIncident>().ToPostgresqlQueue("incident_commands");
                opts.PublishMessage<AcknowledgeResolution>().ToPostgresqlQueue("incident_commands");
                opts.PublishMessage<CloseIncident>().ToPostgresqlQueue("incident_commands");
                opts.PublishMessage<ArchiveIncident>().ToPostgresqlQueue("incident_commands");
                opts.ListenToPostgresqlQueue("incident_callbacks");

                opts.AddCritterWatchMonitoring(
                    "postgresql://critterwatch".ToUri(),
                    "postgresql://incident_publisher_control".ToUri());
            })
            .ConfigureServices(services =>
            {
                services.AddHostedService<KickOffPublishing>();
                services.AddResourceSetupOnStartup();
            });
    }
}
