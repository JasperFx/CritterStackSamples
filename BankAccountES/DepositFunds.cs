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
    [WolverinePost("/api/accounts/{accountId}/deposits")]
    [AggregateHandler]
    public static (IResult, FundsDeposited) Post(DepositFunds command, Account account)
    {
        var newBalance = account.Balance + command.Amount;
        return (Results.NoContent(), new FundsDeposited(command.AccountId, command.Amount, newBalance));
    }
}
