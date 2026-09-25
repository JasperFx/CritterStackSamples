namespace CritterCrush.Scheduling;

// Every event the BookingAppointments chapter emits — all 9 of them, and nothing else.
//
// Gathered here rather than beside the commands that append them, because "what can happen
// in this chapter" is a question about the chapter. A slice file holds its own slice.
//
// Events that arrive from OUTSIDE this model are not here — they keep their own files, and
// each says to version rather than edit it.
//
//   HomeCheckAppointmentProposed
//   FosterHandoverAppointmentProposed
//   SurrenderIntakeAppointmentProposed
//   AppointmentConfirmed
//   AppointmentRescheduleRequested
//   AppointmentRescheduled
//   AppointmentCompleted
//   AppointmentCancelled
//   AppointmentNoShowRecorded

/// <summary>A home-check visit was proposed and awaits the owner's confirmation</summary>
public record HomeCheckAppointmentProposed(Guid OwnerId, Guid ShelterId, string Kind, Guid SourceId, DateTimeOffset ScheduledFor) : IOwnerEvent, IShelterEvent;

/// <summary>A foster handover was proposed and awaits the foster carer's confirmation</summary>
public record FosterHandoverAppointmentProposed(Guid OwnerId, Guid ShelterId, string Kind, Guid SourceId, DateTimeOffset ScheduledFor) : IOwnerEvent, IShelterEvent;

/// <summary>A surrender intake was proposed and awaits the owner's confirmation</summary>
public record SurrenderIntakeAppointmentProposed(Guid OwnerId, Guid ShelterId, string Kind, Guid SourceId, DateTimeOffset ScheduledFor) : IOwnerEvent, IShelterEvent;

/// <summary>The counterparty accepted the proposed time</summary>
public record AppointmentConfirmed(Guid OwnerId, Guid ShelterId, DateTimeOffset ConfirmedAt) : IOwnerEvent, IShelterEvent;

/// <summary>The counterparty asked for a different time</summary>
public record AppointmentRescheduleRequested(Guid OwnerId, Guid ShelterId, DateTimeOffset RequestedFor, string Reason);

/// <summary>The shelter moved the appointment to a new time</summary>
public record AppointmentRescheduled(Guid OwnerId, Guid ShelterId, DateTimeOffset ScheduledFor);

/// <summary>The visit happened</summary>
public record AppointmentCompleted(Guid OwnerId, Guid ShelterId, DateTimeOffset CompletedAt) : IOwnerEvent, IShelterEvent;

/// <summary>
/// The appointment will not happen. `wasConfirmed` is carried because the counting views cannot otherwise know which of their buckets this appointment was sitting in, and the aggregate is the only place that knows — a projection has no prior state to consult.
/// 
/// </summary>
public record AppointmentCancelled(Guid OwnerId, Guid ShelterId, bool WasConfirmed, string Reason) : IOwnerEvent, IShelterEvent;

/// <summary>The counterparty never arrived</summary>
public record AppointmentNoShowRecorded(Guid OwnerId, Guid ShelterId, DateTimeOffset RecordedAt) : IOwnerEvent, IShelterEvent;
