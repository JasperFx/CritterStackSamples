@domain:Discovery
Feature: Swiping
  Triggered by the dog owner swiping

  @slice:SwipeOnDog
  Scenario: A like is recorded
    Given no events for SwipePair "22222222-2222-2222-2222-222222222222"
    When SwipeOnDog is received
      | SwipePairId                          | SwiperDogId                          | TargetDogId                          | Liked |
      | 22222222-2222-2222-2222-222222222222 | aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa | bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb | true  |
    Then DogLiked is emitted

  @slice:SwipeOnDog
  Scenario: Swiping the same dog twice is a clean no-op
    Given no events for SwipePair "33333333-3333-3333-3333-333333333333"
    And events for SwipePair
      | Event    | SwipePairId                          | SwiperDogId                          | TargetDogId                          |
      | DogLiked | 33333333-3333-3333-3333-333333333333 | aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa | bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb |
    When SwipeOnDog is received
      | SwipePairId                          | SwiperDogId                          | TargetDogId                          | Liked |
      | 33333333-3333-3333-3333-333333333333 | aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa | bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb | true  |
    Then no events are emitted

  @slice:SwipeOnDog
  Scenario: A pass is recorded without a like
    Given no events for SwipePair "55555555-5555-5555-5555-555555555555"
    When SwipeOnDog is received
      | SwipePairId                          | SwiperDogId                          | TargetDogId                          | Liked |
      | 55555555-5555-5555-5555-555555555555 | aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa | bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb | false |
    Then DogPassed is emitted

  @slice:DetectMutualMatch
  Scenario: A mutual like produces a match
    Given no events for SwipePair "44444444-4444-4444-4444-444444444444"
    And events for SwipePair
      | Event    | SwipePairId                          | SwiperDogId                          | TargetDogId                          |
      | DogLiked | 44444444-4444-4444-4444-444444444444 | aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa | bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb |
    When SwipeOnDog is received
      | SwipePairId                          | SwiperDogId                          | TargetDogId                          | Liked |
      | 44444444-4444-4444-4444-444444444444 | bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb | aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa | true  |
    Then DogLiked is emitted
    And MutualMatchDetected is emitted
