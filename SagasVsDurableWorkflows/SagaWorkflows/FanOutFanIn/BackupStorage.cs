using System.Collections.Concurrent;

namespace SagaWorkflows.FanOutFanIn;

/// <summary>A file share and blob storage, in memory</summary>
public class BackupStorage
{
    private readonly ConcurrentDictionary<string, long> _files = new();

    public ConcurrentDictionary<string, long> Copied { get; } = new();
    public ConcurrentBag<string> Unreadable { get; } = [];
    public ConcurrentQueue<BackupCompleted> Completed { get; } = new();

    public void Add(string path, long bytes) => _files[path] = bytes;

    public string[] List(string root) => _files.Keys.Where(x => x.StartsWith(root + "/")).Order().ToArray();

    public long Copy(string path)
    {
        if (Unreadable.Contains(path)) throw new IOException($"{path} could not be read");

        var bytes = _files[path];
        Copied[path] = bytes;
        return bytes;
    }
}

public static class BackupFileHandler
{
    public static FileBackedUp Handle(BackupFile command, BackupStorage storage)
        => new(command.BackupId, command.Path, storage.Copy(command.Path));

    public static void Handle(BackupCompleted completed, BackupStorage storage) => storage.Completed.Enqueue(completed);
}
