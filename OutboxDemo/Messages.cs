namespace OutboxDemo;

// --- Messages (replace MassTransit contracts) ---

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
/// Sent by the saga to trigger email notification.
/// </summary>
public record SendRegistrationEmail(
    Guid RegistrationId,
    DateTime RegistrationDate,
    string MemberId,
    string EventId
);

/// <summary>
/// Sent by the saga to add the member as an event attendee.
/// </summary>
public record AddEventAttendee(
    Guid RegistrationId,
    string MemberId,
    string EventId
);

/// <summary>
/// Published after registration validation completes.
/// </summary>
public record RegistrationValidated(Guid RegistrationId);
