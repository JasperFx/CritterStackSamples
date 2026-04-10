using FluentValidation;
using Marten;
using Wolverine.Http;

namespace BankAccountES;

public record EnrollClient(string Name, string Email)
{
    public class Validator : AbstractValidator<EnrollClient>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
        }
    }
}

public static class EnrollClientEndpoint
{
    [WolverinePost("/api/clients")]
    public static Client Post(EnrollClient command, IDocumentSession session)
    {
        var clientId = Guid.NewGuid();
        var evt = new ClientEnrolled(clientId, command.Name, command.Email);

        session.Events.StartStream<Client>(clientId, evt);

        var client = new Client();
        client.Apply(evt);
        return client;
    }
}
