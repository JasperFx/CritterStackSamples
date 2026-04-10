using FluentValidation;
using Marten;
using Wolverine.Http;

namespace Administration;

public record ProposeNewMeetingGroup(string Name, string Description, string LocationCity, string LocationCountryCode, Guid ProposalUserId)
{
    public class Validator : AbstractValidator<ProposeNewMeetingGroup>
    {
        public Validator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.LocationCity).NotEmpty();
            RuleFor(x => x.LocationCountryCode).NotEmpty().Length(2);
        }
    }
}

public static class ProposeNewMeetingGroupEndpoint
{
    [WolverinePost("/api/administration/proposals")]
    public static MeetingGroupProposal Post(ProposeNewMeetingGroup command, IDocumentSession session)
    {
        var proposal = new MeetingGroupProposal
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            Description = command.Description,
            LocationCity = command.LocationCity,
            LocationCountryCode = command.LocationCountryCode,
            ProposalUserId = command.ProposalUserId,
            ProposalDate = DateTimeOffset.UtcNow,
        };

        session.Store(proposal);
        return proposal;
    }
}
