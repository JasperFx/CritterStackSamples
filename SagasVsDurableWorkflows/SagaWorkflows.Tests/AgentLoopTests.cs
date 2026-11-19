using JasperFx.Core;
using SagaWorkflows.AgentLoop;
using Wolverine.Tracking;

namespace SagaWorkflows.Tests;

public class AgentLoopTests(AppFixture fixture) : WorkflowContext(fixture)
{
    private ToolBox Tools => Service<ToolBox>();

    private static string order() => Random.Shared.Next(1000, 9999).ToString();

    [Fact]
    public async Task runs_tools_in_parallel_waits_for_approval_then_answers()
    {
        var number = order();
        var id = Guid.NewGuid();

        var asked = await Run(new AskAgent(id, $"refund order {number}", MaxTurns: 5));

        // Turn 1 ran two tools at once; turn 2 wants to issue the refund, which needs a person
        asked.Executed.MessagesOf<RunTool>().Select(x => x.Call.Tool).Order().ShouldBe(["lookup_order", "refund_policy"]);
        asked.Executed.SingleMessage<ApprovalNeeded>().Call.Tool.ShouldBe("issue_refund");
        Tools.Refunded.ShouldNotContain(number);

        var approved = await Run(new ToolApproval(id, "c3", Approved: true));

        approved.Executed.SingleMessage<AgentAnswered>().ShouldBe(new AgentAnswered(id, $"Done. c3: refunded order {number}", 3));
        Tools.Refunded.ShouldContain(number);
    }

    [Fact]
    public async Task a_declined_tool_call_is_an_answer_the_model_sees()
    {
        var number = order();
        var id = Guid.NewGuid();

        await Run(new AskAgent(id, $"refund order {number}", MaxTurns: 5));
        var declined = await Run(new ToolApproval(id, "c3", Approved: false));

        declined.Executed.SingleMessage<AgentAnswered>().Answer.ShouldBe("Done. c3: The user declined this action");
        Tools.Refunded.ShouldNotContain(number);
    }

    [Fact]
    public async Task nobody_approving_in_time_is_an_answer_too()
    {
        var id = Guid.NewGuid();

        var asked = await Run(new AskAgent(id, $"refund order {order()}", MaxTurns: 5));
        var timedOut = await asked.PlayScheduledMessagesAsync(30.Seconds());

        timedOut.Executed.SingleMessage<AgentAnswered>().Answer.ShouldBe("Done. c3: Nobody approved this action in time");
    }

    [Fact]
    public async Task stops_after_the_last_turn()
    {
        var id = Guid.NewGuid();

        var session = await Run(new AskAgent(id, "what is the meaning of life", MaxTurns: 4));

        session.Executed.SingleMessage<AgentGaveUp>().Turns.ShouldBe(4);
        session.Executed.MessagesOf<CallModel>().Count().ShouldBe(4);
        session.Executed.MessagesOf<RunTool>().Count().ShouldBe(3);
    }

    [Fact]
    public async Task a_tool_that_keeps_failing_becomes_an_observation()
    {
        var number = order();
        Tools.Broken.Add($"refund_policy:{number}");
        var id = Guid.NewGuid();

        var session = await Run(new AskAgent(id, $"refund order {number}", MaxTurns: 5));

        // Retried, dead lettered, and its Fault<RunTool> came back to the run as a result for the model
        session.MovedToErrorQueue.SingleMessage<RunTool>().Call.Tool.ShouldBe("refund_policy");
        session.Executed.SingleMessage<ApprovalNeeded>();

        var run = await LoadSaga<AgentRun>(id);
        run!.Conversation.Select(x => x.Text).ShouldContain("c2: The tool failed: refund_policy returned 503");
    }
}
