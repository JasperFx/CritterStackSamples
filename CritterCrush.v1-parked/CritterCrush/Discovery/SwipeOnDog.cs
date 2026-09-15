using JasperFx;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;
using Wolverine.Persistence.EventSourcing;

namespace CritterCrush.Discovery;

public record SwipeRequest(Guid SwiperDogId, Guid TargetDogId, bool Liked)
{
    /// <summary>
    /// The pair's deterministic stream identity, computed once, here — [Identity] is what lets
    /// [WriteModel] load the right stream straight off the request body, so the endpoint needs
    /// no translation hop and no injected session.
    /// </summary>
    [Identity]
    public Guid SwipePairId => SwipePair.IdFor(SwiperDogId, TargetDogId);
}

public record SwipeReceipt(Guid SwipePairId);

public static class SwipeOnDogEndpoint
{
    /// <summary>Shape validation belongs on the railway, not in the decision.</summary>
    public static ProblemDetails Validate(SwipeRequest request)
        => request.SwiperDogId == request.TargetDogId
            ? new ProblemDetails { Detail = "A dog cannot swipe on itself", Status = 400 }
            : WolverineContinue.NoProblems;

    /// <summary>
    /// The endpoint IS the handler: decides only "is this swipe recordable" — the mutual-match
    /// consequence is the DetectMutualMatch automation, triggered by the DogLiked event this
    /// appends. The nullable [WriteModel] parameter is the maybe-new-stream shape: the first
    /// swipe of a pair starts the stream.
    /// </summary>
    [WolverinePost("/api/discovery/swipes")]
    public static (SwipeReceipt, EventsToAppend) Post(SwipeRequest request, [WriteModel] SwipePair? pair)
    {
        var receipt = new SwipeReceipt(request.SwipePairId);

        if (pair is not null && pair.LikedBy.Contains(request.SwiperDogId))
        {
            // Recorded already; swiping twice is a no-op, not an error.
            return (receipt, []);
        }

        object swipe = request.Liked
            ? new DogLiked(request.SwipePairId, request.SwiperDogId, request.TargetDogId)
            : new DogPassed(request.SwipePairId, request.SwiperDogId, request.TargetDogId);

        return (receipt, [swipe]);
    }
}
