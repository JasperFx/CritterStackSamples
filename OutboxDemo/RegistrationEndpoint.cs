using Marten;
using Wolverine;
using Wolverine.Http;

namespace OutboxDemo;

public record RegistrationRequest(string EventId, string MemberId, decimal Payment);
public record RegistrationResponse(Guid RegistrationId, DateTime RegistrationDate, string MemberId, string EventId);

public static class RegistrationEndpoint
{
    /// <summary>
    /// Submit a new registration. Stores the Registration document and publishes
    /// RegistrationSubmitted — both in the same Marten/Wolverine outbox transaction.
    ///
    /// This replaces: RegistrationController → IRegistrationService → IPublishEndpoint → DbContext.SaveChangesAsync()
    /// In the original, the MassTransit outbox intercepted SaveChangesAsync() to write
    /// the outbox message in the same SQL transaction. Wolverine+Marten does this natively
    /// via the integrated outbox.
    /// </summary>
    [WolverinePost("/registration")]
    public static async Task<IResult> Submit(RegistrationRequest request, IDocumentSession session, IMessageBus bus)
    {
        var registrationId = Guid.NewGuid();
        var registrationDate = DateTime.UtcNow;

        // Check for duplicate registration
        var duplicate = await session.Query<Registration>()
            .AnyAsync(r => r.MemberId == request.MemberId && r.EventId == request.EventId);

        if (duplicate)
            return Results.Conflict(new { Message = $"Duplicate registration for member {request.MemberId} at event {request.EventId}" });

        // Store the registration document
        var registration = new Registration
        {
            Id = registrationId,
            RegistrationDate = registrationDate,
            MemberId = request.MemberId,
            EventId = request.EventId,
            Payment = request.Payment,
        };

        session.Store(registration);

        // Publish RegistrationSubmitted — goes through the Marten outbox
        // so the message and registration are committed atomically
        await bus.PublishAsync(new RegistrationSubmitted(
            registrationId,
            registrationDate,
            request.MemberId,
            request.EventId,
            request.Payment
        ));

        await session.SaveChangesAsync();

        return Results.Ok(new RegistrationResponse(registrationId, registrationDate, request.MemberId, request.EventId));
    }
}
