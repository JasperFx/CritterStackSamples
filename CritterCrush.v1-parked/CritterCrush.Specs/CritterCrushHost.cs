using Alba;
using Wolverine;

namespace CritterCrush.Specs;

/// <summary>
/// The application under test, booted once for the run through Alba and shared by every spec. This is
/// what <c>SpecsRunner</c> registered as an <c>AlbaResource&lt;Program&gt;</c> on the Bobcat runner,
/// which Bobcat 1.0 no longer has.
/// </summary>
public class CritterCrushHost : IAsyncLifetime
{
    public IAlbaHost Host { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Host = await AlbaHost.For<Program>(x => x.ConfigureServices(services =>
            // Under xUnit v3 the test project is the entry assembly, so tell Wolverine where the handlers are
            services.ConfigureWolverine(opts => opts.ApplicationAssembly = typeof(Program).Assembly)));
    }

    public async ValueTask DisposeAsync()
    {
        if (Host is not null) await Host.DisposeAsync();
    }
}
