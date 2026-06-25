using Fleet.Common;
using JasperFx;
using JasperFx.Core;
using JasperFx.Resources;
using Marten;
using Microsoft.Extensions.Hosting;
using TripMessages;
using Wolverine;
using Wolverine.AzureServiceBus;
using Wolverine.CritterWatch;
using Wolverine.ErrorHandling;
using Wolverine.Marten;
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

                // ---- Azure Service Bus transport (transport swap vs. the RabbitMQ flagship) -----------
                opts.UseAzureServiceBus(SampleConnections.AzureServiceBus());

                // #356 emulator constraint — drop the per-node system queues to stay under the 50-entity
                // cap. Entities are emulator-owned (declared in the AppHost), so no ManagementConnectionString
                // and no AutoProvision (see TripService for the full rationale).
                var asb = opts.Transports.GetOrCreate<AzureServiceBusTransport>();
                asb.SystemQueuesEnabled = false;

                // #356 emulator constraint — EXPLICIT routing. The shop receives repair requests on its app
                // inbox and returns the completion to TripService's app inbox. ConductRepairs stays on a
                // local (in-process) queue — no ASB entity. Each ASB queue maps 1:1 to an AppHost
                // AddServiceBusQueue.
                opts.ListenToAzureServiceBusQueue("repair_shop_app");
                opts.PublishMessage<RepairsCompleted>().ToAzureServiceBusQueue("trip_service_app");

                opts.Services.AddMarten(m =>
                    {
                        m.Connection(SampleConnections.Postgres());
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

                // #345: must be the `asb://queue/{name}` form (see TripService for the why).
                opts.AddCritterWatchMonitoring(
                    "asb://queue/critterwatch".ToUri(),
                    "asb://queue/repair_shop".ToUri()).EnableEventStoreExplorer = true;
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
