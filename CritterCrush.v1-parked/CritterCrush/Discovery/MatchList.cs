using Marten;
using Marten.Events.Projections;
using Wolverine.Http;

namespace CritterCrush.Discovery;

/// <summary>
/// The Discovery read model: one document per dog, listing who it matched with. Fed by a
/// multi-stream projection — one MutualMatchDetected fans out to both dogs' documents — and
/// therefore Async: the daemon owns it, and reading it is eventually consistent.
/// </summary>
public class MatchList
{
    public Guid Id { get; set; }
    public List<Guid> MatchedDogIds { get; set; } = [];
    public int MatchCount { get; set; }
}

public class MatchListProjection : MultiStreamProjection<MatchList, Guid>
{
    public MatchListProjection()
    {
        Identities<MutualMatchDetected>(x => [x.Dog1Id, x.Dog2Id]);
    }

    public void Apply(MutualMatchDetected matched, MatchList list)
    {
        var other = matched.Dog1Id == list.Id ? matched.Dog2Id : matched.Dog1Id;
        if (list.MatchedDogIds.Contains(other)) return;

        list.MatchedDogIds.Add(other);
        list.MatchCount = list.MatchedDogIds.Count;
    }
}

public static class GetMatchesEndpoint
{
    /// <summary>The read half of a state-view slice stays boring: load the document, return it.</summary>
    [WolverineGet("/api/dogs/{dogId}/matches")]
    public static async Task<MatchList> Get(Guid dogId, IQuerySession session, CancellationToken ct)
        => await session.LoadAsync<MatchList>(dogId, ct) ?? new MatchList { Id = dogId };
}
