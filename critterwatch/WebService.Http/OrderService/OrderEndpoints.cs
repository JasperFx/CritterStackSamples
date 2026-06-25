using Marten;
using Wolverine.Http;
using Wolverine.Http.Marten;
using Wolverine.Marten;

namespace OrderService;

// =============================================================================================
// The Orders HTTP API — WolverineFx.Http endpoints over the event-sourced Order aggregate.
//
// These are WOLVERINE HTTP endpoints (mapped by app.MapWolverineEndpoints()), so CritterWatch's HTTP graph
// surfaces them automatically. The bare ASP.NET endpoints in Program.cs (/, /health) are the ones that need
// AddCritterWatchHttp() to show up on the HTTP tab (#538).
// =============================================================================================

// ---- Command contracts ------------------------------------------------------------------------
public record PlaceOrder(string Customer, string[] Items);

public record AddItem(string Item);

public static class OrderEndpoints
{
    // begin-snippet: order-http-endpoints
    // POST /orders — start a brand-new event stream. MartenOps.StartStream captures the OrderPlaced event;
    // the tuple's IStartStream member is what makes Wolverine persist it. Returns 201 + Location.
    [WolverinePost("/orders")]
    public static (CreationResponse, IStartStream) Place(PlaceOrder command)
    {
        var start = MartenOps.StartStream<Order>(new OrderPlaced(command.Customer, command.Items));
        return (new CreationResponse($"/orders/{start.StreamId}"), start);
    }

    // POST /orders/{id}/items — append to an EXISTING stream. [Aggregate] loads the Order by {id}; the
    // returned event is appended. [EmptyResponse] → 204 No Content.
    [WolverinePost("/orders/{id}/items"), EmptyResponse]
    public static OrderItemAdded AddItem(AddItem command, [Aggregate] Order order)
        => new(command.Item);

    // POST /orders/{id}/ship — append OrderShipped to the existing stream.
    [WolverinePost("/orders/{id}/ship"), EmptyResponse]
    public static OrderShipped Ship([Aggregate] Order order)
        => new(DateTimeOffset.UtcNow);

    // GET /orders/{id} — load the latest aggregated Order (read-only, no events appended).
    [WolverineGet("/orders/{id}")]
    public static Order Get(Guid id, [ReadAggregate] Order order) => order;
    // end-snippet
}
