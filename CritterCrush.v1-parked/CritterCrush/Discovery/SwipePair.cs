using System.Security.Cryptography;
using System.Text;

namespace CritterCrush.Discovery;

public record DogLiked(Guid SwipePairId, Guid SwiperDogId, Guid TargetDogId);

public record DogPassed(Guid SwipePairId, Guid SwiperDogId, Guid TargetDogId);

public record MutualMatchDetected(Guid SwipePairId, Guid Dog1Id, Guid Dog2Id);

/// <summary>
/// The Discovery write model: one stream per unordered pair of dogs, so both directions of
/// swiping land in one place and the mutual-match decision has everything it needs in one
/// aggregation. Registered as an Inline snapshot.
/// </summary>
public class SwipePair
{
    public Guid Id { get; set; }
    public List<Guid> LikedBy { get; set; } = [];
    public List<Guid> Dogs { get; set; } = [];
    public bool Matched { get; set; }

    /// <summary>
    /// The deterministic stream id for a pair: sort the two ids so (a,b) and (b,a) are the same
    /// stream, then hash. Computed in exactly one place — an inlined version of this at two call
    /// sites is how two directions of a swipe end up on two different streams.
    /// </summary>
    public static Guid IdFor(Guid one, Guid two)
    {
        var (first, second) = one.CompareTo(two) <= 0 ? (one, two) : (two, one);
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes($"{first:N}:{second:N}"));
        return new Guid(bytes);
    }

    public static SwipePair Create(DogLiked liked) => applyTo(new SwipePair { Id = liked.SwipePairId }, liked);

    public static SwipePair Create(DogPassed passed) => new()
    {
        Id = passed.SwipePairId,
        Dogs = [passed.SwiperDogId, passed.TargetDogId],
    };

    public void Apply(DogLiked liked) => applyTo(this, liked);

    public void Apply(DogPassed passed) => remember(passed.SwiperDogId, passed.TargetDogId);

    public void Apply(MutualMatchDetected _) => Matched = true;

    private static SwipePair applyTo(SwipePair pair, DogLiked liked)
    {
        pair.remember(liked.SwiperDogId, liked.TargetDogId);
        if (!pair.LikedBy.Contains(liked.SwiperDogId)) pair.LikedBy.Add(liked.SwiperDogId);
        return pair;
    }

    private void remember(params Guid[] dogs)
    {
        foreach (var dog in dogs)
        {
            if (!Dogs.Contains(dog)) Dogs.Add(dog);
        }
    }
}
