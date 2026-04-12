using FluentValidation;
using Marten;
using BookingMonolith;
using Wolverine.Http;

namespace Passenger;

public record CreatePassenger(string Name, string PassportNumber, PassengerType Type, int Age)
{
    public class Validator : AbstractValidator<CreatePassenger>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.PassportNumber).NotEmpty();
        }
    }
}

public static class CreatePassengerEndpoint
{
    [WolverinePost("/api/passengers")]
    public static (Passenger, PassengerCreated) Post(CreatePassenger command, IDocumentSession session)
    {
        var passenger = new Passenger
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            PassportNumber = command.PassportNumber,
            Type = command.Type,
            Age = command.Age,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        session.Store(passenger);
        return (passenger, new PassengerCreated(passenger.Id, passenger.Name));
    }
}

/// <summary>
/// Handles UserCreated from the Identity module — auto-creates a passenger stub.
/// Replaces: MassTransit IConsumer + RegisterNewUserHandler
/// </summary>
public static class UserCreatedHandler
{
    public static void Handle(UserCreated message, IDocumentSession session)
    {
        session.Store(new Passenger
        {
            Id = message.UserId,
            Name = $"{message.FirstName} {message.LastName}",
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }
}
