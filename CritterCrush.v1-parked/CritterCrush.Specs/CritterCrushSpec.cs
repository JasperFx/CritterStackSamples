using Alba;
using Bobcat;
using Bobcat.CritterStack;
using JasperFx.Events.EventModeling;
using Marten;
using Wolverine;
using Wolverine.Tracking;

namespace CritterCrush.Specs;

/// <summary>
/// The base for the CritterCrush specifications: Bobcat's store-only <see cref="CritterStackSpec" />
/// vocabulary (arrange events, assert events and aggregates) plus the one way this application is driven,
/// over HTTP, which is what the old <c>DogProfilesFixture</c> / <c>SwipingFixture</c> steps did by hand.
/// </summary>
/// <remarks>
/// <para>
/// <b>The act runs inside a Wolverine tracked session.</b> <see cref="CritterStackSpec.WhenActing" /> takes
/// the store's high-water mark, runs the act and reads back what it appended; running the Alba request
/// inside <c>TrackActivity().ExecuteAndWaitAsync</c> is what makes the forwarded <c>DogLiked</c> and the
/// DetectMutualMatch automation settle first, so a <c>ThenEvents</c> sees the match too (the hand-rolled
/// TrackedHttpCall the Gherkin fixture had, bobcat#211).
/// </para>
/// <para>
/// <b>Why it re-declares the Event Model attributes.</b> The generator reads them off the method a test
/// calls, never off what that method's body calls, so <see cref="WhenPosted" /> carries its own command
/// role and HTTP trigger.
/// </para>
/// </remarks>
public abstract class CritterCrushSpec(CritterCrushHost app) : CritterStackSpec(app.Host), IAsyncLifetime
{
    private IScenarioResult? _lastResponse;

    protected IAlbaHost Alba => app.Host;

    /// <summary>
    /// Marten's daemon-aware reset (it pauses the async daemon around the truncation), not
    /// <see cref="CritterStackSpec.ResetAsync" />, whose store-agnostic reset truncates underneath the running
    /// MatchList projection: that is the "Progression 'MatchList:All' is out of order" noise the old suite logged.
    /// </summary>
    public async ValueTask InitializeAsync() => await Alba.ResetAllMartenDataAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>POST <paramref name="command" /> to <paramref name="route" /> and wait for everything it caused.</summary>
    [EventModelRole(SliceRole.Command, nameof(command))]
    [EventModelTrigger(TriggerKind.Http, Route = nameof(route), HttpMethod = "POST")]
    public Task WhenPosted(object command, string route)
        => WhenActing($"{command} is posted to {route}", async _ =>
        {
            // Typed explicitly: an async lambda matches both the Task and ValueTask overloads
            Func<IMessageContext, Task> request = async _ =>
            {
                _lastResponse = await Alba.Scenario(x =>
                {
                    x.Post.Json(command).ToUrl(route);

                    // A refusal is an answer to assert on, not an exception
                    x.IgnoreStatusCode();
                });
            };

            await Alba.TrackActivity().ExecuteAndWaitAsync(request);
        });

    [Then("the response is {status}")]
    public void ThenTheResponseIs(int status)
        => SpecAssert.Check("status", _lastResponse?.Context.Response.StatusCode, status);
}
