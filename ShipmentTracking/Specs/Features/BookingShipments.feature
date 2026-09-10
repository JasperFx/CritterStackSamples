@domain:Shipments
Feature: Booking Shipments
  Triggered by a shipper

  @slice:BookShipment
  Scenario: Booking a shipment stores it and cascades the booking event
    When BookShipment is received
      | ShipmentId                           | Origin | Destination | Carrier | WeightKg |
      | 7760a001-0000-0000-0000-000000000001 | Dallas | Austin      | acme    | 12.5     |
    Then the Shipment with id "7760a001-0000-0000-0000-000000000001" has
      | Origin | Destination | Carrier | Status |
      | Dallas | Austin      | acme    | Booked |
    And ShipmentBooked is sent

  @slice:BookShipment
  Scenario: The booking endpoint accepts and does not handle inline
    When BookShipmentRequest is posted to "/shipments"
      | Origin | Destination | Carrier | WeightKg |
      | Dallas | Austin      | acme    | 12.5     |
    Then the response is 202
    And BookShipment is sent
