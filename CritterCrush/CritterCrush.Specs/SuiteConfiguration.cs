using Bobcat;
using Bobcat.Alba;
using Bobcat.CritterStack;
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
        => runner.Suite.AddResource(new AlbaResource<Program>(reset: host => host.ResetEventStoresAsync()));
}
