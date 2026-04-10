namespace OutboxDemo;

/// <summary>
/// Registration document stored in Marten.
/// </summary>
public class Registration
{
    public Guid Id { get; set; }
    public DateTime RegistrationDate { get; set; }
    public string MemberId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public decimal Payment { get; set; }
}

/// <summary>
/// Saga state tracking the registration workflow.
/// Wolverine sagas use the document as the state — no separate state entity.
/// </summary>
public class RegistrationSaga
{
    public Guid Id { get; set; } // CorrelationId = RegistrationId
    public string CurrentState { get; set; } = "Submitted";
    public DateTime RegistrationDate { get; set; }
    public string MemberId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public decimal Payment { get; set; }
}
