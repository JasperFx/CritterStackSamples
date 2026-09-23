using Alba;
using Xunit;

namespace CritterCrush.Specs;

/// <summary>
/// The one application host every spec class shares.
/// </summary>
/// <remarks>
/// <para>
/// This is what <c>SuiteConfiguration.Configure</c> used to register on the Bobcat runner. Lane A
/// hands the entry point to xUnit (<c>BobcatGenerateEntryPoint=false</c>), so nothing calls that
/// method any more — BOBCAT017 says so at build time — and the host, the schema name and the reset
/// all have to live somewhere xUnit will run them. This is that somewhere.
/// </para>
/// <para>
/// Booted once per run rather than per class: starting Wolverine, Marten and the async daemon costs
/// seconds, and every spec wants the same application. Isolation comes from the per-test reset in
/// <see cref="CritterCrushSpec.InitializeAsync"/>, not from a fresh host — which also means the
/// spec classes must not run in parallel with each other, hence the collection.
/// </para>
/// </remarks>
public sealed class CritterCrushHost : IAsyncLifetime
{
    public const string CollectionName = "crittercrush";

    public IAlbaHost Host { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        // One schema per working copy. Every run resets the event store, so several agents building
        // different slices in their own git worktrees against one Postgres would wipe each other's
        // data mid-run — not a race that looks like a race, but as another slice's specs failing
        // for no visible reason. The directory name is the only thing that reliably differs between
        // worktrees, so it names the schema. Carried over from SuiteConfiguration.cs.
        Environment.SetEnvironmentVariable("Marten__SchemaName", schemaName());

        Host = await AlbaHost.For<Program>();
    }

    public async ValueTask DisposeAsync() => await Host.DisposeAsync();

    private static string schemaName()
    {
        var root = Directory.GetCurrentDirectory();
        while (root is not null && !Directory.Exists(Path.Combine(root, ".git")) &&
               !File.Exists(Path.Combine(root, ".git")))
        {
            root = Path.GetDirectoryName(root);
        }

        var name = Path.GetFileName(root ?? Directory.GetCurrentDirectory());
        var cleaned = new string(name.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray());
        var schema = "crittercrush_" + cleaned;
        return schema.Length > 63 ? schema[..63] : schema;
    }
}

/// <summary>
/// Puts every spec class in one collection, so they share the host above and run one at a time.
/// </summary>
/// <remarks>
/// Serial is not a default worth fighting here: the specs reset the event store between tests, and
/// two classes resetting concurrently would each wipe the other's arranged history. The Gherkin
/// lane got this from the Bobcat runner; under xUnit it is the collection that provides it.
/// </remarks>
[CollectionDefinition(CritterCrushHost.CollectionName)]
public sealed class CritterCrushCollection : ICollectionFixture<CritterCrushHost>;
