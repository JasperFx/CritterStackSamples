using Bobcat;
using Bobcat.Xunit;
using Xunit;
using CritterCrush.Volunteering;

namespace CritterCrush.Specs;

/// <summary>
/// Projected specifications for VolunteerApplicationsQueue.
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
[BobcatFeature("VolunteerApplicationsQueue")]
[Collection(CritterCrushHost.CollectionName)]
[BobcatSlice(SliceType = typeof(VolunteerApplicationsQueue))]
public class VolunteerApplicationsQueueSpecs(CritterCrushHost fixture) : CritterCrushSpec(fixture)
{
    [Fact]
    public void A_new_application_shows_as_submitted()
    {
        // Given VolunteerApplicationSubmitted (applicantOwnerId = 0e5e0021-0000-0000-0000-000000000021, areasOfInterest = HomeChecks)
        // Then the VolunteerApplicationsQueue read model contains (AreasOfInterest = HomeChecks, Status = Submitted)

        throw new NotImplementedException("VolunteerApplicationsQueue: A new application shows as submitted");
    }

    [Fact]
    public void An_approved_application_shows_the_decision()
    {
        // Given VolunteerApplicationSubmitted (applicantOwnerId = 0e5e0022-0000-0000-0000-000000000022, areasOfInterest = FosterSupport)
        // Given VolunteerApplicationReviewed (applicantOwnerId = 0e5e0022-0000-0000-0000-000000000022)
        // Given VolunteerApproved (applicantOwnerId = 0e5e0022-0000-0000-0000-000000000022)
        // Then the VolunteerApplicationsQueue read model contains (AreasOfInterest = FosterSupport, Status = Approved)

        throw new NotImplementedException("VolunteerApplicationsQueue: An approved application shows the decision");
    }
}
