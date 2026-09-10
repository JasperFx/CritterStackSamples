using Bobcat;
using Bobcat.CritterStack;

namespace ShipmentTracking.Specs;

// Bobcat 0.18.0 ships the document lane (#270), so this project composes it instead of
// carrying its own. [FixtureTitle] is gone too: #273 fixed the matching that made the
// convention names fail here.
[IncludeGrammars(typeof(HttpGrammars))]
[IncludeGrammars(typeof(DocumentGrammars))]
public class BookingShipments : CritterStackFixture;

[IncludeGrammars(typeof(HttpGrammars))]
[IncludeGrammars(typeof(DocumentGrammars))]
public class CancellingShipments : CritterStackFixture;
