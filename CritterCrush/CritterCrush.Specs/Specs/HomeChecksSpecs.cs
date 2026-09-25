using Bobcat;
using Bobcat.Xunit;
using CritterCrush.Scheduling;
using CritterCrush.Volunteering;
using Xunit;

namespace CritterCrush.Specs;

/// <summary>
/// Projected specifications for RequestHomeCheck, AcceptHomeCheckAssignment, SubmitHomeCheckReport.
/// </summary>
/// <remarks>
/// Steps render from the marker comments in each test body; the verdict comes from the
/// runner. [BobcatSlice] carries the BINDING only — the slice's domain, chapter and pattern
/// are stated once, on the event model, and merge in by slice name.
/// <para>
/// [BobcatScenario] is what OPENS the recording each test's steps go into. Without it
/// ScenarioRecorder.Current is null, every [BobcatStep] interceptor records into
/// NoStep.Instance, and the suite goes green having rendered nothing at all (issue #379).
/// </para>
/// </remarks>
[BobcatScenario]
[BobcatFeature("HomeChecks")]
[Collection(CritterCrushHost.CollectionName)]
public class HomeChecksSpecs(CritterCrushHost host) : CritterCrushSpec(host)
{
    [Fact]
    [BobcatSlice(SliceType = typeof(RequestHomeCheck))]
    public async Task An_admin_requests_a_home_check()
    {
        var homeCheckId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await GivenNoEvents<HomeCheck>(homeCheckId);

        await WhenPosted(new RequestHomeCheck(homeCheckId, applicationId, ownerId, Guid.NewGuid()),
            "/api/volunteering/requesthomecheck");

        ThenEvents(typeof(HomeCheckRequested));
        await ThenStreamIsStarted(typeof(HomeCheck), homeCheckId);

        var requested = TheEvent<HomeCheckRequested>();
        Assert.Equal(applicationId, requested.ApplicationId);
        Assert.Equal(ownerId, requested.OwnerId);
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RequestHomeCheck))]
    public async Task Requesting_the_same_home_check_twice_is_refused()
    {
        var homeCheckId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();

        await GivenEvents<HomeCheck>(homeCheckId, new HomeCheckRequested(applicationId, ownerId, shelterId));

        await WhenPosted(new RequestHomeCheck(homeCheckId, applicationId, ownerId, shelterId),
            "/api/volunteering/requesthomecheck");

        // Refused with "This home check has already been requested".
        ThenResponseIs(400);
        ThenNoEvents();
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(AcceptHomeCheckAssignment))]
    public async Task A_volunteer_accepts_an_assignment_and_proposes_a_time()
    {
        var homeCheckId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var volunteerOwnerId = Guid.NewGuid();
        var proposedFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<HomeCheck>(homeCheckId, new HomeCheckRequested(Guid.NewGuid(), ownerId, Guid.NewGuid()));

        await WhenPosted(new AcceptHomeCheckAssignment(homeCheckId, volunteerOwnerId, proposedFor),
            "/api/volunteering/accepthomecheckassignment");

        ThenEvents(typeof(HomeCheckAssignmentAccepted));

        var accepted = TheEvent<HomeCheckAssignmentAccepted>();
        Assert.Equal(ownerId, accepted.OwnerId);
        Assert.Equal(volunteerOwnerId, accepted.VolunteerOwnerId);
        Assert.Equal(proposedFor, accepted.ProposedFor);
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(AcceptHomeCheckAssignment))]
    /// <summary>
    /// The scenario the manifest names as <c>coveredBy</c> for ProposeHomeCheckAppointment: the
    /// chain from this command, through the bus, into the automation that starts the appointment
    /// stream, and on into the async projection. That chain is not expressible in the model, which
    /// is exactly why the cover has to be declared rather than inferred.
    /// </summary>
    public async Task Accepting_an_assignment_books_the_home_check_as_an_appointment()
    {
        var homeCheckId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var proposedFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<HomeCheck>(homeCheckId, new HomeCheckRequested(Guid.NewGuid(), ownerId, Guid.NewGuid()));

        await WhenPosted(new AcceptHomeCheckAssignment(homeCheckId, Guid.NewGuid(), proposedFor),
            "/api/volunteering/accepthomecheckassignment");

        // MyAppointments is keyed by OwnerId, not by the stream.
        var mine = await ThenReadModel<MyAppointments>(ownerId);
        Assert.Equal(1, mine.AwaitingConfirmation);
        Assert.Equal(0, mine.Confirmed);
        Assert.Equal(0, mine.Closed);
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(AcceptHomeCheckAssignment))]
    public async Task An_assignment_already_accepted_is_not_accepted_again()
    {
        var homeCheckId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        var proposedFor = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);

        await GivenEvents<HomeCheck>(homeCheckId,
            new HomeCheckRequested(Guid.NewGuid(), ownerId, shelterId),
            new HomeCheckAssignmentAccepted(homeCheckId, ownerId, shelterId, Guid.NewGuid(), proposedFor));

        await WhenPosted(new AcceptHomeCheckAssignment(homeCheckId, Guid.NewGuid(), proposedFor.AddDays(1)),
            "/api/volunteering/accepthomecheckassignment");

        // Refused with "This home check is already assigned".
        ThenResponseIs(400);
        ThenNoEvents();
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(SubmitHomeCheckReport))]
    public async Task A_volunteer_reports_on_a_visit_they_accepted()
    {
        var homeCheckId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var shelterId = Guid.NewGuid();
        const string notes = "Secure garden, calm household, good fit for a shy dog";

        await GivenEvents<HomeCheck>(homeCheckId,
            new HomeCheckRequested(Guid.NewGuid(), ownerId, shelterId),
            new HomeCheckAssignmentAccepted(homeCheckId, ownerId, shelterId, Guid.NewGuid(),
                new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero)));

        await WhenPosted(new SubmitHomeCheckReport(homeCheckId, "Pass", notes),
            "/api/volunteering/submithomecheckreport");

        ThenEvents(typeof(HomeCheckReportSubmitted));

        var submitted = TheEvent<HomeCheckReportSubmitted>();
        Assert.Equal("Pass", submitted.Outcome);
        Assert.Equal(notes, submitted.Notes);
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(SubmitHomeCheckReport))]
    public async Task A_visit_nobody_accepted_cannot_be_reported_on()
    {
        var homeCheckId = Guid.NewGuid();

        await GivenEvents<HomeCheck>(homeCheckId,
            new HomeCheckRequested(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        await WhenPosted(new SubmitHomeCheckReport(homeCheckId, "Pass", "Secure garden"),
            "/api/volunteering/submithomecheckreport");

        // Refused with "Nobody has accepted this home check".
        ThenResponseIs(400);
        ThenNoEvents();
    }


    /// <summary>
    /// Wolverine's own not-found guard, which answers before Validate runs. Declared on the model
    /// (bobcat#337) rather than carried as an orphan: that declaration is what makes the write
    /// model non-nullable, which is what makes a hand-written null check unreachable.
    /// </summary>
    [Fact]
    [BobcatSlice(SliceType = typeof(AcceptHomeCheckAssignment))]
    public async Task Accepting_a_home_check_that_does_not_exist_is_not_found()
    {
        await WhenPosted(new AcceptHomeCheckAssignment(Guid.NewGuid(), Guid.NewGuid(), new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero)), "/api/volunteering/accepthomecheckassignment");

        ThenResponseIs(404);
        ThenNoEvents();
    }

    /// <summary>
    /// Wolverine's own not-found guard, which answers before Validate runs. Declared on the model
    /// (bobcat#337) rather than carried as an orphan: that declaration is what makes the write
    /// model non-nullable, which is what makes a hand-written null check unreachable.
    /// </summary>
    [Fact]
    [BobcatSlice(SliceType = typeof(SubmitHomeCheckReport))]
    public async Task Reporting_on_a_home_check_that_does_not_exist_is_not_found()
    {
        await WhenPosted(new SubmitHomeCheckReport(Guid.NewGuid(), "Pass", "Secure garden, calm household, good fit for a shy dog"), "/api/volunteering/submithomecheckreport");

        ThenResponseIs(404);
        ThenNoEvents();
    }
}
