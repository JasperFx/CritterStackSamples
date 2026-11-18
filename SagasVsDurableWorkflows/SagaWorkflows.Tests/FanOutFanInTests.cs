using SagaWorkflows.FanOutFanIn;
using Wolverine.Tracking;

namespace SagaWorkflows.Tests;

public class FanOutFanInTests(AppFixture fixture) : WorkflowContext(fixture)
{
    private BackupStorage Storage => Service<BackupStorage>();

    private string directoryOf(int files)
    {
        var root = $"/sites/{Guid.NewGuid():N}";
        for (var i = 1; i <= files; i++) Storage.Add($"{root}/file-{i:00}.txt", i * 100);
        return root;
    }

    [Fact]
    public async Task copies_every_file_in_parallel_then_adds_up_the_bytes()
    {
        var root = directoryOf(25);
        var id = Guid.NewGuid();

        var session = await Run(new BackupDirectory(id, root));

        var completed = session.Executed.SingleMessage<BackupCompleted>();
        completed.FilesCopied.ShouldBe(25);
        completed.TotalBytes.ShouldBe(Enumerable.Range(1, 25).Sum(i => i * 100L));
        completed.Failed.ShouldBeEmpty();

        // Each reply was counted exactly once, however many lost the race to save the saga and ran again
        session.Executed.MessagesOf<FileBackedUp>().Count().ShouldBe(25);
        (await LoadSaga<Backup>(id)).ShouldBeNull();
    }

    [Fact]
    public async Task a_file_that_cannot_be_copied_is_reported_and_the_rest_carry_on()
    {
        var root = directoryOf(6);
        Storage.Unreadable.Add($"{root}/file-03.txt");
        var id = Guid.NewGuid();

        var session = await Run(new BackupDirectory(id, root));

        session.MovedToErrorQueue.SingleMessage<BackupFile>().Path.ShouldBe($"{root}/file-03.txt");

        var completed = session.Executed.SingleMessage<BackupCompleted>();
        completed.FilesCopied.ShouldBe(5);
        completed.Failed.ShouldBe([$"{root}/file-03.txt"]);
    }

    [Fact]
    public async Task an_empty_directory_finishes_at_once()
    {
        var id = Guid.NewGuid();

        // Nothing to fan out, so nothing will ever reply: the saga has to notice that itself
        var session = await Run(new BackupDirectory(id, $"/sites/{Guid.NewGuid():N}"));

        session.Executed.MessagesOf<BackupFile>().ShouldBeEmpty();
        session.Executed.SingleMessage<BackupCompleted>().FilesCopied.ShouldBe(0);
        (await LoadSaga<Backup>(id)).ShouldBeNull();
    }

    [Fact]
    public void a_duplicate_reply_is_not_counted_twice()
    {
        var backup = new Backup { Id = Guid.NewGuid(), Pending = ["/a", "/b"] };

        backup.Handle(new FileBackedUp(backup.Id, "/a", 10)).ShouldBeNull();
        backup.Handle(new FileBackedUp(backup.Id, "/a", 10)).ShouldBeNull();

        backup.Handle(new FileBackedUp(backup.Id, "/b", 5))!.TotalBytes.ShouldBe(15);
    }
}
