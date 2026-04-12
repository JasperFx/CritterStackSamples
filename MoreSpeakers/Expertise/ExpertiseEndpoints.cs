using Marten;
using Wolverine.Http;

namespace Expertise;

public static class GetExpertiseCategoriesEndpoint
{
    [WolverineGet("/api/expertise")]
    public static Task<IReadOnlyList<ExpertiseCategory>> Get(IQuerySession session, CancellationToken ct)
        => session.Query<ExpertiseCategory>()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Sector)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);
}

public static class SearchExpertiseEndpoint
{
    [WolverineGet("/api/expertise/search")]
    public static Task<IReadOnlyList<ExpertiseCategory>> Get(string q, IQuerySession session, CancellationToken ct)
        => session.Query<ExpertiseCategory>()
            .Where(c => c.IsActive && (
                c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.Skills.Any(s => s.Contains(q, StringComparison.OrdinalIgnoreCase))))
            .ToListAsync(ct);
}
