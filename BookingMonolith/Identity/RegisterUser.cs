using FluentValidation;
using BookingMonolith;
using Wolverine.Http;

namespace Identity;

public class UserAccount
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public record RegisterUser(string Email, string FirstName, string LastName, string Password)
{
    public class Validator : AbstractValidator<RegisterUser>
    {
        public Validator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.FirstName).NotEmpty();
            RuleFor(x => x.LastName).NotEmpty();
            RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        }
    }
}

public static class RegisterUserEndpoint
{
    [WolverinePost("/api/identity/register")]
    public static (UserAccount, UserCreated) Post(RegisterUser command, Marten.IDocumentSession session)
    {
        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            Email = command.Email,
            FirstName = command.FirstName,
            LastName = command.LastName,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        session.Store(user);
        return (user, new UserCreated(user.Id, user.Email, user.FirstName, user.LastName));
    }
}
