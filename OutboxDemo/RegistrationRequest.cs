using Marten;
using Microsoft.AspNetCore.Mvc;
using Wolverine;
using Wolverine.Http;

namespace OutboxDemo;

public record RegistrationRequest(string EventId, string MemberId, decimal Payment);

public static class RegistrationEndpoint
{
    // Sad path: duplicate registration check
    public static async Task<ProblemDetails> ValidateAsync(
        RegistrationRequest request,
        IQuerySession session)
    {
        var duplicate = await session.Query<Registration>()
            .AnyAsync(r => r.MemberId == request.MemberId && r.EventId == request.EventId);

        if (duplicate)
            return new ProblemDetails
            {
                Detail = $"Duplicate registration for member {request.MemberId} at event {request.EventId}",
                Status = 409,
            };

        return WolverineContinue.NoProblems;
    }

    // Happy path: store the registration and publish RegistrationSubmitted.
    // Both the document write and the message publish participate in the
    // same Marten outbox transaction — committed atomically by Wolverine.
    [WolverinePost("/registration")]
    public static Registration Post(RegistrationRequest request, IDocumentSession session, IMessageBus bus)
    {
        var registration = new Registration
        {
            Id = Guid.NewGuid(),
            RegistrationDate = DateTime.UtcNow,
            MemberId = request.MemberId,
            EventId = request.EventId,
            Payment = request.Payment,
        };

        session.Store(registration);

        bus.PublishAsync(new RegistrationSubmitted(
            registration.Id,
            registration.RegistrationDate,
            registration.MemberId,
            registration.EventId,
            registration.Payment
        ));

        return registration;
    }
}
