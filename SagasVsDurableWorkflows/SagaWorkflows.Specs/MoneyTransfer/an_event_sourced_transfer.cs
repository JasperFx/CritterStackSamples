using Bobcat.CritterStack;
using SagaWorkflows.MoneyTransfer;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace SagaWorkflows.Specs.MoneyTransfer;

// The same money transfer as an event-sourced saga, on the same messages. Its stream is its history: read it a page at
// a time, or read the saga as it was at any step, with no audit table and no extra writes.

[BobcatFeature("An event-sourced transfer keeps its whole history")]
[Collection(nameof(EventSourcedSagaCollection))]
public class an_event_sourced_transfer(EventSourcedSagas app) : SagaWorkflowSpec(app)
{
    private Guid TransferId { get; } = Guid.NewGuid();

    [BobcatSpec(typeof(TransferMoney))]
    public async Task a_transfer_the_ledger_cannot_record_is_reversed_newest_first()
    {
        AccountHolds("alice", 500m);
        TheLedgerRefuses(TransferId);

        await WhenReceived(new TransferMoney(TransferId, "alice", "bob", 100m));

        await ThenTheSagaCompensated<EventSourcedTransfer, RecordTransfer, LedgerUnavailableException>(TransferId);
        ThenMessageSent(new ReverseDeposit(TransferId, "bob", 100m));
        ThenMessageSent(new RefundWithdrawal(TransferId, "alice", 100m));
        await ThenSaga<EventSourcedTransfer>(TransferId, Specify<EventSourcedTransfer>()
            .With(x => x.UndoneBecause, "The ledger is unavailable")
            .With(x => x.Status, SagaStatus.Compensated));
        ThenAccountHolds("alice", 500m);
    }

    // Completing appends Wolverine's SagaCompleted event. Nothing is deleted: the stream and the snapshot stay
    [BobcatSpec(typeof(TransferMoney))]
    public async Task a_completed_transfer_keeps_its_stream()
    {
        AccountHolds("alice", 500m);

        await WhenReceived(new TransferMoney(TransferId, "alice", "bob", 100m));

        ThenMessageSent(new TransferCompleted(TransferId));
        await ThenTheSagaIsCompleted<EventSourcedTransfer>(TransferId);
    }

    // The history CritterWatch shows on its saga timeline: the business events and Wolverine's own (the undo stack
    // here), in order
    [BobcatSpec]
    public async Task the_history_of_a_transfer_is_its_stream()
    {
        AccountHolds("alice", 500m);
        await GivenReceived(new TransferMoney(TransferId, "alice", "bob", 100m));

        await WhenTheSagaHistoryIsRead<EventSourcedTransfer>(TransferId, page: 1, pageSize: 4);

        ThenTheSagaHistoryIs(typeof(TransferStarted), typeof(FundsWithdrawn), typeof(SagaUndoPushed), typeof(SagaUndoConfirmed));
        ThenTheSagaHistoryHasInAll(8);
        await ThenTheSagaSupportsHistory<EventSourcedTransfer>();
    }

    // Time travel: the saga as it was right after the withdrawal, rebuilt from its stream by Marten
    [BobcatSpec]
    public async Task the_transfer_is_read_as_it_was_after_the_withdrawal()
    {
        AccountHolds("alice", 500m);
        await GivenReceived(new TransferMoney(TransferId, "alice", "bob", 100m));

        await WhenTheSagaIsReadAtVersion<EventSourcedTransfer>(TransferId, 2);

        ThenTheSagaWas(Specify<EventSourcedTransfer>()
            .With(x => x.Withdrawn, true)
            .With(x => x.Deposited, false)
            .With(x => x.CompletedAt, (DateTimeOffset?)null));
    }

    // Or by time: the saga as it was at the moment the money left alice's account
    [BobcatSpec]
    public async Task the_transfer_is_read_as_it_was_at_a_point_in_time()
    {
        AccountHolds("alice", 500m);
        await GivenReceived(new TransferMoney(TransferId, "alice", "bob", 100m));
        var withdrawnAt = await TheTimeOf(version: 2);

        await WhenTheSagaIsReadAsOf<EventSourcedTransfer>(TransferId, withdrawnAt, "when the money was withdrawn");

        ThenTheSagaWas(Specify<EventSourcedTransfer>()
            .With(x => x.Withdrawn, true)
            .With(x => x.Deposited, false));
    }

    /// <summary>When the event at this version of the transfer's stream was recorded. A query, not a step</summary>
    private async Task<DateTimeOffset> TheTimeOf(long version)
    {
        var events = await EventStores.FetchStreamAsync(Scenario.EventStore, TransferId);
        return events.Single(x => x.Version == version).Timestamp;
    }

    // Wolverine's compensation events are history like any other, so time travel shows the status at each step
    [BobcatSpec]
    public async Task the_transfer_is_read_as_it_was_while_it_was_being_compensated()
    {
        AccountHolds("alice", 500m);
        TheLedgerRefuses(TransferId);
        await WhenReceived(new TransferMoney(TransferId, "alice", "bob", 100m));

        await WhenTheSagaHistoryIsRead<EventSourcedTransfer>(TransferId, page: 2, pageSize: 4);
        ThenTheSagaHistoryIs(typeof(FundsDeposited), typeof(SagaUndoPushed), typeof(SagaUndoConfirmed), typeof(SagaCompensating));

        await WhenTheSagaIsReadAtVersion<EventSourcedTransfer>(TransferId, 8);

        ThenTheSagaWas(Specify<EventSourcedTransfer>().With(x => x.Status, SagaStatus.Compensating));
    }
}
