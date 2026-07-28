namespace MoreSpeakers.Tests;

/// <summary>
/// Every test class here shares ONE Postgres database and wipes it in its InitializeAsync
/// (<c>Advanced.Clean.DeleteAllDocumentsAsync()</c>), which runs before each test. xUnit
/// parallelizes across test classes by default, so without this collection two classes
/// interleave and one deletes the documents the other just seeded — producing a flaky
/// "expected 200 but was 404" in whichever class loses the race.
///
/// Putting every class in a single collection serializes them. Tests within one class already
/// run sequentially, so this is the only synchronization needed.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DatabaseCollection
{
    public const string Name = "MoreSpeakers database";
}
