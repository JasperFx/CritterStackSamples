using Bobcat.Alba;
using Bobcat.CritterStack;
using Bobcat.Runtime;

namespace CritterCrush.Specs;

/// <summary>
/// Bobcat spec-runner entry point. An explicit Main class rather than top-level statements —
/// the referenced host synthesizes its own Program from top-level statements, and two of those
/// in one assembly make AlbaResource&lt;Program&gt; bind to the wrong one.
/// </summary>
public static class SpecsRunner
{
    public static Task<int> Main(string[] args)
        => BobcatRunner.Run(args, runner =>
        {
            // ResetEventStoresAsync empties documents AND event streams between runs — the
            // snapshots are documents, but the swipes that produced them are events, and
            // deleting one half leaves the other.
            runner.Suite.AddResource(new AlbaResource<Program>(reset: host => host.ResetEventStoresAsync()));
            runner.ScanForFeatures(typeof(SpecsRunner).Assembly);
        });
}
