@domain:Appointments
Feature: BookingAppointments
  Triggered by HomeCheckAssignmentAccepted

  @slice:ProposeHomeCheckAppointment
  Scenario: Accepting a home check assignment proposes an appointment
    Given no events for Appointment "83328332-8332-8332-8332-833283328332"
    When HomeCheckAssignmentAccepted is received
    Then HomeCheckAppointmentProposed is emitted

  @slice:ProposeFosterHandoverAppointment
  Scenario: Approving a foster placement proposes a handover appointment
    Given no events for Appointment "66166616-6616-6616-6616-661666166616"
    When FosterPlacementApproved is received
    Then FosterHandoverAppointmentProposed is emitted

  @slice:ProposeSurrenderIntakeAppointment
  Scenario: Approving a surrender request proposes an intake appointment
    Given no events for Appointment "47544754-4754-4754-4754-475447544754"
    When SurrenderRequestApproved is received
    Then SurrenderIntakeAppointmentProposed is emitted

  @slice:ConfirmAppointment
  Scenario: A proposed appointment is confirmed
    Given no events for Appointment "43254325-4325-4325-4325-432543254325"
    And events for Appointment
      | Event |
      | HomeCheckAppointmentProposed |
    When ConfirmAppointmentRequest is posted to "/api/appointments/confirmappointment"
    Then AppointmentConfirmed is emitted

  @slice:ConfirmAppointment
  Scenario: Confirming an already cancelled appointment is refused
    Given no events for Appointment "77607760-7760-7760-7760-776077607760"
    And events for Appointment
      | Event |
      | HomeCheckAppointmentProposed |
    And events for Appointment
      | Event |
      | AppointmentCancelled |
    When ConfirmAppointmentRequest is posted to "/api/appointments/confirmappointment"
    Then validation fails with "This appointment was cancelled"
    And no events are emitted

  @slice:RequestReschedule
  Scenario: An owner asks for a different time
    Given no events for Appointment "99549954-9954-9954-9954-995499549954"
    And events for Appointment
      | Event |
      | HomeCheckAppointmentProposed |
    When RequestRescheduleRequest is posted to "/api/appointments/requestreschedule"
    Then RescheduleRequested is emitted

  @slice:RescheduleAppointment
  Scenario: The shelter moves an appointment to a new time
    Given no events for Appointment "98539853-9853-9853-9853-985398539853"
    And events for Appointment
      | Event |
      | HomeCheckAppointmentProposed |
    And events for Appointment
      | Event |
      | RescheduleRequested |
    When RescheduleAppointmentRequest is posted to "/api/appointments/rescheduleappointment"
    Then AppointmentRescheduled is emitted

  @slice:CompleteAppointment
  Scenario: A confirmed appointment is completed
    Given no events for Appointment "36343634-3634-3634-3634-363436343634"
    And events for Appointment
      | Event |
      | HomeCheckAppointmentProposed |
    And events for Appointment
      | Event |
      | AppointmentConfirmed |
    When CompleteAppointmentRequest is posted to "/api/appointments/completeappointment"
    Then AppointmentCompleted is emitted

  @slice:CancelAppointment
  Scenario: An appointment is cancelled before it happens
    Given no events for Appointment "64106410-6410-6410-6410-641064106410"
    And events for Appointment
      | Event |
      | HomeCheckAppointmentProposed |
    When CancelAppointmentRequest is posted to "/api/appointments/cancelappointment"
    Then AppointmentCancelled is emitted

  @slice:CancelAppointment
  Scenario: A completed appointment cannot be cancelled
    Given no events for Appointment "12421242-1242-1242-1242-124212421242"
    And events for Appointment
      | Event |
      | HomeCheckAppointmentProposed |
    And events for Appointment
      | Event |
      | AppointmentConfirmed |
    And events for Appointment
      | Event |
      | AppointmentCompleted |
    When CancelAppointmentRequest is posted to "/api/appointments/cancelappointment"
    Then validation fails with "This appointment is already completed"
    And no events are emitted

  @slice:RecordAppointmentNoShow
  Scenario: Nobody turned up
    Given no events for Appointment "71717171-7171-7171-7171-717171717171"
    And events for Appointment
      | Event |
      | HomeCheckAppointmentProposed |
    And events for Appointment
      | Event |
      | AppointmentConfirmed |
    When RecordAppointmentNoShowRequest is posted to "/api/appointments/recordappointmentnoshow"
    Then AppointmentNoShowRecorded is emitted
