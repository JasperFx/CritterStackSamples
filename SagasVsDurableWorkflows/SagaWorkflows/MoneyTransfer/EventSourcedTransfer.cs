using Wolverine;

namespace SagaWorkflows.MoneyTransfer;

// The same money transfer as an event-sourced saga, written with Apply methods. Its state is its own event stream in
// Marten, so its whole history can be read back, and so can its state at any step.
//
// - Start returns events and messages, never the saga: the starting state comes only from Apply(TransferStarted).
// - Handle and Compensate return events (appended to this saga's stream) and messages (sent through the outbox).
// - The undo stack is kept as Wolverine's own events on the stream, so the history shows every push.
// - The steps go out in OutgoingMessages, which no signature shows, but they have handlers in this application, so
//   Wolverine still knows that Compensate(ITransferStep, ...) covers Withdraw, Deposit and RecordTransfer.

public record TransferStarted(Guid TransferId, string FromAccount, string ToAccount, decimal Amount);
public record FundsWithdrawn;
public record FundsDeposited;
public record TransferUndone(string Reason);

public class EventSourcedTransfer : EventSourcedSaga
{
    public Guid Id { get; set; }
    public string FromAccount { get; set; } = "";
    public string ToAccount { get; set; } = "";
    public decimal Amount { get; set; }

    public bool Withdrawn { get; set; }
    public bool Deposited { get; set; }
    public string? UndoneBecause { get; set; }

    public static (TransferStarted, OutgoingMessages) Start(TransferMoney command)
        => (new TransferStarted(command.TransferId, command.FromAccount, command.ToAccount, command.Amount),
            [new Withdraw(command.TransferId, command.FromAccount, command.Amount)]);

    public (FundsWithdrawn, OutgoingMessages) Handle(MoneyWithdrawn withdrawn)
    {
        PushUndo(new RefundWithdrawal(Id, FromAccount, Amount), "withdrawal");
        ConfirmUndo("withdrawal");
        return (new FundsWithdrawn(), [new Deposit(Id, ToAccount, Amount)]);
    }

    public (FundsDeposited, OutgoingMessages) Handle(MoneyDeposited deposited)
    {
        PushUndo(new ReverseDeposit(Id, ToAccount, Amount), "deposit");
        ConfirmUndo("deposit");
        return (new FundsDeposited(), [new RecordTransfer(Id, FromAccount, ToAccount, Amount)]);
    }

    // Wolverine's SagaCompleted event ends the saga. The stream and the snapshot stay
    public (SagaCompleted, OutgoingMessages) Handle(TransferRecorded recorded)
        => (new SagaCompleted(), [new TransferCompleted(Id)]);

    public (TransferUndone, OutgoingMessages) Compensate(ITransferStep failed, ExceptionInfo ex)
        => (new TransferUndone(ex.Message), [..UndoMessages(), new TransferFailed(Id, ex.Message)]);

    // ---- the state, rebuilt from the stream -----------------------------------------------------------------------

    public void Apply(TransferStarted e)
    {
        Id = e.TransferId;
        FromAccount = e.FromAccount;
        ToAccount = e.ToAccount;
        Amount = e.Amount;
    }

    public void Apply(FundsWithdrawn e) => Withdrawn = true;

    public void Apply(FundsDeposited e) => Deposited = true;

    public void Apply(TransferUndone e) => UndoneBecause = e.Reason;
}
