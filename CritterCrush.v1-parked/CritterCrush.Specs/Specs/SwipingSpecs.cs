using Bobcat;
using Bobcat.Xunit;
using CritterCrush.Discovery;

namespace CritterCrush.Specs;

/// <summary>
/// Features/Swiping.feature, one [BobcatSpec] per scenario under the scenario's own name. A swipe lands on
/// the pair's deterministic stream (<see cref="SwipePair.IdFor" />), so "already liked" is that stream's
/// history and "the swipe appended" is what the request appended to it.
/// </summary>
[BobcatFeature("Swiping")]
public class SwipingSpecs(CritterCrushHost app) : CritterCrushSpec(app)
{
    private const string Swipes = "/api/discovery/swipes";

    private static readonly Guid Rex = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Bella = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Pair = SwipePair.IdFor(Rex, Bella);

    [BobcatSpec(SliceName = "SwipeOnDog", Domain = "Discovery", TriggeredBy = "the dog owner swiping")]
    public async Task A_like_is_recorded()
    {
        await WhenPosted(new SwipeRequest(Rex, Bella, Liked: true), Swipes);

        ThenTheResponseIs(200);
        ThenEvents(new DogLiked(Pair, Rex, Bella));
    }

    // Named explicitly: the method name would read "clean no op", and the scenario's identity is the feature's
    [BobcatSpec(SliceName = "SwipeOnDog", Domain = "Discovery", TriggeredBy = "the dog owner swiping",
        Name = "Swiping the same dog twice is a clean no-op")]
    public async Task Swiping_the_same_dog_twice_is_a_clean_no_op()
    {
        await GivenEvents<SwipePair>(Pair, new DogLiked(Pair, Rex, Bella));

        await WhenPosted(new SwipeRequest(Rex, Bella, Liked: true), Swipes);

        ThenNoEvents();
    }

    [BobcatSpec(SliceName = "SwipeOnDog", Domain = "Discovery", TriggeredBy = "the dog owner swiping")]
    public async Task A_pass_is_recorded_without_a_like()
    {
        await WhenPosted(new SwipeRequest(Rex, Bella, Liked: false), Swipes);

        ThenEvents(new DogPassed(Pair, Rex, Bella));
    }

    [BobcatSpec(SliceName = "SwipeOnDog", Domain = "Discovery", TriggeredBy = "the dog owner swiping")]
    public async Task Swiping_yourself_is_refused()
    {
        await WhenPosted(new SwipeRequest(Rex, Rex, Liked: true), Swipes);

        ThenTheResponseIs(400);
        ThenNoEvents();
    }

    [BobcatSpec(SliceName = "DetectMutualMatch", Domain = "Discovery", Pattern = "Automation", TriggeredBy = "DogLiked")]
    public async Task A_mutual_like_produces_a_match()
    {
        await GivenEvents<SwipePair>(Pair, new DogLiked(Pair, Rex, Bella));

        await WhenPosted(new SwipeRequest(Bella, Rex, Liked: true), Swipes);

        // The like the request appended, then the match the automation appended on the same stream
        ThenEvents(typeof(DogLiked), typeof(MutualMatchDetected));
        ThenEmitted(new MutualMatchDetected(Pair, Rex, Bella));
    }
}
