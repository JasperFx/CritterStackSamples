@domain:Volunteering
@chapter:VolunteeringAndHomeChecks
Feature: VolunteerApplicationsQueue
  Triggered by Volunteer Applications Queue

  # Fixture: derive from CritterStackFixture — every act below dispatches over the bus.

  @slice:VolunteerApplicationsQueue
  Scenario: A new application shows as submitted
    Given no events for VolunteerApplication "48234823-4823-4823-4823-482348234823"
    And VolunteerApplicationSubmitted occurred
      | applicantOwnerId                     | areasOfInterest |
      | 0e5e0021-0000-0000-0000-000000000021 | HomeChecks      |
    Then the VolunteerApplicationsQueue read model contains
      | AreasOfInterest | Status    |
      | HomeChecks      | Submitted |

  @slice:VolunteerApplicationsQueue
  Scenario: An approved application shows the decision
    Given no events for VolunteerApplication "34643464-3464-3464-3464-346434643464"
    And VolunteerApplicationSubmitted occurred
      | applicantOwnerId                     | areasOfInterest |
      | 0e5e0022-0000-0000-0000-000000000022 | FosterSupport   |
    And VolunteerApplicationReviewed occurred
      | applicantOwnerId                     |
      | 0e5e0022-0000-0000-0000-000000000022 |
    And VolunteerApproved occurred
      | applicantOwnerId                     |
      | 0e5e0022-0000-0000-0000-000000000022 |
    Then the VolunteerApplicationsQueue read model contains
      | AreasOfInterest | Status   |
      | FosterSupport   | Approved |
