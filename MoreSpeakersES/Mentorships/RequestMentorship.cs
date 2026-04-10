using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Mvc;
using Speakers;
using Wolverine.Http;
using Wolverine.Persistence;

namespace Mentorships;

public record RequestMentorship(
    Guid MentorId, Guid MenteeId, MentorshipType Type,
    string? RequestMessage, List<string>? FocusAreas, string? PreferredFrequency
)
{
    public class Validator : AbstractValidator<RequestMentorship>
    {
        public Validator()
        {
            RuleFor(x => x.MentorId).NotEmpty();
            RuleFor(x => x.MenteeId).NotEmpty();
            RuleFor(x => x.MentorId).NotEqual(x => x.MenteeId).WithMessage("Cannot mentor yourself");
        }
    }
}

public static class RequestMentorshipEndpoint
{
    public static ProblemDetails Validate(
        RequestMentorship command,
        [Entity("MentorId", OnMissing = OnMissing.ProblemDetailsWith400)] Speaker? mentor)
    {
        if (mentor is not null && !mentor.IsAvailableForMentoring)
            return new ProblemDetails { Detail = "Mentor is not available for mentoring", Status = 400 };
        return WolverineContinue.NoProblems;
    }

    [WolverinePost("/api/mentorships")]
    public static Mentorship Post(
        RequestMentorship command,
        [Entity("MentorId", Required = true)] Speaker mentor,
        [Entity("MenteeId", Required = true)] Speaker mentee,
        IDocumentSession session)
    {
        var mentorshipId = Guid.NewGuid();
        var evt = new MentorshipRequested(
            mentorshipId, mentor.Id, mentor.FullName,
            mentee.Id, mentee.FullName, command.Type,
            command.FocusAreas ?? [], command.RequestMessage, command.PreferredFrequency);

        session.Events.StartStream<Mentorship>(mentorshipId, evt);

        var mentorship = new Mentorship();
        mentorship.Apply(evt);
        return mentorship;
    }
}
