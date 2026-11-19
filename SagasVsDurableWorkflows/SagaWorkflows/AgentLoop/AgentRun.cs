using JasperFx;
using JasperFx.Core;
using Wolverine;

namespace SagaWorkflows.AgentLoop;

// Rebuilt from the agent loops that Temporal (temporal-community/temporal-ai-agent) and Restate (restatedev/ai-examples,
// the "tour of agents" human approval agent) publish: call the model, run the tools it asks for, feed the results back,
// and go round again until it answers. A tool with side effects waits for a person to approve it first, and the loop
// stops after a set number of turns.

public record ChatMessage(string Role, string Text);
public record ToolCall(string CallId, string Tool, string Argument);

public record AskAgent(Guid AgentRunId, string Question, int MaxTurns);

// Activities: the model, and the tools
public record CallModel(Guid AgentRunId, ChatMessage[] Conversation);
public record ModelReplied(Guid AgentRunId, string? Answer, ToolCall[] ToolCalls);
public record RunTool(Guid AgentRunId, ToolCall Call);
public record ToolFinished(Guid AgentRunId, string CallId, string Result) : IFanInReply;

// Human in the loop
public record ApprovalNeeded(Guid AgentRunId, ToolCall Call);
public record ToolApproval(Guid AgentRunId, string CallId, bool Approved);
public record ToolApprovalTimedOut(Guid AgentRunId, string CallId) : TimeoutMessage(1.Hours());

public record AgentAnswered(Guid AgentRunId, string Answer, int Turns);
public record AgentGaveUp(Guid AgentRunId, int Turns);

public class AgentRun : Saga, IRevisioned
{
    // Tools that change something outside the agent need a person's yes
    public static readonly string[] NeedApproval = ["issue_refund"];

    public Guid Id { get; set; }

    public int MaxTurns { get; set; }
    public int Turns { get; set; }
    public List<ChatMessage> Conversation { get; set; } = [];

    // The tool calls of the current turn that have no result yet, and the ones waiting for a person
    public List<string> Pending { get; set; } = [];
    public List<ToolCall> AwaitingApproval { get; set; } = [];

    public static (AgentRun, CallModel) Start(AskAgent command)
    {
        var run = new AgentRun
        {
            Id = command.AgentRunId,
            MaxTurns = command.MaxTurns,
            Conversation = [new ChatMessage("user", command.Question)]
        };

        return (run, new CallModel(run.Id, [..run.Conversation]));
    }

    public OutgoingMessages Handle(ModelReplied reply)
    {
        Turns++;

        if (reply.Answer is not null)
        {
            Conversation.Add(new ChatMessage("assistant", reply.Answer));
            MarkCompleted();
            return [new AgentAnswered(Id, reply.Answer, Turns)];
        }

        if (Turns >= MaxTurns)
        {
            MarkCompleted();
            return [new AgentGaveUp(Id, Turns)];
        }

        // Run the tools the model asked for, all at once; ask first for the ones that need it
        var messages = new OutgoingMessages();
        foreach (var call in reply.ToolCalls)
        {
            Conversation.Add(new ChatMessage("assistant", $"call {call.Tool}({call.Argument})"));
            Pending.Add(call.CallId);

            if (NeedApproval.Contains(call.Tool))
            {
                AwaitingApproval.Add(call);
                messages.Add(new ApprovalNeeded(Id, call));
                messages.Add(new ToolApprovalTimedOut(Id, call.CallId));
            }
            else
            {
                messages.Add(new RunTool(Id, call));
            }
        }

        return messages;
    }

    public object? Handle(ToolApproval approval)
    {
        var call = AwaitingApproval.FirstOrDefault(x => x.CallId == approval.CallId);
        if (call is null) return null;

        AwaitingApproval.Remove(call);
        return approval.Approved ? new RunTool(Id, call) : record(call.CallId, "The user declined this action");
    }

    public CallModel? Handle(ToolApprovalTimedOut timedOut)
    {
        var call = AwaitingApproval.FirstOrDefault(x => x.CallId == timedOut.CallId);
        if (call is null) return null;

        AwaitingApproval.Remove(call);
        return record(call.CallId, "Nobody approved this action in time");
    }

    public CallModel? Handle(ToolFinished finished) => record(finished.CallId, finished.Result);

    // A tool that failed after its retries is an observation for the model, not the end of the run
    public CallModel? Compensate(RunTool failed, ExceptionInfo ex) => record(failed.Call.CallId, $"The tool failed: {ex.Message}");

    // Go round again once every tool call of this turn has a result
    private CallModel? record(string callId, string result)
    {
        if (!Pending.Remove(callId)) return null;

        Conversation.Add(new ChatMessage("tool", $"{callId}: {result}"));
        return Pending.Count == 0 ? new CallModel(Id, [..Conversation]) : null;
    }
}
