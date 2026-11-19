using Wolverine.Persistence.Sagas;

namespace SagaWorkflows.MoneyTransfer;

// Rebuilt from Temporal's .NET "Saga" sample (samples-dotnet/src/Saga): withdraw from one account, deposit to
// another, then a third step that can fail. When a step fails for good, undo what already happened, newest first.
//
// The same messages drive two sagas: Transfer (a document) and EventSourcedTransfer (an event stream). The application
// runs one or the other (Sagas:EventSourced in Program.cs). [SagaIdentity] says which member is the saga id, because
// the {SagaName}Id convention would look for TransferId on one and EventSourcedTransferId on the other.

public record TransferMoney([property: SagaIdentity] Guid TransferId, string FromAccount, string ToAccount, decimal Amount);

/// <summary>A step of a transfer: something the bank is asked to do, which may fail there</summary>
public interface ITransferStep
{
    Guid TransferId { get; }
}

// The steps. Temporal calls these activities; here they are messages to ordinary handlers (Bank.cs), which could just
// as well live in another service
public record Withdraw([property: SagaIdentity] Guid TransferId, string Account, decimal Amount) : ITransferStep;
public record Deposit([property: SagaIdentity] Guid TransferId, string Account, decimal Amount) : ITransferStep;
public record RecordTransfer([property: SagaIdentity] Guid TransferId, string FromAccount, string ToAccount, decimal Amount)
    : ITransferStep;

// The bank's replies
public record MoneyWithdrawn([property: SagaIdentity] Guid TransferId);
public record MoneyDeposited([property: SagaIdentity] Guid TransferId);
public record TransferRecorded([property: SagaIdentity] Guid TransferId);

// The undo steps
public record RefundWithdrawal([property: SagaIdentity] Guid TransferId, string Account, decimal Amount);
public record ReverseDeposit([property: SagaIdentity] Guid TransferId, string Account, decimal Amount);

// How it ended
public record TransferCompleted([property: SagaIdentity] Guid TransferId);
public record TransferFailed([property: SagaIdentity] Guid TransferId, string Reason);
