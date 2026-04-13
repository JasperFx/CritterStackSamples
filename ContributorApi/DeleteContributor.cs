using Marten;
using Wolverine.Http;
using Wolverine.Persistence;

namespace ContributorApi;

public static class DeleteContributorEndpoint
{
    [WolverineDelete("/api/contributors/{id}")]
    public static void Delete(int id, [Entity(Required = true)] Contributor contributor, IDocumentSession session)
    {
        session.Delete(contributor);
    }
}
