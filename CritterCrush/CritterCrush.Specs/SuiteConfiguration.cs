using Bobcat;
using Bobcat.Alba;
using Bobcat.CritterStack;
using Bobcat.Runtime;
using JasperFx.Events.EventModeling;

// The declared model, the host, and the specs all merge by this one name.
[assembly: EventModelName("CritterCrush")]

namespace CritterCrush.Specs;

/// <summary>
/// There is no Main here on purpose (bobcat #207): Bobcat.Generators emits the entry point, and
/// this is where a hand-written Main's configure lambda would have gone.
/// </summary>
public static class SuiteConfiguration
{
    [BobcatConfiguration]
    public static void Configure(BobcatRunner runner)
    {
        // One schema per working copy. Every spec run resets the event store, so several agents
        // building different slices in their own git worktrees against one Postgres would wipe
        // each other's data mid-run — not a race that shows up as a race, but as another slice's
        // scenarios failing for no visible reason. The directory name is the only thing that
        // reliably differs between worktrees, so it names the schema.
        Environment.SetEnvironmentVariable("Marten__SchemaName", schemaName());

        runner.Suite.AddResource(new AlbaResource<Program>(reset: host => host.ResetEventStoresAsync()));
    }

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
        return ("crittercrush_" + cleaned).Length > 63 ? ("crittercrush_" + cleaned)[..63] : "crittercrush_" + cleaned;
    }
}
