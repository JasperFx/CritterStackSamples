namespace OutboxDemo;

/// <summary>
/// Sent by the saga to add the member as an event attendee.
/// </summary>
public record AddEventAttendee(
    Guid RegistrationId,
    string MemberId,
    string EventId
);

public static class AddEventAttendeeHandler
{
    public static void Handle(AddEventAttendee message)
    {
        Console.WriteLine($"[AddAttendee] Added member {message.MemberId} to event {message.EventId}");
    }
}

/// <summary>
/// Replaces ValidateRegistrationConsumer — validates and publishes RegistrationValidated.
/// </summary>
public static class ValidateRegistrationHandler
{
    public static RegistrationValidated Handle(AddEventAttendee message)
    {
        Console.WriteLine($"[ValidateRegistration] Validating registration {message.RegistrationId}");
        return new RegistrationValidated(message.RegistrationId);
    }
}
