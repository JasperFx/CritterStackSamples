@domain:Appointments
Feature: BookingAppointments
  Triggered by HomeCheckAssignmentAccepted

  @slice:ProposeHomeCheckAppointment
  Scenario: Accepting a home check assignment proposes an appointment
    Given no events for Appointment "83328332-8332-8332-8332-833283328332"
    When HomeCheckAssignmentAccepted is received
      | assignmentId | ownerId | shelterId | dogId | volunteerId | proposedFor |
      | a5510001-0000-0000-0000-000000000001 | 0e5e0001-0000-0000-0000-000000000001 | 5e1e0001-0000-0000-0000-000000000001 | d0670001-0000-0000-0000-000000000001 | 0e1e0001-0000-0000-0000-000000000001 | 2026-10-01T15:00:00Z |
    Then HomeCheckAppointmentProposed is emitted
      | ownerId | proposedFor |
      | 0e5e0001-0000-0000-0000-000000000001 | 2026-10-01T15:00:00Z |

  @slice:ProposeFosterHandoverAppointment
  Scenario: Approving a foster placement proposes a handover appointment
    Given no events for Appointment "66166616-6616-6616-6616-661666166616"
    When FosterPlacementApproved is received
      | placementId | ownerId | shelterId | dogId | fosterId | proposedFor |
      | b1ac0002-0000-0000-0000-000000000002 | 0e5e0002-0000-0000-0000-000000000002 | 5e1e0001-0000-0000-0000-000000000001 | d0670002-0000-0000-0000-000000000002 | f05e0002-0000-0000-0000-000000000002 | 2026-10-02T10:30:00Z |
    Then FosterHandoverAppointmentProposed is emitted
      | fosterId | proposedFor |
      | f05e0002-0000-0000-0000-000000000002 | 2026-10-02T10:30:00Z |

  @slice:ProposeSurrenderIntakeAppointment
  Scenario: Approving a surrender request proposes an intake appointment
    Given no events for Appointment "47544754-4754-4754-4754-475447544754"
    When SurrenderRequestApproved is received
      | requestId | ownerId | shelterId | dogId | proposedFor |
      | c0110003-0000-0000-0000-000000000003 | 0e5e0003-0000-0000-0000-000000000003 | 5e1e0001-0000-0000-0000-000000000001 | d0670003-0000-0000-0000-000000000003 | 2026-10-03T09:00:00Z |
    Then SurrenderIntakeAppointmentProposed is emitted
      | ownerId | proposedFor |
      | 0e5e0003-0000-0000-0000-000000000003 | 2026-10-03T09:00:00Z |

  @slice:ConfirmAppointment
  Scenario: A proposed appointment is confirmed
    Given no events for Appointment "43254325-4325-4325-4325-432543254325"
    And events for Appointment
      | Event | AppointmentId | OwnerId | ShelterId | DogId | VolunteerId | ProposedFor |
      | HomeCheckAppointmentProposed | 43254325-4325-4325-4325-432543254325 | 0e5e0001-0000-0000-0000-000000000001 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 2026-10-01T15:00:00Z |
    When ConfirmAppointmentRequest is posted to "/api/appointments/confirmappointment"
      | appointmentId |
      | 43254325-4325-4325-4325-432543254325 |
    Then AppointmentConfirmed is emitted
      | ownerId |
      | 0e5e0001-0000-0000-0000-000000000001 |

  @slice:ConfirmAppointment
  Scenario: Confirming an already cancelled appointment is refused
    Given no events for Appointment "77607760-7760-7760-7760-776077607760"
    And events for Appointment
      | Event | AppointmentId | OwnerId | ShelterId | DogId | VolunteerId | ProposedFor |
      | HomeCheckAppointmentProposed | 77607760-7760-7760-7760-776077607760 | 0e5e0001-0000-0000-0000-000000000001 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 2026-10-01T15:00:00Z |
    And events for Appointment
      | Event | AppointmentId | OwnerId | Reason | CancelledAt |
      | AppointmentCancelled | 77607760-7760-7760-7760-776077607760 | 00000000-0000-0000-0000-000000000000 | The owner moved away | 2026-01-01T00:00:00Z |
    When ConfirmAppointmentRequest is posted to "/api/appointments/confirmappointment"
      | appointmentId |
      | 77607760-7760-7760-7760-776077607760 |
    Then the response is 400
    And no events are emitted

  @slice:RequestReschedule
  Scenario: An owner asks for a different time
    Given no events for Appointment "99549954-9954-9954-9954-995499549954"
    And events for Appointment
      | Event | AppointmentId | OwnerId | ShelterId | DogId | VolunteerId | ProposedFor |
      | HomeCheckAppointmentProposed | 99549954-9954-9954-9954-995499549954 | 0e5e0001-0000-0000-0000-000000000001 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 2026-10-01T15:00:00Z |
    When RequestRescheduleRequest is posted to "/api/appointments/requestreschedule"
      | appointmentId | reason | preferredFor |
      | 99549954-9954-9954-9954-995499549954 | Working that afternoon | 2026-10-04T18:00:00Z |
    Then RescheduleRequested is emitted
      | reason | preferredFor |
      | Working that afternoon | 2026-10-04T18:00:00Z |

  @slice:RescheduleAppointment
  Scenario: The shelter moves an appointment to a new time
    Given no events for Appointment "98539853-9853-9853-9853-985398539853"
    And events for Appointment
      | Event | AppointmentId | OwnerId | ShelterId | DogId | VolunteerId | ProposedFor |
      | HomeCheckAppointmentProposed | 98539853-9853-9853-9853-985398539853 | 0e5e0001-0000-0000-0000-000000000001 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 2026-10-01T15:00:00Z |
    And events for Appointment
      | Event | AppointmentId | OwnerId | Reason | PreferredFor | RequestedAt |
      | RescheduleRequested | 98539853-9853-9853-9853-985398539853 | 00000000-0000-0000-0000-000000000000 | Working that afternoon | 2026-10-04T18:00:00Z | 2026-01-01T00:00:00Z |
    When RescheduleAppointmentRequest is posted to "/api/appointments/rescheduleappointment"
      | appointmentId | scheduledFor |
      | 98539853-9853-9853-9853-985398539853 | 2026-10-04T18:00:00Z |
    Then AppointmentRescheduled is emitted
      | scheduledFor |
      | 2026-10-04T18:00:00Z |

  @slice:CompleteAppointment
  Scenario: A confirmed appointment is completed
    Given no events for Appointment "36343634-3634-3634-3634-363436343634"
    And events for Appointment
      | Event | AppointmentId | OwnerId | ShelterId | DogId | VolunteerId | ProposedFor |
      | HomeCheckAppointmentProposed | 36343634-3634-3634-3634-363436343634 | 0e5e0001-0000-0000-0000-000000000001 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 2026-10-01T15:00:00Z |
    And events for Appointment
      | Event | AppointmentId | OwnerId | ConfirmedAt |
      | AppointmentConfirmed | 36343634-3634-3634-3634-363436343634 | 0e5e0001-0000-0000-0000-000000000001 | 2026-01-01T00:00:00Z |
    When CompleteAppointmentRequest is posted to "/api/appointments/completeappointment"
      | appointmentId | notes |
      | 36343634-3634-3634-3634-363436343634 | Garden is fenced, two cats, all good |
    Then AppointmentCompleted is emitted
      | notes |
      | Garden is fenced, two cats, all good |

  @slice:CancelAppointment
  Scenario: An appointment is cancelled before it happens
    Given no events for Appointment "64106410-6410-6410-6410-641064106410"
    And events for Appointment
      | Event | AppointmentId | OwnerId | ShelterId | DogId | VolunteerId | ProposedFor |
      | HomeCheckAppointmentProposed | 64106410-6410-6410-6410-641064106410 | 0e5e0001-0000-0000-0000-000000000001 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 2026-10-01T15:00:00Z |
    When CancelAppointmentRequest is posted to "/api/appointments/cancelappointment"
      | appointmentId | reason |
      | 64106410-6410-6410-6410-641064106410 | The owner withdrew the application |
    Then AppointmentCancelled is emitted
      | reason |
      | The owner withdrew the application |

  @slice:CancelAppointment
  Scenario: A completed appointment cannot be cancelled
    Given no events for Appointment "12421242-1242-1242-1242-124212421242"
    And events for Appointment
      | Event | AppointmentId | OwnerId | ShelterId | DogId | VolunteerId | ProposedFor |
      | HomeCheckAppointmentProposed | 12421242-1242-1242-1242-124212421242 | 0e5e0001-0000-0000-0000-000000000001 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 2026-10-01T15:00:00Z |
    And events for Appointment
      | Event | AppointmentId | OwnerId | ConfirmedAt |
      | AppointmentConfirmed | 12421242-1242-1242-1242-124212421242 | 0e5e0001-0000-0000-0000-000000000001 | 2026-01-01T00:00:00Z |
    And events for Appointment
      | Event | AppointmentId | OwnerId | Notes | CompletedAt |
      | AppointmentCompleted | 12421242-1242-1242-1242-124212421242 | 00000000-0000-0000-0000-000000000000 | Garden is fenced, two cats, all good | 2026-01-01T00:00:00Z |
    When CancelAppointmentRequest is posted to "/api/appointments/cancelappointment"
      | appointmentId | reason |
      | 12421242-1242-1242-1242-124212421242 | Too late |
    Then the response is 400
    And no events are emitted

  @slice:RecordAppointmentNoShow
  Scenario: Nobody turned up
    Given no events for Appointment "71717171-7171-7171-7171-717171717171"
    And events for Appointment
      | Event | AppointmentId | OwnerId | ShelterId | DogId | VolunteerId | ProposedFor |
      | HomeCheckAppointmentProposed | 71717171-7171-7171-7171-717171717171 | 0e5e0001-0000-0000-0000-000000000001 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 2026-10-01T15:00:00Z |
    And events for Appointment
      | Event | AppointmentId | OwnerId | ConfirmedAt |
      | AppointmentConfirmed | 71717171-7171-7171-7171-717171717171 | 0e5e0001-0000-0000-0000-000000000001 | 2026-01-01T00:00:00Z |
    When RecordAppointmentNoShowRequest is posted to "/api/appointments/recordappointmentnoshow"
      | appointmentId |
      | 71717171-7171-7171-7171-717171717171 |
    Then AppointmentNoShowRecorded is emitted
      | ownerId |
      | 0e5e0001-0000-0000-0000-000000000001 |

  @slice:AppointmentsQueue
  Scenario: A proposed home check waits in the queue for the owner
    Given no events for Appointment "94349434-9434-9434-9434-943494349434"
    And events for Appointment
      | Event | AppointmentId | OwnerId | ShelterId | DogId | VolunteerId | ProposedFor |
      | HomeCheckAppointmentProposed | 94349434-9434-9434-9434-943494349434 | 0e5e0001-0000-0000-0000-000000000001 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 2026-10-01T15:00:00Z |
    Then the AppointmentsQueue read model contains
      | Kind | Status | AwaitingAction |
      | HomeCheck | Proposed | true |

  @slice:AppointmentsQueue
  Scenario: A completed appointment stops awaiting action
    Given no events for Appointment "51395139-5139-5139-5139-513951395139"
    And events for Appointment
      | Event | AppointmentId | OwnerId | ShelterId | DogId | VolunteerId | ProposedFor |
      | HomeCheckAppointmentProposed | 51395139-5139-5139-5139-513951395139 | 0e5e0001-0000-0000-0000-000000000001 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 00000000-0000-0000-0000-000000000000 | 2026-10-01T15:00:00Z |
    And events for Appointment
      | Event | AppointmentId | OwnerId | ConfirmedAt |
      | AppointmentConfirmed | 51395139-5139-5139-5139-513951395139 | 0e5e0001-0000-0000-0000-000000000001 | 2026-01-01T00:00:00Z |
    And events for Appointment
      | Event | AppointmentId | OwnerId | Notes | CompletedAt |
      | AppointmentCompleted | 51395139-5139-5139-5139-513951395139 | 00000000-0000-0000-0000-000000000000 | Garden is fenced, two cats, all good | 2026-01-01T00:00:00Z |
    Then the AppointmentsQueue read model contains
      | Status | AwaitingAction |
      | Completed | false |
