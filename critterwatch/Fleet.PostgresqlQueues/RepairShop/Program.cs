using Fleet.Common;
using JasperFx;
using JasperFx.Core;
using JasperFx.Resources;
using Marten;
using Microsoft.Extensions.Hosting;
using TripMessages;
using Wolverine;
using Wolverine.CritterWatch;
using Wolverine.ErrorHandling;
using Wolverine.Marten;
using Wolverine.Persistence.Durability;
using Wolverine.Postgresql;
using Wolverine.Runtime.Handlers;

return await RepairShopProgram.CreateHostBuilder(args).RunJasperFxCommands(args);

public static class RepairShopProgram
{
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .UseWolverine(opts =>
            {
                opts.ServiceName = "RepairShop";
                opts.ApplicationAssembly = typeof(RepairShopProgram).Assembly;
                opts.Durability.Mode = DurabilityMode.Solo;
                opts.EnableAutomaticFailureAcks = false;

                var connectionString = SampleConnections.Postgres();

                // Wolverine PostgreSQL DB-backed queue transport (no broker). Ancillary so the Marten store
                // below stays Main; default transport schema (wolverine_queues) shared fleet-wide.
                opts.UsePostgresqlPersistenceAndTransport(connectionString, role: MessageStoreRole.Ancillary)
                    .AutoProvision();

                opts.Services.AddMarten(m =>
                    {
                        m.Connection(connectionString);
                        m.DatabaseSchemaName = "repair_shop";
                        m.DisableNpgsqlLogging = true;

                        m.Schema.For<RepairWork>();
                    })
                    .IntegrateWithWolverine();

                opts.Policies.UseDurableInboxOnAllListeners();
                opts.Policies.UseDurableOutboxOnAllSendingEndpoints();

                // Repairs for a given state run on a sequential local queue — demonstrates Wolverine's
                // local-queue routing (RepairRequestedHandler routes by state) in the CritterWatch view.
                opts.Policies.AllLocalQueues(listener => listener.Sequential());

                opts.Services.AddResourceSetupOnStartup();

                // ---- Cross-app routing (explicit — Postgres transport has no conventional routing) -------
                // Receive RepairRequested from TripService on "repair_commands"; reply RepairsCompleted
                // back to TripService's "trip_commands" queue (handled there by TripMessageHandler).
                opts.ListenToPostgresqlQueue("repair_commands");
                opts.PublishMessage<RepairsCompleted>().ToPostgresqlQueue("trip_commands");

                // This service's own CritterWatch control queue (distinct from the business queue).
                opts.ListenToPostgresqlQueue("repair_shop_control");

                opts.AddCritterWatchMonitoring(
                    "postgresql://critterwatch".ToUri(),
                    "postgresql://repair_shop_control".ToUri()).EnableEventStoreExplorer = true;
            });
    }
}

/// <summary>Marten document tracking a repair job. Minimal — RepairShop's value here is the message flow.</summary>
public class RepairWork
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public string State { get; set; } = string.Empty;
}

/// <summary>
/// Receives a <see cref="RepairRequested"/> from TripService and dispatches a <see cref="ConductRepairs"/>
/// to a per-state local queue. The occasional <see cref="RepairShopTooBusyException"/> exercises the
/// move-to-error-queue policy, giving CritterWatch some dead-letter activity to display.
/// </summary>
public static class RepairRequestedHandler
{
    public static void Configure(HandlerChain chain) => chain.OnAnyException().MoveToErrorQueue();

    public static void Before(RepairRequested requested)
    {
        if (Random.Shared.NextDouble() < .05)
        {
            throw new RepairShopTooBusyException(requested.State + " is just too busy");
        }
    }

    public static object Handle(RepairRequested requested)
    {
        var localQueue = new Uri($"local://{requested.State.ToLowerInvariant()}");
        return new ConductRepairs(requested.TripId).ToDestination(localQueue);
    }
}

/// <summary>Performs the (simulated) repair work and replies <see cref="RepairsCompleted"/> to TripService.</summary>
public static class ConductRepairsHandler
{
    public static async Task<RepairsCompleted> HandleAsync(ConductRepairs message)
    {
        await Task.Delay(Random.Shared.Next(0, 2000));
        return new RepairsCompleted(message.TripId);
    }
}
