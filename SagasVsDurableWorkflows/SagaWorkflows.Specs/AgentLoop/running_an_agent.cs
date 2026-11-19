using JasperFx.Core;
using SagaWorkflows.AgentLoop;

namespace SagaWorkflows.Specs.AgentLoop;

// temporal-ai-agent and Restate's tour of agents: call the model, run the tools it asks for in parallel, ask a person
// before the risky one, and go round again until it answers.

[BobcatFeature("An agent run loops through the model and its tools")]
[Collection(nameof(DocumentSagaCollection))]
public class running_an_agent(DocumentSagas app) : SagaWorkflowSpec(app)
{
    private Guid AgentRunId { get; } = Guid.NewGuid();

    // Turn 1 runs two tools at once; their replies are fan-in replies, handled one at a time for this run
    [BobcatSpec(typeof(AskAgent))]
    public async Task tools_run_in_parallel_and_the_refund_waits_for_a_person()
    {
        await WhenReceived(new AskAgent(AgentRunId, "refund order 1042", MaxTurns: 5));

        ThenSentExactly(typeof(RunTool), 2);
        ThenGroupedBy<ToolFinished>(AgentRunId);
        ThenMessageSent(Specify<ApprovalNeeded>().With(x => x.Call, new ToolCall("c3", "issue_refund", "1042")));
    }

    [BobcatSpec(typeof(ToolApproval))]
    public async Task an_approved_refund_runs_and_the_agent_answers()
    {
        await GivenReceived(new AskAgent(AgentRunId, "refund order 1042", MaxTurns: 5));

        await WhenReceived(new ToolApproval(AgentRunId, "c3", Approved: true));

        ThenMessageSent(new AgentAnswered(AgentRunId, "Done. c3: refunded order 1042", 3));
    }

    // Nobody answers within the hour: that is an answer the model sees too
    [BobcatSpec(typeof(ToolApprovalTimedOut))]
    public async Task nobody_approving_in_time_is_an_answer_too()
    {
        await WhenReceived(new AskAgent(AgentRunId, "refund order 1042", MaxTurns: 5));

        await WhenTimePasses(1.Hours());

        ThenMessageSent(new AgentAnswered(AgentRunId, "Done. c3: Nobody approved this action in time", 3));
    }

    // A tool that fails through its retries comes back through Compensate(RunTool, ...) as an observation
    [BobcatSpec(typeof(AskAgent))]
    public async Task a_tool_that_keeps_failing_becomes_an_observation_for_the_model()
    {
        ToolFails("refund_policy", "1042");

        await WhenReceived(new AskAgent(AgentRunId, "refund order 1042", MaxTurns: 5));

        ThenDeadLettered<RunTool>();
        await ThenSaga<AgentRun>(AgentRunId, Specify<AgentRun>()
            .With(x => x.Conversation, Expect.Containing(new ChatMessage("tool", "c2: The tool failed: refund_policy returned 503"))));
    }

    // A run waiting on a person stops being written to. Its LastModified says so, and Wolverine can find it
    [BobcatSpec(typeof(AskAgent))]
    public async Task a_run_waiting_for_approval_shows_up_among_the_idle_runs()
    {
        await WhenReceived(new AskAgent(AgentRunId, "refund order 1042", MaxTurns: 5));

        await ThenTheSagaIsIdleSince<AgentRun>(AgentRunId, DateTimeOffset.UtcNow.AddSeconds(1));
    }
}
