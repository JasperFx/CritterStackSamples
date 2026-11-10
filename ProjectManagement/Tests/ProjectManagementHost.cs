using Alba;
using JasperFx.CommandLine;
using Wolverine;

namespace Tests;

/// <summary>
/// The application under test, booted once for the run through Alba and shared by every
/// Bobcat specification.
/// </summary>
public class ProjectManagementHost : IAsyncLifetime
{
    public IAlbaHost Host { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        // Necessary when the application ends in RunJasperFxCommands and is booted through
        // WebApplicationFactory, which Alba uses
        JasperFxEnvironment.AutoStartHost = true;

        Host = await AlbaHost.For<Program>(x => x.ConfigureServices(services =>
        {
            services.DisableAllExternalWolverineTransports();

            // Under xUnit v3 the test project is the entry assembly, so tell Wolverine where the handlers are
            services.ConfigureWolverine(opts => opts.ApplicationAssembly = typeof(Program).Assembly);
        }));
    }

    public async ValueTask DisposeAsync()
    {
        if (Host is not null) await Host.DisposeAsync();
    }
}
