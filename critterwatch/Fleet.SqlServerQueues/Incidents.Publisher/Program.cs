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
using Wolverine.Persistence.Durability;
using Wolverine.SqlServer;

return await IncidentPublisherProgram.CreateHostBuilder(args).RunJasperFxCommands(args);

/// <summary>
/// Console publisher that drives synthetic incident traffic into IncidentService over the SQL Server queue
/// transport. Mirrors <c>TripPublisherProgram</c>: Solo durability, an <c>IHostedService</c> that walks
/// ~50 in-flight <see cref="IncidentStream"/>s, explicit routing to the service's command queue.
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

                var sqlServer = SampleConnections.SqlServer();

                opts.UseSqlServerPersistenceAndTransport(sqlServer, role: MessageStoreRole.Ancillary)
                    .AutoProvision();

                // Explicit routing for every incident command type → IncidentService's command queue.
                opts.PublishMessage<LogIncident>().ToSqlServerQueue("incident_commands");
                opts.PublishMessage<CategoriseIncident>().ToSqlServerQueue("incident_commands");
                opts.PublishMessage<PrioritiseIncident>().ToSqlServerQueue("incident_commands");
                opts.PublishMessage<AssignAgentToIncident>().ToSqlServerQueue("incident_commands");
                opts.PublishMessage<RecordAgentResponseToIncident>().ToSqlServerQueue("incident_commands");
                opts.PublishMessage<RecordCustomerResponseToIncident>().ToSqlServerQueue("incident_commands");
                opts.PublishMessage<ResolveIncident>().ToSqlServerQueue("incident_commands");
                opts.PublishMessage<AcknowledgeResolution>().ToSqlServerQueue("incident_commands");
                opts.PublishMessage<CloseIncident>().ToSqlServerQueue("incident_commands");
                opts.PublishMessage<ArchiveIncident>().ToSqlServerQueue("incident_commands");

                // The publisher's own inbound queue — it RECEIVES ContinueIncident back from the service.
                opts.ListenToSqlServerQueue("incident_publisher");

                opts.Policies.UseDurableInboxOnAllListeners();

                opts.AddCritterWatchMonitoring(
                    "sqlserver://critterwatch".ToUri(),
                    "sqlserver://incident_publisher".ToUri());
            })
            .ConfigureServices(services =>
            {
                services.AddHostedService<KickOffPublishing>();
                services.AddResourceSetupOnStartup();
            });
    }
}
