using Alba;
using Bobcat;
using Bobcat.CritterStack;
using JasperFx.Events;
using JasperFx.Events.EventModeling;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Tracking;
using Xunit;

namespace CritterCrush.Specs;

/// <summary>
/// The store vocabulary for CritterCrush's projected specs: a hand-written scenario base over Bobcat's
/// public store primitives (<see cref="EventStoreAuthoring"/>, <see cref="EventStores"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not <c>CritterStackSpec</c> / <c>WolverineSpec</c>.</b> Bobcat 1.0 ships both, and the bus act
/// here is exactly <c>WolverineSpec.WhenReceived</c>. But this application is mostly collapsed HTTP
/// endpoints, and without WolverineFx.Bobcat.Http (which drags in Alba 9 prereleases) an HTTP act is
/// <c>WhenTracked</c>, which (checked against WolverineFx.Bobcat 6.51.0-alpha.1) counts every event the
/// request's cascade appended as the act's own, so <c>ThenEvents(typeof(HomeCheckAssignmentAccepted))</c>
/// fails on the automation's <c>HomeCheckAppointmentProposed</c>, and does not see a 400 ProblemDetails
/// as a refusal, so <c>ThenRefusedWith</c> says the act succeeded. This base reads the delta of the
/// stream the scenario arranged and keeps the response, which is what these specs assert on.
/// </para>
/// <para>
/// <b>Every helper is a step.</b> <c>[Given]</c>/<c>[When]</c>/<c>[Then]</c> (until 1.0 spelled
/// <c>[BobcatStep(..., Keyword = "...")]</c>, which still works) on the shared helpers renders every test
/// that calls them, with no marker comments in the test bodies. <c>[EventModelRole]</c> /
/// <c>[EventModelTrigger]</c> on the methods the tests call hand the generator each slice's command,
/// aggregate, events and read models (bobcat#462), as the shipped vocabularies do; a wrapper has to
/// re-declare them, since the generator reads the method the test calls, not what it calls.
/// </para>
/// <para>
/// <b>Which is why every step helper is <c>public</c> and not <c>protected</c>.</b> The generated
/// interceptor is an extension method, which cannot see a protected member. Since 1.0 that is
/// BOBCAT039 at the helper rather than a CS0122 from generated code.
/// </para>
/// </remarks>
public abstract class CritterCrushSpec : IAsyncLifetime
{
    private readonly CritterCrushHost _host;
    private object? _stream;
    private Type? _aggregate;
    private ActCapture _last = ActCapture.None;
    private IScenarioResult? _response;

    protected CritterCrushSpec(CritterCrushHost host) => _host = host;

    protected IAlbaHost Host => _host.Host;

    private IEventStore Store => Host.Services.EventStore();

    /// <summary>
    /// Marten's daemon-aware reset, not <c>ResetEventStoresAsync</c>.
    /// </summary>
    /// <remarks>
    /// The store-agnostic one truncates the event store underneath a RUNNING daemon: the async
    /// agents keep the floor they read at start-up, so the next test either looks already caught up
    /// (nothing projected, no progress written) or writes progress the truncated table no longer
    /// matches. Either way every read-model assertion times out. This one pauses the coordinator,
    /// resets, and resumes. Carried over verbatim from SuiteConfiguration.cs, which learned it.
    /// </remarks>
    public async ValueTask InitializeAsync() => await Host.ResetAllMartenDataAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ---- arrange ---------------------------------------------------------------------------

    /// <summary>The stream this scenario acts against has no events yet.</summary>
    [EventModelRole(SliceRole.Aggregate, nameof(T))]
    public Task GivenNoEvents<T>(Guid id) where T : class => GivenEvents(typeof(T), id);

    /// <summary>The stream starts with exactly these events — the prior history a command runs against.</summary>
    [EventModelRole(SliceRole.Aggregate, nameof(T))]
    [EventModelRole(SliceRole.ArrangedEvent, nameof(events))]
    public Task GivenEvents<T>(Guid id, params object[] events) where T : class
        => GivenEvents(typeof(T), id, events);

