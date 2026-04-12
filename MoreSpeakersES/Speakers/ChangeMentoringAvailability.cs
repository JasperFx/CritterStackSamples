using Marten.Events;
using Wolverine.Http;
using Wolverine.Http.Marten;
using Wolverine.Marten;

namespace Speakers;

public record ChangeMentoringAvailability(Guid SpeakerId, bool IsAvailable, int MaxMentees, string? Focus);

public static class ChangeMentoringAvailabilityEndpoint
{
    [WolverinePut("/api/speakers/{speakerId}/mentoring")]
    public static void Put(ChangeMentoringAvailability command, [WriteAggregate] IEventStream<Speaker> stream)
    {
        stream.AppendOne(new MentoringAvailabilityChanged(command.SpeakerId, command.IsAvailable, command.MaxMentees, command.Focus));
    }
}
