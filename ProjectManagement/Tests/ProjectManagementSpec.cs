using System.Text.RegularExpressions;
using Alba;
using Bobcat;
using Bobcat.CritterStack;
using JasperFx.Events.EventModeling;

namespace Tests;

/// <summary>
/// The base for the ProjectManagement specifications: Bobcat's store-only <see cref="CritterStackSpec" />
/// vocabulary (assert the events a request appended), plus the one way this
/// application is driven, over HTTP.
/// </summary>
/// <remarks>
/// <see cref="CritterStackSpec.WhenActing" /> takes the store's high-water mark, runs the request and
/// reads back everything appended since, so <c>ThenEvents</c> and <c>ThenNoEvents</c> judge exactly what
/// the request appended. That matters here: the endpoint is a Minimal API delegate appending through its
/// own <c>IDocumentSession</c>, not a Wolverine endpoint, so a Wolverine tracked session would hear no commit.
/// </remarks>
public abstract class ProjectManagementSpec(ProjectManagementHost app) : CritterStackSpec(app.Host)
{
    private IScenarioResult? _lastResponse;

    protected IAlbaHost Alba => app.Host;

    /// <summary>POST <paramref name="command" /> to <paramref name="route" />, a template with its values after it, in order.</summary>
    /// <remarks>Declares its Event Model roles: the generator reads them off the method a test calls.</remarks>
    [EventModelRole(SliceRole.Command, nameof(command))]
    [EventModelTrigger(TriggerKind.Http, Route = nameof(route), HttpMethod = "POST")]
    public Task WhenPosted(object command, string route, params object[] routeValues)
        => WhenActing($"{command.GetType().Name} is posted to {route}", async _ =>
        {
            _lastResponse = await Alba.Scenario(x =>
            {
                x.Post.Json(command).ToUrl(fill(route, routeValues));

                // A refusal is an answer to assert on, not an exception
                x.IgnoreStatusCode();
            });
        });

    // "/api/things/{id}/x" + [id] => "/api/things/<id>/x"
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
