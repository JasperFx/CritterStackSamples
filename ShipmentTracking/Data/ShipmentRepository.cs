using Dapper;
using Microsoft.Data.SqlClient;

namespace ShipmentTracking.Data;

public class Shipment
{
    public Guid Id { get; set; }
    public string Origin { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public string Status { get; set; } = "Draft";
    public string? TrackingNumber { get; set; }
    public string? LastLocation { get; set; }
    public DateTimeOffset? LastScanAt { get; set; }
}

/// <summary>
/// Hand-rolled data access over SQL Server, carried across from the NServiceBus
/// version unchanged. It still opens its own connection outside Wolverine's
/// outbox transaction — phase 2 replaces the whole thing with Polecat, which is
/// where that gets fixed.
/// </summary>
public class ShipmentRepository(string connectionString)
{
    public async Task InsertAsync(Shipment shipment)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.ExecuteAsync(
            """
            insert into Shipments (Id, Origin, Destination, Carrier, WeightKg, Status)
            values (@Id, @Origin, @Destination, @Carrier, @WeightKg, @Status)
            """, shipment);
    }

    public async Task<Shipment?> LoadAsync(Guid id)
    {
        await using var connection = new SqlConnection(connectionString);
        return await connection.QuerySingleOrDefaultAsync<Shipment>(
            "select * from Shipments where Id = @id", new { id });
    }

    public async Task UpdateStatusAsync(Guid id, string status)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.ExecuteAsync(
            "update Shipments set Status = @status where Id = @id", new { id, status });
    }

    public async Task RecordScanAsync(Guid id, string location, DateTimeOffset occurredAt)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.ExecuteAsync(
            """
            update Shipments
            set LastLocation = @location, LastScanAt = @occurredAt
            where Id = @id and (LastScanAt is null or LastScanAt < @occurredAt)
            """, new { id, location, occurredAt });
    }

    public async Task SetTrackingNumberAsync(Guid id, string trackingNumber)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.ExecuteAsync(
            "update Shipments set TrackingNumber = @trackingNumber, Status = 'Labelled' where Id = @id",
            new { id, trackingNumber });
    }

    public async Task<IReadOnlyList<Shipment>> ListAsync()
    {
        await using var connection = new SqlConnection(connectionString);
        return (await connection.QueryAsync<Shipment>("select * from Shipments")).ToList();
    }
}
