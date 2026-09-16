@domain:Volunteering
@chapter:VolunteeringAndHomeChecks
Feature: Volunteering

  # Fixture: derive from CritterStackHttpFixture. At least one act below POSTs to a
  # collapsed endpoint, and `is posted to` is HttpGrammars' step — CritterStackFixture
  # alone carries the store vocabulary but not the HTTP one. Routes here are absolute,
  # so leave the module's route prefix empty.

  @arrangement
  Scenario: volunteer application submitted
    Given VolunteerApplicationSubmitted occurred
      | areasOfInterest |
      | HomeChecks      |

  @arrangement
  Scenario: volunteer application submitted, then volunteer application reviewed
    Given the arrangement "volunteer application submitted"
    And VolunteerApplicationReviewed occurred

  @arrangement
  Scenario: volunteer application submitted, then volunteer application reviewed, then volunteer approved
    Given the arrangement "volunteer application submitted, then volunteer application reviewed"
    And VolunteerApproved occurred

  @slice:ApplyToVolunteer
  Scenario: Somebody applies to volunteer
    Triggered by Volunteer Program
    Given no events for VolunteerApplication "57825782-5782-5782-5782-578257825782"
    When ApplyToVolunteer is posted to "/api/volunteering/applytovolunteer"
      | applicantOwnerId                     | areasOfInterest |
      | 57825782-5782-5782-5782-578257825782 | HomeChecks      |
    Then VolunteerApplicationSubmitted is emitted
      | areasOfInterest |
      | HomeChecks      |

  @slice:ApplyToVolunteer
  Scenario: Applying twice is refused
    Triggered by Volunteer Program
    Given no events for VolunteerApplication "58715871-5871-5871-5871-587158715871"
    And the arrangement "volunteer application submitted"
    When ApplyToVolunteer is posted to "/api/volunteering/applytovolunteer"
      | applicantOwnerId                     | areasOfInterest |
      | 58715871-5871-5871-5871-587158715871 | Transport       |
    # refused with: "You have already applied to volunteer"
    Then the response is 400
    And no events are emitted

  @slice:ReviewVolunteerApplication
  Scenario: An admin reviews a submitted application
    Triggered by Volunteer Applications Queue
    Given no events for VolunteerApplication "28982898-2898-2898-2898-289828982898"
    And the arrangement "volunteer application submitted"
    When ReviewVolunteerApplication is posted to "/api/volunteering/reviewvolunteerapplication"
      | applicantOwnerId                     |
      | 28982898-2898-2898-2898-289828982898 |
    Then VolunteerApplicationReviewed is emitted

  @slice:ReviewVolunteerApplication
  Scenario: An application already decided is not reviewed again
    Triggered by Volunteer Applications Queue
    Given no events for VolunteerApplication "59355935-5935-5935-5935-593559355935"
    And the arrangement "volunteer application submitted, then volunteer application reviewed, then volunteer approved"
    When ReviewVolunteerApplication is posted to "/api/volunteering/reviewvolunteerapplication"
      | applicantOwnerId                     |
      | 59355935-5935-5935-5935-593559355935 |
    # refused with: "This application has already been decided"
    Then the response is 400
    And no events are emitted

  @slice:ApproveVolunteer
  Scenario: A reviewed applicant is approved
    Triggered by Approve Volunteer
    Given no events for VolunteerApplication "51005100-5100-5100-5100-510051005100"
    And the arrangement "volunteer application submitted, then volunteer application reviewed"
    When ApproveVolunteer is posted to "/api/volunteering/approvevolunteer"
      | applicantOwnerId                     |
      | 51005100-5100-5100-5100-510051005100 |
    Then VolunteerApproved is emitted

  @slice:ApproveVolunteer
  Scenario: An applicant nobody reviewed is not approved
    Triggered by Approve Volunteer
    Given no events for VolunteerApplication "27372737-2737-2737-2737-273727372737"
    And the arrangement "volunteer application submitted"
    When ApproveVolunteer is posted to "/api/volunteering/approvevolunteer"
      | applicantOwnerId                     |
      | 27372737-2737-2737-2737-273727372737 |
    # refused with: "This application has not been reviewed"
    Then the response is 400
    And no events are emitted

  @slice:RejectVolunteerApplication
  Scenario: A reviewed applicant is rejected with a reason
    Triggered by Reject Volunteer Application
    Given no events for VolunteerApplication "21962196-2196-2196-2196-219621962196"
    And the arrangement "volunteer application submitted, then volunteer application reviewed"
    When RejectVolunteerApplication is posted to "/api/volunteering/rejectvolunteerapplication"
      | applicantOwnerId                     | reason                            |
      | 21962196-2196-2196-2196-219621962196 | Outside our current coverage area |
    Then VolunteerApplicationRejected is emitted
      | reason                            |
      | Outside our current coverage area |

  @slice:RejectVolunteerApplication
  Scenario: An approved volunteer is not then rejected
    Triggered by Reject Volunteer Application
    Given no events for VolunteerApplication "52635263-5263-5263-5263-526352635263"
    And the arrangement "volunteer application submitted, then volunteer application reviewed, then volunteer approved"
    When RejectVolunteerApplication is posted to "/api/volunteering/rejectvolunteerapplication"
      | applicantOwnerId                     | reason                            |
      | 52635263-5263-5263-5263-526352635263 | Outside our current coverage area |
    # refused with: "This application has already been decided"
    Then the response is 400
    And no events are emitted
