using Bobcat;
using JasperFx.Core;
using JasperFx.Events.EventModeling;
using Polecat;
using ShipmentTracking.Sagas;
using Wolverine;
using Wolverine.Bobcat;
using Wolverine.Polecat;

namespace Tests;

/// <summary>
/// The base for the ShipmentTracking specifications: Bobcat's <see cref="WolverineSpec" /> vocabulary (acts
/// that wait for every message they caused, asserts on what was sent and on Polecat documents), plus the
/// steps this application needs and the vocabulary does not have: an HTTP act, arranging state through the
/// real handlers, "this message was handled", and "the act threw nothing".
/// </summary>
/// <remarks>
/// <para>
/// This app has no event sourcing — Polecat documents and a Wolverine saga — so there is nothing to
/// <c>GivenEvents</c> with. A shipment is arranged the way the original tests arranged it, through the real
/// <c>BookShipment</c> handler, inside a <c>[Given]</c> step so it reads as an arrangement, not a second act.
/// </para>
/// <para>
/// The tracked sessions wait on external transports (WolverineSpec's default, the original suite's
/// <c>IncludeExternalTransports()</c>): every command here is routed to RabbitMQ, and stubbing the transports
/// would drop them on the floor. See the comment on <see cref="IntegrationContext" />.
/// </para>
/// </remarks>
public abstract class ShipmentSpec : WolverineSpec, IAsyncLifetime
{
    private IScenarioResult? _lastResponse;
    private ITrackedSession? _booking;

    protected ShipmentSpec(AppFixture app) : base(app.Host)
    {
        Alba = app.Host;

        // The original Track() waited 30 seconds; WolverineSpec waits five by default
        Timeout = 30.Seconds();
    }

    public IAlbaHost Alba { get; }

    /// <summary>The Polecat store. (CritterStackSpec.EventStore is the event store.)</summary>
    public IDocumentStore Documents => Alba.DocumentStore();

    /// <summary>A clean slate per specification, as for the original tests.</summary>
    public async ValueTask InitializeAsync() => await Alba.ResetAllPolecatDataAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ---- arrange, through the real handlers ---------------------------------------------------

    private TrackedSessionConfiguration track() =>
        Alba.TrackActivity().IncludeExternalTransports().Timeout(30.Seconds());

    [Given("shipment {id} is booked from Dallas to Austin with acme")]
    public async Task GivenABookedShipment(Guid id)
        => _booking = await track().InvokeMessageAndWaitAsync(new BookShipment(id, "Dallas", "Austin", "acme", 12.5m));

    [Given("shipment {id} is cancelled because {reason}")]
    public Task GivenTheShipmentIsCancelled(Guid id, string reason)
        => track().InvokeMessageAndWaitAsync(new CancelShipment(id, reason));

    [Given("shipment {id} is delivered in Austin TX at {deliveredAt}")]
    public Task GivenTheShipmentIsDelivered(Guid id, DateTimeOffset deliveredAt)
        => track().InvokeMessageAndWaitAsync(new RecordCarrierScan(id, "Austin TX", "DELIVERED", deliveredAt));

    [Given("the delivery saga for shipment {id} is open")]
    public async Task GivenTheSagaIsOpen(Guid id)
        => SpecAssert.Fact(await theSaga(id) is not null, $"Expected the delivery saga for {id} to be open, but there is none");

    [Given("the delivery saga for shipment {id} has completed")]
    public async Task GivenTheSagaHasCompleted(Guid id)
        => SpecAssert.Fact(await theSaga(id) is null, $"Expected the delivery saga for {id} to have completed, but it is still open");

    /// <summary>
    /// The SLA timeout the booking scheduled, to deliver as an act: what the original's
    /// <c>PlayScheduledMessagesAsync</c> did, five days early. Not a step: the act it is handed to is.
    /// </summary>
    public DeliverySlaExpired TheScheduledSlaTimeout()
    {
        var scheduled = (_booking ?? throw new InvalidOperationException("No shipment has been booked yet."))
            .Scheduled.MessagesOf<DeliverySlaExpired>().ToArray();

        return scheduled.Length == 1
            ? scheduled[0]
            : throw new InvalidOperationException($"Expected the booking to schedule one DeliverySlaExpired, but it scheduled {scheduled.Length}.");
    }

    private async Task<ShipmentDeliverySaga?> theSaga(Guid id)
    {
        await using var session = Documents.QuerySession();
        return await session.LoadAsync<ShipmentDeliverySaga>(id);
    }

    // ---- act: a timeout coming due ------------------------------------------------------------

    /// <summary>
    /// <paramref name="timeout" /> is handled now, inline, as if its delay had passed: what the original's
    /// <c>PlayScheduledMessagesAsync</c> did, five days early.
    /// </summary>
    /// <remarks>
    /// Not <c>WhenReceived(timeout)</c>: a <c>TimeoutMessage</c> carries its own delay, so sending it just
    /// schedules it again for five days out, and the act "succeeds" having handled nothing.
    /// </remarks>
    [EventModelRole(SliceRole.Command, nameof(timeout))]
    public Task WhenTheTimeoutComesDue(DeliverySlaExpired timeout)
        => WhenTracked($"the {nameof(DeliverySlaExpired)} timeout for shipment {timeout.ShipmentId} comes due",
            () => Alba.MessageBus().InvokeAsync(timeout));

    // ---- act: HTTP --------------------------------------------------------------------------

    /// <summary>POST <paramref name="request" /> to <paramref name="url" />, waiting for every message it caused.</summary>
    /// <remarks>
    /// A wrapper over <see cref="WolverineSpec.WhenTracked" /> stamps nothing on the Event Model by itself, so it
    /// declares its own roles: the request it posts, and the HTTP route it posts to.
    /// </remarks>
    [EventModelRole(SliceRole.Command, nameof(request))]
    [EventModelTrigger(TriggerKind.Http, Route = nameof(url))]
    public Task WhenPosted(object request, string url)
        => WhenTracked($"{request.GetType().Name} is posted to {url}", async () =>
        {
            _lastResponse = await Alba.Scenario(x =>
            {
                x.Post.Json(request).ToUrl(url);

                // A refusal is an answer to assert on, not an exception
                x.IgnoreStatusCode();
            });
        });

    [Then("the response status is {status}")]
    public void ThenTheResponseStatusIs(int status)
        => SpecAssert.Check("status", _lastResponse?.Context.Response.StatusCode, status);

    [Then("the Location header is {location}")]
    public void ThenTheLocationIs(string location)
        => SpecAssert.Check("Location", _lastResponse?.Context.Response.Headers.Location.ToString(), location);

    /// <summary>The last response's body, read as JSON. Not a step: the assertion that follows it is.</summary>
    public T TheResponse<T>()
        => (_lastResponse ?? throw new InvalidOperationException("No request has been sent yet.")).ReadAsJson<T>()!;

    // ---- assert: what the act's tracked session saw ------------------------------------------

    [Then("a {messageType} is handled")]
    public void ThenHandled(Type messageType)
        => SpecAssert.Check($"{messageType.Name} handled", executed(messageType), 1);

    [Then("no {messageType} is handled")]
    public void ThenNotHandled(Type messageType)
        => SpecAssert.Check($"{messageType.Name} handled", executed(messageType), 0);

    // "The act succeeded" is Bobcat's own ThenTheActSucceeded() (bobcat#683): it fails on an act whose tracked
    // session threw anywhere or timed out (WolverineScenario captures both as the act's error) and on a refusal.

    private int executed(Type messageType)
        => LastAct.Session?.Executed.Envelopes().Count(x => x.Message?.GetType() == messageType) ?? 0;
}
