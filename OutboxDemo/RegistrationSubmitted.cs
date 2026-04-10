using Marten;

namespace OutboxDemo;

/// <summary>
/// Published when a new registration is submitted.
/// Triggers the saga workflow.
/// </summary>
public record RegistrationSubmitted(
    Guid RegistrationId,
    DateTime RegistrationDate,
    string MemberId,
    string EventId,
    decimal Payment
);

/// <summary>
/// Handles RegistrationSubmitted: creates saga state and cascades follow-up messages.
/// Replaces MassTransit's RegistrationStateMachine + RegistrationState + RegistrationStateDefinition.
/// </summary>
public static class RegistrationSubmittedHandler
{
    public static (SendRegistrationEmail, AddEventAttendee) Handle(
        RegistrationSubmitted message,
        IDocumentSession session)
    {
        var saga = new RegistrationSaga
        {
            Id = message.RegistrationId,
            CurrentState = "Registered",
            RegistrationDate = message.RegistrationDate,
            MemberId = message.MemberId,
            EventId = message.EventId,
            Payment = message.Payment,
        };

        session.Store(saga);

        Console.WriteLine($"[NotifyRegistration] Member {message.MemberId} registered for event {message.EventId}");

        // Cascade messages — sent via the Marten outbox in the same transaction
        return (
            new SendRegistrationEmail(message.RegistrationId, message.RegistrationDate, message.MemberId, message.EventId),
            new AddEventAttendee(message.RegistrationId, message.MemberId, message.EventId)
        );
    }
}
