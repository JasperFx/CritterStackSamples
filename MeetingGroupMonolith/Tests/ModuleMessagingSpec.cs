using JasperFx.Events.EventModeling;
using System.Text.RegularExpressions;
using Alba;
using Bobcat;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Bobcat;

namespace Tests;

/// <summary>
/// The base for the specifications whose point is a message from one module to another over a durable
/// local queue: WolverineFx.Bobcat's <see cref="WolverineSpec" />, whose acts run inside Wolverine's tracked
/// session, so a step after the act sees what the receiving module's handler did, with no
/// <c>Task.Delay</c>. The HTTP request is the act, run through <see cref="WolverineSpec.WhenTracked" />.
/// </summary>
public abstract class ModuleMessagingSpec(MeetingGroupsHost app) : WolverineSpec(app.Host)
{
    private IScenarioResult? _lastResponse;

    protected IAlbaHost Alba => app.Host;

    /// <summary>POST <paramref name="command" /> to <paramref name="route" /> (a template with its values after it, in order) and wait for every message it cascades.</summary>
    /// <remarks>Declares its Event Model roles: the generator reads them off the method a test calls.</remarks>
    [EventModelRole(SliceRole.Command, nameof(command))]
    [EventModelTrigger(TriggerKind.Http, Route = nameof(route), HttpMethod = "POST")]
    public Task WhenPosted(object command, string route, params object[] routeValues)
        => WhenTracked($"{command.GetType().Name} is posted to {route}", () => post(command, fill(route, routeValues)));

    /// <summary>POST to <paramref name="route" /> with an empty body, for an endpoint that reads only its route.</summary>
    [EventModelTrigger(TriggerKind.Http, Route = nameof(route), HttpMethod = "POST")]
    public Task WhenPostedTo(string route, params object[] routeValues)
        => WhenTracked($"POST {route}", () => post(new { }, fill(route, routeValues)));

    private async Task post(object body, string url)
    {
        _lastResponse = await Alba.Scenario(x =>
        {
            x.Post.Json(body).ToUrl(url);
            x.IgnoreStatusCode();
        });
    }

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

    /// <summary>Every stored document of <typeparamref name="T" />, for a document whose id a handler minted.</summary>
    public async Task<IReadOnlyList<T>> AllDocuments<T>() where T : notnull
    {
        await using var session = Alba.Services.GetRequiredService<IDocumentStore>().QuerySession();
        return await session.Query<T>().ToListAsync();
    }
}
