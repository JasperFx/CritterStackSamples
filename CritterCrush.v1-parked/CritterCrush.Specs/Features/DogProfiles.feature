@domain:Profiles
Feature: DogProfiles
  Triggered by the dog owner

  @slice:CreateDogProfile
  Scenario: Creating a profile starts its stream
    When a dog profile is posted
      | Name    | Breed | AgeInMonths | OwnerId                              |
      | Biscuit | Corgi | 24          | aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa |
    Then the response is 201
    And the new DogProfile contains
      | Name    | Breed |
      | Biscuit | Corgi |
