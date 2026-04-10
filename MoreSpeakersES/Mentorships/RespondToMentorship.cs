using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;
using Wolverine.Marten;

namespace Mentorships;

// --- Accept ---

public record AcceptMentorship(Guid MentorshipId, string? ResponseMessage);

public static class AcceptMentorshipEndpoint
{
    public static ProblemDetails Validate(AcceptMentorship command, Mentorship mentorship)
    {
        if (mentorship.Status != MentorshipStatus.Pending)
            return new ProblemDetails { Detail = $"Cannot accept a mentorship in '{mentorship.Status}' status", Status = 400 };
        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/mentorships/{mentorshipId}/accept")]
    [AggregateHandler]
    public static MentorshipAccepted Post(AcceptMentorship command, Mentorship mentorship)
    {
        return new MentorshipAccepted(command.MentorshipId, command.ResponseMessage);
    }
}

// --- Decline ---

public record DeclineMentorship(Guid MentorshipId, string? ResponseMessage);

public static class DeclineMentorshipEndpoint
{
    public static ProblemDetails Validate(DeclineMentorship command, Mentorship mentorship)
    {
        if (mentorship.Status != MentorshipStatus.Pending)
            return new ProblemDetails { Detail = $"Cannot decline a mentorship in '{mentorship.Status}' status", Status = 400 };
        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/mentorships/{mentorshipId}/decline")]
    [AggregateHandler]
    public static MentorshipDeclined Post(DeclineMentorship command, Mentorship mentorship)
    {
        return new MentorshipDeclined(command.MentorshipId, command.ResponseMessage);
    }
}

// --- Complete ---

public record CompleteMentorship(Guid MentorshipId);

public static class CompleteMentorshipEndpoint
{
    public static ProblemDetails Validate(CompleteMentorship command, Mentorship mentorship)
    {
        if (mentorship.Status != MentorshipStatus.Active)
            return new ProblemDetails { Detail = "Only active mentorships can be completed", Status = 400 };
        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/mentorships/{mentorshipId}/complete")]
    [AggregateHandler]
    public static MentorshipCompleted Post(CompleteMentorship command, Mentorship mentorship)
    {
        return new MentorshipCompleted(command.MentorshipId);
    }
}
