using Marten;
using Wolverine.Http;
using Wolverine.Persistence;

namespace ContributorApi;

public static class GetContributorsEndpoint
{
    [WolverineGet("/api/contributors")]
    public static Task<IReadOnlyList<Contributor>> Get(IQuerySession session, CancellationToken ct)
        => session.Query<Contributor>().OrderBy(c => c.Name).ToListAsync(ct);
}

public static class GetContributorByIdEndpoint
{
    [WolverineGet("/api/contributors/{id}")]
    public static Contributor? Get(int id, [Entity] Contributor? contributor) => contributor;
}
