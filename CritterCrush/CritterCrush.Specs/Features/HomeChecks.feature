@domain:Volunteering
@chapter:VolunteeringAndHomeChecks
Feature: HomeChecks

  # Fixture: derive from CritterStackHttpFixture. At least one act below POSTs to a
  # collapsed endpoint, and `is posted to` is HttpGrammars' step — CritterStackFixture
  # alone carries the store vocabulary but not the HTTP one. Routes here are absolute,
  # so leave the module's route prefix empty.

  @arrangement
  Scenario: home check requested
    Given HomeCheckRequested occurred
      | applicationId                        | ownerId                              | shelterId                            |
      | a99a0002-0000-0000-0000-000000000002 | 0e5e0012-0000-0000-0000-000000000012 | 5e110001-0000-0000-0000-000000000001 |

  @arrangement
  Scenario: home check requested (2)
    Given HomeCheckRequested occurred
      | applicationId                        | ownerId                              | shelterId                            |
      | a99a0003-0000-0000-0000-000000000003 | 0e5e0013-0000-0000-0000-000000000013 | 5e110001-0000-0000-0000-000000000001 |

  @slice:RequestHomeCheck
  Scenario: An admin requests a home check
    Triggered by Request Home Check
    Given no events for HomeCheck "52095209-5209-5209-5209-520952095209"
    When RequestHomeCheck is posted to "/api/volunteering/requesthomecheck"
      | homeCheckId                          | applicationId                        | ownerId                              | shelterId                            |
      | 52095209-5209-5209-5209-520952095209 | a99a0001-0000-0000-0000-000000000001 | 0e5e0011-0000-0000-0000-000000000011 | 5e110001-0000-0000-0000-000000000001 |
    Then HomeCheckRequested is emitted
      | applicationId                        | ownerId                              |
      | a99a0001-0000-0000-0000-000000000001 | 0e5e0011-0000-0000-0000-000000000011 |

  @slice:RequestHomeCheck
  Scenario: Requesting the same home check twice is refused
    Triggered by Request Home Check
    Given no events for HomeCheck "61476147-6147-6147-6147-614761476147"
    And HomeCheckRequested occurred
      | applicationId                        | ownerId                              | shelterId                            |
      | a99a0001-0000-0000-0000-000000000001 | 0e5e0011-0000-0000-0000-000000000011 | 5e110001-0000-0000-0000-000000000001 |
    When RequestHomeCheck is posted to "/api/volunteering/requesthomecheck"
      | homeCheckId                          | applicationId                        | ownerId                              | shelterId                            |
      | 61476147-6147-6147-6147-614761476147 | a99a0001-0000-0000-0000-000000000001 | 0e5e0011-0000-0000-0000-000000000011 | 5e110001-0000-0000-0000-000000000001 |
    # refused with: "This home check has already been requested"
    Then the response is 400
    And no events are emitted

  @slice:AcceptHomeCheckAssignment
  Scenario: A volunteer accepts an assignment and proposes a time
    Triggered by Home Check Assigned
    Given no events for HomeCheck "89138913-8913-8913-8913-891389138913"
    And the arrangement "home check requested"
    When AcceptHomeCheckAssignment is posted to "/api/volunteering/accepthomecheckassignment"
      | homeCheckId                          | volunteerOwnerId                     | proposedFor          |
      | 89138913-8913-8913-8913-891389138913 | 0e5e0099-0000-0000-0000-000000000099 | 2026-10-01T15:00:00Z |
    Then HomeCheckAssignmentAccepted is emitted
      | ownerId                              | volunteerOwnerId                     | proposedFor          |
      | 0e5e0012-0000-0000-0000-000000000012 | 0e5e0099-0000-0000-0000-000000000099 | 2026-10-01T15:00:00Z |

  @slice:AcceptHomeCheckAssignment
  Scenario: Accepting an assignment books the home check as an appointment
    Triggered by Home Check Assigned
    Given no events for HomeCheck "49184918-4918-4918-4918-491849184918"
    And HomeCheckRequested occurred
      | applicationId                        | ownerId                              | shelterId                            |
      | a99a0004-0000-0000-0000-000000000004 | 0e5e0014-0000-0000-0000-000000000014 | 5e110004-0000-0000-0000-000000000004 |
    When AcceptHomeCheckAssignment is posted to "/api/volunteering/accepthomecheckassignment"
      | homeCheckId                          | volunteerOwnerId                     | proposedFor          |
      | 49184918-4918-4918-4918-491849184918 | 0e5e0099-0000-0000-0000-000000000099 | 2026-10-01T15:00:00Z |
    Then the MyAppointments read model with id "0e5e0014-0000-0000-0000-000000000014" contains
      | AwaitingConfirmation | Confirmed | Closed |
      | 1                    | 0         | 0      |

  @slice:AcceptHomeCheckAssignment
  Scenario: An assignment already accepted is not accepted again
    Triggered by Home Check Assigned
    Given no events for HomeCheck "99009900-9900-9900-9900-990099009900"
    And the arrangement "home check requested"
    And HomeCheckAssignmentAccepted occurred
      | ownerId                              | shelterId                            | volunteerOwnerId                     | proposedFor          |
      | 0e5e0012-0000-0000-0000-000000000012 | 5e110001-0000-0000-0000-000000000001 | 0e5e0099-0000-0000-0000-000000000099 | 2026-10-01T15:00:00Z |
    When AcceptHomeCheckAssignment is posted to "/api/volunteering/accepthomecheckassignment"
      | homeCheckId                          | volunteerOwnerId                     | proposedFor          |
      | 99009900-9900-9900-9900-990099009900 | 0e5e0098-0000-0000-0000-000000000098 | 2026-10-02T15:00:00Z |
    # refused with: "This home check is already assigned"
    Then the response is 400
    And no events are emitted

  @slice:SubmitHomeCheckReport
  Scenario: A volunteer reports on a visit they accepted
    Triggered by Submit Home Check Report
    Given no events for HomeCheck "39323932-3932-3932-3932-393239323932"
    And the arrangement "home check requested (2)"
    And HomeCheckAssignmentAccepted occurred
      | ownerId                              | shelterId                            | volunteerOwnerId                     | proposedFor          |
      | 0e5e0013-0000-0000-0000-000000000013 | 5e110001-0000-0000-0000-000000000001 | 0e5e0099-0000-0000-0000-000000000099 | 2026-10-01T15:00:00Z |
    When SubmitHomeCheckReport is posted to "/api/volunteering/submithomecheckreport"
      | homeCheckId                          | outcome | notes                                                 |
      | 39323932-3932-3932-3932-393239323932 | Pass    | Secure garden, calm household, good fit for a shy dog |
    Then HomeCheckReportSubmitted is emitted
      | outcome | notes                                                 |
      | Pass    | Secure garden, calm household, good fit for a shy dog |

  @slice:SubmitHomeCheckReport
  Scenario: A visit nobody accepted cannot be reported on
    Triggered by Submit Home Check Report
    Given no events for HomeCheck "83508350-8350-8350-8350-835083508350"
    And the arrangement "home check requested (2)"
    When SubmitHomeCheckReport is posted to "/api/volunteering/submithomecheckreport"
      | homeCheckId                          | outcome | notes                                                 |
      | 83508350-8350-8350-8350-835083508350 | Pass    | Secure garden, calm household, good fit for a shy dog |
    # refused with: "Nobody has accepted this home check"
    Then the response is 400
    And no events are emitted
