@domain:Discovery
Feature: Swiping
  Triggered by the dog owner swiping

  @slice:SwipeOnDog
  Scenario: A like is recorded
    When dog "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" swipes right on dog "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
    Then the response is 200
    And the swipe appended a DogLiked

  @slice:SwipeOnDog
  Scenario: Swiping the same dog twice is a clean no-op
    Given dog "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" already liked dog "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
    When dog "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" swipes right on dog "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
    Then the swipe appended nothing

  @slice:SwipeOnDog
  Scenario: A pass is recorded without a like
    When dog "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" swipes left on dog "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
    Then the swipe appended a DogPassed

  @slice:SwipeOnDog
  Scenario: Swiping yourself is refused
    When dog "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" swipes right on dog "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
    Then the response is 400
    And the swipe appended nothing

  @slice:DetectMutualMatch
  Scenario: A mutual like produces a match
    Given dog "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" already liked dog "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
    When dog "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb" swipes right on dog "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
    Then the swipe appended a DogLiked
    And the swipe appended a MutualMatchDetected
