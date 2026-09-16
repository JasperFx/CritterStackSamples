@domain:Scheduling
@chapter:BookingAppointments
Feature: BookingAppointments

  # Fixture: derive from CritterStackHttpFixture. At least one act below POSTs to a
  # collapsed endpoint, and `is posted to` is HttpGrammars' step — CritterStackFixture
  # alone carries the store vocabulary but not the HTTP one. Routes here are absolute,
  # so leave the module's route prefix empty.

  @arrangement
  Scenario: home check appointment proposed
    Given HomeCheckAppointmentProposed occurred
      | ownerId                              | shelterId                            | kind      | scheduledFor         |
      | 0e5e0001-0000-0000-0000-000000000001 | 5e110001-0000-0000-0000-000000000001 | HomeCheck | 2026-10-01T15:00:00Z |

  @arrangement
  Scenario: home check appointment proposed, then appointment confirmed
    Given the arrangement "home check appointment proposed"
    And AppointmentConfirmed occurred
      | ownerId                              | shelterId                            |
      | 0e5e0001-0000-0000-0000-000000000001 | 5e110001-0000-0000-0000-000000000001 |

  @arrangement
  Scenario: home check appointment proposed, then appointment confirmed, then appointment completed
    Given the arrangement "home check appointment proposed, then appointment confirmed"
    And AppointmentCompleted occurred
      | ownerId                              | shelterId                            | completedAt          |
      | 0e5e0001-0000-0000-0000-000000000001 | 5e110001-0000-0000-0000-000000000001 | 2026-10-01T16:00:00Z |

  @slice:ProposeHomeCheckAppointment
  Scenario: An accepted home check assignment proposes a visit
    Triggered by HomeCheckAssignmentAccepted
    Given no events for Appointment "53355335-5335-5335-5335-533553355335"
    When HomeCheckAssignmentAccepted is received
      | assignmentId                         | ownerId                              | shelterId                            | volunteerOwnerId                     | proposedFor          |
      | 53355335-5335-5335-5335-533553355335 | 0e5e0001-0000-0000-0000-000000000001 | 5e110001-0000-0000-0000-000000000001 | 0e5e0099-0000-0000-0000-000000000099 | 2026-10-01T15:00:00Z |
    Then HomeCheckAppointmentProposed is emitted
      | ownerId                              | kind      | sourceId                             | scheduledFor         |
      | 0e5e0001-0000-0000-0000-000000000001 | HomeCheck | 53355335-5335-5335-5335-533553355335 | 2026-10-01T15:00:00Z |

  @slice:ProposeFosterHandoverAppointment
  Scenario: A dog placed in foster proposes a handover
    Triggered by DogPlacedInFoster
    Given no events for Appointment "44364436-4436-4436-4436-443644364436"
    When DogPlacedInFoster is received
      | fosterApplicationId                  | ownerId                              | shelterId                            | proposedFor          |
      | 44364436-4436-4436-4436-443644364436 | 0e5e0002-0000-0000-0000-000000000002 | 5e110001-0000-0000-0000-000000000001 | 2026-10-02T10:30:00Z |
    Then FosterHandoverAppointmentProposed is emitted
      | ownerId                              | kind           | sourceId                             | scheduledFor         |
      | 0e5e0002-0000-0000-0000-000000000002 | FosterHandover | 44364436-4436-4436-4436-443644364436 | 2026-10-02T10:30:00Z |

  @slice:ProposeSurrenderIntakeAppointment
  Scenario: A reviewed surrender request proposes an intake
    Triggered by SurrenderRequestReviewed
    Given no events for Appointment "96369636-9636-9636-9636-963696369636"
    When SurrenderRequestReviewed is received
      | surrenderRequestId                   | ownerId                              | shelterId                            | proposedFor          |
      | 96369636-9636-9636-9636-963696369636 | 0e5e0003-0000-0000-0000-000000000003 | 5e110001-0000-0000-0000-000000000001 | 2026-10-03T09:00:00Z |
    Then SurrenderIntakeAppointmentProposed is emitted
      | ownerId                              | kind            | sourceId                             | scheduledFor         |
      | 0e5e0003-0000-0000-0000-000000000003 | SurrenderIntake | 96369636-9636-9636-9636-963696369636 | 2026-10-03T09:00:00Z |

  @slice:ConfirmAppointment
  Scenario: A proposed appointment is confirmed
    Triggered by Confirm Appointment
    Given no events for Appointment "43254325-4325-4325-4325-432543254325"
    And the arrangement "home check appointment proposed"
    When ConfirmAppointment is posted to "/api/scheduling/confirmappointment"
      | appointmentId                        |
      | 43254325-4325-4325-4325-432543254325 |
    Then AppointmentConfirmed is emitted
      | ownerId                              |
      | 0e5e0001-0000-0000-0000-000000000001 |

  @slice:ConfirmAppointment
  Scenario: A cancelled appointment cannot be confirmed
    Triggered by Confirm Appointment
    Given no events for Appointment "57115711-5711-5711-5711-571157115711"
    And the arrangement "home check appointment proposed"
    And AppointmentCancelled occurred
      | ownerId                              | shelterId                            | wasConfirmed | reason                 |
      | 0e5e0001-0000-0000-0000-000000000001 | 5e110001-0000-0000-0000-000000000001 | false        | The volunteer withdrew |
    When ConfirmAppointment is posted to "/api/scheduling/confirmappointment"
      | appointmentId                        |
      | 57115711-5711-5711-5711-571157115711 |
    # refused with: "This appointment was cancelled"
    Then the response is 400
    And no events are emitted

  @slice:RequestReschedule
  Scenario: A member asks to move a confirmed appointment
    Triggered by Request Reschedule
    Given no events for Appointment "12791279-1279-1279-1279-127912791279"
    And the arrangement "home check appointment proposed, then appointment confirmed"
    When RequestReschedule is posted to "/api/scheduling/requestreschedule"
      | appointmentId                        | requestedFor         | reason                 |
      | 12791279-1279-1279-1279-127912791279 | 2026-10-05T15:00:00Z | Working that afternoon |
    Then AppointmentRescheduleRequested is emitted
      | requestedFor         | reason                 |
      | 2026-10-05T15:00:00Z | Working that afternoon |

  @slice:RequestReschedule
  Scenario: A completed appointment cannot be rescheduled
    Triggered by Request Reschedule
    Given no events for Appointment "92449244-9244-9244-9244-924492449244"
    And the arrangement "home check appointment proposed, then appointment confirmed, then appointment completed"
    When RequestReschedule is posted to "/api/scheduling/requestreschedule"
      | appointmentId                        | requestedFor         | reason                 |
      | 92449244-9244-9244-9244-924492449244 | 2026-10-05T15:00:00Z | Working that afternoon |
    # refused with: "This appointment is already closed"
    Then the response is 400
    And no events are emitted

  @slice:RescheduleAppointment
  Scenario: The shelter moves an appointment a member asked to move
    Triggered by Reschedule Appointment
    Given no events for Appointment "42534253-4253-4253-4253-425342534253"
    And the arrangement "home check appointment proposed, then appointment confirmed"
    And AppointmentRescheduleRequested occurred
      | ownerId                              | shelterId                            | requestedFor         | reason                 |
      | 0e5e0001-0000-0000-0000-000000000001 | 5e110001-0000-0000-0000-000000000001 | 2026-10-05T15:00:00Z | Working that afternoon |
    When RescheduleAppointment is posted to "/api/scheduling/rescheduleappointment"
      | appointmentId                        | scheduledFor         |
      | 42534253-4253-4253-4253-425342534253 | 2026-10-05T15:00:00Z |
    Then AppointmentRescheduled is emitted
      | scheduledFor         |
      | 2026-10-05T15:00:00Z |

  @slice:RescheduleAppointment
  Scenario: An appointment nobody asked to move is not moved
    Triggered by Reschedule Appointment
    Given no events for Appointment "81218121-8121-8121-8121-812181218121"
    And the arrangement "home check appointment proposed, then appointment confirmed"
    When RescheduleAppointment is posted to "/api/scheduling/rescheduleappointment"
      | appointmentId                        | scheduledFor         |
      | 81218121-8121-8121-8121-812181218121 | 2026-10-05T15:00:00Z |
    # refused with: "Nobody asked to move this appointment"
    Then the response is 400
    And no events are emitted

  @slice:CompleteAppointment
  Scenario: A confirmed appointment is completed
    Triggered by Complete Appointment
    Given no events for Appointment "36343634-3634-3634-3634-363436343634"
    And the arrangement "home check appointment proposed, then appointment confirmed"
    When CompleteAppointment is posted to "/api/scheduling/completeappointment"
      | appointmentId                        |
      | 36343634-3634-3634-3634-363436343634 |
    Then AppointmentCompleted is emitted
      | ownerId                              |
      | 0e5e0001-0000-0000-0000-000000000001 |

  @slice:CompleteAppointment
  Scenario: An appointment nobody confirmed is not completed
    Triggered by Complete Appointment
    Given no events for Appointment "30843084-3084-3084-3084-308430843084"
    And the arrangement "home check appointment proposed"
    When CompleteAppointment is posted to "/api/scheduling/completeappointment"
      | appointmentId                        |
      | 30843084-3084-3084-3084-308430843084 |
    # refused with: "This appointment has not been confirmed"
    Then the response is 400
    And no events are emitted

  @slice:CancelAppointment
  Scenario: A confirmed appointment is cancelled
    Triggered by Cancel Appointment
    Given no events for Appointment "71257125-7125-7125-7125-712571257125"
    And the arrangement "home check appointment proposed, then appointment confirmed"
    When CancelAppointment is posted to "/api/scheduling/cancelappointment"
      | appointmentId                        | reason                 |
      | 71257125-7125-7125-7125-712571257125 | The volunteer withdrew |
    Then AppointmentCancelled is emitted
      | wasConfirmed | reason                 |
      | true         | The volunteer withdrew |

  @slice:CancelAppointment
  Scenario: A completed appointment cannot be cancelled
    Triggered by Cancel Appointment
    Given no events for Appointment "12421242-1242-1242-1242-124212421242"
    And the arrangement "home check appointment proposed, then appointment confirmed, then appointment completed"
    When CancelAppointment is posted to "/api/scheduling/cancelappointment"
      | appointmentId                        | reason                 |
      | 12421242-1242-1242-1242-124212421242 | The volunteer withdrew |
    # refused with: "This appointment is already closed"
    Then the response is 400
    And no events are emitted

  @slice:RecordAppointmentNoShow
  Scenario: A no-show is recorded against a confirmed appointment
    Triggered by Record Appointment No-Show
    Given no events for Appointment "70877087-7087-7087-7087-708770877087"
    And the arrangement "home check appointment proposed, then appointment confirmed"
    When RecordAppointmentNoShow is posted to "/api/scheduling/recordappointmentnoshow"
      | appointmentId                        |
      | 70877087-7087-7087-7087-708770877087 |
    Then AppointmentNoShowRecorded is emitted
      | ownerId                              |
      | 0e5e0001-0000-0000-0000-000000000001 |

  @slice:RecordAppointmentNoShow
  Scenario: A completed appointment cannot be marked a no-show
    Triggered by Record Appointment No-Show
    Given no events for Appointment "18431843-1843-1843-1843-184318431843"
    And the arrangement "home check appointment proposed, then appointment confirmed, then appointment completed"
    When RecordAppointmentNoShow is posted to "/api/scheduling/recordappointmentnoshow"
      | appointmentId                        |
      | 18431843-1843-1843-1843-184318431843 |
    # refused with: "This appointment is already closed"
    Then the response is 400
    And no events are emitted
