using Microsoft.AspNetCore.Http.HttpResults;
using ShipmentTracking.Data;
using ShipmentTracking.Messages;
using Wolverine.Http;
using Wolverine.Persistence;

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
    /// Phase 2 got the 404 from a nullable return. Phase 3 gets it one step earlier:
    /// [Entity] resolves the id from the {id} route parameter, loads the document, and
    /// short-circuits with a 404 before the endpoint body runs — and still contributes
    /// Produces(404) to the OpenAPI document, so the generated contract is unchanged.
    ///
    /// These are genuinely two different mechanisms and they are easy to conflate: the
    /// entity guard fires when the *load* misses, while a nullable return fires when
    /// the endpoint runs and produces no *body*. Only one of them is in play here,
    /// because the guard runs first.
    /// </summary>
    [WolverineGet("/shipments/{id}")]
    public static Shipment Get([Entity] Shipment shipment) => shipment;

    /// <summary>
    /// [All] is the declarative "every document of this type" — no IQuerySession, no
    /// repository, no await. It is deliberately unfiltered and deliberately blunt, and
    /// it is the right shape only while this table stays small; a real one wants a
    /// compiled query or a query plan behind [FromQuerySpecification].
    /// </summary>
    [WolverineGet("/shipments")]
    public static IReadOnlyList<Shipment> GetAll([All] IReadOnlyList<Shipment> shipments) => shipments;

    /// <summary>
    /// The carrier webhook. The scan arrives as the request body and is cascaded
    /// straight back out; the global partitioned topology picks the shard from
    /// ShipmentId.
    /// </summary>
    [WolverinePost("/webhooks/carrier-scan")]
    public static (Accepted, RecordCarrierScan) CarrierScan(RecordCarrierScan scan)
        => (TypedResults.Accepted((string?)null), scan);
}
