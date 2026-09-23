using Bobcat;
using Bobcat.Xunit;
using Xunit;
using CritterCrush.Volunteering;

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
public class HomeChecksSpecs(CritterCrushHost fixture) : CritterCrushSpec(fixture)
{
    [Fact]
    [BobcatSlice(SliceType = typeof(RequestHomeCheck))]
    public void An_admin_requests_a_home_check()
    {
        // When RequestHomeCheck is posted to "/api/volunteering/requesthomecheck" (homeCheckId = {streamId}, applicationId = a99a0001-0000-0000-0000-000000000001, ownerId = 0e5e0011-0000-0000-0000-000000000011, shelterId = 5e110001-0000-0000-0000-000000000001)
        // Then HomeCheckRequested is emitted (applicationId = a99a0001-0000-0000-0000-000000000001, ownerId = 0e5e0011-0000-0000-0000-000000000011)
        // Then a HomeCheck stream is started with id "{streamId}"

        throw new NotImplementedException("RequestHomeCheck: An admin requests a home check");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(RequestHomeCheck))]
    public void Requesting_the_same_home_check_twice_is_refused()
    {
        // Given HomeCheckRequested (applicationId = a99a0001-0000-0000-0000-000000000001, ownerId = 0e5e0011-0000-0000-0000-000000000011, shelterId = 5e110001-0000-0000-0000-000000000001)
        // When RequestHomeCheck is posted to "/api/volunteering/requesthomecheck" (homeCheckId = {streamId}, applicationId = a99a0001-0000-0000-0000-000000000001, ownerId = 0e5e0011-0000-0000-0000-000000000011, shelterId = 5e110001-0000-0000-0000-000000000001)
        // # refused with: "This home check has already been requested"
        // Then the response is 400
        // And no events are emitted

        throw new NotImplementedException("RequestHomeCheck: Requesting the same home check twice is refused");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(AcceptHomeCheckAssignment))]
    public void A_volunteer_accepts_an_assignment_and_proposes_a_time()
    {
        // Given HomeCheckRequested (applicationId = a99a0002-0000-0000-0000-000000000002, ownerId = 0e5e0012-0000-0000-0000-000000000012, shelterId = 5e110001-0000-0000-0000-000000000001)
        // When AcceptHomeCheckAssignment is posted to "/api/volunteering/accepthomecheckassignment" (homeCheckId = {streamId}, volunteerOwnerId = 0e5e0099-0000-0000-0000-000000000099, proposedFor = 2026-10-01T15:00:00Z)
        // Then HomeCheckAssignmentAccepted is emitted (ownerId = 0e5e0012-0000-0000-0000-000000000012, volunteerOwnerId = 0e5e0099-0000-0000-0000-000000000099, proposedFor = 2026-10-01T15:00:00Z)

        throw new NotImplementedException("AcceptHomeCheckAssignment: A volunteer accepts an assignment and proposes a time");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(AcceptHomeCheckAssignment))]
    public void Accepting_an_assignment_books_the_home_check_as_an_appointment()
    {
        // Given HomeCheckRequested (applicationId = a99a0004-0000-0000-0000-000000000004, ownerId = 0e5e0014-0000-0000-0000-000000000014, shelterId = 5e110004-0000-0000-0000-000000000004)
        // When AcceptHomeCheckAssignment is posted to "/api/volunteering/accepthomecheckassignment" (homeCheckId = {streamId}, volunteerOwnerId = 0e5e0099-0000-0000-0000-000000000099, proposedFor = 2026-10-01T15:00:00Z)
        // Then the MyAppointments read model contains (AwaitingConfirmation = 1, Confirmed = 0, Closed = 0)

        throw new NotImplementedException("AcceptHomeCheckAssignment: Accepting an assignment books the home check as an appointment");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(AcceptHomeCheckAssignment))]
    public void An_assignment_already_accepted_is_not_accepted_again()
    {
        // Given HomeCheckRequested (applicationId = a99a0002-0000-0000-0000-000000000002, ownerId = 0e5e0012-0000-0000-0000-000000000012, shelterId = 5e110001-0000-0000-0000-000000000001)
        // Given HomeCheckAssignmentAccepted (ownerId = 0e5e0012-0000-0000-0000-000000000012, shelterId = 5e110001-0000-0000-0000-000000000001, volunteerOwnerId = 0e5e0099-0000-0000-0000-000000000099, proposedFor = 2026-10-01T15:00:00Z)
        // When AcceptHomeCheckAssignment is posted to "/api/volunteering/accepthomecheckassignment" (homeCheckId = {streamId}, volunteerOwnerId = 0e5e0098-0000-0000-0000-000000000098, proposedFor = 2026-10-02T15:00:00Z)
        // # refused with: "This home check is already assigned"
        // Then the response is 400
        // And no events are emitted

        throw new NotImplementedException("AcceptHomeCheckAssignment: An assignment already accepted is not accepted again");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(AcceptHomeCheckAssignment))]
    public void Accepting_a_home_check_that_does_not_exist_is_not_found()
    {
        // When AcceptHomeCheckAssignment is posted to "/api/volunteering/accepthomecheckassignment" (homeCheckId = {streamId}, volunteerOwnerId = 0e5e0099-0000-0000-0000-000000000099, proposedFor = 2026-10-01T15:00:00Z)
        // # refused with: "No home check with that id"
        // Then the response is 404
        // And no events are emitted

        throw new NotImplementedException("AcceptHomeCheckAssignment: Accepting a home check that does not exist is not found");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(SubmitHomeCheckReport))]
    public void A_volunteer_reports_on_a_visit_they_accepted()
    {
        // Given HomeCheckRequested (applicationId = a99a0003-0000-0000-0000-000000000003, ownerId = 0e5e0013-0000-0000-0000-000000000013, shelterId = 5e110001-0000-0000-0000-000000000001)
        // Given HomeCheckAssignmentAccepted (ownerId = 0e5e0013-0000-0000-0000-000000000013, shelterId = 5e110001-0000-0000-0000-000000000001, volunteerOwnerId = 0e5e0099-0000-0000-0000-000000000099, proposedFor = 2026-10-01T15:00:00Z)
        // When SubmitHomeCheckReport is posted to "/api/volunteering/submithomecheckreport" (homeCheckId = {streamId}, outcome = Pass, notes = Secure garden, calm household, good fit for a shy dog)
        // Then HomeCheckReportSubmitted is emitted (outcome = Pass, notes = Secure garden, calm household, good fit for a shy dog)

        throw new NotImplementedException("SubmitHomeCheckReport: A volunteer reports on a visit they accepted");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(SubmitHomeCheckReport))]
    public void A_visit_nobody_accepted_cannot_be_reported_on()
    {
        // Given HomeCheckRequested (applicationId = a99a0003-0000-0000-0000-000000000003, ownerId = 0e5e0013-0000-0000-0000-000000000013, shelterId = 5e110001-0000-0000-0000-000000000001)
        // When SubmitHomeCheckReport is posted to "/api/volunteering/submithomecheckreport" (homeCheckId = {streamId}, outcome = Pass, notes = Secure garden, calm household, good fit for a shy dog)
        // # refused with: "Nobody has accepted this home check"
        // Then the response is 400
        // And no events are emitted

        throw new NotImplementedException("SubmitHomeCheckReport: A visit nobody accepted cannot be reported on");
    }

    [Fact]
    [BobcatSlice(SliceType = typeof(SubmitHomeCheckReport))]
    public void Reporting_on_a_home_check_that_does_not_exist_is_not_found()
    {
        // When SubmitHomeCheckReport is posted to "/api/volunteering/submithomecheckreport" (homeCheckId = {streamId}, outcome = Pass, notes = Secure garden, calm household, good fit for a shy dog)
        // # refused with: "No home check with that id"
        // Then the response is 404
        // And no events are emitted

        throw new NotImplementedException("SubmitHomeCheckReport: Reporting on a home check that does not exist is not found");
    }
}
