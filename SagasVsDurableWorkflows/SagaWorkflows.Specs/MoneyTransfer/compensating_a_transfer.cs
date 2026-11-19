using SagaWorkflows.MoneyTransfer;
using Wolverine;

namespace SagaWorkflows.Specs.MoneyTransfer;

// Temporal's money transfer as a document saga. Each step pushes its undo message as it happens; one Compensate method
// for ITransferStep answers the failure of any step and unwinds the stack newest first.

[BobcatFeature("A failed step undoes the transfer, newest first")]
[Collection(nameof(DocumentSagaCollection))]
public class compensating_a_transfer(DocumentSagas app) : SagaWorkflowSpec(app)
{
    private Guid TransferId { get; } = Guid.NewGuid();

    [BobcatSpec(typeof(TransferMoney))]
    public async Task a_transfer_runs_every_step_and_completes()
    {
        AccountHolds("alice", 500m);

        await WhenReceived(new TransferMoney(TransferId, "alice", "bob", 100m));

        ThenMessageSent(new TransferCompleted(TransferId));
        ThenNoMessageSent<TransferFailed>();
        ThenAccountHolds("alice", 400m);
        ThenAccountHolds("bob", 100m);
        await ThenNoSaga<Transfer>(TransferId);
    }

    // A [TerminalFailure] is not retried: one attempt, and the saga compensates at once
    [BobcatSpec(typeof(TransferMoney))]
    public async Task a_withdrawal_refused_for_insufficient_funds_fails_with_nothing_to_undo()
    {
        AccountHolds("alice", 50m);

        await WhenReceived(new TransferMoney(TransferId, "alice", "bob", 100m));

        ThenAttempted<Withdraw>(1);
        ThenDeadLettered<Withdraw>();
        await ThenTheSagaCompensated<Transfer, Withdraw, InsufficientFundsException>(TransferId);
        ThenMessageSent(new TransferFailed(TransferId, "Account alice has insufficient funds"));
        ThenNoMessageSent<RefundWithdrawal>();
        await ThenTheSagaStatusIs<Transfer>(TransferId, SagaStatus.Compensated);
    }

    // Not terminal: three attempts, then the step's Fault<RecordTransfer> comes back to the saga
    [BobcatSpec(typeof(TransferMoney))]
    public async Task a_transfer_the_ledger_cannot_record_is_reversed_newest_first()
    {
        AccountHolds("alice", 500m);
        TheLedgerRefuses(TransferId);

        await WhenReceived(new TransferMoney(TransferId, "alice", "bob", 100m));

        ThenAttempted<RecordTransfer>(3);
        await ThenTheSagaCompensated<Transfer, RecordTransfer, LedgerUnavailableException>(TransferId);
        await ThenTheLastCompensationUndid<Transfer>(TransferId, "deposit", "withdrawal");
        await ThenTheUndoStackHolds<Transfer>(TransferId);
        ThenMessageSent(new TransferFailed(TransferId, "The ledger is unavailable"));
        ThenAccountHolds("alice", 500m);
        ThenAccountHolds("bob", 0m);
    }

    // Compensate(Deposit, ExceptionInfo) is more specific than Compensate(ITransferStep, ExceptionInfo)
    [BobcatSpec(typeof(TransferMoney))]
    public async Task a_refused_deposit_is_compensated_by_the_method_for_its_exact_type()
    {
        AccountHolds("alice", 500m);
        AccountIsClosed("bob");

        await WhenReceived(new TransferMoney(TransferId, "alice", "bob", 100m));

        await ThenTheSagaCompensated<Transfer, Deposit, AccountClosedException>(TransferId);
        ThenMessageSent(new RefundWithdrawal(TransferId, "alice", 100m));
        ThenMessageSent(new TransferFailed(TransferId, "The deposit to bob was refused: Account bob is closed"));
        ThenNoMessageSent<ReverseDeposit>();
        ThenAccountHolds("alice", 500m);
    }

    // The failed step's dead letter carries the saga id, so a monitoring tool puts it on the saga's own timeline
    [BobcatSpec(typeof(TransferMoney))]
    public async Task the_dead_letter_of_the_failed_step_is_linked_to_the_transfer()
    {
        AccountHolds("alice", 500m);
        TheLedgerRefuses(TransferId);

        await WhenReceived(new TransferMoney(TransferId, "alice", "bob", 100m));

        await ThenTheDeadLettersOfTheSagaAre(TransferId, typeof(RecordTransfer));
    }

    // What describe and CritterWatch show: the concrete steps each Compensate method covers. Deposit is covered by both
    // methods; the exact one wins when a deposit fails
    [BobcatSpec]
    public async Task the_service_describes_which_steps_each_Compensate_method_covers()
    {
        await ThenTheCompensateMethodCovers<Transfer>(typeof(ITransferStep),
            typeof(Deposit), typeof(RecordTransfer), typeof(Withdraw));
        await ThenTheCompensateMethodCovers<Transfer>(typeof(Deposit), typeof(Deposit));
    }
}
