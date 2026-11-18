using SagaWorkflows.MoneyTransfer;
using Wolverine;
using Wolverine.Tracking;

namespace SagaWorkflows.Tests;

public class MoneyTransferTests(AppFixture fixture) : WorkflowContext(fixture)
{
    private Bank Bank => Service<Bank>();

    private (string from, string to) openAccounts(decimal fromBalance = 100m)
    {
        var from = $"from-{Guid.NewGuid():N}";
        var to = $"to-{Guid.NewGuid():N}";
        Bank.Open(from, fromBalance);
        Bank.Open(to, 0m);
        return (from, to);
    }

    [Fact]
    public async Task a_transfer_runs_every_step_and_completes()
    {
        var (from, to) = openAccounts();
        var id = Guid.NewGuid();

        var session = await Run(new TransferMoney(id, from, to, 40m));

        session.Executed.SingleMessage<TransferCompleted>().TransferId.ShouldBe(id);
        Bank.BalanceOf(from).ShouldBe(60m);
        Bank.BalanceOf(to).ShouldBe(40m);
        (await LoadSaga<Transfer>(id)).ShouldBeNull();
    }

    [Fact]
    public async Task a_failed_withdrawal_has_nothing_to_undo()
    {
        var (from, to) = openAccounts(fromBalance: 10m);
        var id = Guid.NewGuid();

        var session = await Run(new TransferMoney(id, from, to, 40m));

        // A terminal failure: one attempt, no retries
        session.AllRecordsInOrder(MessageEventType.ExecutionStarted).Count(x => x.Message is Withdraw).ShouldBe(1);
        session.Executed.SingleMessage<TransferFailed>().Reason.ShouldContain("insufficient funds");
        session.Sent.MessagesOf<RefundWithdrawal>().ShouldBeEmpty();
        session.Sent.MessagesOf<ReverseDeposit>().ShouldBeEmpty();

        var transfer = await LoadSaga<Transfer>(id);
        transfer!.Status.ShouldBe(SagaStatus.Compensated);
        Bank.BalanceOf(from).ShouldBe(10m);
    }

    [Fact]
    public async Task a_failed_deposit_refunds_the_withdrawal()
    {
        var (from, to) = openAccounts();
        Bank.ClosedAccounts.Add(to);
        var id = Guid.NewGuid();

        var session = await Run(new TransferMoney(id, from, to, 40m));

        session.Executed.SingleMessage<RefundWithdrawal>().Account.ShouldBe(from);
        session.Sent.MessagesOf<ReverseDeposit>().ShouldBeEmpty();
        Bank.BalanceOf(from).ShouldBe(100m);
        Bank.BalanceOf(to).ShouldBe(0m);
    }

    [Fact]
    public async Task a_last_step_that_keeps_failing_undoes_both_earlier_steps_newest_first()
    {
        var (from, to) = openAccounts();
        var id = Guid.NewGuid();
        Bank.LedgerRefuses.Add(id);

        var session = await Run(new TransferMoney(id, from, to, 40m));

        // Not terminal: three attempts in all, then the fault comes back to the saga
        session.AllRecordsInOrder(MessageEventType.ExecutionStarted)
            .Where(x => x.Message is RecordTransfer).Select(x => x.AttemptNumber).Distinct().Count().ShouldBe(3);

        var undo = session.Sent.AllMessages().Where(x => x is ReverseDeposit or RefundWithdrawal).ToArray();
        undo.Select(x => x.GetType()).ShouldBe([typeof(ReverseDeposit), typeof(RefundWithdrawal)]);

        Bank.BalanceOf(from).ShouldBe(100m);
        Bank.BalanceOf(to).ShouldBe(0m);

        var transfer = await LoadSaga<Transfer>(id);
        transfer!.Status.ShouldBe(SagaStatus.Compensated);
        transfer.CompensationLog.ShouldHaveSingleItem().ExceptionType.ShouldBe(typeof(LedgerUnavailableException).FullName);
    }

    // The saga's methods are pure functions, so the decisions can be tested with no host at all
    [Fact]
    public void undo_only_covers_the_steps_that_happened()
    {
        var transfer = new Transfer { Id = Guid.NewGuid(), FromAccount = "a", ToAccount = "b", Amount = 5m };
        transfer.Handle(new MoneyWithdrawn(transfer.Id));

        var undo = transfer.Compensate(new Deposit(transfer.Id, "b", 5m),
            new ExceptionInfo("AccountClosedException", "closed", null, []));

        undo.Select(x => x.GetType()).ShouldBe([typeof(RefundWithdrawal), typeof(TransferFailed)]);
    }
}
