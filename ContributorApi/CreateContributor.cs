using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace ContributorApi;

public record CreateContributor(string Name, string? PhoneCountryCode, string? PhoneNumber, string? PhoneExtension)
{
    public class Validator : AbstractValidator<CreateContributor>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(100);
        }
    }
}

public static class CreateContributorEndpoint
{
    public static async Task<ProblemDetails> ValidateAsync(CreateContributor command, IQuerySession session)
    {
        var exists = await session.Query<Contributor>().AnyAsync(c => c.Name == command.Name);
        if (exists)
            return new ProblemDetails { Detail = $"Contributor '{command.Name}' already exists", Status = 409 };
        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/contributors")]
    public static Contributor Post(CreateContributor command, IDocumentSession session)
    {
        var contributor = new Contributor
        {
            Name = command.Name,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        if (!string.IsNullOrEmpty(command.PhoneCountryCode) && !string.IsNullOrEmpty(command.PhoneNumber))
        {
            contributor.PhoneNumber = new PhoneNumber
            {
                CountryCode = command.PhoneCountryCode,
                Number = command.PhoneNumber,
                Extension = command.PhoneExtension,
            };
        }

        session.Store(contributor);
        return contributor;
    }
}
