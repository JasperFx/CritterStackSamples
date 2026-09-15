using Alba;
using Marten;
using Microsoft.AspNetCore.Hosting;
using Testcontainers.PostgreSql;

namespace Tests;

/// <summary>
/// PostgreSQL is small enough to spin up per test run. Testcontainers starts a throwaway
/// postgres:17 container, Marten builds its own schema in it, and the container is gone
/// when the run finishes. No shared database, no cleanup scripts, no port collisions.
/// </summary>
public class AppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    public IAlbaHost Host { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Host = await AlbaHost.For<Program>(x =>
        {
            x.UseSetting("ConnectionStrings:Marten", _postgres.GetConnectionString());
        });
    }

    public async Task DisposeAsync()
    {
        await Host.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(nameof(AppCollection))]
public class AppCollection : ICollectionFixture<AppFixture>;
