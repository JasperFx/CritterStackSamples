using Bobcat;
using Bobcat.Xunit;
using CritterCrush.Scheduling;
using CritterCrush.Volunteering;
using Xunit;

namespace CritterCrush.Specs;

/// <summary>
/// Projected specifications for VolunteerApplicationsQueue.
/// </summary>
/// <remarks>
/// Steps render from the decorated helpers on <see cref="CritterCrushSpec"/>; the verdict comes from
/// the runner. <c>[BobcatSpec]</c> is the <c>[Fact]</c>, opens the recording and carries the slice
/// BINDING only — the slice's domain, chapter and pattern are stated once, on the event model
/// (CritterCrushEventModel.cs in the app), and merge in by slice name. Through Bobcat 0.27 this was
/// <c>[Fact]</c> + <c>[BobcatSlice]</c> with <c>[BobcatScenario]</c> on the class, and forgetting the
/// last one rendered nothing at all (issue #379); <c>[BobcatSpec]</c> cannot come apart that way.
/// </remarks>
[BobcatFeature("VolunteerApplicationsQueue")]
[Collection(CritterCrushHost.CollectionName)]
public class VolunteerApplicationsQueueSpecs(CritterCrushHost host) : CritterCrushSpec(host)
{
    [BobcatSpec(typeof(VolunteerApplicationsQueue))]
    public async Task A_new_application_shows_as_submitted()
    {
        var applicantOwnerId = Guid.NewGuid();

        await GivenEventsOn<VolunteerApplication>(applicantOwnerId,
            new VolunteerApplicationSubmitted(applicantOwnerId, "HomeChecks"));

        // Single-stream: the document id IS the stream id, which here is the applicant's own id.
        var row = await ThenReadModel<VolunteerApplicationsQueue>(applicantOwnerId);
        Assert.Equal("HomeChecks", row.AreasOfInterest);
        Assert.Equal(VolunteerApplicationStatus.Submitted, row.Status);
    }

    [BobcatSpec(typeof(VolunteerApplicationsQueue))]
    public async Task An_approved_application_shows_the_decision()
    {
        var applicantOwnerId = Guid.NewGuid();

        await GivenEventsOn<VolunteerApplication>(applicantOwnerId,
            new VolunteerApplicationSubmitted(applicantOwnerId, "FosterSupport"),
            new VolunteerApplicationReviewed(applicantOwnerId),
            new VolunteerApproved(applicantOwnerId));

        var row = await ThenReadModel<VolunteerApplicationsQueue>(applicantOwnerId);
        Assert.Equal("FosterSupport", row.AreasOfInterest);
        Assert.Equal(VolunteerApplicationStatus.Approved, row.Status);
    }
}
