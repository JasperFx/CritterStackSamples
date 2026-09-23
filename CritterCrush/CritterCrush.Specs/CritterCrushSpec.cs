using Alba;
using Bobcat;
using Bobcat.CritterStack;
using JasperFx.Events;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Tracking;
using Xunit;

namespace CritterCrush.Specs;

/// <summary>
/// The store vocabulary for CritterCrush's projected specs — the Lane A counterpart of
/// <c>CritterStackFixture</c>, for tests that run under xUnit rather than inside a Bobcat run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists at all.</b> <c>CritterStackFixture</c>'s typed steps are public, but every one
/// of them reaches the store through an <c>IStepContext</c>, which only exists inside a Bobcat run.
/// A plain <c>[Fact]</c> has none. Bobcat 0.25.0 made <see cref="EventStoreAuthoring"/> public, so
/// the arrange and read-model halves are now reachable directly; what is left to write here is the
/// host, the act, and the rendering.
/// </para>
/// <para>
/// <b>Every helper carries <see cref="BobcatStepAttribute"/>.</b> That is the whole leverage of this
/// lane: decorate the shared helpers once and every test that calls them renders as steps, with no
/// marker comments in the test bodies. Marten's <c>DaemonContext</c> is the precedent — eight
/// decorated helpers rendered hundreds of existing tests without touching a test file.
/// </para>
/// <para>
/// <b>Which is why every step helper is <c>public</c> and not <c>protected</c>.</b> The generated
/// interceptor is an extension method — a requirement of the interceptor feature rather than a
/// choice — and an extension method cannot see a protected member. So the two ideas collide: a base
/// class naturally wants protected members, and <c>[BobcatStep]</c> forbids them. Marking them
/// protected is not a degraded experience but a hard CS0122 from generated code, which reads as a
/// compiler bug until you know the rule.
/// </para>
/// </remarks>
public abstract class CritterCrushSpec : IAsyncLifetime
{
    private readonly CritterCrushHost _host;
    private object? _stream;
    private Type? _aggregate;
    private ActExecution _last = ActExecution.None;
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
    public Task GivenNoEvents<T>(Guid id) where T : class => GivenEvents(typeof(T), id);

    /// <summary>The stream starts with exactly these events — the prior history a command runs against.</summary>
    public Task GivenEvents<T>(Guid id, params object[] events) where T : class
        => GivenEvents(typeof(T), id, events);

    /// <summary>
    /// The stepped core. Non-generic on purpose: <c>[BobcatStep]</c> generates an INTERCEPTOR, and
    /// an interceptor is an extension method whose type arguments have to be inferable from the call
    /// site — which they never are for <c>GivenEvents&lt;Appointment&gt;(id)</c>, where the aggregate
    /// appears only as a type argument. That is CS0411 from generated code.
    /// </summary>
    /// <remarks>
    /// So the generic wrappers above give the call site its ergonomics and this carries the
    /// rendering. The shipped Gherkin fixture takes <c>Type</c> here too, for its own reasons; the
    /// shapes agreeing is a happy accident rather than a plan.
    /// </remarks>
    [BobcatStep("{aggregate} \"{id}\" has already recorded these events", Keyword = "Given")]
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
    public Task GivenEventsOn<T>(Guid id, params object[] events) where T : class
        => GivenEventsOn(typeof(T), id, events);

    /// <inheritdoc cref="GivenEventsOn{T}"/>
    [BobcatStep("{aggregate} \"{id}\" has already recorded these events", Keyword = "And")]
    public Task GivenEventsOn(Type aggregate, Guid id, params object[] events)
        => events.Length == 0 ? Task.CompletedTask : EventStoreAuthoring.AppendAsync(Store, aggregate, id, events);

    // ---- act -------------------------------------------------------------------------------

    /// <summary>Dispatch a message over the bus and wait for everything it caused to settle.</summary>
    [BobcatStep("{message} is received", Keyword = "When")]
    public Task WhenReceived(object message)
        => actAsync(() => Host.TrackActivity().IncludeExternalTransports().SendMessageAndWaitAsync(message));

    /// <summary>
    /// POST to a collapsed endpoint inside a tracked session, so the events it appends and the
    /// messages it cascades are both captured — the HTTP twin of <see cref="WhenReceived"/>.
    /// </summary>
    [BobcatStep("{command} is posted to \"{route}\"", Keyword = "When")]
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

            // The Outcome seam stays null on purpose. 0.27.0 split the capture so the grammar in
            // core knows only an IActOutcome, and Bobcat.Wolverine's WolverineActOutcome is what
            // carries the ITrackedSession across it. Nothing here reads the session — awaiting it
            // is what makes the cascade settle, and every assertion below works off NewEvents or
            // Error — so taking the package to hold a value no step reads would be a dependency
            // for nothing. It earns its place the day a step asserts on messages sent.
            _ = session;
            _last = new ActExecution(null, appended, null);
        }
        catch (Exception e)
        {
            _last = new ActExecution(null, [], e);
        }
    }

    // ---- assert ----------------------------------------------------------------------------

    /// <summary>The act appended exactly these event types, in order.</summary>
    [BobcatStep("{event} is emitted", Keyword = "Then")]
    public void ThenEvents(params Type[] @event)
    {
        rethrowUnexpected();
        Assert.Equal(@event, _last.NewEvents.Select(x => x.Data.GetType()).ToArray());
    }

    /// <summary>The act appended nothing — the refusal half of a guard.</summary>
    [BobcatStep("no events are emitted", Keyword = "Then")]
    public void ThenNoEvents() => Assert.Empty(_last.NewEvents);

    /// <summary>The single event the act appended, for asserting on its fields. Not a step — it
    /// reads the capture rather than doing anything, and the assertion that follows is the step.</summary>
    public T TheEvent<T>()
    {
        rethrowUnexpected();
        return Assert.IsType<T>(Assert.Single(_last.NewEvents).Data);
    }

    /// <summary>A collapsed endpoint refuses with ProblemDetails and a status, not by throwing.</summary>
    [BobcatStep("the response is {status}", Keyword = "Then")]
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
    [BobcatStep("refused with \"{message}\"", Keyword = "Then")]
    public async Task ThenRefusedWith(string message)
    {
        ThenResponseIs(400);

        var body = await (_response ?? throw new InvalidOperationException("No HTTP response was captured."))
            .ReadAsTextAsync();

        Assert.Contains(message, body);
    }

    /// <summary>A bus-dispatched command refuses by throwing, which the act captured.</summary>
    [BobcatStep("validation fails with \"{message}\"", Keyword = "Then")]
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
    [BobcatStep("a {aggregate} stream is started with id \"{id}\"", Keyword = "Then")]
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
    [BobcatStep("the {readmodel} read model has caught up", Keyword = "Then")]
    public Task ThenProjectionsAreCaughtUp(Type readmodel)
        => EventStores.WaitForNonStaleProjectionsAsync(Store, EventStores.DefaultProjectionTimeout);

    /// <summary>The write model, folded from its stream.</summary>
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
}
