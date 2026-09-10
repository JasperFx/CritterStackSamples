using Bobcat;
using Bobcat.CritterStack;

namespace ShipmentTracking.Specs;

[FixtureTitle("BookingShipments")]
[IncludeGrammars(typeof(HttpGrammars))]
[IncludeGrammars(typeof(DocumentGrammars))]
public class BookingShipmentsFixture : CritterStackFixture;

[FixtureTitle("CancellingShipments")]
[IncludeGrammars(typeof(HttpGrammars))]
[IncludeGrammars(typeof(DocumentGrammars))]
public class CancellingShipmentsFixture : CritterStackFixture;
