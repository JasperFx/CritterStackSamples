using ShipmentTracking.Data;
using ShipmentTracking.Messages;
using Wolverine;

namespace ShipmentTracking.Api;

public record BookShipmentRequest(
    string Origin, string Destination, string Carrier, decimal WeightKg);

public record CancelShipmentRequest(string Reason);

public static class ShipmentEndpoints
{
    public static void MapShipmentEndpoints(this WebApplication app)
    {
        // IMessageBus replaces NServiceBus' IMessageSession as the way to reach
        // the bus from outside a handler.
        app.MapPost("/shipments", async (BookShipmentRequest request, IMessageBus bus) =>
        {
            var id = Guid.NewGuid();

            await bus.SendAsync(new BookShipment(
                id, request.Origin, request.Destination, request.Carrier, request.WeightKg));

            return Results.Accepted($"/shipments/{id}", new { shipmentId = id });
        });

        app.MapPost("/shipments/{id:guid}/cancel", async (
            Guid id, CancelShipmentRequest request, IMessageBus bus) =>
        {
            await bus.SendAsync(new CancelShipment(id, request.Reason));
            return Results.Accepted();
        });

        app.MapGet("/shipments/{id:guid}", async (Guid id, ShipmentRepository repository) =>
        {
            var shipment = await repository.LoadAsync(id);
            return shipment is null ? Results.NotFound() : Results.Ok(shipment);
        });

        app.MapGet("/shipments", async (ShipmentRepository repository) =>
            Results.Ok(await repository.ListAsync()));

        // The carrier webhook. This is the firehose.
        app.MapPost("/webhooks/carrier-scan", async (
            RecordCarrierScan scan, HttpRequest http, IMessageBus bus) =>
        {
            // SendOptions -> DeliveryOptions. No GroupId is set here: the global
            // partitioned topology infers grouping from ShipmentId, so the shard
            // is chosen for us and every scan for one shipment stays ordered.
            await bus.SendAsync(scan, new DeliveryOptions
            {
                Headers = { ["Carrier.ScanId"] = http.Headers["X-Carrier-Scan-Id"].ToString() }
            });

            return Results.Accepted();
        });
    }
}
