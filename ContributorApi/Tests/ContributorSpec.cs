using Alba;
using Bobcat;
using JasperFx.Events.EventModeling;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Bobcat;

namespace ContributorApi.Tests;

/// <summary>
/// The base for the ContributorApi specifications: Wolverine's <see cref="WolverineSpec" />
/// vocabulary (arrange and assert Marten documents, tracked acts), plus the one way this application
/// is driven, over HTTP through Alba.
/// </summary>
/// <remarks>
/// Every request runs inside <see cref="WolverineSpec.WhenTracked" />, so the act waits for all the
/// Wolverine work it caused. Each specification starts from an empty store, as the original tests did.
/// </remarks>
public abstract class ContributorSpec(ContributorHost app) : WolverineSpec(app.Host), IAsyncLifetime
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

    /// <summary>PUT <paramref name="command" /> to <paramref name="url" />.</summary>
    [EventModelRole(SliceRole.Command, nameof(command))]
    [EventModelTrigger(TriggerKind.Http, Route = nameof(url), HttpMethod = "PUT")]
    public Task WhenPut(object command, string url)
        => send($"{command.GetType().Name} is put to {url}", x => x.Put.Json(command).ToUrl(url));

    /// <summary>PATCH <paramref name="command" /> to <paramref name="url" />.</summary>
    [EventModelRole(SliceRole.Command, nameof(command))]
    [EventModelTrigger(TriggerKind.Http, Route = nameof(url), HttpMethod = "PATCH")]
    public Task WhenPatched(object command, string url)
        => send($"{command.GetType().Name} is patched to {url}", x => x.Patch.Json(command).ToUrl(url));

    /// <summary>DELETE <paramref name="url" />, with no body.</summary>
    [EventModelTrigger(TriggerKind.Http, Route = nameof(url), HttpMethod = "DELETE")]
    public Task WhenDeleted(string url)
        => send($"DELETE {url} is sent", x => x.Delete.Url(url));

    /// <summary>GET <paramref name="url" /> and keep the response.</summary>
    [EventModelTrigger(TriggerKind.Http, Route = nameof(url), HttpMethod = "GET")]
    public Task WhenReading(string url)
        => send($"GET {url} is read", x => x.Get.Url(url));

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

    /// <summary>The last response's body, read as JSON. Not a step: the assertion that follows it is.</summary>
    public Task<T> TheResponse<T>()
        => (_lastResponse ?? throw new InvalidOperationException("No request has been sent yet.")).ReadAsJsonAsync<T>()!;
}
