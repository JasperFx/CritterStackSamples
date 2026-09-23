using Bobcat;
using Bobcat.Xunit;
using CritterCrush.Scheduling;
using CritterCrush.Volunteering;
using Xunit;

namespace CritterCrush.Specs;

/// <summary>
/// Projected specifications for ApplyToVolunteer, ReviewVolunteerApplication, ApproveVolunteer, RejectVolunteerApplication.
/// </summary>
/// <remarks>
/// Steps render from the marker comments in each test body; the verdict comes from the
/// runner. [BobcatSlice] carries the BINDING only — the slice's domain, chapter and pattern
/// are stated once, on the event model, and merge in by slice name.
/// </remarks>
[BobcatScenario]
[BobcatFeature("Volunteering")]
[Collection(CritterCrushHost.CollectionName)]
public class VolunteeringSpecs(CritterCrushHost host) : CritterCrushSpec(host)
{
    [Fact]
    [BobcatSlice(SliceType = typeof(ApplyToVolunteer))]
    public async Task Somebody_applies_to_volunteer()
    {
        var applicantOwnerId = Guid.NewGuid();

        await GivenNoEvents<VolunteerApplication>(applicantOwnerId);

        await WhenPosted(new ApplyToVolunteer(applicantOwnerId, "HomeChecks"),
            "/api/volunteering/applytovolunteer");

        ThenEvents(typeof(VolunteerApplicationSubmitted));
        await ThenStreamIsStarted(typeof(VolunteerApplication), applicantOwnerId);
        Assert.Equal("HomeChecks", TheEvent<VolunteerApplicationSubmitted>().AreasOfInterest);
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ApplyToVolunteer))]
    public async Task Applying_twice_is_refused()
    {
        var applicantOwnerId = Guid.NewGuid();

        await GivenEvents<VolunteerApplication>(applicantOwnerId,
            new VolunteerApplicationSubmitted(applicantOwnerId, "HomeChecks"));

        await WhenPosted(new ApplyToVolunteer(applicantOwnerId, "Transport"),
            "/api/volunteering/applytovolunteer");

        // Refused with "You have already applied to volunteer".
        ThenResponseIs(400);
        ThenNoEvents();
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ReviewVolunteerApplication))]
    public async Task An_admin_reviews_a_submitted_application()
    {
        var applicantOwnerId = Guid.NewGuid();

        await GivenEvents<VolunteerApplication>(applicantOwnerId,
            new VolunteerApplicationSubmitted(applicantOwnerId, "HomeChecks"));

        await WhenPosted(new ReviewVolunteerApplication(applicantOwnerId),
            "/api/volunteering/reviewvolunteerapplication");

        ThenEvents(typeof(VolunteerApplicationReviewed));
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ReviewVolunteerApplication))]
    public async Task An_application_already_decided_is_not_reviewed_again()
    {
        var applicantOwnerId = Guid.NewGuid();

        await GivenEvents<VolunteerApplication>(applicantOwnerId,
            new VolunteerApplicationSubmitted(applicantOwnerId, "HomeChecks"),
            new VolunteerApplicationReviewed(applicantOwnerId),
            new VolunteerApproved(applicantOwnerId));

        await WhenPosted(new ReviewVolunteerApplication(applicantOwnerId),
            "/api/volunteering/reviewvolunteerapplication");

        // Refused with "This application has already been decided".
        ThenResponseIs(400);
        ThenNoEvents();
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ApproveVolunteer))]
    public async Task A_reviewed_applicant_is_approved()
    {
        var applicantOwnerId = Guid.NewGuid();

        await GivenEvents<VolunteerApplication>(applicantOwnerId,
            new VolunteerApplicationSubmitted(applicantOwnerId, "HomeChecks"),
            new VolunteerApplicationReviewed(applicantOwnerId));

        await WhenPosted(new ApproveVolunteer(applicantOwnerId), "/api/volunteering/approvevolunteer");

        ThenEvents(typeof(VolunteerApproved));
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ApproveVolunteer))]
    public async Task An_applicant_nobody_reviewed_is_not_approved()
    {
        var applicantOwnerId = Guid.NewGuid();

        await GivenEvents<VolunteerApplication>(applicantOwnerId,
            new VolunteerApplicationSubmitted(applicantOwnerId, "HomeChecks"));

        await WhenPosted(new ApproveVolunteer(applicantOwnerId), "/api/volunteering/approvevolunteer");

        // Refused with "This application has not been reviewed".
        ThenResponseIs(400);
        ThenNoEvents();
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RejectVolunteerApplication))]
    public async Task A_reviewed_applicant_is_rejected_with_a_reason()
    {
        var applicantOwnerId = Guid.NewGuid();

        await GivenEvents<VolunteerApplication>(applicantOwnerId,
            new VolunteerApplicationSubmitted(applicantOwnerId, "HomeChecks"),
            new VolunteerApplicationReviewed(applicantOwnerId));

        await WhenPosted(new RejectVolunteerApplication(applicantOwnerId, "Outside our current coverage area"),
            "/api/volunteering/rejectvolunteerapplication");

        ThenEvents(typeof(VolunteerApplicationRejected));
        Assert.Equal("Outside our current coverage area", TheEvent<VolunteerApplicationRejected>().Reason);
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RejectVolunteerApplication))]
    public async Task An_approved_volunteer_is_not_then_rejected()
    {
        var applicantOwnerId = Guid.NewGuid();

        await GivenEvents<VolunteerApplication>(applicantOwnerId,
            new VolunteerApplicationSubmitted(applicantOwnerId, "HomeChecks"),
            new VolunteerApplicationReviewed(applicantOwnerId),
            new VolunteerApproved(applicantOwnerId));

        await WhenPosted(new RejectVolunteerApplication(applicantOwnerId, "Outside our current coverage area"),
            "/api/volunteering/rejectvolunteerapplication");

        // Refused with "This application has already been decided".
        ThenResponseIs(400);
        ThenNoEvents();
    }


    /// <summary>
    /// Wolverine's own not-found guard, which answers before Validate runs. Declared on the model
    /// (bobcat#337) rather than carried as an orphan: that declaration is what makes the write
    /// model non-nullable, which is what makes a hand-written null check unreachable.
    /// </summary>
    [Fact]
    [BobcatSlice(SliceType = typeof(ReviewVolunteerApplication))]
    public async Task Reviewing_an_application_that_does_not_exist_is_not_found()
    {
        await WhenPosted(new ReviewVolunteerApplication(Guid.NewGuid()), "/api/volunteering/reviewvolunteerapplication");

        ThenResponseIs(404);
        ThenNoEvents();
    }

    /// <summary>
    /// Wolverine's own not-found guard, which answers before Validate runs. Declared on the model
    /// (bobcat#337) rather than carried as an orphan: that declaration is what makes the write
    /// model non-nullable, which is what makes a hand-written null check unreachable.
    /// </summary>
    [Fact]
    [BobcatSlice(SliceType = typeof(ApproveVolunteer))]
    public async Task Approving_an_application_that_does_not_exist_is_not_found()
    {
        await WhenPosted(new ApproveVolunteer(Guid.NewGuid()), "/api/volunteering/approvevolunteer");

        ThenResponseIs(404);
        ThenNoEvents();
    }

    /// <summary>
    /// Wolverine's own not-found guard, which answers before Validate runs. Declared on the model
    /// (bobcat#337) rather than carried as an orphan: that declaration is what makes the write
    /// model non-nullable, which is what makes a hand-written null check unreachable.
    /// </summary>
    [Fact]
    [BobcatSlice(SliceType = typeof(RejectVolunteerApplication))]
    public async Task Rejecting_an_application_that_does_not_exist_is_not_found()
    {
        await WhenPosted(new RejectVolunteerApplication(Guid.NewGuid(), "Not enough availability"), "/api/volunteering/rejectvolunteerapplication");

        ThenResponseIs(404);
        ThenNoEvents();
    }
}
