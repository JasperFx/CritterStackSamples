using Wolverine.Http;
using Wolverine.Persistence;
using Wolverine.Persistence.Sagas;
using Wolverine.Runtime;

namespace SagaWorkflows.OrderApproval;

public record ApprovalAnswer(bool Approved, string Manager);

public record OrderStatus(Guid OrderId, string Stage, bool? Processed, bool Finished);

// One row of the order's message history: what it handled, and what came of it
public record OrderStep(long Step, DateTimeOffset At, string Message, string? Outcome);

public static class OrderEndpoints
{
    // Start the workflow. The PlaceOrder goes out through the outbox when the request commits
    [WolverinePost("/orders")]
    public static (AcceptResponse, PlaceOrder) Post(PlaceOrder command)
        => (new AcceptResponse($"/orders/{command.OrderId}"), command);

    // Signal the running workflow
    [WolverinePost("/orders/{orderId}/approval")]
    public static (AcceptResponse, ManagerDecision) Approve(Guid orderId, ApprovalAnswer answer)
        => (new AcceptResponse($"/orders/{orderId}"), new ManagerDecision(orderId, answer.Approved, answer.Manager));

    // Query it: the saga is a document, so this is a load by id
    [WolverineGet("/orders/{id}")]
    public static OrderStatus Get([Entity] Order order)
        => new(order.Id, order.Stage, order.Processed, order.IsCompleted());

    // How it got there. The order records a row for every message it handles ([RecordSagaHistory]), read through the
    // same ISagaHistoryDiagnostics that CritterWatch uses for an event-sourced saga's stream
    [WolverineGet("/orders/{id}/history")]
    public static async Task<OrderStep[]> History(Guid id, IWolverineRuntime runtime, CancellationToken cancellation)
    {
        var history = (ISagaHistoryDiagnostics)runtime.SagaStorage;
        var page = await history.ReadSagaHistoryAsync(typeof(Order).FullName!, id, new SagaHistoryPageRequest(), cancellation);

        return page.Events
            .Select(x => new OrderStep(x.Version, x.Timestamp, x.EventTypeName,
                x.Data.TryGetProperty("Outcome", out var outcome) ? outcome.ToString() : null))
            .ToArray();
    }
}
