using Bobcat;
using Bobcat.CritterStack;
using JasperFx.Events.EventModeling;

// The declared model, the running host, and the specs all merge by this one name — it must
// match opts.ServiceName in Program.cs and the `model:` field of models/CritterCrush.emodel.yaml.
[assembly: EventModelName("CritterCrush")]

namespace CritterCrush.Specs;

// One shell fixture per feature, bound by title. CritterStackFixture ships the whole
// grammar — these classes deliberately add nothing.

[FixtureTitle("DogProfiles")]
public class DogProfilesFixture : CritterStackFixture;

[FixtureTitle("Swiping")]
public class SwipingFixture : CritterStackFixture;
