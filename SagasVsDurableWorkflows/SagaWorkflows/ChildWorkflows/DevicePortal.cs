using System.Collections.Concurrent;

namespace SagaWorkflows.ChildWorkflows;

/// <summary>Package storage and the channel to the devices, in memory</summary>
public class DevicePortal
{
    public ConcurrentBag<string> BrokenDevices { get; } = [];
    public ConcurrentQueue<SendPackageUrlToDevice> SentToDevices { get; } = new();
    public ConcurrentQueue<RolloutCompleted> Completed { get; } = new();
}

public static class DevicePortalHandler
{
    public static PackageCreated Handle(CreateInstallationPackage command, DevicePortal portal)
    {
        if (portal.BrokenDevices.Contains(command.DeviceId))
            throw new InvalidOperationException($"No installation package can be built for {command.DeviceId}");

        return new PackageCreated(command.DeviceProvisioningId, new Uri($"https://packages.example.com/{command.DeviceId}"));
    }

    public static void Handle(SendPackageUrlToDevice command, DevicePortal portal) => portal.SentToDevices.Enqueue(command);

    public static void Handle(RolloutCompleted completed, DevicePortal portal) => portal.Completed.Enqueue(completed);
}
