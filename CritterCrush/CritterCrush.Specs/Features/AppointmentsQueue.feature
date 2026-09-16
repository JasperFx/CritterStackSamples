@domain:Scheduling
@chapter:BookingAppointments
Feature: AppointmentsQueue
  Triggered by Appointments Queue

  # Fixture: derive from CritterStackFixture — every act below dispatches over the bus.

  @slice:AppointmentsQueue
  Scenario: The queue counts appointments from every stream in the shelter
    Given no events for Appointment "25422542-2542-2542-2542-254225422542"
    And HomeCheckAppointmentProposed occurred
      | ownerId                              | shelterId                            | kind      | scheduledFor         |
      | 0e5e0001-0000-0000-0000-000000000001 | 5e110001-0000-0000-0000-000000000001 | HomeCheck | 2026-10-01T15:00:00Z |
    And AppointmentConfirmed occurred
      | ownerId                              | shelterId                            |
      | 0e5e0001-0000-0000-0000-000000000001 | 5e110001-0000-0000-0000-000000000001 |
    And no events for Appointment "62216221-6221-6221-6221-622162216221"
    And SurrenderIntakeAppointmentProposed occurred
      | ownerId                              | shelterId                            | kind            | scheduledFor         |
      | 0e5e0003-0000-0000-0000-000000000003 | 5e110001-0000-0000-0000-000000000001 | SurrenderIntake | 2026-10-03T09:00:00Z |
    Then the AppointmentsQueue read model with id "5e110001-0000-0000-0000-000000000001" contains
      | AwaitingConfirmation | Confirmed | Closed |
      | 1                    | 1         | 0      |

  @slice:AppointmentsQueue
  Scenario: An appointment cancelled before anyone confirmed it leaves the awaiting count
    Given no events for Appointment "74037403-7403-7403-7403-740374037403"
    And HomeCheckAppointmentProposed occurred
      | ownerId                              | shelterId                            | kind      | scheduledFor         |
      | 0e5e0001-0000-0000-0000-000000000001 | 5e110003-0000-0000-0000-000000000003 | HomeCheck | 2026-10-01T15:00:00Z |
    And AppointmentCancelled occurred
      | ownerId                              | shelterId                            | wasConfirmed | reason                 |
      | 0e5e0001-0000-0000-0000-000000000001 | 5e110003-0000-0000-0000-000000000003 | false        | The volunteer withdrew |
    Then the AppointmentsQueue read model with id "5e110003-0000-0000-0000-000000000003" contains
      | AwaitingConfirmation | Confirmed | Closed |
      | 0                    | 0         | 1      |

  @slice:AppointmentsQueue
  Scenario: A completed appointment leaves the queue
    Given no events for Appointment "30353035-3035-3035-3035-303530353035"
    And HomeCheckAppointmentProposed occurred
      | ownerId                              | shelterId                            | kind      | scheduledFor         |
      | 0e5e0001-0000-0000-0000-000000000001 | 5e110002-0000-0000-0000-000000000002 | HomeCheck | 2026-10-01T15:00:00Z |
    And AppointmentConfirmed occurred
      | ownerId                              | shelterId                            |
      | 0e5e0001-0000-0000-0000-000000000001 | 5e110002-0000-0000-0000-000000000002 |
    And AppointmentCompleted occurred
      | ownerId                              | shelterId                            | completedAt          |
      | 0e5e0001-0000-0000-0000-000000000001 | 5e110002-0000-0000-0000-000000000002 | 2026-10-01T16:00:00Z |
    Then the AppointmentsQueue read model with id "5e110002-0000-0000-0000-000000000002" contains
      | AwaitingConfirmation | Confirmed | Closed |
      | 0                    | 0         | 1      |
