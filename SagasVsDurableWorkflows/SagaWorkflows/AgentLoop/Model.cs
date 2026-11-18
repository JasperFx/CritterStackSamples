using System.Collections.Concurrent;

namespace SagaWorkflows.AgentLoop;

public interface IChatModel
{
    Task<ModelReplied> CompleteAsync(Guid runId, ChatMessage[] conversation, CancellationToken cancellation);
}

/// <summary>
/// A scripted stand-in for an LLM with tools. For "refund order N" it looks the order up and checks the refund
/// policy (two tools at once), then asks to issue the refund, then answers. For anything else it keeps asking to
/// search, forever
/// </summary>
public class ScriptedChatModel : IChatModel
{
    public Task<ModelReplied> CompleteAsync(Guid runId, ChatMessage[] conversation, CancellationToken cancellation)
    {
        var question = conversation[0].Text;
        var toolResults = conversation.Where(x => x.Role == "tool").ToArray();
        var turn = conversation.Count(x => x.Role == "assistant" && x.Text.StartsWith("call "));

        ModelReplied reply;
        if (question.StartsWith("refund order "))
        {
            var order = question["refund order ".Length..];
            reply = toolResults.Length switch
            {
                0 => new ModelReplied(runId, null, [new ToolCall("c1", "lookup_order", order), new ToolCall("c2", "refund_policy", order)]),
                2 => new ModelReplied(runId, null, [new ToolCall("c3", "issue_refund", order)]),
                _ => new ModelReplied(runId, $"Done. {toolResults[^1].Text}", [])
            };
        }
        else
        {
            reply = new ModelReplied(runId, null, [new ToolCall($"s{turn}", "search", question)]);
        }

        return Task.FromResult(reply);
    }
}

/// <summary>The tools the agent can call, in memory</summary>
public class ToolBox
{
    public ConcurrentBag<string> Refunded { get; } = [];
    // "tool:argument" pairs that fail every time
    public ConcurrentBag<string> Broken { get; } = [];
    public ConcurrentQueue<ApprovalNeeded> ApprovalRequests { get; } = new();

    public string Run(ToolCall call)
    {
        if (Broken.Contains($"{call.Tool}:{call.Argument}")) throw new HttpRequestException($"{call.Tool} returned 503");

        switch (call.Tool)
        {
            case "issue_refund":
                Refunded.Add(call.Argument);
                return $"refunded order {call.Argument}";
            case "lookup_order":
                return $"order {call.Argument}: 1 kettle, $40, delivered broken";
            case "refund_policy":
                return "damaged goods are refunded in full";
            default:
                return "nothing found";
        }
    }
}

public static class AgentActivityHandler
{
    public static Task<ModelReplied> Handle(CallModel command, IChatModel model, CancellationToken cancellation)
        => model.CompleteAsync(command.AgentRunId, command.Conversation, cancellation);

    public static ToolFinished Handle(RunTool command, ToolBox tools)
        => new(command.AgentRunId, command.Call.CallId, tools.Run(command.Call));

    public static void Handle(ApprovalNeeded request, ToolBox tools) => tools.ApprovalRequests.Enqueue(request);

    public static void Handle(AgentAnswered _) { }
    public static void Handle(AgentGaveUp _) { }
}
