using Fleet.Common;
using JasperFx;
using JasperFx.Core;
using JasperFx.Resources;
using Microsoft.Extensions.Hosting;
using Polecat;
using TripMessages;
using Wolverine;
using Wolverine.CritterWatch;
using Wolverine.ErrorHandling;
using Wolverine.Persistence.Durability;
using Wolverine.Polecat;
using Wolverine.Runtime.Handlers;
using Wolverine.SqlServer;

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

                var sqlServer = SampleConnections.SqlServer();

                // Same shared SQL Server DB-queue transport as the rest of the fleet. role: Ancillary keeps
                // the Polecat event store as Main. transportSchema: "critterwatch_wolverine" — the "critterwatch"
                // control queue must live in the ONE shared transport schema, the console's (see TripService note).
                opts.UseSqlServerPersistenceAndTransport(sqlServer, transportSchema: "critterwatch_wolverine", role: MessageStoreRole.Ancillary)
                    .AutoProvision();

                opts.Services.AddPolecat(m =>
                    {
                        m.ConnectionString = sqlServer;
                        // RepairShop's OWN event-store schema, distinct from the other services.
                        m.DatabaseSchemaName = "repair_shop";

                        m.Schema.For<RepairWork>();
                    })
                    .IntegrateWithWolverine()
                    // polecat#187 — provision the Polecat schema explicitly (see TripService note).
                    .ApplyAllDatabaseChangesOnStartup();

                opts.Policies.UseDurableInboxOnAllListeners();
                opts.Policies.UseDurableOutboxOnAllSendingEndpoints();

                // Repairs for a given state run on a sequential local queue — demonstrates Wolverine's
                // local-queue routing (RepairRequestedHandler routes by state) in the CritterWatch view.
                opts.Policies.AllLocalQueues(listener => listener.Sequential());

                // Domain routing (SQL Server transport has no conventional routing): RepairShop LISTENS on
                // "repair_shop_work" for RepairRequested from TripService, and SENDS RepairsCompleted back
                // to TripService's "trip_commands" queue (handled there as a Trip aggregate command).
                opts.ListenToSqlServerQueue("repair_shop_work");
                opts.PublishMessage<RepairsCompleted>().ToSqlServerQueue("trip_commands");

                // The service's own inbound control queue for CritterWatch operator commands.
                opts.ListenToSqlServerQueue("repair_shop");

                opts.Services.AddResourceSetupOnStartup();

                opts.AddCritterWatchMonitoring(
                    "sqlserver://critterwatch".ToUri(),
                    "sqlserver://repair_shop".ToUri()).EnableEventStoreExplorer = true;
            });
    }
}

/// <summary>Polecat document tracking a repair job. Minimal — RepairShop's value here is the message flow.</summary>
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
