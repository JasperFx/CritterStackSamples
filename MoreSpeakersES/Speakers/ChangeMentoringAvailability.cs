using Wolverine.Http;
using Wolverine.Marten;

namespace Speakers;

public record ChangeMentoringAvailability(Guid SpeakerId, bool IsAvailable, int MaxMentees, string? Focus);

public static class ChangeMentoringAvailabilityEndpoint
{
    [WolverinePut("/api/speakers/{speakerId}/mentoring")]
    [AggregateHandler]
    public static MentoringAvailabilityChanged Put(ChangeMentoringAvailability command, Speaker speaker)
    {
        return new MentoringAvailabilityChanged(command.SpeakerId, command.IsAvailable, command.MaxMentees, command.Focus);
    }
}
