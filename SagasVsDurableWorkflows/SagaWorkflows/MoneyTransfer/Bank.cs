using System.Collections.Concurrent;
using Wolverine.ErrorHandling;

namespace SagaWorkflows.MoneyTransfer;

// Not worth retrying: Wolverine skips the retries and the saga compensates at once
[TerminalFailure]
public class InsufficientFundsException(string account) : Exception($"Account {account} has insufficient funds");

[TerminalFailure]
public class AccountClosedException(string account) : Exception($"Account {account} is closed");

// Worth retrying, then dead lettered and compensated
public class LedgerUnavailableException() : Exception("The ledger is unavailable");

/// <summary>A stand-in for the bank's systems, in memory</summary>
public class Bank
{
    private readonly ConcurrentDictionary<string, decimal> _balances = new();

    public ConcurrentBag<string> ClosedAccounts { get; } = [];

    // Transfers the ledger refuses to record, to make the last step fail
    public ConcurrentBag<Guid> LedgerRefuses { get; } = [];

    public void Reset()
    {
        _balances.Clear();
        ClosedAccounts.Clear();
        LedgerRefuses.Clear();
    }

    public void Open(string account, decimal balance) => _balances[account] = balance;

    public decimal BalanceOf(string account) => _balances.GetValueOrDefault(account);

    public void Withdraw(string account, decimal amount)
    {
        if (BalanceOf(account) < amount) throw new InsufficientFundsException(account);
        _balances.AddOrUpdate(account, -amount, (_, balance) => balance - amount);
    }

    public void Deposit(string account, decimal amount)
    {
        if (ClosedAccounts.Contains(account)) throw new AccountClosedException(account);
        _balances.AddOrUpdate(account, amount, (_, balance) => balance + amount);
    }

    public void Record(Guid transferId)
    {
        if (LedgerRefuses.Contains(transferId)) throw new LedgerUnavailableException();
    }
}

// The activities. Each is a plain handler that replies to the saga
public static class BankHandler
{
    public static MoneyWithdrawn Handle(Withdraw command, Bank bank)
    {
        bank.Withdraw(command.Account, command.Amount);
        return new MoneyWithdrawn(command.TransferId);
    }

    public static MoneyDeposited Handle(Deposit command, Bank bank)
    {
        bank.Deposit(command.Account, command.Amount);
        return new MoneyDeposited(command.TransferId);
    }

    public static TransferRecorded Handle(RecordTransfer command, Bank bank)
    {
        bank.Record(command.TransferId);
        return new TransferRecorded(command.TransferId);
    }

    // Undo steps. In a real bank these would need to be idempotent; so would Temporal's
    public static void Handle(RefundWithdrawal command, Bank bank) => bank.Deposit(command.Account, command.Amount);

    public static void Handle(ReverseDeposit command, Bank bank) => bank.Withdraw(command.Account, command.Amount);

    public static void Handle(TransferCompleted _) { }
    public static void Handle(TransferFailed _) { }
}
