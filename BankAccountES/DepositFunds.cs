using FluentValidation;
using Wolverine.Http;
using Wolverine.Marten;

namespace BankAccountES;

public record DepositFunds(Guid AccountId, decimal Amount)
{
    public class Validator : AbstractValidator<DepositFunds>
    {
        public Validator()
        {
            RuleFor(x => x.AccountId).NotEmpty();
            RuleFor(x => x.Amount).GreaterThan(0);
        }
    }
}

public static class DepositFundsEndpoint
{
    // [AggregateHandler] loads the Account from the event stream,
    // appends the returned event, and commits — all in one transaction
    [WolverinePost("/api/accounts/{accountId}/deposits")]
    [AggregateHandler]
    public static FundsDeposited Post(DepositFunds command, Account account)
    {
        var newBalance = account.Balance + command.Amount;
        return new FundsDeposited(command.AccountId, command.Amount, newBalance);
    }
}
