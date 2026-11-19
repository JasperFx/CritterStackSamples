using Wolverine.Http;
using Wolverine.Persistence;

namespace SagaWorkflows.OrderApproval;

public record ApprovalAnswer(bool Approved, string Manager);

public record OrderStatus(Guid OrderId, string Stage, bool? Processed, bool Finished);

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
}
