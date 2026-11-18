using JasperFx.Core;
using SagaWorkflows.ChildWorkflows;
using Wolverine.Tracking;

namespace SagaWorkflows.Tests;

public class ChildWorkflowTests(AppFixture fixture) : WorkflowContext(fixture)
{
    private DevicePortal Portal => Service<DevicePortal>();

    private static string[] devices(int count) => Enumerable.Range(1, count).Select(_ => $"dev-{Guid.NewGuid():N}").ToArray();

    [Fact]
    public async Task the_rollout_finishes_when_every_child_has_reported_back()
    {
        var ids = devices(3);
        var rolloutId = Guid.NewGuid();

        var started = await Run(new RollOutDevices(rolloutId, ids));

        // Three children, each now waiting for its device
        var packages = started.Executed.MessagesOf<SendPackageUrlToDevice>().ToArray();
        packages.Select(x => x.DeviceId).Order().ShouldBe(ids.Order());
        (await LoadSaga<DeviceRollout>(rolloutId))!.Pending.Count.ShouldBe(3);

        // Two devices acknowledge
        await Run(new DownloadAcknowledged(packages[0].DeviceProvisioningId));
        await Run(new DownloadAcknowledged(packages[1].DeviceProvisioningId));
        (await LoadSaga<DeviceRollout>(rolloutId))!.Pending.ShouldBe([packages[2].DeviceId]);

        // The third never does: an hour later its child gives up, and the parent finishes
        var timedOut = await started.PlayScheduledMessagesAsync(30.Seconds());

        var completed = timedOut.Executed.SingleMessage<RolloutCompleted>();
        completed.Provisioned.Order().ShouldBe(new[] { packages[0].DeviceId, packages[1].DeviceId }.Order());
        completed.Failed.ShouldBe([packages[2].DeviceId]);
        (await LoadSaga<DeviceRollout>(rolloutId)).ShouldBeNull();
    }

    [Fact]
    public async Task a_child_that_fails_is_a_result_for_the_parent()
    {
        var ids = devices(2);
        Portal.BrokenDevices.Add(ids[1]);
        var rolloutId = Guid.NewGuid();

        var started = await Run(new RollOutDevices(rolloutId, ids));
        started.MovedToErrorQueue.SingleMessage<CreateInstallationPackage>().DeviceId.ShouldBe(ids[1]);
        started.Executed.SingleMessage<DeviceProvisioningFailed>().Reason.ShouldContain("No installation package");

        var package = started.Executed.SingleMessage<SendPackageUrlToDevice>();
        var finished = await Run(new DownloadAcknowledged(package.DeviceProvisioningId));

        var completed = finished.Executed.SingleMessage<RolloutCompleted>();
        completed.Provisioned.ShouldBe([ids[0]]);
        completed.Failed.ShouldBe([ids[1]]);
    }
}
