using JasperFx;

namespace ShipmentTracking.Data;

/// <summary>
/// A Polecat document. There is no mapping, no table definition and no repository —
/// Polecat stores this as native SQL Server 2025 <c>json</c> keyed on <see cref="Id"/>,
/// and creates the table on first use.
///
/// <para>
/// <b>IRevisioned is not decoration.</b> The Dapper version wrote individual columns
/// (<c>set LastLocation = @location</c>), so two handlers touching different fields of
/// the same shipment could never clobber each other. A document store writes the
/// <i>whole document</i>, so they now can. Implementing IRevisioned is what puts the
/// guard back: Polecat detects the interface, keeps a numeric revision, and stamps the
/// expected revision into the UPDATE's WHERE clause. A losing write throws
/// <c>ConcurrencyException</c> instead of silently discarding the other handler's change,
/// and Program.cs retries it.
/// </para>
/// </summary>
public class Shipment : IRevisioned
{
    public Guid Id { get; set; }

    /// <summary>Managed by Polecat. Read on load, checked on update, incremented on save.</summary>
    public int Version { get; set; }

    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public string Status { get; set; } = "Draft";
    public string? TrackingNumber { get; set; }
    public string? LastLocation { get; set; }
    public DateTimeOffset? LastScanAt { get; set; }
}
