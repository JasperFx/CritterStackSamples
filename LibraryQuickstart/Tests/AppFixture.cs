using Alba;
using Microsoft.AspNetCore.Hosting;
using Testcontainers.PostgreSql;
using Wolverine;

namespace Tests;

/// <summary>
/// PostgreSQL is small enough to spin up per test run. Testcontainers starts a throwaway
/// postgres:17 container, Marten builds its own schema in it, and the container is gone
/// when the run finishes. No shared database, no cleanup scripts, no port collisions.
/// Registered once for the whole assembly (AssemblyAttributes.cs), so the original tests and
/// the Bobcat specifications share the one container and the one host.
/// </summary>
public class AppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    public IAlbaHost Host { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        Host = await AlbaHost.For<Program>(x =>
        {
            x.UseSetting("ConnectionStrings:Marten", _postgres.GetConnectionString());

            // Under xUnit v3 the test project is the entry assembly, so tell Wolverine where the handlers are
            x.ConfigureServices(services =>
                services.ConfigureWolverine(opts => opts.ApplicationAssembly = typeof(Program).Assembly));
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (Host is not null) await Host.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
