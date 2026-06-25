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
using Wolverine.RabbitMQ;

return await IncidentServiceProgram.CreateHostBuilder(args).RunJasperFxCommands(args);

/// <summary>
/// Bootstraps the Incidents service — RabbitMQ + Marten + CritterWatch monitoring, mirroring
/// <c>TripServiceProgram</c>. Stays in a dedicated <c>incidents</c> schema so its rows don't collide with
/// the Trip-side Marten schemas.
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

        opts.UseRabbitMq(SampleConnections.RabbitMq())
            .DisableDeadLetterQueueing()
            .UseConventionalRouting()
            .AutoProvision();

        opts.Policies.UseDurableInboxOnAllListeners();
        opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
        opts.Policies.UseDurableLocalQueues();
        opts.Policies.AutoApplyTransactions();

        // ContinueIncident is published by this service and handled by the publisher — declare it.
        opts.Discovery.IncludeType<ContinueIncident>();

        opts.Services.AddMarten(m =>
            {
                m.Connection(SampleConnections.Postgres());
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

        opts.ListenToRabbitQueue("incident_service");

        opts.AddCritterWatchMonitoring(
            "rabbitmq://queue/critterwatch".ToUri(),
            "rabbitmq://queue/incident_service".ToUri()).EnableEventStoreExplorer = true;

        // Continuously schedule future-dated reminders so the Scheduled Messages page has live rows.
        opts.Services.AddHostedService<ReminderScheduler>();

        opts.Services.AddResourceSetupOnStartup();
    }
}
