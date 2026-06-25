namespace TripService;

/// <summary>The single-stream Trip aggregate snapshot, projected by <see cref="TripProjection"/>.</summary>
public class Trip
{
    public Guid Id { get; set; }

    public int EndedOn { get; set; }

    public double Traveled { get; set; }

    public string State { get; set; } = string.Empty;

    public bool Active { get; set; }

    public int StartedOn { get; set; }

    public bool WaitingRepairs { get; set; }
}
