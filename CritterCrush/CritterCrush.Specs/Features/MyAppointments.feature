@domain:Scheduling
@chapter:BookingAppointments
Feature: MyAppointments
  Triggered by My Appointments

  # Fixture: derive from CritterStackFixture — every act below dispatches over the bus.

  @slice:MyAppointments
  Scenario: An owner sees an appointment awaiting their confirmation
    Given no events for Appointment "86548654-8654-8654-8654-865486548654"
    And HomeCheckAppointmentProposed occurred
      | ownerId                              | shelterId                            | kind      | scheduledFor         |
      | 0e5e0004-0000-0000-0000-000000000004 | 5e110001-0000-0000-0000-000000000001 | HomeCheck | 2026-10-01T15:00:00Z |
    Then the MyAppointments read model with id "0e5e0004-0000-0000-0000-000000000004" contains
      | AwaitingConfirmation | Confirmed | Closed |
      | 1                    | 0         | 0      |

  @slice:MyAppointments
  Scenario: An owner's page spans every appointment stream they have
    Given no events for Appointment "61526152-6152-6152-6152-615261526152"
    And HomeCheckAppointmentProposed occurred
      | ownerId                              | shelterId                            | kind      | scheduledFor         |
      | 0e5e0005-0000-0000-0000-000000000005 | 5e110001-0000-0000-0000-000000000001 | HomeCheck | 2026-10-01T15:00:00Z |
    And AppointmentConfirmed occurred
      | ownerId                              | shelterId                            |
      | 0e5e0005-0000-0000-0000-000000000005 | 5e110001-0000-0000-0000-000000000001 |
    And no events for Appointment "67546754-6754-6754-6754-675467546754"
    And SurrenderIntakeAppointmentProposed occurred
      | ownerId                              | shelterId                            | kind            | scheduledFor         |
      | 0e5e0005-0000-0000-0000-000000000005 | 5e110001-0000-0000-0000-000000000001 | SurrenderIntake | 2026-10-03T09:00:00Z |
    Then the MyAppointments read model with id "0e5e0005-0000-0000-0000-000000000005" contains
      | AwaitingConfirmation | Confirmed | Closed |
      | 1                    | 1         | 0      |
