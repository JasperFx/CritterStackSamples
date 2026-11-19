using JasperFx;
using Wolverine;

namespace SagaWorkflows.ChildWorkflows;

// Rebuilt from Durable Functions' sub-orchestrations sample (ProvisionNewDevices calling DeviceProvisioningOrchestration
// once per device and awaiting them all). Temporal's child workflows are the same idea. Here the parent and the child
// are each an ordinary saga; the child reports back to the parent with a message that names the parent.

public record RollOutDevices(Guid DeviceRolloutId, string[] DeviceIds);

// The child's result, sent to the parent. DeviceRolloutId is how Wolverine finds the parent saga
public record DeviceProvisioned(Guid DeviceRolloutId, string DeviceId) : IFanInReply;
public record DeviceProvisioningFailed(Guid DeviceRolloutId, string DeviceId, string Reason) : IFanInReply;

public record RolloutCompleted(Guid RolloutId, string[] Provisioned, string[] Failed);

// The parent: start a child per device, then wait for all of them
public class DeviceRollout : Saga, IRevisioned
{
    public Guid Id { get; set; }

    public List<string> Pending { get; set; } = [];
    public List<string> Provisioned { get; set; } = [];
    public List<string> Failed { get; set; } = [];

    public static (DeviceRollout, OutgoingMessages) Start(RollOutDevices command)
    {
        var rollout = new DeviceRollout { Id = command.DeviceRolloutId, Pending = [..command.DeviceIds] };

        // Starting a child workflow is sending the message that starts the child saga
        var children = new OutgoingMessages();
        children.AddRange(command.DeviceIds.Select(device =>
            new ProvisionDevice(Guid.NewGuid(), command.DeviceRolloutId, device)));

        return (rollout, children);
    }

    public RolloutCompleted? Handle(DeviceProvisioned child)
    {
        if (!Pending.Remove(child.DeviceId)) return null;
        Provisioned.Add(child.DeviceId);
        return completeWhenAllAreBack();
    }

    public RolloutCompleted? Handle(DeviceProvisioningFailed child)
    {
        if (!Pending.Remove(child.DeviceId)) return null;
        Failed.Add(child.DeviceId);
        return completeWhenAllAreBack();
    }

    private RolloutCompleted? completeWhenAllAreBack()
    {
        if (Pending.Count > 0) return null;

        MarkCompleted();
        return new RolloutCompleted(Id, [..Provisioned], [..Failed]);
    }
}
