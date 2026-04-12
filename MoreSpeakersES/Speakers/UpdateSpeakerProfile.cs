using FluentValidation;
using Marten.Events;
using Wolverine.Http;
using Wolverine.Http.Marten;
using Wolverine.Marten;

namespace Speakers;

public record UpdateSpeakerProfile(
    Guid SpeakerId,
    string FirstName,
    string LastName,
    string? Bio,
    string? Goals,
    string? HeadshotUrl,
    List<string> Expertise,
    List<SocialLink> SocialLinks
)
{
    public class Validator : AbstractValidator<UpdateSpeakerProfile>
    {
        public Validator()
        {
            RuleFor(x => x.FirstName).NotEmpty();
            RuleFor(x => x.LastName).NotEmpty();
        }
    }
}

public static class UpdateSpeakerProfileEndpoint
{
    [WolverinePut("/api/speakers/{speakerId}")]
    public static void Put(UpdateSpeakerProfile command, [WriteAggregate] IEventStream<Speaker> stream)
    {
        stream.AppendOne(new SpeakerProfileUpdated(
            command.SpeakerId, command.FirstName, command.LastName,
            command.Bio, command.Goals, command.HeadshotUrl,
            command.Expertise, command.SocialLinks));
    }
}
