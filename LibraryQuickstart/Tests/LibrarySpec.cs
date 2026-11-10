using Alba;
using Bobcat;
using JasperFx.Events.EventModeling;
using Wolverine.Bobcat;

namespace Tests;

/// <summary>
/// The base for the LibraryQuickstart specifications: Bobcat's <see cref="WolverineSpec" /> vocabulary
/// (arrange events, assert events, read models and the messages an act caused), plus the one way this
/// application is driven, over HTTP.
/// </summary>
/// <remarks>
/// Every request runs inside <see cref="WolverineSpec.WhenTracked" />, so the act waits for whatever the
/// request caused, the fast-forwarded <c>IEvent&lt;BookReturned&gt;</c> that the reactor handles included.
/// </remarks>
public abstract class LibrarySpec(AppFixture app) : WolverineSpec(app.Host)
{
    private IScenarioResult? _lastResponse;

    protected IAlbaHost Alba => app.Host;

    /// <summary>POST <paramref name="command" /> to <paramref name="url" />.</summary>
    /// <remarks>
    /// A wrapper over <see cref="WolverineSpec.WhenTracked" /> stamps nothing on the Event Model by itself, so it
    /// declares its own roles: the command it posts, and the HTTP route it posts to.
    /// </remarks>
    [EventModelRole(SliceRole.Command, nameof(command))]
    [EventModelTrigger(TriggerKind.Http, Route = nameof(url))]
    public Task WhenPosted(object command, string url)
        => WhenTracked($"{command.GetType().Name} is posted to {url}", async () =>
        {
            _lastResponse = await Alba.Scenario(x =>
            {
                x.Post.Json(command).ToUrl(url);

                // A refusal is an answer to assert on, not an exception
                x.IgnoreStatusCode();
            });
        });

    /// <summary>POST to <paramref name="url" /> with no body: an endpoint that takes only route values.</summary>
    [EventModelTrigger(TriggerKind.Http, Route = nameof(url))]
    public Task WhenPostedTo(string url)
        => WhenTracked($"POST {url}", async () =>
        {
            _lastResponse = await Alba.Scenario(x =>
            {
                x.Post.Url(url);
                x.IgnoreStatusCode();
            });
        });

    [Then("the response status is {status}")]
    public void ThenTheResponseStatusIs(int status)
        => SpecAssert.Check("status", _lastResponse?.Context.Response.StatusCode, status);

    [Then("the response says {text}")]
    public void ThenTheResponseSays(string text)
    {
        var body = _lastResponse?.ReadAsText() ?? "";
        SpecAssert.Fact(body.Contains(text), $"Expected the response to contain \"{text}\", but it was: {body}");
    }

    /// <summary>The last response's body, read as JSON. Not a step: the assertion that follows it is.</summary>
    public T TheResponse<T>()
        => (_lastResponse ?? throw new InvalidOperationException("No request has been sent yet.")).ReadAsJson<T>()!;
}
