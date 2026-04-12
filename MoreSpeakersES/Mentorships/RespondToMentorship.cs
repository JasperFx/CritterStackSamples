using Marten.Events;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;
using Wolverine.Http.Marten;

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
    public static void Post(AcceptMentorship command, [WriteAggregate] IEventStream<Mentorship> stream)
    {
        stream.AppendOne(new MentorshipAccepted(command.MentorshipId, command.ResponseMessage));
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
    public static void Post(DeclineMentorship command, [WriteAggregate] IEventStream<Mentorship> stream)
    {
        stream.AppendOne(new MentorshipDeclined(command.MentorshipId, command.ResponseMessage));
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
    public static void Post(CompleteMentorship command, [WriteAggregate] IEventStream<Mentorship> stream)
    {
        stream.AppendOne(new MentorshipCompleted(command.MentorshipId));
    }
}
