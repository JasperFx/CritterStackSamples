using Bobcat;
using Xunit;
using CritterCrush.Volunteering;

namespace CritterCrush.Specs;

/// <summary>
/// Projected specifications for ApplyToVolunteer, ReviewVolunteerApplication, ApproveVolunteer, RejectVolunteerApplication.
/// </summary>
/// <remarks>
/// Steps render from the marker comments in each test body; the verdict comes from the
/// runner. [BobcatSlice] carries the BINDING only — the slice's domain, chapter and pattern
/// are stated once, on the event model, and merge in by slice name.
/// </remarks>
[BobcatFeature("Volunteering")]
[Collection(CritterCrushHost.CollectionName)]
public class VolunteeringSpecs(CritterCrushHost fixture) : CritterCrushSpec(fixture)
{
    [Fact]
    [BobcatSlice(SliceType = typeof(ApplyToVolunteer))]
    public void Somebody_applies_to_volunteer()
    {
        // When ApplyToVolunteer is posted to "/api/volunteering/applytovolunteer" (applicantOwnerId = {streamId}, areasOfInterest = HomeChecks)
        // Then VolunteerApplicationSubmitted is emitted (areasOfInterest = HomeChecks)
        // Then a VolunteerApplication stream is started with id "{streamId}"

        throw new NotImplementedException("ApplyToVolunteer: Somebody applies to volunteer");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ApplyToVolunteer))]
    public void Applying_twice_is_refused()
    {
        // Given VolunteerApplicationSubmitted (areasOfInterest = HomeChecks)
        // When ApplyToVolunteer is posted to "/api/volunteering/applytovolunteer" (applicantOwnerId = {streamId}, areasOfInterest = Transport)
        // # refused with: "You have already applied to volunteer"
        // Then the response is 400
        // And no events are emitted

        throw new NotImplementedException("ApplyToVolunteer: Applying twice is refused");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ReviewVolunteerApplication))]
    public void An_admin_reviews_a_submitted_application()
    {
        // Given VolunteerApplicationSubmitted (areasOfInterest = HomeChecks)
        // When ReviewVolunteerApplication is posted to "/api/volunteering/reviewvolunteerapplication" (applicantOwnerId = {streamId})
        // Then VolunteerApplicationReviewed is emitted

        throw new NotImplementedException("ReviewVolunteerApplication: An admin reviews a submitted application");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ReviewVolunteerApplication))]
    public void An_application_already_decided_is_not_reviewed_again()
    {
        // Given VolunteerApplicationSubmitted (areasOfInterest = HomeChecks)
        // Given VolunteerApplicationReviewed
        // Given VolunteerApproved
        // When ReviewVolunteerApplication is posted to "/api/volunteering/reviewvolunteerapplication" (applicantOwnerId = {streamId})
        // # refused with: "This application has already been decided"
        // Then the response is 400
        // And no events are emitted

        throw new NotImplementedException("ReviewVolunteerApplication: An application already decided is not reviewed again");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ReviewVolunteerApplication))]
    public void Reviewing_an_application_that_does_not_exist_is_not_found()
    {
        // When ReviewVolunteerApplication is posted to "/api/volunteering/reviewvolunteerapplication" (applicantOwnerId = {streamId})
        // # refused with: "No volunteer application with that id"
        // Then the response is 404
        // And no events are emitted

        throw new NotImplementedException("ReviewVolunteerApplication: Reviewing an application that does not exist is not found");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ApproveVolunteer))]
    public void A_reviewed_applicant_is_approved()
    {
        // Given VolunteerApplicationSubmitted (areasOfInterest = HomeChecks)
        // Given VolunteerApplicationReviewed
        // When ApproveVolunteer is posted to "/api/volunteering/approvevolunteer" (applicantOwnerId = {streamId})
        // Then VolunteerApproved is emitted

        throw new NotImplementedException("ApproveVolunteer: A reviewed applicant is approved");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ApproveVolunteer))]
    public void An_applicant_nobody_reviewed_is_not_approved()
    {
        // Given VolunteerApplicationSubmitted (areasOfInterest = HomeChecks)
        // When ApproveVolunteer is posted to "/api/volunteering/approvevolunteer" (applicantOwnerId = {streamId})
        // # refused with: "This application has not been reviewed"
        // Then the response is 400
        // And no events are emitted

        throw new NotImplementedException("ApproveVolunteer: An applicant nobody reviewed is not approved");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(ApproveVolunteer))]
    public void Approving_an_application_that_does_not_exist_is_not_found()
    {
        // When ApproveVolunteer is posted to "/api/volunteering/approvevolunteer" (applicantOwnerId = {streamId})
        // # refused with: "No volunteer application with that id"
        // Then the response is 404
        // And no events are emitted

        throw new NotImplementedException("ApproveVolunteer: Approving an application that does not exist is not found");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RejectVolunteerApplication))]
    public void A_reviewed_applicant_is_rejected_with_a_reason()
    {
        // Given VolunteerApplicationSubmitted (areasOfInterest = HomeChecks)
        // Given VolunteerApplicationReviewed
        // When RejectVolunteerApplication is posted to "/api/volunteering/rejectvolunteerapplication" (applicantOwnerId = {streamId}, reason = Outside our current coverage area)
        // Then VolunteerApplicationRejected is emitted (reason = Outside our current coverage area)

        throw new NotImplementedException("RejectVolunteerApplication: A reviewed applicant is rejected with a reason");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RejectVolunteerApplication))]
    public void An_approved_volunteer_is_not_then_rejected()
    {
        // Given VolunteerApplicationSubmitted (areasOfInterest = HomeChecks)
        // Given VolunteerApplicationReviewed
        // Given VolunteerApproved
        // When RejectVolunteerApplication is posted to "/api/volunteering/rejectvolunteerapplication" (applicantOwnerId = {streamId}, reason = Outside our current coverage area)
        // # refused with: "This application has already been decided"
        // Then the response is 400
        // And no events are emitted

        throw new NotImplementedException("RejectVolunteerApplication: An approved volunteer is not then rejected");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RejectVolunteerApplication))]
    public void Rejecting_an_application_that_does_not_exist_is_not_found()
    {
        // When RejectVolunteerApplication is posted to "/api/volunteering/rejectvolunteerapplication" (applicantOwnerId = {streamId}, reason = Not enough availability)
        // # refused with: "No volunteer application with that id"
        // Then the response is 404
        // And no events are emitted

        throw new NotImplementedException("RejectVolunteerApplication: Rejecting an application that does not exist is not found");
    }
}
