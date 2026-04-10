using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace Speakers;

public record RegisterSpeaker(string Email, string FirstName, string LastName, SpeakerType Type)
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
        var speakerId = Guid.NewGuid();
        var evt = new SpeakerRegistered(speakerId, command.Email, command.FirstName, command.LastName, command.Type);
        session.Events.StartStream<Speaker>(speakerId, evt);

        var speaker = new Speaker();
        speaker.Apply(evt);
        return speaker;
    }
}
