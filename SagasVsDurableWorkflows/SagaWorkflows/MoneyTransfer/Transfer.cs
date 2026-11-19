using JasperFx;
using Wolverine;

namespace SagaWorkflows.MoneyTransfer;

// The money transfer as a document saga.
//
// - Each step's reply moves the transfer on, and pushes the message that undoes that step onto the saga's undo stack.
//   No Withdrawn / Deposited flags and no hand-written undo() method: the stack is the saga's record of what happened.
// - One Compensate method answers the failure of any step, because every step implements ITransferStep. Wolverine
//   files it under the Fault<T> of each concrete step it knows the saga sends (Withdraw, Deposit, RecordTransfer) and
//   asks for those faults.
// - An exact message type beats the interface, so a refused deposit gets its own explanation.
public class Transfer : CompensatingSaga, IRevisioned
{
    public Guid Id { get; set; }
    public string FromAccount { get; set; } = "";
    public string ToAccount { get; set; } = "";
    public decimal Amount { get; set; }

    public static (Transfer, Withdraw) Start(TransferMoney command)
        => (new Transfer
            {
                Id = command.TransferId,
                FromAccount = command.FromAccount,
                ToAccount = command.ToAccount,
                Amount = command.Amount
            },
            new Withdraw(command.TransferId, command.FromAccount, command.Amount));

    public Deposit Handle(MoneyWithdrawn withdrawn)
    {
        // The money left the account, so this undo is confirmed now, not when the deposit replies
        PushUndo(new RefundWithdrawal(Id, FromAccount, Amount), "withdrawal");
        ConfirmUndo("withdrawal");

        return new Deposit(Id, ToAccount, Amount);
    }

    public RecordTransfer Handle(MoneyDeposited deposited)
    {
        PushUndo(new ReverseDeposit(Id, ToAccount, Amount), "deposit");
        ConfirmUndo("deposit");

        return new RecordTransfer(Id, FromAccount, ToAccount, Amount);
    }

    public TransferCompleted Handle(TransferRecorded recorded)
    {
        MarkCompleted();
        return new TransferCompleted(Id);
    }

    // Any step that failed for good, after its retries or at once for a [TerminalFailure]: undo what already
    // happened, newest first, in one outbox transaction
    public OutgoingMessages Compensate(ITransferStep failed, ExceptionInfo ex)
        => [..UndoMessages(), new TransferFailed(Id, ex.Message)];

    // An exact type beats an interface
    public OutgoingMessages Compensate(Deposit failed, ExceptionInfo ex)
        => [..UndoMessages(), new TransferFailed(Id, $"The deposit to {failed.Account} was refused: {ex.Message}")];
}
