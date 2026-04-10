using FluentValidation;
using Marten;
using MeetingGroupMonolith;
using Wolverine.Http;
using Wolverine.Persistence;

namespace Meetings;

public record CreateMeeting(
    Guid MeetingGroupId,
    string Title,
    string Description,
    DateTime TermStartDate,
    DateTime TermEndDate,
    string LocationAddress,
    int? AttendeesLimit,
    decimal Fee
)
{
    public class Validator : AbstractValidator<CreateMeeting>
    {
        public Validator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.MeetingGroupId).NotEmpty();
            RuleFor(x => x.TermStartDate).LessThan(x => x.TermEndDate);
        }
    }
}

public static class CreateMeetingEndpoint
{
    [WolverinePost("/api/meetings")]
    public static Meeting Post(
        CreateMeeting command,
        [Entity("MeetingGroupId", Required = true)] MeetingGroup group,
        IDocumentSession session)
    {
        var meeting = new Meeting
        {
            Id = Guid.NewGuid(),
            MeetingGroupId = group.Id,
            Title = command.Title,
            Description = command.Description,
            TermStartDate = command.TermStartDate,
            TermEndDate = command.TermEndDate,
            LocationAddress = command.LocationAddress,
            AttendeesLimit = command.AttendeesLimit,
            Fee = command.Fee,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        session.Store(meeting);
        return meeting;
    }
}
