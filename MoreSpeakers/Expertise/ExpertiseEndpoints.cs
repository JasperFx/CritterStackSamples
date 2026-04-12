using Marten;
using Marten.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace Expertise;

public static class GetExpertiseCategoriesEndpoint
{
    [WolverineGet("/api/expertise")]
    
    // It's an imperfect world. I've never been able to come up with a syntax
    // option that would eliminate the need for this attribute that isn't as ugly
    // as using the attribute, so ¯\_(ツ)_/¯
    [ProducesResponseType<ExpertiseCategory[]>(200, "application/json")]
    public static Task Get(IQuerySession session, HttpContext context)
        => session.Query<ExpertiseCategory>()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Sector)
            .ThenBy(c => c.Name)
            .WriteArray(context);
}

public static class SearchExpertiseEndpoint
{
    [WolverineGet("/api/expertise/search")]
    public static Task Get(string q, IQuerySession session, HttpContext context)
        => session.Query<ExpertiseCategory>()
            .Where(c => c.IsActive && (
                c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.Skills.Any(s => s.Contains(q, StringComparison.OrdinalIgnoreCase))))
            .WriteArray(context);
}
