using ShipmentTracking.Data;
using ShipmentTracking.Messages;
using Wolverine;
using Wolverine.Persistence;

namespace ShipmentTracking.Handlers;

/// <summary>
/// Calls the carrier's label API, which routinely takes 30-90 seconds. It holds the
/// delivery unsettled for the whole call, which is exactly why this endpoint stays
/// Durable rather than following the carrier-scan endpoint onto NativeAck — see the
/// note in Program.cs.
///
/// <para>
/// <b>It no longer touches the database.</b> The Dapper version loaded nothing and
/// issued one targeted UPDATE after the call returned, so the slow call sat outside
/// any read-modify-write. Moving to a document store would have changed that: [Entity]
/// loads at the <i>start</i> of the chain, so the shipment would have been read, held
/// across a 45-second carrier call, and written back against a revision that is by then
/// the stalest in the system. Every concurrent scan would lose, and every retry would
/// re-run the carrier call.
/// </para>
///
/// <para>
/// So the slow I/O and the state transition were split. This handler is pure
/// integration; RecordTrackingNumberHandler owns the write, and its conflict window is
/// microseconds instead of a minute.
/// </para>
/// </summary>
public static class GenerateLabelHandler
{
    public static async Task<OutgoingMessages> Handle(
        GenerateLabel command,
        ICarrierLabelClient labels,
        ILogger logger,
        CancellationToken token)
    {
        logger.LogInformation("Requesting a label for {ShipmentId} from {Carrier}",
            command.ShipmentId, command.Carrier);

        var trackingNumber = await labels.CreateLabelAsync(command.ShipmentId, command.Carrier, token);

        return
        [
            new RecordTrackingNumber(command.ShipmentId, trackingNumber),
            new LabelGenerated(command.ShipmentId, trackingNumber)
        ];
    }
}

/// <summary>
/// The other half of the split: a fast, purely declarative write.
/// </summary>
public static class RecordTrackingNumberHandler
{
    public static Update<Shipment> Handle(
        RecordTrackingNumber command,
        [Entity(Required = true, OnMissing = OnMissing.ThrowException)] Shipment shipment)
    {
        shipment.TrackingNumber = command.TrackingNumber;

        // The Dapper version wrote Status = 'Labelled' unconditionally, which would
        // resurrect a shipment cancelled while the carrier call was in flight. Easy to
        // miss inside a SQL string; hard to miss here.
        if (shipment.Status == "Booked")
        {
            shipment.Status = "Labelled";
        }

        return Storage.Update(shipment);
    }
}

public interface ICarrierLabelClient
{
    Task<string> CreateLabelAsync(Guid shipmentId, string carrier, CancellationToken token);
}

/// <summary>Stand-in for the real carrier integration.</summary>
public class FakeCarrierLabelClient : ICarrierLabelClient
{
    public async Task<string> CreateLabelAsync(Guid shipmentId, string carrier, CancellationToken token)
    {
        await Task.Delay(TimeSpan.FromSeconds(45), token);
        return $"{carrier.ToUpperInvariant()}-{shipmentId.ToString("N")[..10].ToUpperInvariant()}";
    }
}
