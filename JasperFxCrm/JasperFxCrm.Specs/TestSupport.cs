using Alba;
using JasperFxCrm;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Bobcat.Http;

namespace JasperFxCrm.Specs;

#region sample_spec_fixture
/// <summary>
/// One application host for the whole spec run: the real Program, the real Marten and
/// Wolverine configuration, a spec-only schema, no external transports.
/// </summary>
public sealed class CrmFixture : IAsyncLifetime
{
    public IAlbaHost Host { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Host = await AlbaHost.For<Program>(x =>
        {
            x.UseSetting("Marten:SchemaName", "crm_specs");
            x.ConfigureServices(services =>
            {
                services.DisableAllExternalWolverineTransports();
                services.RunWolverineInSoloMode();
                services.MartenDaemonModeIsSolo();
                services.ConfigureWolverine(opts => opts.ApplicationAssembly = typeof(JasperFxCrmHost).Assembly);
            });
        });
    }

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class CrmCollection : ICollectionFixture<CrmFixture>
{
    public const string Name = "JasperFxCrm";
}

/// <summary>
/// Every spec: the Wolverine.HTTP vocabulary over the shared host, with the event store
/// reset before each one so whole-store assertions stay honest.
/// </summary>
[Collection(CrmCollection.Name)]
public abstract class CrmSpec(CrmFixture app) : WolverineHttpSpec(app.Host), IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await ResetAsync();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
#endregion
