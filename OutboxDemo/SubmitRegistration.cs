using Marten;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace OutboxDemo;

public record SubmitRegistration(string EventId, string MemberId, decimal Payment);

public static class SubmitRegistrationEndpoint
{
    // Sad path: duplicate registration check
    public static async Task<ProblemDetails> ValidateAsync(
        SubmitRegistration command,
        IQuerySession session)
    {
        var duplicate = await session.Query<Registration>()
            .AnyAsync(r => r.MemberId == command.MemberId && r.EventId == command.EventId);

        if (duplicate)
            return new ProblemDetails
            {
                Detail = $"Duplicate registration for member {command.MemberId} at event {command.EventId}",
                Status = 409,
            };

        return WolverineContinue.NoProblems;
    }

    // [EmptyResponse] tells Wolverine that ALL return values are cascading messages,
    // not the HTTP response body. Wolverine will:
    // 1. Auto-persist the Registration (it extends Saga)
    // 2. Publish RegistrationSubmitted via the outbox
    // 3. Return 204 No Content
    [WolverinePost("/registration"), EmptyResponse]
    public static (Registration, RegistrationSubmitted) Post(
        SubmitRegistration command)
    {
        var registration = new Registration
        {
            Id = Guid.NewGuid(),
            RegistrationDate = DateTime.UtcNow,
            MemberId = command.MemberId,
            EventId = command.EventId,
            Payment = command.Payment,
        };

        return (
            registration,
            new RegistrationSubmitted(
                registration.Id,
                registration.RegistrationDate,
                registration.MemberId,
                registration.EventId,
                registration.Payment)
        );
    }
}
