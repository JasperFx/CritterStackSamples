using Marten;

namespace OutboxDemo;

// --- Message Handlers (replace MassTransit consumers) ---

/// <summary>
/// Handles RegistrationSubmitted: starts the saga and cascades follow-up messages.
///
/// In MassTransit, this was a full state machine saga with RegistrationState entity,
/// RegistrationStateDefinition, and RegistrationStateMap. In Wolverine, the handler
/// returns cascade messages that are sent within the same Marten transaction (outbox).
/// </summary>
public static class RegistrationSubmittedHandler
{
    public static async Task<(SendRegistrationEmail, AddEventAttendee)> Handle(
        RegistrationSubmitted message,
        IDocumentSession session)
    {
        // Create the saga state document
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

        // Log the registration (replaces NotifyRegistrationConsumer)
        Console.WriteLine($"[NotifyRegistration] Member {message.MemberId} registered for event {message.EventId}");

        // Return cascade messages — Wolverine sends these via the Marten outbox
        // in the same transaction as the saga state write
        return (
            new SendRegistrationEmail(message.RegistrationId, message.RegistrationDate, message.MemberId, message.EventId),
            new AddEventAttendee(message.RegistrationId, message.MemberId, message.EventId)
        );
    }
}

/// <summary>
/// Replaces SendRegistrationEmailConsumer.
/// </summary>
public static class SendRegistrationEmailHandler
{
    public static void Handle(SendRegistrationEmail message)
    {
        Console.WriteLine($"[SendEmail] Registration email sent to member {message.MemberId} for event {message.EventId}");
    }
}

/// <summary>
/// Replaces AddEventAttendeeConsumer.
/// </summary>
public static class AddEventAttendeeHandler
{
    public static void Handle(AddEventAttendee message)
    {
        Console.WriteLine($"[AddAttendee] Added member {message.MemberId} to event {message.EventId}");
    }
}

/// <summary>
/// Replaces ValidateRegistrationConsumer — publishes RegistrationValidated.
/// </summary>
public static class ValidateRegistrationHandler
{
    public static RegistrationValidated Handle(AddEventAttendee message)
    {
        Console.WriteLine($"[ValidateRegistration] Validating registration {message.RegistrationId}");

        // Return cascades the validation message — sent via Marten outbox
        return new RegistrationValidated(message.RegistrationId);
    }
}

/// <summary>
/// Handles the final validation event.
/// </summary>
public static class RegistrationValidatedHandler
{
    public static void Handle(RegistrationValidated message)
    {
        Console.WriteLine($"[RegistrationValidated] Registration {message.RegistrationId} validated successfully");
    }
}
