using Wolverine;
using Wolverine.Http;
using Wolverine.Persistence.EventSourcing;

namespace CritterCrush.Discovery;

/// <summary>The command, addressed to the pair's stream via its SwipePairId member.</summary>
public record SwipeOnDog(Guid SwipePairId, Guid SwiperDogId, Guid TargetDogId, bool Liked);

public record SwipeRequest(Guid SwiperDogId, Guid TargetDogId, bool Liked);

public record SwipeReceipt(Guid SwipePairId);

public static class SwipeOnDogEndpoint
{
    /// <summary>
    /// Pure translation at the edge: compute the pair's deterministic stream id once, here, and
    /// cascade the command through the outbox.
    /// </summary>
    [WolverinePost("/api/discovery/swipes")]
    public static (SwipeReceipt, SwipeOnDog) Post(SwipeRequest request)
    {
        var pairId = SwipePair.IdFor(request.SwiperDogId, request.TargetDogId);
        return (new SwipeReceipt(pairId),
            new SwipeOnDog(pairId, request.SwiperDogId, request.TargetDogId, request.Liked));
    }
}

public static class SwipeOnDogHandler
{
    /// <summary>
    /// A state-change slice decides only "is this swipe recordable" — it never decides what a
    /// like *leads to*. The mutual-match consequence is the DetectMutualMatch automation,
    /// triggered by the DogLiked event this emits. The nullable [WriteModel] parameter is the
    /// maybe-new-stream shape: the first swipe of a pair starts the stream.
    /// </summary>
    public static EventsToAppend Handle(SwipeOnDog command, [WriteModel] SwipePair? pair)
    {
        if (pair is not null && pair.LikedBy.Contains(command.SwiperDogId))
        {
            // Recorded already; swiping twice is a no-op, not an error.
            return [];
        }

        object swipe = command.Liked
            ? new DogLiked(command.SwipePairId, command.SwiperDogId, command.TargetDogId)
            : new DogPassed(command.SwipePairId, command.SwiperDogId, command.TargetDogId);

        return [swipe];
    }
}