    /// <summary>
    /// The stepped core. Non-generic on purpose: a step attribute generates an INTERCEPTOR, and
    /// an interceptor is an extension method whose type arguments have to be inferable from the call
    /// site — which they never are for <c>GivenEvents&lt;Appointment&gt;(id)</c>, where the aggregate
    /// appears only as a type argument. That is CS0411 from generated code.
    /// </summary>
    /// <remarks>
    /// So the generic wrappers above give the call site its ergonomics (and carry the Event Model roles)
    /// and this carries the rendering. Bobcat 1.0 does intercept a generic step with its type arguments
    /// (bobcat#534), but the <c>{aggregate}</c> placeholder binds by parameter name, so the Type-taking
    /// core stays.
    /// </remarks>
    [Given("{aggregate} \"{id}\" has already recorded these events")]
    public async Task GivenEvents(Type aggregate, Guid id, params object[] events)
    {
        _stream = id;
        _aggregate = aggregate;
        if (events.Length > 0) await EventStoreAuthoring.AppendAsync(Store, aggregate, id, events);
    }

    /// <summary>
    /// Arrange events on a DIFFERENT stream than the one the act runs against — a second aggregate,
    /// or a second stream of the same one, for a rule that spans both or a fan-out read model.
    /// </summary>
    /// <remarks>
    /// The one thing this does NOT do is what makes it a separate method: it leaves the stream the
    /// act runs against alone, where <see cref="GivenEvents(Type, Guid, object[])"/> declares it.
    /// <para>
    /// Its keyword is <c>Given</c>, not <c>And</c>. It usually follows one, and hardcoding <c>And</c>
    /// to suit that read as a specification opening mid-sentence in the seven scenarios where this
    /// is the FIRST step. Bobcat 0.27.3 renders a repeated keyword as <c>And</c> on its own, which
    /// is a fact about the scenario rather than about this helper — so the helper states what it is
    /// and the recorder decides how it reads.
    /// </para>
    /// </remarks>
    [EventModelRole(SliceRole.Aggregate, nameof(T))]
    [EventModelRole(SliceRole.ArrangedEvent, nameof(events))]
    public Task GivenEventsOn<T>(Guid id, params object[] events) where T : class
        => GivenEventsOn(typeof(T), id, events);

    /// <inheritdoc cref="GivenEventsOn{T}"/>
    [Given("{aggregate} \"{id}\" has already recorded these events")]
    public Task GivenEventsOn(Type aggregate, Guid id, params object[] events)
        => events.Length == 0 ? Task.CompletedTask : EventStoreAuthoring.AppendAsync(Store, aggregate, id, events);

    // ---- act -------------------------------------------------------------------------------

    /// <summary>Dispatch a message over the bus and wait for everything it caused to settle.</summary>
    [When("{message} is received")]
    [EventModelRole(SliceRole.Command, nameof(message))]
    public Task WhenReceived(object message)
        => actAsync(() => Host.TrackActivity().IncludeExternalTransports().SendMessageAndWaitAsync(message));

    /// <summary>
    /// POST to a collapsed endpoint inside a tracked session, so the events it appends and the
    /// messages it cascades are both captured — the HTTP twin of <see cref="WhenReceived"/>.
    /// </summary>
    [When("{command} is posted to \"{route}\"")]
    [EventModelRole(SliceRole.Command, nameof(command))]
    [EventModelTrigger(TriggerKind.Http, Route = nameof(route), HttpMethod = "POST")]
    public async Task WhenPosted(object command, string route)
    {
        // Typed explicitly: an async lambda matches both the Task and ValueTask overloads, which
        // is CS0121.
        Func<IMessageContext, Task> call = async _ =>
        {
            _response = await Host.Scenario(x =>
            {
                x.Post.Json(command).ToUrl(route);
                // The refusal path is a 400 and the happy path a 2xx; asserting either here would
                // pre-empt the Then step, so the scenario is told to accept both.
                x.IgnoreStatusCode();
            });
        };

        await actAsync(() => Host.TrackActivity().IncludeExternalTransports().ExecuteAndWaitAsync(call));
    }

