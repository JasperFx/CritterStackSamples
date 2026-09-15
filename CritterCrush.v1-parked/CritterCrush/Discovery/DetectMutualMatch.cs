using Wolverine.Persistence.EventSourcing;

namespace CritterCrush.Discovery;

/// <summary>
/// The automation slice: triggered by the DogLiked event (forwarded by Marten after the swipe's
/// transaction commits — see SubscribeToEvent in Program.cs), never by a route. The decision
/// state is the same aggregate, aggregated for us by [WriteModel] — the attribute hides the
/// stream aggregation. Designed for at-least-once: a redelivered DogLiked meets Matched == true
/// and cleanly does nothing.
/// </summary>
public static class DetectMutualMatchHandler
{
    public static EventsToAppend Handle(DogLiked liked, [WriteModel] SwipePair pair)
    {
        if (pair.Matched) return [];
        if (pair.LikedBy.Count < 2) return [];

        return [new MutualMatchDetected(pair.Id, pair.LikedBy[0], pair.LikedBy[1])];
    }
}
