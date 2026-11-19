using JasperFx.Core.Reflection;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using SagaWorkflows.AgentLoop;
using SagaWorkflows.FanOutFanIn;
using SagaWorkflows.MoneyTransfer;
using SagaWorkflows.OrderApproval;
using SagaWorkflows.Polling;
using SagaWorkflows.TripBooking;
using Wolverine;
using Wolverine.Bobcat;
using Wolverine.Configuration.Capabilities;
using Wolverine.Persistence.Durability.DeadLetterManagement;
using Wolverine.Persistence.Sagas;
using Wolverine.Runtime;
using Wolverine.Tracking;

namespace SagaWorkflows.Specs;

/// <summary>
/// The base of every spec here. It acts on the SagaWorkflows application through Wolverine, treats a dead letter as an
/// outcome to judge rather than a failed act, and adds the steps the workflows need: the outside systems' state, the
/// undo stack, dead letters by saga, and what the service says about its sagas.
/// </summary>
public abstract class SagaWorkflowSpec : WolverineSpec, IAsyncLifetime
{
    private readonly SagaWorkflowsHost _app;

    protected SagaWorkflowSpec(SagaWorkflowsHost app) : base(app.Host)
    {
        _app = app;
        DeadLettersFailTheAct = false;

        // Retries, a dead letter, a fault back to the saga and an unwind, each in a transaction of its own
        Timeout = TimeSpan.FromSeconds(30);
    }

    public async ValueTask InitializeAsync() => await _app.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private T service<T>() where T : notnull => _app.Host.Services.GetRequiredService<T>();

    private IWolverineRuntime runtime => service<IWolverineRuntime>();

    // ---- the bank -----------------------------------------------------------------------------------------------------

    [Given("account {account} holds {balance}")]
    public void AccountHolds(string account, decimal balance) => service<Bank>().Open(account, balance);

    [Given("account {account} is closed")]
    public void AccountIsClosed(string account) => service<Bank>().ClosedAccounts.Add(account);

    [Given("the ledger refuses to record transfer {transferId}")]
    public void TheLedgerRefuses(Guid transferId) => service<Bank>().LedgerRefuses.Add(transferId);

    [Then("account {account} holds {balance}")]
    public void ThenAccountHolds(string account, decimal balance)
        => SpecAssert.Check("balance", service<Bank>().BalanceOf(account), balance);

    // ---- the travel providers -----------------------------------------------------------------------------------------

    private TravelProviders travel => service<TravelProviders>();

    [Given("the {booking} provider refuses {customer}")]
    public void ProviderRefuses(Booking booking, string customer) => travel.Refusals.Add((booking, customer));

    [Given("there are no cars for {customer}")]
    public void NoCarsFor(string customer) => travel.NoCars.Add(customer);

    [Given("the hotel has no rooms for {customer}")]
    public void HotelIsFull(string customer) => travel.FullHotels.Add(customer);

    [Given("the {booking} provider is down for {customer}")]
    public void ProviderIsDown(Booking booking, string customer) => travel.Outages.Add((booking, customer));

    [Given("the {booking} provider refuses to cancel for {customer}")]
    public void ProviderRefusesToCancel(Booking booking, string customer)
        => travel.CancellationRefusals.Add((booking, customer));

    [Then("{customer} holds no {booking} booking")]
    public void ThenNoBooking(string customer, Booking booking)
        => SpecAssert.Check(booking.ToString(), travel.HasReservation(booking, customer) ? "booked" : "not booked", "not booked");

    [Then("{customer} still holds a {booking} booking")]
    public void ThenStillBooked(string customer, Booking booking)
        => SpecAssert.Check(booking.ToString(), travel.HasReservation(booking, customer) ? "booked" : "not booked", "booked");

    // ---- the storefront, the reporting system, the file share and the agent's tools ----------------------------------

    [Given("the warehouse holds {quantity} {item}")]
    public void WarehouseHolds(int quantity, string item) => service<Storefront>().Stock(item, quantity);