    /// <summary>
    /// The act, and the capture every assertion below reads.
    /// </summary>
    /// <remarks>
    /// This is <c>TrackedActs.ExecuteAsync</c> rewritten against public primitives, because that
    /// one takes an <c>IStepContext</c>. Same two rules: with a stream established, "what the act
    /// appended" is that stream's delta; with none — a slice that MINTS its own id — it is whatever
    /// the store issued after a high-water floor taken first, since the scenario has no id to name
    /// (bobcat#319). A failure is CAPTURED, never thrown, which is what lets a refusal be asserted.
    /// </remarks>
    private async Task actAsync(Func<Task<ITrackedSession>> dispatch)
    {
        var floor = _stream is null ? await EventStores.HighWaterSequenceAsync(Store) : (long?)null;
        var before = _stream is Guid id ? await EventStores.FetchStreamAsync(Store, id) : [];

        try
        {
            var session = await dispatch();
            var appended = _stream is Guid streamId
                ? (IReadOnlyList<IEvent>)(await EventStores.FetchStreamAsync(Store, streamId)).Skip(before.Count).ToList()
                : await EventStores.QueryEventsSinceAsync(Store, floor!.Value + 1);

            // Nothing here reads the session: awaiting it is what makes the cascade settle, and every
            // assertion below works off the appended events or the error. (Bobcat 1.0 removed the
            // ActExecution record this used to be captured into, so ActCapture below is our own.)
            _ = session;
            _last = new ActCapture(appended, null);
        }
        catch (Exception e)
        {
            _last = new ActCapture([], e);
        }
    }

    // ---- assert ----------------------------------------------------------------------------

    /// <summary>The act appended exactly these event types, in order.</summary>
    [Then("{event} is emitted")]
    [EventModelRole(SliceRole.Event, nameof(@event))]
    public void ThenEvents(params Type[] @event)
    {
        rethrowUnexpected();
        Assert.Equal(@event, _last.NewEvents.Select(x => x.Data.GetType()).ToArray());
    }

    /// <summary>The act appended nothing — the refusal half of a guard.</summary>
    [Then("no events are emitted")]
    public void ThenNoEvents() => Assert.Empty(_last.NewEvents);

    /// <summary>The single event the act appended, for asserting on its fields. Not a step — it
    /// reads the capture rather than doing anything, and the assertion that follows is the step.</summary>
    [EventModelRole(SliceRole.Event, nameof(T))]
    public T TheEvent<T>()
    {
        rethrowUnexpected();
        return Assert.IsType<T>(Assert.Single(_last.NewEvents).Data);
    }

    /// <summary>A collapsed endpoint refuses with ProblemDetails and a status, not by throwing.</summary>
    [Then("the response is {status}")]
    public void ThenResponseIs(int status)
        => Assert.Equal(status, (_response ?? throw new InvalidOperationException(
            "No HTTP response was captured — this scenario acted over the bus, where a refusal throws. "
            + "Use ThenValidationFails instead.")).Context.Response.StatusCode);

    /// <summary>
    /// A collapsed endpoint's refusal, message and all.
    /// </summary>
    /// <remarks>
    /// The status alone is a weak assertion: every guard on an endpoint answers 400, so a test that
    /// checks only the code passes when the WRONG rule refused. That is not hypothetical — rewriting
    /// ConfirmAppointment's three `if`s as one `switch` left the message as the only thing telling
    /// the branches apart, and nothing asserted it.
    /// </remarks>
    [Then("refused with \"{message}\"")]
    public async Task ThenRefusedWith(string message)
    {
        ThenResponseIs(400);

        var body = await (_response ?? throw new InvalidOperationException("No HTTP response was captured."))
            .ReadAsTextAsync();

        Assert.Contains(message, body);
    }

