using Bobcat;
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
/// </remarks>
[BobcatFeature("VolunteerApplicationsQueue")]
// TODO — these are integration slices: give this class the store. Derive from (or
// inject) this repository's host/store fixture; the arrange/act/assert helpers are in
// Bobcat.CritterStack. A unit-tested slice needs none of that — see the model's
// spec-ownership manifest for which slices are which.
[BobcatSlice(SliceType = typeof(VolunteerApplicationsQueue))]
public class VolunteerApplicationsQueueSpecs
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
