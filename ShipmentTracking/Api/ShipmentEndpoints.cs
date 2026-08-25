using Microsoft.AspNetCore.Http.HttpResults;
using ShipmentTracking.Data;
using ShipmentTracking.Messages;
using Wolverine.Http;

namespace ShipmentTracking.Api;

public record BookShipmentRequest(
    string Origin, string Destination, string Carrier, decimal WeightKg);

public record CancelShipmentRequest(string Reason);

/// <summary>
/// 202 + Location + body, via Wolverine's own AcceptResponse — the 202 sibling of
/// CreationResponse, in the same file, on the same IHttpAware seam.
/// </summary>
public record ShipmentAccepted(Guid ShipmentId) : AcceptResponse($"/shipments/{ShipmentId}");

public static class ShipmentEndpoints
{
    /// <summary>
    /// The explicit IMessageBus call is gone. The command is the second tuple
    /// element, so Wolverine sends it through the outbox after the response is
    /// written — the same cascading shape the message handlers use.
    ///
    /// 202 through Wolverine's AcceptResponse, which sets the status and the
    /// Location header and contributes the OpenAPI metadata.
    /// </summary>
    [WolverinePost("/shipments")]
    public static (ShipmentAccepted, BookShipment) Book(BookShipmentRequest request)
    {
        var id = Guid.NewGuid();

        return (
            new ShipmentAccepted(id),
            new BookShipment(id, request.Origin, request.Destination, request.Carrier, request.WeightKg));
    }

    /// <summary>
    /// 202 with no body. AcceptResponse requires a Url and always stamps
    /// Location; this route never had one, so the typed ASP.NET Core result is
    /// the closer match. Accepted implements IEndpointMetadataProvider, which
    /// Wolverine honours, so 202 still reaches OpenAPI.
    /// </summary>
    [WolverinePost("/shipments/{id}/cancel")]
    public static (Accepted, CancelShipment) Cancel(Guid id, CancelShipmentRequest request)
        => (TypedResults.Accepted((string?)null), new CancelShipment(id, request.Reason));

    /// <summary>
    /// A nullable return is Wolverine's 404: no explicit Results.NotFound(), and
    /// OpenAPI gets both 200 and 404 without an attribute.
    /// </summary>
    [WolverineGet("/shipments/{id}")]
    public static Task<Shipment?> Get(Guid id, ShipmentRepository repository)
        => repository.LoadAsync(id);

    [WolverineGet("/shipments")]
    public static Task<IReadOnlyList<Shipment>> GetAll(ShipmentRepository repository)
        => repository.ListAsync();

    /// <summary>
    /// The carrier webhook. The scan arrives as the request body and is cascaded
    /// straight back out; the global partitioned topology picks the shard from
    /// ShipmentId.
    /// </summary>
    [WolverinePost("/webhooks/carrier-scan")]
    public static (Accepted, RecordCarrierScan) CarrierScan(RecordCarrierScan scan)
        => (TypedResults.Accepted((string?)null), scan);
}
