using Bobcat;
using Bobcat.CritterStack;

namespace CritterCrush.Specs;

// One shell fixture per feature. CritterStackHttpFixture is an assembly of the store vocabulary
// plus HttpGrammars (bobcat #210/#212) — so an HTTP-driven collapsed slice specs entirely in the
// shipped, compile-checked vocabulary, with no hand-written steps at all.
[FixtureTitle("BookingAppointments")]
public class BookingAppointmentsFixture : CritterStackHttpFixture;
