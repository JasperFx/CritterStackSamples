using System.Text.RegularExpressions;
using Alba;
using Bobcat;
using Bobcat.CritterStack;
using JasperFx.Events.EventModeling;

namespace BankAccountES.Tests;

/// <summary>
/// The base for the BankAccountES specifications: Bobcat's store-only <see cref="CritterStackSpec" />
/// vocabulary (arrange events, assert events and aggregates), plus the one way this application is
/// driven, over HTTP.
/// </summary>
/// <remarks>
/// <see cref="CritterStackSpec.WhenActing" /> takes the store's high-water mark, runs the request and
/// reads back everything appended since, so <c>ThenEvents</c> and <c>ThenNoEvents</c> judge exactly what
/// the request appended.
/// </remarks>
public abstract class BankAccountSpec(BankAccountHost app) : CritterStackSpec(app.Host)
{
    private IScenarioResult? _lastResponse;

    protected IAlbaHost Alba => app.Host;

    /// <summary>
    /// POST <paramref name="command" /> to <paramref name="route" />, written as the endpoint's template
    /// (<c>"/api/accounts/{accountId}/deposits"</c>) with its values after it, in order. A constant template
    /// is what reaches the Event Model as the slice's route.
    /// </summary>
    /// <remarks>
    /// The generator reads the Event Model roles off the method a test calls, never off what that method
    /// calls, so a wrapper act has to declare them itself.
    /// </remarks>
    [EventModelRole(SliceRole.Command, nameof(command))]
    [EventModelTrigger(TriggerKind.Http, Route = nameof(route), HttpMethod = "POST")]
    public Task WhenPosted(object command, string route, params object[] routeValues)
    {
        var url = fill(route, routeValues);
        return WhenActing($"{command.GetType().Name} is posted to {route}", async _ =>
        {
            _lastResponse = await Alba.Scenario(x =>
            {
                x.Post.Json(command).ToUrl(url);

                // A refusal is an answer to assert on, not an exception
                x.IgnoreStatusCode();
            });
        });
    }

    /// <summary>GET <paramref name="route" /> (a template, as for <see cref="WhenPosted" />) and keep the response, read as a <typeparamref name="T" />.</summary>
    [EventModelRole(SliceRole.ReadModel, nameof(T))]
    [EventModelTrigger(TriggerKind.Http, Route = nameof(route), HttpMethod = "GET")]
    public Task WhenReading<T>(string route, params object[] routeValues) where T : class
    {
        var url = fill(route, routeValues);
        return WhenActing($"GET {route} is read", async _ =>
        {
            _lastResponse = await Alba.Scenario(x =>
            {
                x.Get.Url(url);
                x.IgnoreStatusCode();
            });
        });
    }

    // "/api/accounts/{accountId}/deposits" + [id] => "/api/accounts/<id>/deposits"
    private static string fill(string route, object[] values)
    {
        var i = 0;
        var url = Regex.Replace(route, @"\{[^}]+\}", m => i < values.Length ? Uri.EscapeDataString(values[i++].ToString()!) : m.Value);
        if (i != values.Length)
            throw new ArgumentException($"Route '{route}' has {i} placeholder(s) but was given {values.Length} value(s).");
        return url;
    }

    [Then("the response status is {status}")]
    public void ThenTheResponseStatusIs(int status)
        => SpecAssert.Check("status", _lastResponse?.Context.Response.StatusCode, status);

    /// <summary>The last response's body, read as JSON. Not a step: the assertion that follows it is.</summary>
    public Task<T> TheResponse<T>()
        => (_lastResponse ?? throw new InvalidOperationException("No request has been sent yet.")).ReadAsJsonAsync<T>()!;
}