    [Given("export {jobId} is ready after {polls} polls")]
    public void ExportIsReadyAfter(string jobId, int polls) => service<FakeReportingApi>().NotReadyFor[jobId] = polls;

    [Given("the reporting system is down for export {jobId}")]
    public void ReportingIsDown(string jobId) => service<FakeReportingApi>().Down.Add(jobId);

    [Given("{root} holds {count} files")]
    public void DirectoryHolds(string root, int count)
    {
        for (var i = 1; i <= count; i++) service<BackupStorage>().Add($"{root}/file-{i:00}.txt", i * 100);
    }

    [Given("{path} cannot be read")]
    public void FileCannotBeRead(string path) => service<BackupStorage>().Unreadable.Add(path);

    [Given("device {deviceId} cannot get an installation package")]
    public void DeviceIsBroken(string deviceId) => service<SagaWorkflows.ChildWorkflows.DevicePortal>().BrokenDevices.Add(deviceId);

    [Given("the {tool} tool fails for {argument}")]
    public void ToolFails(string tool, string argument) => service<ToolBox>().Broken.Add($"{tool}:{argument}");

    // ---- the undo stack -----------------------------------------------------------------------------------------------

    private async Task<TSaga?> loadSaga<TSaga>(Guid sagaId) where TSaga : Saga
    {
        await using var session = service<IDocumentStore>().QuerySession();
        return await session.LoadAsync<TSaga>(sagaId);
    }

    /// <summary>The steps whose undo messages are still on the saga's undo stack, oldest first</summary>
    [Then("the undo stack of saga {sagaId} holds {steps}")]
    public async Task ThenTheUndoStackHolds<TSaga>(Guid sagaId, params string[] steps) where TSaga : CompensatingSaga
    {
        var saga = await loadSaga<TSaga>(sagaId);
        SpecAssert.Check("undo stack", saga == null ? "(no saga)" : string.Join(", ", saga.UndoStack.Select(x => x.Tag)),
            string.Join(", ", steps));
    }

    /// <summary>The saga's last compensation sent the undo messages of these steps, in this order</summary>
    [Then("the last compensation of saga {sagaId} undid {steps}")]
    public async Task ThenTheLastCompensationUndid<TSaga>(Guid sagaId, params string[] steps) where TSaga : CompensatingSaga
    {
        var saga = await loadSaga<TSaga>(sagaId);
        var entry = saga?.CompensationLog.LastOrDefault();
        SpecAssert.Check("undone", entry == null ? "(no compensation)" : string.Join(", ", (entry.Undone ?? []).Select(x => x.Step)),
            string.Join(", ", steps));
    }

    /// <summary>
    /// The undo messages were handled in this order, and each one was sent only after the one before it was handled:
    /// a sequential unwind, not a batch
    /// </summary>
    [Then("{undoMessages} are handled one at a time, in that order")]
    public void ThenUndoneOneAtATime(params Type[] undoMessages)
    {
        var records = LastAct.Session?.AllRecordsInOrder() ?? [];
        int indexOf(Type type, MessageEventType kind)
            => Array.FindIndex(records, x => x.MessageEventType == kind && type.IsInstanceOfType(x.Message));

        var handled = undoMessages.Select(x => indexOf(x, MessageEventType.MessageSucceeded)).ToArray();
        SpecAssert.Fact(handled.All(x => x >= 0),
            $"Expected every undo message to be handled, but {string.Join(", ", undoMessages.Where((_, i) => handled[i] < 0).Select(x => x.Name))} never was");

        for (var i = 1; i < undoMessages.Length; i++)
        {
            var sent = indexOf(undoMessages[i], MessageEventType.Sent);
            SpecAssert.Fact(sent > handled[i - 1],
                $"Expected {undoMessages[i].Name} to be sent only after {undoMessages[i - 1].Name} was handled");
        }
    }

    // ---- dead letters and messages ------------------------------------------------------------------------------------

