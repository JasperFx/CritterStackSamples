using Fleet.Common;
using Incidents.Domain;
using Incidents.Service;
using JasperFx;
using JasperFx.Core;
using JasperFx.Events.Projections;
using JasperFx.Resources;
using Marten;
using Marten.Events.Projections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.CritterWatch;
using Wolverine.Marten;
using Wolverine.Persistence.Durability;
using Wolverine.Postgresql;

return await IncidentServiceProgram.CreateHostBuilder(args).RunJasperFxCommands(args);

/// <summary>
/// Bootstraps the Incidents service — Wolverine PostgreSQL queue transport + Marten + CritterWatch
/// monitoring, mirroring <c>TripServiceProgram</c>. Stays in a dedicated <c>incidents</c> Marten schema so
/// its event data doesn't collide with the Trip-side schemas; the transport/queue tables live in the
/// shared default <c>wolverine_queues</c> schema like every other participant.
/// </summary>
public static class IncidentServiceProgram
{
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseWolverine(ConfigureWolverine);
    }

    private static void ConfigureWolverine(WolverineOptions opts)
    {
        opts.ServiceName = "IncidentService";
        opts.ApplicationAssembly = typeof(IncidentServiceProgram).Assembly;

        // Async projection needs leader/agent distribution → Balanced (see TripService note).
        opts.Durability.Mode = DurabilityMode.Balanced;
        opts.EnableAutomaticFailureAcks = false;

        var connectionString = SampleConnections.Postgres();

        // Wolverine PostgreSQL DB-backed queue transport (no broker). Ancillary so the Marten event store
        // stays Main; default transport schema (wolverine_queues) shared with the whole fleet, so
        // "postgresql://critterwatch" resolves to the one console control queue.
        opts.UsePostgresqlPersistenceAndTransport(connectionString, role: MessageStoreRole.Ancillary)
            .AutoProvision();

        opts.Policies.UseDurableInboxOnAllListeners();
        opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
        opts.Policies.UseDurableLocalQueues();
        opts.Policies.AutoApplyTransactions();

        // ContinueIncident is published by this service and handled by the publisher — declare it.
        opts.Discovery.IncludeType<ContinueIncident>();

        opts.Services.AddMarten(m =>
            {
                m.Connection(connectionString);
                m.DatabaseSchemaName = "incidents";
                m.DisableNpgsqlLogging = true;

                // Snapshot the Incident aggregate inline.
                m.Projections.Snapshot<Incident>(SnapshotLifecycle.Inline);

                // Headline async projection — the rebuild/pause target shown in CritterWatch.
                m.Projections.Add<IncidentsByCategoryProjection>(ProjectionLifecycle.Async);
            })
            .IntegrateWithWolverine(o =>
            {
                o.UseWolverineManagedEventSubscriptionDistribution = true;
            });

        // ---- Cross-app routing (explicit — Postgres transport has no conventional routing) -----------
        // Receive incident commands from IncidentPublisher on "incident_commands"; reply ContinueIncident
        // back to the publisher's "incident_callbacks" queue.
        opts.ListenToPostgresqlQueue("incident_commands");
        opts.PublishMessage<ContinueIncident>().ToPostgresqlQueue("incident_callbacks");

        // This service's own CritterWatch control queue (distinct from the business queue).
        opts.ListenToPostgresqlQueue("incident_service_control");

        opts.AddCritterWatchMonitoring(
            "postgresql://critterwatch".ToUri(),
            "postgresql://incident_service_control".ToUri()).EnableEventStoreExplorer = true;

        // Continuously schedule future-dated reminders so the Scheduled Messages page has live rows.
        opts.Services.AddHostedService<ReminderScheduler>();

        opts.Services.AddResourceSetupOnStartup();
    }
}
