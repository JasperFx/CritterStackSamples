using Bobcat;
using CritterCrush.Scheduling;
using CritterCrush.Volunteering;
using Xunit;

namespace CritterCrush.Specs;

/// <summary>
/// Projected specifications for VolunteerApplicationsQueue.
/// </summary>
/// <remarks>
/// Steps render from the marker comments in each test body; the verdict comes from the
/// runner. [BobcatSlice] carries the BINDING only — the slice's domain, chapter and pattern
/// are stated once, on the event model, and merge in by slice name.
/// </remarks>
[BobcatFeature("VolunteerApplicationsQueue")]
[BobcatSlice(SliceType = typeof(VolunteerApplicationsQueue))]
[Collection(CritterCrushHost.CollectionName)]
public class VolunteerApplicationsQueueSpecs(CritterCrushHost host) : CritterCrushSpec(host)
{
    [Fact]
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

    [Fact]
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
