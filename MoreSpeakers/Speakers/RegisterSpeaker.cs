using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace Speakers;

public record RegisterSpeaker(
    string Email,
    string FirstName,
    string LastName,
    SpeakerType Type,
    string? Bio = null,
    string? Goals = null,
    List<string>? Expertise = null
)
{
    public class Validator : AbstractValidator<RegisterSpeaker>
    {
        public Validator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.FirstName).NotEmpty();
            RuleFor(x => x.LastName).NotEmpty();
        }
    }
}

public static class RegisterSpeakerEndpoint
{
    public static async Task<ProblemDetails> ValidateAsync(RegisterSpeaker command, IQuerySession session)
    {
        var exists = await session.Query<Speaker>().AnyAsync(s => s.Email == command.Email);
        if (exists)
            return new ProblemDetails { Detail = $"Speaker with email '{command.Email}' already registered", Status = 409 };

        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/speakers")]
    public static Speaker Post(RegisterSpeaker command, IDocumentSession session)
    {
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Email = command.Email,
            FirstName = command.FirstName,
            LastName = command.LastName,
            Type = command.Type,
            Bio = command.Bio,
            Goals = command.Goals,
            Expertise = command.Expertise ?? [],
            CreatedAt = DateTimeOffset.UtcNow,
        };

        session.Store(speaker);
        return speaker;
    }
}
