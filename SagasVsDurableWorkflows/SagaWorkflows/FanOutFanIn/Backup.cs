using JasperFx;
using Wolverine;

namespace SagaWorkflows.FanOutFanIn;

// Rebuilt from Durable Functions' fan-out/fan-in sample (the "cloud backup" walkthrough, BackupSiteContent): list the
// files under a directory, copy every one in parallel, then add up the bytes copied. Restate's parallelizework example
// and Dapr's WorkflowFanOutFanIn have the same shape: split, run the parts in parallel, aggregate.

public record BackupDirectory(Guid BackupId, string RootDirectory);

public record BackupFile(Guid BackupId, string Path);
public record FileBackedUp(Guid BackupId, string Path, long Bytes);

public record BackupCompleted(Guid BackupId, int FilesCopied, long TotalBytes, string[] Failed);

public class Backup : Saga, IRevisioned
{
    public Guid Id { get; set; }

    // Every reply for this backup lands on the same document. IRevisioned turns a lost update into a
    // ConcurrencyException, which Program.cs retries
    public int Version { get; set; }

    // What a workflow tool keeps as a list of pending tasks. Removing a path as its reply arrives also makes a
    // duplicate reply harmless
    public List<string> Pending { get; set; } = [];
    public List<string> Failed { get; set; } = [];
    public int FilesCopied { get; set; }
    public long TotalBytes { get; set; }

    // The listing is the first activity; doing it here is fine because it is a read, safe to repeat on a retry
    public static (Backup, OutgoingMessages) Start(BackupDirectory command, BackupStorage storage)
    {
        var files = storage.List(command.RootDirectory);
        var backup = new Backup { Id = command.BackupId, Pending = [..files] };

        // Fan out: one message per file, processed in parallel by BackupFileHandler wherever it runs
        var messages = new OutgoingMessages();
        messages.AddRange(files.Select(file => new BackupFile(command.BackupId, file)));

        // Task.WhenAll over nothing finishes at once. Here nothing would ever reply, so say so now
        if (files.Length == 0) messages.Add(backup.finishWhenDone()!);

        return (backup, messages);
    }

    // Fan in
    public BackupCompleted? Handle(FileBackedUp copied)
    {
        if (!Pending.Remove(copied.Path)) return null;

        FilesCopied++;
        TotalBytes += copied.Bytes;
        return finishWhenDone();
    }

    // A file that could not be copied after its retries comes back as its Fault<T> (Wolverine asks for it because
    // this method exists). Durable Functions would fail the whole orchestration at Task.WhenAll; this carries on
    public BackupCompleted? Handle(Fault<BackupFile> fault)
    {
        if (!Pending.Remove(fault.Message.Path)) return null;

        Failed.Add(fault.Message.Path);
        return finishWhenDone();
    }

    private BackupCompleted? finishWhenDone()
    {
        if (Pending.Count > 0) return null;

        MarkCompleted();
        return new BackupCompleted(Id, FilesCopied, TotalBytes, [..Failed]);
    }
}
