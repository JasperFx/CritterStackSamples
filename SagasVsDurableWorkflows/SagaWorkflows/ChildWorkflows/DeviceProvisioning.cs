using JasperFx;
using JasperFx.Core;
using Wolverine;

namespace SagaWorkflows.ChildWorkflows;

public record ProvisionDevice(Guid DeviceProvisioningId, Guid RolloutId, string DeviceId);

public record CreateInstallationPackage(Guid DeviceProvisioningId, string DeviceId);
public record PackageCreated(Guid DeviceProvisioningId, Uri PackageUrl);

public record SendPackageUrlToDevice(Guid DeviceProvisioningId, string DeviceId, Uri PackageUrl);

// From the device, over whatever channel it uses (Durable Functions' "DownloadCompletedAck" external event)
public record DownloadAcknowledged(Guid DeviceProvisioningId);
public record DownloadTimedOut(Guid DeviceProvisioningId) : TimeoutMessage(1.Hours());

// The child workflow
public class DeviceProvisioning : CompensatingSaga, IRevisioned
{
    public Guid Id { get; set; }
    public int Version { get; set; }

    public Guid RolloutId { get; set; }
    public string DeviceId { get; set; } = "";

    public static (DeviceProvisioning, CreateInstallationPackage) Start(ProvisionDevice command)
        => (new DeviceProvisioning { Id = command.DeviceProvisioningId, RolloutId = command.RolloutId, DeviceId = command.DeviceId },
            new CreateInstallationPackage(command.DeviceProvisioningId, command.DeviceId));

    public (SendPackageUrlToDevice, DownloadTimedOut) Handle(PackageCreated created)
        => (new SendPackageUrlToDevice(Id, DeviceId, created.PackageUrl), new DownloadTimedOut(Id));

    // Returning to the parent is just another cascaded message
    public DeviceProvisioned Handle(DownloadAcknowledged _)
    {
        MarkCompleted();
        return new DeviceProvisioned(RolloutId, DeviceId);
    }

    public DeviceProvisioningFailed Handle(DownloadTimedOut _)
    {
        MarkCompleted();
        return new DeviceProvisioningFailed(RolloutId, DeviceId, "The device never acknowledged the download");
    }

    // The child failing is a result for the parent, not an exception that crosses into it
    public DeviceProvisioningFailed Compensate(CreateInstallationPackage failed, ExceptionInfo ex)
    {
        MarkCompleted();
        return new DeviceProvisioningFailed(RolloutId, DeviceId, ex.Message);
    }

    // An acknowledgement after the child already timed out
    public static void NotFound(DownloadAcknowledged _) { }
}
