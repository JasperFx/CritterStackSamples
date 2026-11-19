using SagaWorkflows.FanOutFanIn;

namespace SagaWorkflows.Specs.FanOutFanIn;

// Durable Functions' cloud backup: one message per file, copied in parallel, and the replies all come back to one
// saga. Global partitioning with inferred grouping hands that saga its replies one at a time.

[BobcatFeature("A backup copies every file in parallel and adds up the bytes")]
[Collection(nameof(DocumentSagaCollection))]
public class backing_up_a_directory(DocumentSagas app) : SagaWorkflowSpec(app)
{
    private Guid BackupId { get; } = Guid.NewGuid();
    private string Root { get; } = $"/sites/{Guid.NewGuid():N}";

    [BobcatSpec(typeof(BackupDirectory))]
    public async Task twenty_five_files_are_copied_and_counted_once_each()
    {
        DirectoryHolds(Root, 25);

        await WhenReceived(new BackupDirectory(BackupId, Root));

        ThenSentExactly(typeof(BackupFile), 25);
        ThenMessageSent(new BackupCompleted(BackupId, 25, Enumerable.Range(1, 25).Sum(i => i * 100L), []));
        await ThenNoSaga<Backup>(BackupId);
    }

    // UseInferredMessageGrouping(): every reply is grouped by the saga it belongs to, so one backup's replies share a
    // partition, and the saga is never saved by two replies at once
    [BobcatSpec(typeof(BackupDirectory))]
    public async Task every_reply_is_grouped_by_its_backup()
    {
        DirectoryHolds(Root, 6);

        await WhenReceived(new BackupDirectory(BackupId, Root));

        ThenGroupedBy<FileBackedUp>(BackupId);
    }

    // A file that cannot be copied comes back as Fault<BackupFile>; the backup reports it and carries on
    [BobcatSpec(typeof(BackupDirectory))]
    public async Task a_file_that_cannot_be_copied_is_reported_and_the_rest_carry_on()
    {
        DirectoryHolds(Root, 6);
        FileCannotBeRead($"{Root}/file-03.txt");

        await WhenReceived(new BackupDirectory(BackupId, Root));

        ThenDeadLettered<BackupFile>();
        ThenMessageSent(Specify<BackupCompleted>()
            .With(x => x.FilesCopied, 5)
            .With(x => x.Failed, Expect.Exactly($"{Root}/file-03.txt")));
    }

    // Nothing to fan out, so nothing will ever reply: the saga has to notice that itself
    [BobcatSpec(typeof(BackupDirectory))]
    public async Task an_empty_directory_finishes_at_once()
    {
        await WhenReceived(new BackupDirectory(BackupId, Root));

        ThenNoMessageSent<BackupFile>();
        ThenMessageSent(new BackupCompleted(BackupId, 0, 0, []));
    }
}
