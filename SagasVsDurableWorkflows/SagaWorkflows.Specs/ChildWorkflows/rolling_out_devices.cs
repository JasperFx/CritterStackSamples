using JasperFx.Core;
using SagaWorkflows.ChildWorkflows;

namespace SagaWorkflows.Specs.ChildWorkflows;

// Durable Functions' sub-orchestrations: a rollout saga starts a provisioning saga per device and waits for all of
// them. Starting a child is sending its start message; the child reports back with a message that names the parent.

[BobcatFeature("A rollout waits for a child workflow per device")]
[Collection(nameof(DocumentSagaCollection))]
public class rolling_out_devices(DocumentSagas app) : SagaWorkflowSpec(app)
{
    private Guid RolloutId { get; } = Guid.NewGuid();

    // Two devices never acknowledge their download: an hour later their children give up, and the parent finishes
    [BobcatSpec(typeof(DownloadTimedOut))]
    public async Task the_rollout_finishes_when_every_child_has_reported_back()
    {
        await WhenReceived(new RollOutDevices(RolloutId, ["dev-1", "dev-2"]));
        ThenSentExactly(typeof(SendPackageUrlToDevice), 2);

        await WhenTimePasses(1.Hours());

        ThenMessageSent(Specify<RolloutCompleted>()
            .With(x => x.Provisioned, Expect.Exactly())
            .With(x => x.Failed, Expect.InAnyOrder("dev-1", "dev-2")));
        await ThenNoSaga<DeviceRollout>(RolloutId);
    }

    // A child failing is a result for the parent, not an exception that crosses into it
    [BobcatSpec(typeof(RollOutDevices))]
    public async Task a_child_that_fails_is_a_result_for_the_parent()
    {
        DeviceIsBroken("dev-9");

        await WhenReceived(new RollOutDevices(RolloutId, ["dev-9"]));

        ThenDeadLettered<CreateInstallationPackage>();
        ThenMessageSent(new RolloutCompleted(RolloutId, [], ["dev-9"]));
    }

    // [DiscardIfSagaMissing] on the DownloadAcknowledged message type: a device that answers after its provisioning
    // timed out is logged and dropped, wherever the answer goes
    [BobcatSpec(typeof(DownloadAcknowledged))]
    public async Task an_acknowledgement_after_the_child_timed_out_is_dropped()
    {
        await WhenReceived(new RollOutDevices(RolloutId, ["dev-1"]));
        var child = TheMessagesSent.OfType<SendPackageUrlToDevice>().Single().DeviceProvisioningId;
        await WhenTimePasses(1.Hours());

        await WhenReceived(new DownloadAcknowledged(child));

        ThenHandled<DownloadAcknowledged>();
        ThenNotDeadLettered<DownloadAcknowledged>();
        ThenNoMessageSent<DeviceProvisioned>();
    }
}
