namespace CritterCrush.Volunteering;

// Every event the VolunteeringAndHomeChecks chapter emits — all 7 of them, and nothing else.
//
// Gathered here rather than beside the commands that append them, because "what can happen
// in this chapter" is a question about the chapter. A slice file holds its own slice.
//
// Events that arrive from OUTSIDE this model are not here — they keep their own files, and
// each says to version rather than edit it.
//
//   VolunteerApplicationSubmitted
//   VolunteerApplicationReviewed
//   VolunteerApproved
//   VolunteerApplicationRejected
//   HomeCheckRequested
//   HomeCheckAssignmentAccepted
//   HomeCheckReportSubmitted

/// <summary>Somebody asked to volunteer</summary>
public record VolunteerApplicationSubmitted(Guid ApplicantOwnerId, string AreasOfInterest);

/// <summary>An admin read the application and it now awaits a decision</summary>
public record VolunteerApplicationReviewed(Guid ApplicantOwnerId);

/// <summary>The applicant may now be assigned home checks</summary>
public record VolunteerApproved(Guid ApplicantOwnerId);

/// <summary>The applicant will not be volunteering</summary>
public record VolunteerApplicationRejected(Guid ApplicantOwnerId, string Reason);

/// <summary>A home check is needed and awaits a volunteer</summary>
public record HomeCheckRequested(Guid ApplicationId, Guid OwnerId, Guid ShelterId);

/// <summary>A volunteer took the assignment and proposed a time to visit</summary>
public record HomeCheckAssignmentAccepted(Guid AssignmentId, Guid OwnerId, Guid ShelterId, Guid VolunteerOwnerId, DateTimeOffset ProposedFor);

/// <summary>The volunteer visited and wrote it up</summary>
public record HomeCheckReportSubmitted(Guid ApplicationId, Guid OwnerId, Guid ShelterId, string Outcome, string Notes);

