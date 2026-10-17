using Alba;
using Bobcat;
using JasperFx.Events.EventModeling;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Bobcat;

namespace MoreSpeakers.Tests;

/// <summary>
/// The base for the MoreSpeakers specifications: Wolverine's <see cref="WolverineSpec" />
/// vocabulary (arrange and assert Marten documents, tracked acts), plus the one way this application
/// is driven, over HTTP through Alba.
/// </summary>
/// <remarks>
/// Every request runs inside <see cref="WolverineSpec.WhenTracked" />, so the act waits for all the
/// Wolverine work it caused. Each specification starts from an empty store, as the original tests did.
/// </remarks>
public abstract class MoreSpeakersSpec(MoreSpeakersHost app) : WolverineSpec(app.Host), IAsyncLifetime
{
    private IScenarioResult? _lastResponse;

    protected IAlbaHost Alba => app.Host;

    public virtual async ValueTask InitializeAsync()
    {
        // The first request to each endpoint compiles its handler at runtime (Dynamic code generation),
        // which on a busy machine can outlast the tracked session's 5 second default
        Timeout = TimeSpan.FromSeconds(30);
        await ResetAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>POST <paramref name="command" /> to <paramref name="url" />.</summary>
    [EventModelRole(SliceRole.Command, nameof(command))]
    [EventModelTrigger(TriggerKind.Http, Route = nameof(url), HttpMethod = "POST")]
    public Task WhenPosted(object command, string url)
        => send($"{command.GetType().Name} is posted to {url}", x => x.Post.Json(command).ToUrl(url));

    private Task send(string description, Action<Scenario> request)
        => WhenTracked(description, async () =>
        {
            _lastResponse = await Alba.Scenario(x =>
            {
                request(x);

                // A refusal is an answer to assert on, not an exception
                x.IgnoreStatusCode();
            });
        });

    [Then("the response status is {status}")]
    public void ThenTheResponseStatusIs(int status)
        => SpecAssert.Check("status", _lastResponse?.Context.Response.StatusCode, status);

    [Then("the request is refused with {status}: {detail}")]
    public async Task ThenTheRequestIsRefusedWith(int status, string detail)
    {
        var problem = await TheResponse<ProblemDetails>();
        SpecAssert.Check("status", _lastResponse!.Context.Response.StatusCode, status);
        SpecAssert.Check("detail", problem.Detail, detail);
    }

    [Then("the request fails validation with {status}: {message}")]
    public async Task ThenTheRequestFailsValidationWith(int status, string message)
    {
        var problem = await TheResponse<ValidationProblemDetails>();
        SpecAssert.Check("status", _lastResponse!.Context.Response.StatusCode, status);
        SpecAssert.Fact(problem.Errors.Values.SelectMany(x => x).Contains(message), $"a validation error reads '{message}'");
    }

    /// <summary>The last response's body, read as JSON. Not a step: the assertion that follows it is.</summary>
    public Task<T> TheResponse<T>()
        => (_lastResponse ?? throw new InvalidOperationException("No request has been sent yet.")).ReadAsJsonAsync<T>()!;
}
