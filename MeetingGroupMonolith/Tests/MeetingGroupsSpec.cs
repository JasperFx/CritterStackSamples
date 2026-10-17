using Alba;
using Bobcat;
using Bobcat.CritterStack;

namespace Tests;

/// <summary>
/// The base for the event-sourced Payments specifications: Bobcat's store-only <see cref="CritterStackSpec" />
/// vocabulary (arrange events, assert events and aggregates), plus the one way this application is driven,
/// over HTTP.
/// </summary>
/// <remarks>
/// <see cref="CritterStackSpec.WhenActing" /> takes the store's high-water mark, runs the request and reads
/// back everything appended since, so <c>ThenEvents</c> and <c>ThenNoEvents</c> judge exactly what the
/// request appended.
/// </remarks>
public abstract class MeetingGroupsSpec(MeetingGroupsHost app) : CritterStackSpec(app.Host)
{
    private IScenarioResult? _lastResponse;

    protected IAlbaHost Alba => app.Host;

    /// <summary>POST <paramref name="command" /> to <paramref name="url" />.</summary>
    public Task WhenPosted(object command, string url)
        => WhenActing($"{command.GetType().Name} is posted to {url}", async _ =>
        {
            _lastResponse = await Alba.Scenario(x =>
            {
                x.Post.Json(command).ToUrl(url);

                // A refusal is an answer to assert on, not an exception
                x.IgnoreStatusCode();
            });
        });

    [Then("the response status is {status}")]
    public void ThenTheResponseStatusIs(int status)
        => SpecAssert.Check("status", _lastResponse?.Context.Response.StatusCode, status);

    /// <summary>The last response's body, read as JSON. Not a step: the assertion that follows it is.</summary>
    public Task<T> TheResponse<T>()
        => (_lastResponse ?? throw new InvalidOperationException("No request has been sent yet.")).ReadAsJsonAsync<T>()!;
}
