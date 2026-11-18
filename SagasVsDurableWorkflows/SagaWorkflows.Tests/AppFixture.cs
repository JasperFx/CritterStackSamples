using Alba;
using JasperFx.CommandLine;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Tracking;

[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace SagaWorkflows.Tests;

public class AppFixture : IAsyncLifetime
{
    public IAlbaHost Host { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        JasperFxEnvironment.AutoStartHost = true;
        Host = await AlbaHost.For<Program>();
        await Host.CleanAllMartenDataAsync();
    }

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();
}

[CollectionDefinition(nameof(AppCollection))]
public class AppCollection : ICollectionFixture<AppFixture>;

[Collection(nameof(AppCollection))]
public abstract class WorkflowContext(AppFixture fixture)
{
    protected IAlbaHost Host => fixture.Host;

    protected T Service<T>() where T : notnull => Host.Services.GetRequiredService<T>();

    protected async Task<T?> LoadSaga<T>(object id) where T : notnull
    {
        await using var session = Host.DocumentStore().QuerySession();
        return id switch
        {
            Guid guid => await session.LoadAsync<T>(guid),
            string text => await session.LoadAsync<T>(text),
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };
    }

    /// <summary>Send a message, and wait until everything it set off is done, failures included</summary>
    protected Task<ITrackedSession> Run(object message)
        => Host.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(30))
            .DoNotAssertOnExceptionsDetected()
            .SendMessageAndWaitAsync(message);
}
