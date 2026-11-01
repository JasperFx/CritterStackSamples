using Alba;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Tests;

/// <summary>
/// One service of the solution, booted once for the run through Alba. Every service's Program is the
/// same top-level class name, so a host is found by any public type of its assembly instead.
/// </summary>
public abstract class ServiceHost<TEntryPoint>(bool stubExternalTransports) : IAsyncLifetime where TEntryPoint : class
{
    public IAlbaHost Host { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Host = await AlbaHost.For<TEntryPoint>(x => x.ConfigureServices(services =>
        {
            // Under xUnit v3 the test project is the entry assembly, so tell Wolverine where the handlers are
            services.ConfigureWolverine(opts => opts.ApplicationAssembly = typeof(TEntryPoint).Assembly);

            // Basket publishes to RabbitMQ and Ordering listens to it: stub the broker, so a spec sees what
            // was sent without a broker running
            if (stubExternalTransports) services.DisableAllExternalWolverineTransports();
        }));
    }

    public async ValueTask DisposeAsync()
    {
        if (Host is not null) await Host.DisposeAsync();
    }
}

public class CatalogHost() : ServiceHost<Catalog.Product>(false);

public class BasketHost() : ServiceHost<Basket.ShoppingCart>(true);

public class OrderingHost() : ServiceHost<Ordering.Order>(true);
