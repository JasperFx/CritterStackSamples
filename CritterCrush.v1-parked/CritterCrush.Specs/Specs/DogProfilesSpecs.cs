using Bobcat;
using Bobcat.Xunit;
using CritterCrush.Profiles;

namespace CritterCrush.Specs;

/// <summary>
/// Features/DogProfiles.feature, one [BobcatSpec] per scenario under the scenario's own name, so the
/// spec identity (<c>DogProfiles/Creating a profile starts its stream</c>) is the one the model declared.
/// </summary>
[BobcatFeature("DogProfiles")]
public class DogProfilesSpecs(CritterCrushHost app) : CritterCrushSpec(app)
{
    private static readonly Guid Owner = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [BobcatSpec(SliceName = "CreateDogProfile", Domain = "Profiles", TriggeredBy = "the dog owner")]
    public async Task Creating_a_profile_starts_its_stream()
    {
        await WhenPosted(new CreateDogProfileRequest("Biscuit", "Corgi", 24, Owner), "/api/profiles");

        ThenTheResponseIs(201);
        ThenEvents(Specify<DogProfileCreated>().With(x => x.Name, "Biscuit").With(x => x.Breed, "Corgi"));
        await ThenAggregate<DogProfile>(TheStartedStream<DogProfile>(),
            Specify<DogProfile>().With(x => x.Name, "Biscuit").With(x => x.Breed, "Corgi"));
    }
}
