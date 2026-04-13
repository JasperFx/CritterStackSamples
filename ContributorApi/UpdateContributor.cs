using FluentValidation;
using Marten;
using Wolverine.Http;
using Wolverine.Persistence;

namespace ContributorApi;

public record UpdateContributor(int ContributorId, string Name)
{
    public class Validator : AbstractValidator<UpdateContributor>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MinimumLength(2).MaximumLength(100);
        }
    }
}

public static class UpdateContributorEndpoint
{
    [WolverinePut("/api/contributors/{contributorId}")]
    public static Contributor Put(
        UpdateContributor command,
        [Entity("ContributorId", Required = true)] Contributor contributor,
        IDocumentSession session)
    {
        contributor.Name = command.Name;
        contributor.UpdatedAt = DateTimeOffset.UtcNow;

        session.Store(contributor);
        return contributor;
    }
}
