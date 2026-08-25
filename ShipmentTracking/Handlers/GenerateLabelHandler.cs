using ShipmentTracking.Data;
using ShipmentTracking.Messages;

namespace ShipmentTracking.Handlers;

/// <summary>
/// Calls the carrier's label API, which routinely takes 30-90 seconds. It holds
/// the delivery unsettled for the whole call, which is exactly why this endpoint
/// stays Durable rather than following the carrier-scan endpoint onto NativeAck
/// — see the note in Program.cs.
/// </summary>
public static class GenerateLabelHandler
{
    public static async Task<LabelGenerated> Handle(
        GenerateLabel command,
        ShipmentRepository repository,
        ICarrierLabelClient labels,
        ILogger logger,
        CancellationToken token)
    {
        logger.LogInformation("Requesting a label for {ShipmentId} from {Carrier}",
            command.ShipmentId, command.Carrier);

        var trackingNumber = await labels.CreateLabelAsync(command.ShipmentId, command.Carrier, token);

        await repository.SetTrackingNumberAsync(command.ShipmentId, trackingNumber);

        return new LabelGenerated(command.ShipmentId, trackingNumber);
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
