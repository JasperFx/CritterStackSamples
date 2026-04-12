using FluentValidation;
using Wolverine.Http;
using Wolverine.Marten;

namespace BankAccountES;

public record UpdateClient(Guid ClientId, string Name, string Email)
{
    public class Validator : AbstractValidator<UpdateClient>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
        }
    }
}

public static class UpdateClientEndpoint
{
    [WolverinePut("/api/clients/{clientId}")]
    [AggregateHandler]
    public static (IResult, ClientUpdated) Put(UpdateClient command, Client client)
    {
        return (Results.NoContent(), new ClientUpdated(command.ClientId, command.Name, command.Email));
    }
}
