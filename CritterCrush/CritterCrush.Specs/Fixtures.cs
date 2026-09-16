using Bobcat;
using Bobcat.CritterStack;

namespace CritterCrush.Specs;

// One shell fixture per feature, and the base type is chosen by what the feature ACTS with, not by
// taste. CritterStackHttpFixture is an assembly of the store vocabulary plus HttpGrammars (bobcat
// #210/#212), so an HTTP-driven collapsed slice specs entirely in the shipped, compile-checked
// vocabulary with no hand-written steps. The view features never act at all — they arrange history
// and assert a read model — so the store vocabulary alone is what they need.

[FixtureTitle("BookingAppointments")]
public class BookingAppointmentsFixture : CritterStackHttpFixture;

[FixtureTitle("AppointmentsQueue")]
public class AppointmentsQueueFixture : CritterStackFixture;

[FixtureTitle("MyAppointments")]
public class MyAppointmentsFixture : CritterStackFixture;

[FixtureTitle("Volunteering")]
public class VolunteeringFixture : CritterStackHttpFixture;

[FixtureTitle("HomeChecks")]
public class HomeChecksFixture : CritterStackHttpFixture;

[FixtureTitle("VolunteerApplicationsQueue")]
public class VolunteerApplicationsQueueFixture : CritterStackFixture;
