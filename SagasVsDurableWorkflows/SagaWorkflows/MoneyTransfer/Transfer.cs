using JasperFx;
using Wolverine;

namespace SagaWorkflows.MoneyTransfer;

// Rebuilt from Temporal's .NET "Saga" sample (samples-dotnet/src/Saga): withdraw from one account, deposit to
// another, then a third step that can fail. When a step fails for good, undo what already happened, newest first.

public record TransferMoney(Guid TransferId, string FromAccount, string ToAccount, decimal Amount);

// The steps. Temporal calls these activities; here they are messages to ordinary handlers (Bank.cs), which could just
// as well live in another service
public record Withdraw(Guid TransferId, string Account, decimal Amount);
public record Deposit(Guid TransferId, string Account, decimal Amount);
public record RecordTransfer(Guid TransferId, string FromAccount, string ToAccount, decimal Amount);

public record MoneyWithdrawn(Guid TransferId);
public record MoneyDeposited(Guid TransferId);
public record TransferRecorded(Guid TransferId);

// The undo steps
public record RefundWithdrawal(Guid TransferId, string Account, decimal Amount);
public record ReverseDeposit(Guid TransferId, string Account, decimal Amount);

// How it ended
public record TransferCompleted(Guid TransferId);
public record TransferFailed(Guid TransferId, string Reason);

public class Transfer : CompensatingSaga, IRevisioned
{
    public Guid Id { get; set; }
    public int Version { get; set; }

    public string FromAccount { get; set; } = "";
    public string ToAccount { get; set; } = "";
    public decimal Amount { get; set; }

    // The saga's state is the compensation stack: what has actually happened so far
    public bool Withdrawn { get; set; }
    public bool Deposited { get; set; }

    public static (Transfer, Withdraw) Start(TransferMoney command)
        => (new Transfer
            {
                Id = command.TransferId,
                FromAccount = command.FromAccount,
                ToAccount = command.ToAccount,
                Amount = command.Amount
            },
            new Withdraw(command.TransferId, command.FromAccount, command.Amount));

    public Deposit Handle(MoneyWithdrawn _)
    {
        Withdrawn = true;
        return new Deposit(Id, ToAccount, Amount);
    }

    public RecordTransfer Handle(MoneyDeposited _)
    {
        Deposited = true;
        return new RecordTransfer(Id, FromAccount, ToAccount, Amount);
    }

    public TransferCompleted Handle(TransferRecorded _)
    {
        MarkCompleted();
        return new TransferCompleted(Id);
    }

    // A step failed for good (after its retries, or at once for a [TerminalFailure]). Wolverine brings the step's
    // Fault<T> back to this saga because these methods exist. One method per step: a Compensate for a step this saga
    // *sends* has to name the concrete message type
    public OutgoingMessages Compensate(Withdraw failed, ExceptionInfo ex) => undo(ex);
    public OutgoingMessages Compensate(Deposit failed, ExceptionInfo ex) => undo(ex);
    public OutgoingMessages Compensate(RecordTransfer failed, ExceptionInfo ex) => undo(ex);

    private OutgoingMessages undo(ExceptionInfo ex)
    {
        // Newest first, like Temporal's reversed list of compensations. These go out together in one outbox
        // transaction; they don't wait on each other (see TripBooking for undo steps that do)
        var messages = new OutgoingMessages();
        if (Deposited) messages.Add(new ReverseDeposit(Id, ToAccount, Amount));
        if (Withdrawn) messages.Add(new RefundWithdrawal(Id, FromAccount, Amount));
        messages.Add(new TransferFailed(Id, ex.Message));

        return messages;
    }
}
