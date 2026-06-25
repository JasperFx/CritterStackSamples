using Xunit;

namespace CritterWatch.Samples.Testing;

/// <summary>
/// xUnit trait category name for tests that boot a real Aspire AppHost and therefore require a running
/// Docker daemon. Filter them out where Docker is unavailable with
/// <c>dotnet test --filter "Category!=DockerRequired"</c> (and ensure Docker-in-CI where they should run).
/// </summary>
public static class DockerRequired
{
    /// <summary>The trait <c>Category</c> value: <c>DockerRequired</c>.</summary>
    public const string Category = "DockerRequired";
}

/// <summary>
/// Shared xUnit collection for the Docker-requiring sample batteries. Applying
/// <c>[Collection(DockerRequiredCollection.Name)]</c> to a test class both serializes the
/// container-heavy tests (so multiple sample AppHosts don't fight over Docker/ports at once) and lets a
/// single <see cref="CritterWatchAppHostFixture{TAppHost}"/> be shared across the class's tests.
/// </summary>
/// <remarks>
/// Each sample solution defines its own concrete collection by deriving an
/// <c>ICollectionFixture&lt;CritterWatchAppHostFixture&lt;Projects.MyAppHost&gt;&gt;</c> marker — the
/// generic fixture can't name a concrete <c>Projects.*</c> type here. This base only carries the
/// <c>Category=DockerRequired</c> trait and a stable collection name to standardize on.
/// </remarks>
[Trait("Category", DockerRequired.Category)]
[CollectionDefinition(Name)]
public sealed class DockerRequiredCollection
{
    /// <summary>The collection name to pass to <c>[Collection(...)]</c>.</summary>
    public const string Name = "CritterWatch Docker AppHost";
}
