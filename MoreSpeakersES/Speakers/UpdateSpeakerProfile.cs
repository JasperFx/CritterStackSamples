using FluentValidation;
using Wolverine.Http;
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
    [AggregateHandler]
    public static SpeakerProfileUpdated Put(UpdateSpeakerProfile command, Speaker speaker)
    {
        return new SpeakerProfileUpdated(
            command.SpeakerId, command.FirstName, command.LastName,
            command.Bio, command.Goals, command.HeadshotUrl,
            command.Expertise, command.SocialLinks);
    }
}
