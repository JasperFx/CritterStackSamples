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

    // Happy path: store the registration and cascade RegistrationSubmitted.
    // The return tuple's first element is the HTTP response body (Registration).
    // The second element is a cascading message — published via the Marten outbox
    // in the same transaction. No IMessageBus needed.
    [WolverinePost("/registration")]
    public static (Registration, RegistrationSubmitted) Post(
        SubmitRegistration command,
        IDocumentSession session)
    {
        var registration = new Registration
        {
            Id = Guid.NewGuid(),
            RegistrationDate = DateTime.UtcNow,
            MemberId = command.MemberId,
            EventId = command.EventId,
            Payment = command.Payment,
        };

        // Wolverine will automatically persist the new Registration
        // saga
        // session.Store(registration);

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
