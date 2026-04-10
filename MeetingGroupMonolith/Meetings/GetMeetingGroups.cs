using Marten;
using Wolverine.Http;
using Wolverine.Persistence;

namespace Meetings;

public static class GetMeetingGroupsEndpoint
{
    [WolverineGet("/api/meeting-groups")]
    public static Task<IReadOnlyList<MeetingGroup>> Get(IQuerySession session)
        => session.Query<MeetingGroup>().ToListAsync();
}

public static class GetMeetingGroupByIdEndpoint
{
    [WolverineGet("/api/meeting-groups/{id}")]
    public static MeetingGroup? Get(Guid id, [Entity] MeetingGroup? group) => group;
}

public static class GetMeetingsEndpoint
{
    [WolverineGet("/api/meetings")]
    public static Task<IReadOnlyList<Meeting>> Get(IQuerySession session)
        => session.Query<Meeting>().OrderByDescending(m => m.TermStartDate).ToListAsync();
}
