using Marten;
using Wolverine.Http;
using Wolverine.Persistence;

namespace Speakers;

public static class GetSpeakersEndpoint
{
    [WolverineGet("/api/speakers")]
    public static Task<IReadOnlyList<Speaker>> Get(IQuerySession session, CancellationToken ct)
        => session.Query<Speaker>().OrderBy(s => s.LastName).ToListAsync(ct);
}

public static class GetSpeakerByIdEndpoint
{
    [WolverineGet("/api/speakers/{id}")]
    public static Speaker? Get(Guid id, [Entity] Speaker? speaker) => speaker;
}

public static class GetAvailableMentorsEndpoint
{
    [WolverineGet("/api/speakers/mentors")]
    public static Task<IReadOnlyList<Speaker>> Get(IQuerySession session, CancellationToken ct)
        => session.Query<Speaker>()
            .Where(s => s.IsAvailableForMentoring && s.Type == SpeakerType.Experienced)
            .OrderBy(s => s.LastName)
            .ToListAsync(ct);
}

public static class SearchSpeakersByExpertiseEndpoint
{
    [WolverineGet("/api/speakers/search")]
    public static Task<IReadOnlyList<Speaker>> Get(string expertise, IQuerySession session, CancellationToken ct)
        => session.Query<Speaker>()
            .Where(s => s.Expertise.Contains(expertise))
            .ToListAsync(ct);
}
