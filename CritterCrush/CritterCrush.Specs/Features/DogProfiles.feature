@domain:Profiles
Feature: DogProfiles
  Triggered by the dog owner

  @slice:CreateDogProfile
  Scenario: Creating a profile starts its stream
    Given no events for DogProfile "11111111-1111-1111-1111-111111111111"
    When CreateDogProfile is received
      | DogProfileId                         | Name    | Breed | AgeInMonths | OwnerId                              |
      | 11111111-1111-1111-1111-111111111111 | Biscuit | Corgi | 24          | aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa |
    Then DogProfileCreated is emitted
    And the DogProfile read model contains
      | Name    | Breed |
      | Biscuit | Corgi |
