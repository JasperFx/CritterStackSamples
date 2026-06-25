using Fleet.Common;
using Incidents.Domain;
using Incidents.Service;
using JasperFx;
using JasperFx.Core;
using JasperFx.Events;
using JasperFx.Events.Projections;
using JasperFx.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polecat;
using Wolverine;
using Wolverine.CritterWatch;
using Wolverine.Persistence.Durability;
using Wolverine.Polecat;
using Wolverine.SqlServer;

return await IncidentServiceProgram.CreateHostBuilder(args).RunJasperFxCommands(args);

/// <summary>
/// Bootstraps the Incidents service — SQL Server DB-queue transport + Polecat + CritterWatch monitoring,
/// mirroring <c>TripServiceProgram</c>. Stays in a dedicated <c>incidents</c> Polecat schema so its rows
/// don't collide with the Trip-side schemas (only the Wolverine transport schema is shared fleet-wide).
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

        var sqlServer = SampleConnections.SqlServer();

        // Shared SQL Server DB-queue transport (no broker). role: Ancillary keeps the Polecat event store
        // as Main. NO per-service transportSchema — the "critterwatch" control queue lives in the ONE
        // shared transport schema (see the TripService schema-coupling note).
        opts.UseSqlServerPersistenceAndTransport(sqlServer, role: MessageStoreRole.Ancillary)
            .AutoProvision();

        opts.Policies.UseDurableInboxOnAllListeners();
        opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
        opts.Policies.UseDurableLocalQueues();
        opts.Policies.AutoApplyTransactions();

        // ContinueIncident is published by this service and handled by the publisher — declare it.
        opts.Discovery.IncludeType<ContinueIncident>();

        opts.Services.AddPolecat(m =>
            {
                m.ConnectionString = sqlServer;
                m.DatabaseSchemaName = "incidents";

                // Snapshot the Incident aggregate inline.
                m.Projections.Snapshot<Incident>(SnapshotLifecycle.Inline);

                // Headline async projection — the rebuild/pause target shown in CritterWatch.
                m.Projections.Add<IncidentsByCategoryProjection>(ProjectionLifecycle.Async);
            })
            .IntegrateWithWolverine(o =>
            {
                o.UseWolverineManagedEventSubscriptionDistribution = true;
            })
            // polecat#187 — provision the Polecat schema explicitly (see TripService note).
            .ApplyAllDatabaseChangesOnStartup();

        // Domain routing (SQL Server transport has no conventional routing): the service LISTENS on
        // "incident_commands" for the publisher's commands, and SENDS ContinueIncident back to the
        // publisher's "incident_publisher" queue.
        opts.ListenToSqlServerQueue("incident_commands");
        opts.PublishMessage<ContinueIncident>().ToSqlServerQueue("incident_publisher");

        // The service's own inbound control queue for CritterWatch operator commands.
        opts.ListenToSqlServerQueue("incident_service");

        opts.AddCritterWatchMonitoring(
            "sqlserver://critterwatch".ToUri(),
            "sqlserver://incident_service".ToUri()).EnableEventStoreExplorer = true;

        // Continuously schedule future-dated reminders so the Scheduled Messages page has live rows.
        opts.Services.AddHostedService<ReminderScheduler>();

        opts.Services.AddResourceSetupOnStartup();
    }
}
