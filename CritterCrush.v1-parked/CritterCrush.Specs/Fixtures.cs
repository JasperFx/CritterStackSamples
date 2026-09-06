using Alba;
using Bobcat;
using Bobcat.Engine;
using Bobcat.Alba;
using Bobcat.CritterStack;
using CritterCrush.Discovery;
using CritterCrush.Profiles;
using JasperFx.Events;
using JasperFx.Events.EventModeling;
using Wolverine.Http;
using Wolverine.Tracking;

// The declared model, the running host, and the specs all merge by this one name — it must
// match opts.ServiceName in Program.cs and the `model:` field of models/CritterCrush.emodel.yaml.
[assembly: EventModelName("CritterCrush")]

namespace CritterCrush.Specs;

// These fixtures drive the slices over HTTP, because the slices ARE their endpoints — the
// collapsed shape (one transaction, honest status codes) is the house default, and a separate
// message handler exists only when a command genuinely needs bus visibility. The custom steps
// below are the interim cost of that: the shipped grammar's `When {command} is received` binds
// only to bus dispatch today. bobcat#210 (an HTTP grammar / CritterStackHttpFixture) and
// bobcat#211 (TrackedHttpCall as a first-class seam) are the follow-ups that make most of this
// file disappear; bobcat#212 is why `Then the response is {int}` has to be copy-pasted between
// the two fixtures instead of composed in.

[FixtureTitle("DogProfiles")]
public class DogProfilesFixture : CritterStackFixture
{
    private int _status;
    private Guid _profileId;

    [When("a dog profile is posted")]
    public async Task WhenProfilePosted(StepTable table)
    {
        var row = table.AsDictionaries()[0];
        var request = new CreateDogProfileRequest(
            row["Name"], row["Breed"], int.Parse(row["AgeInMonths"]), Guid.Parse(row["OwnerId"]));

        var result = await Context!.PostJsonAsync<CreateDogProfileRequest, CreationResponse>(
            "/api/profiles", request);

        _status = result.StatusCode;
        _profileId = result.Body is { Url: { } url } ? Guid.Parse(url.Split('/').Last()) : Guid.Empty;

        Context!.RecordTouchedType(typeof(CreateDogProfileRequest));
    }

    [Then("the response is {int}")]
    public void ThenStatus(int expected)
    {
        if (_status != expected)
            throw new SpecAssertionException($"Expected HTTP {expected}, but the endpoint answered {_status}.");
    }

    [Then("the new DogProfile contains")]
    public Task ThenProfileContains(StepTable table)
        => ThenDocument<DogProfile>(_profileId, profile =>
        {
            foreach (var (column, expected) in table.AsDictionaries()[0])
            {
                var actual = typeof(DogProfile).GetProperty(column)?.GetValue(profile)?.ToString();
                if (!string.Equals(actual, expected, StringComparison.Ordinal))
                    throw new SpecAssertionException($"{column}: expected '{expected}', was '{actual}'.");
            }
        });
}

[FixtureTitle("Swiping")]
public class SwipingFixture : CritterStackFixture
{
    private IReadOnlyList<IEvent> _appended = [];
    private int _status;

    [Given("dog {string} already liked dog {string}")]
    public Task GivenAlreadyLiked(string swiper, string target)
    {
        var (swiperId, targetId) = (Guid.Parse(swiper), Guid.Parse(target));
        var pairId = SwipePair.IdFor(swiperId, targetId);
        return GivenEvents<SwipePair>(pairId, new DogLiked(pairId, swiperId, targetId));
    }

    [When("dog {string} swipes right on dog {string}")]
    public Task WhenSwipesRight(string swiper, string target) => swipeCore(swiper, target, liked: true);

    [When("dog {string} swipes left on dog {string}")]
    public Task WhenSwipesLeft(string swiper, string target) => swipeCore(swiper, target, liked: false);

    /// <summary>
    /// The hand-rolled TrackedHttpCall (bobcat#211): the Alba scenario runs inside a Wolverine
    /// tracked session, so forwarded events and the DetectMutualMatch automation settle before
    /// any Then runs — and the appended events are the before/after diff of the pair's stream.
    /// </summary>
    private async Task swipeCore(string swiper, string target, bool liked)
    {
        var request = new SwipeRequest(Guid.Parse(swiper), Guid.Parse(target), liked);
        var before = await Context!.FetchEventStreamAsync(request.SwipePairId);

        var host = Context!.GetResource<IAlbaResource>().AlbaHost;
        var status = 0;
        await host.TrackActivity().ExecuteAndWaitAsync((Func<Wolverine.IMessageContext, Task>)(async _ =>
        {
            var result = await host.Scenario(x =>
            {
                x.Post.Json(request).ToUrl("/api/discovery/swipes");
                x.IgnoreStatusCode();
            });
            status = result.Context.Response.StatusCode;
        }));

        _status = status;
        var after = await Context!.FetchEventStreamAsync(request.SwipePairId);
        _appended = after.Skip(before.Count).ToList();

        // Observed run evidence for the Event Model, same as the grammar's own When records.
        Context!.RecordTouchedType(typeof(SwipeRequest));
        foreach (var @event in _appended) Context!.RecordTouchedType(@event.Data.GetType());
    }

    [Then("the swipe appended a {event}")]
    public void ThenAppended(Type @event)
    {
        if (_appended.All(x => x.Data.GetType() != @event))
            throw new SpecAssertionException(
                $"Expected a {@event.Name} on the pair's stream, but the appended events were: " +
                $"[{string.Join(", ", _appended.Select(x => x.Data.GetType().Name))}]");
    }

    [Then("the swipe appended nothing")]
    public void ThenAppendedNothing()
    {
        if (_appended.Count > 0)
            throw new SpecAssertionException(
                $"Expected no events, but the swipe appended: [{string.Join(", ", _appended.Select(x => x.Data.GetType().Name))}]");
    }

    [Then("the response is {int}")]
    public void ThenStatus(int expected)
    {
        if (_status != expected)
            throw new SpecAssertionException($"Expected HTTP {expected}, but the endpoint answered {_status}.");
    }
}
