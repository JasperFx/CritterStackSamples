@domain:Shipments
Feature: CancellingShipments
  Triggered by a shipper

  @slice:CancelShipment
  Scenario: Cancelling a booked shipment marks it cancelled
    Given documents of type ShipmentTracking.Data.Shipment
      | Id                                   | Origin | Destination | Carrier | Status |
      | 7760a002-0000-0000-0000-000000000002 | Dallas | Austin      | acme    | Booked |
    When CancelShipment is received
      | ShipmentId                           | Reason              |
      | 7760a002-0000-0000-0000-000000000002 | Customer changed it |
    Then the ShipmentTracking.Data.Shipment with id "7760a002-0000-0000-0000-000000000002" has
      | Status    |
      | Cancelled |
    And ShipmentCancelled is sent
