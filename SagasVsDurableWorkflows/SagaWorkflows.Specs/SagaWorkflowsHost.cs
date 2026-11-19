using Alba;
using JasperFx.CommandLine;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using SagaWorkflows.MoneyTransfer;
using SagaWorkflows.TripBooking;
using Wolverine;
using Wolverine.Runtime;

namespace SagaWorkflows.Specs;

/// <summary>
/// The SagaWorkflows application, started once for every spec that uses it. The money transfer and the trip come in
/// two styles on the same messages, so there are two hosts: one with the document sagas, one with the event-sourced
/// sagas, each in a schema of its own.
/// </summary>
public abstract class SagaWorkflowsHost : IAsyncLifetime
{
    public const string ServiceName = "saga-workflows";

    public IAlbaHost Host { get; private set; } = null!;

    protected abstract bool EventSourced { get; }

    public async ValueTask InitializeAsync()
    {
        JasperFxEnvironment.AutoStartHost = true;
        Host = await AlbaHost.For<Program>(x =>
        {
            x.UseSetting("Sagas:EventSourced", EventSourced.ToString());
            x.UseSetting("Marten:Schema", EventSourced ? "saga_specs_es" : "saga_specs");

            // One node owns every listener, the fan-in shards included, from the moment it starts
            x.ConfigureServices(services => services.RunWolverineInSoloMode());
        });
    }

    /// <summary>Every spec starts from empty stores, no dead letters, and outside systems with nothing arranged</summary>
    public async Task ResetAsync()
    {
        await Host.ResetAllMartenDataAsync();
        await Host.Services.GetRequiredService<IWolverineRuntime>().Storage.Admin.ClearAllAsync();

        Host.Services.GetRequiredService<Bank>().Reset();
        Host.Services.GetRequiredService<TravelProviders>().Reset();
        Host.Services.GetRequiredService<SagaWorkflows.AgentLoop.ToolBox>().Broken.Clear();
        Host.Services.GetRequiredService<SagaWorkflows.ChildWorkflows.DevicePortal>().BrokenDevices.Clear();
        Host.Services.GetRequiredService<SagaWorkflows.FanOutFanIn.BackupStorage>().Unreadable.Clear();
        Host.Services.GetRequiredService<SagaWorkflows.Polling.FakeReportingApi>().Down.Clear();
    }

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();
}

public class DocumentSagas : SagaWorkflowsHost
{
    protected override bool EventSourced => false;
}

public class EventSourcedSagas : SagaWorkflowsHost
{
    protected override bool EventSourced => true;
}

[CollectionDefinition(nameof(DocumentSagaCollection))]
public class DocumentSagaCollection : ICollectionFixture<DocumentSagas>;

[CollectionDefinition(nameof(EventSourcedSagaCollection))]
public class EventSourcedSagaCollection : ICollectionFixture<EventSourcedSagas>;