    /// <summary>The dead letters linked to the saga, by their message types: what CritterWatch puts on the saga's timeline</summary>
    [Then("the dead letters of saga {sagaId} are {messageTypes}")]
    public async Task ThenTheDeadLettersOfTheSagaAre(Guid sagaId, params Type[] messageTypes)
    {
        var results = await runtime.Storage.DeadLetters.QueryAsync(
            new DeadLetterEnvelopeQuery { SagaId = sagaId.ToString() }, CancellationToken.None);

        SpecAssert.Check("dead letters",
            string.Join(", ", results.Envelopes.Select(x => x.MessageType.Split('.', '+').Last()).Order()),
            string.Join(", ", messageTypes.Select(x => x.Name).Order()));
    }

    /// <summary>The act sent exactly <paramref name="times" /> messages of <paramref name="messageType" /></summary>
    [Then("exactly {times} {messageType} sent")]
    public void ThenSentExactly(Type messageType, int times)
        => SpecAssert.Check("sent", TheMessagesSent.Count(messageType.IsInstanceOfType), times);

    // ---- what the service says about its sagas ------------------------------------------------------------------------

    /// <summary>
    /// The service's description (what describe, saga-flow and CritterWatch read) lists the Compensate method of
    /// <typeparamref name="TSaga" /> for <paramref name="failedMessageType" /> as covering exactly these message types
    /// </summary>
    [Then("the Compensate method for {failedMessageType} covers {messageTypes}")]
    public async Task ThenTheCompensateMethodCovers<TSaga>(Type failedMessageType, params Type[] messageTypes)
        where TSaga : Saga
    {
        var capabilities = await ServiceCapabilities.ReadFrom(runtime, null, CancellationToken.None);

        var saga = capabilities.Sagas.SingleOrDefault(x => x.StateType.FullName == typeof(TSaga).FullName);
        var compensation = saga?.Compensations.FirstOrDefault(x => x.FailedMessageType.FullName == failedMessageType.FullName);

        SpecAssert.Fact(compensation is not null,
            $"Expected {typeof(TSaga).Name} to describe a Compensate method for {failedMessageType.Name}, but it describes " +
            (saga is null ? "no such saga" : string.Join(", ", saga.Compensations.Select(x => x.FailedMessageType.Name))));

        var covered = compensation?.Covers.Select(x => x.Name).Order(StringComparer.Ordinal).ToArray() ?? [];
        SpecAssert.Check("covers", string.Join(", ", covered),
            string.Join(", ", messageTypes.Select(x => x.NameInCode()).Order(StringComparer.Ordinal)));
    }

    /// <summary>
    /// The saga is among the instances of its type that nobody has written to since <paramref name="since" />, read
    /// through ISagaIdleDiagnostics by LastModified, as CritterWatch's idle saga counts are
    /// </summary>
    [Then("saga {sagaId} is among the sagas of its type idle since {since}")]
    public async Task ThenTheSagaIsIdleSince<TSaga>(Guid sagaId, DateTimeOffset since) where TSaga : Saga
    {
        var idle = (ISagaIdleDiagnostics)runtime.SagaStorage;
        var sagaType = typeof(TSaga).FullName!;

        var count = await idle.CountIdleSagasAsync(sagaType, since, null, CancellationToken.None);
        var page = await idle.QuerySagasByLastModifiedAsync(sagaType, SagaLastModifiedRange.Before(since),
            new JasperFx.Documents.DocumentQueryOptions(1, 50), CancellationToken.None);

        SpecAssert.Fact(count >= 1, $"Expected at least one idle {typeof(TSaga).Name}, but counted {count}");
        SpecAssert.Fact(page.Instances.Any(x => x.Identity.ToString() == sagaId.ToString()),
            $"Expected {sagaId} among the idle {typeof(TSaga).Name} sagas, but they are " +
            string.Join(", ", page.Instances.Select(x => x.Identity)));
    }
}
