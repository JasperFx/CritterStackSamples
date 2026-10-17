using Alba;
using Bobcat;
using JasperFx.Events.EventModeling;
using Wolverine.Bobcat;

namespace Tests;

/// <summary>
/// The base for the EcommerceMicroservices specifications: WolverineSpec's vocabulary (messages sent,
/// documents stored) over one service, which is driven over HTTP. Every HTTP act runs inside
/// <see cref="WolverineSpec.WhenTracked" />, so whatever the request cascaded has settled before the
/// Thens, and <c>ThenMessageSent</c> can see it. Each spec starts from an empty store.
/// </summary>
public abstract class EcommerceSpec(IAlbaHost alba) : WolverineSpec(alba), IAsyncLifetime
{
    private IScenarioResult? _lastResponse;

    protected IAlbaHost Alba => alba;

    public async ValueTask InitializeAsync() => await ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>POST <paramref name="command" /> to <paramref name="route" />.</summary>
    [EventModelRole(SliceRole.Command, nameof(command))]
    [EventModelTrigger(TriggerKind.Http, Route = nameof(route), HttpMethod = "POST")]
    public Task WhenPosted(object command, string route)
        => send($"{command.GetType().Name} is posted to {route}", x => x.Post.Json(command).ToUrl(route));

    /// <summary>PUT <paramref name="command" /> to <paramref name="route" />.</summary>
    [EventModelRole(SliceRole.Command, nameof(command))]
    [EventModelTrigger(TriggerKind.Http, Route = nameof(route), HttpMethod = "PUT")]
    public Task WhenPut(object command, string route)
        => send($"{command.GetType().Name} is put to {route}", x => x.Put.Json(command).ToUrl(route));

    /// <summary>DELETE <paramref name="route" />, which carries no body.</summary>
    [EventModelTrigger(TriggerKind.Http, Route = nameof(route), HttpMethod = "DELETE")]
    public Task WhenDeleted(string route)
        => send($"DELETE {route} is sent", x => x.Delete.Url(route));

    /// <summary>GET <paramref name="route" /> and keep the response, read as a <typeparamref name="T" />.</summary>
    [EventModelRole(SliceRole.ReadModel, nameof(T))]
    [EventModelTrigger(TriggerKind.Http, Route = nameof(route), HttpMethod = "GET")]
    public Task WhenReading<T>(string route) where T : class
        => send($"GET {route} is read", x => x.Get.Url(route));

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

    /// <summary>The last response's body, read as JSON. Not a step: the assertion that follows it is.</summary>
    public Task<T> TheResponse<T>()
        => (_lastResponse ?? throw new InvalidOperationException("No request has been sent yet.")).ReadAsJsonAsync<T>()!;
}
