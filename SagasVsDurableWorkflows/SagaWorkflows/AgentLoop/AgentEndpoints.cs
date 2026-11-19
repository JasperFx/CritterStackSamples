using Wolverine.Http;
using Wolverine.Persistence.Sagas;
using Wolverine.Runtime;

namespace SagaWorkflows.AgentLoop;

public record IdleAgentRuns(long Count, string[] AgentRunIds);

public static class AgentEndpoints
{
    // Agent runs nobody has touched for a while: usually waiting on a person who never answered. Every saga has a
    // LastModified that Wolverine sets on each save, and Program.cs indexes it for AgentRun, so this is a range query,
    // not a table scan. CritterWatch's idle saga counts and alerts read the same ISagaIdleDiagnostics
    [WolverineGet("/agents/idle")]
    public static async Task<IdleAgentRuns> Idle(int minutes, IWolverineRuntime runtime, CancellationToken cancellation)
    {
        var idle = (ISagaIdleDiagnostics)runtime.SagaStorage;
        var sagaType = typeof(AgentRun).FullName!;
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-minutes);

        var count = await idle.CountIdleSagasAsync(sagaType, cutoff, tenantId: null, cancellation);
        var page = await idle.QuerySagasByLastModifiedAsync(sagaType, SagaLastModifiedRange.Before(cutoff),
            new JasperFx.Documents.DocumentQueryOptions(1, 50), cancellation);

        return new IdleAgentRuns(count, page.Instances.Select(x => x.Identity.ToString()!).ToArray());
    }
}