    /// <summary>A bus-dispatched command refuses by throwing, which the act captured.</summary>
    [Then("validation fails with \"{message}\"")]
    public void ThenValidationFails(string message)
    {
        var error = _last.Error ?? throw new InvalidOperationException(
            "The act succeeded, so there is no failure to assert. A collapsed endpoint refuses with a "
            + "400 rather than by throwing — use ThenResponseIs(400) for those.");

        Assert.Contains(message, error.Message);
    }

    /// <summary>
    /// A creating slice started the stream, under the identity the model says (bobcat#360).
    /// </summary>
    /// <remarks>
    /// <see cref="ThenEvents"/> proves an event of some type was appended — and for a minting slice
    /// it reads a high-water delta, not a stream, so it cannot say WHERE. The identity is usually
    /// the decision: ProposeHomeCheckAppointment uses the assignment's id so a redelivered trigger
    /// collides on StartStream instead of booking a second visit, and nothing else asserts that.
    /// Non-generic and taking Type for the same reason <see cref="GivenEvents(Type, Guid, object[])"/>
    /// is — an interceptor is an extension method whose type arguments must be inferable.
    /// </remarks>
    [Then("a {aggregate} stream is started with id \"{id}\"")]
    [EventModelRole(SliceRole.Aggregate, nameof(aggregate))]
    public async Task ThenStreamIsStarted(Type aggregate, Guid id)
    {
        rethrowUnexpected();

        var events = await EventStores.FetchStreamAsync(Store, id);

        Assert.True(events.Count > 0,
            $"Expected a {aggregate.Name} stream with id {id}, but no stream exists there. The slice "
            + "appended its event somewhere else, or did not start a stream at all — which "
            + "ThenEvents cannot tell you, because it does not address the stream.");
    }

    /// <summary>
    /// A read model, after the async daemon has caught up. The wait is the point: these projections
    /// are async and multi-stream, so reading straight after the act reads a stale document.
    /// </summary>
    [EventModelRole(SliceRole.ReadModel, nameof(T))]
    public async Task<T> ThenReadModel<T>(Guid id) where T : class
    {
        await ThenProjectionsAreCaughtUp(typeof(T));

        return await EventStoreAuthoring.LoadDocumentAsync<T>(Store, id)
               ?? throw new InvalidOperationException(
                   $"No {typeof(T).Name} document with id {id}. The projection ran — the daemon reported "
                   + "non-stale — so either nothing routed to this id, or the identity rule differs.");
    }

    /// <summary>The stepped half of <see cref="ThenReadModel{T}"/>; see <see cref="GivenEvents(Type, Guid, object[])"/>
    /// for why the rendering hangs off a non-generic method.</summary>
    [Then("the {readmodel} read model has caught up")]
    [EventModelRole(SliceRole.ReadModel, nameof(readmodel))]
    public Task ThenProjectionsAreCaughtUp(Type readmodel)
        => EventStores.WaitForNonStaleProjectionsAsync(Store, EventStores.DefaultProjectionTimeout);

    /// <summary>The write model, folded from its stream.</summary>
    [EventModelRole(SliceRole.Aggregate, nameof(T))]
    public Task<T?> TheAggregate<T>(Guid id) where T : class => EventStores.AggregateStreamAsync<T>(Store, id);

    /// <summary>
    /// A failure the test did not ask about is rethrown rather than swallowed. Without this an
    /// unexpected exception surfaces as "expected 1 event, got 0", which sends the reader to the
    /// handler's logic when the real answer is in the exception the act captured.
    /// </summary>
    private void rethrowUnexpected()
    {
        if (_last.Error is { } error)
        {
            throw new InvalidOperationException($"The act failed: {error.Message}", error);
        }
    }

    /// <summary>What one act appended, and the failure it raised if it raised one.</summary>
    private sealed record ActCapture(IReadOnlyList<IEvent> NewEvents, Exception? Error)
    {
        public static readonly ActCapture None = new([], null);
    }
}
