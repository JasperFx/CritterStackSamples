using CritterCrush.Scheduling;
using Xunit;

namespace CritterCrush.Specs;

/// <summary>
/// Every reachable appointment state against every command, checked for one invariant: for a single
/// appointment the shelter queue's three buckets sum to 1 and none is negative.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately NOT a Bobcat spec</b>, and the mechanism is the missing <c>[BobcatFeature]</c> —
/// without it the generator's extractor returns null for this class, so it contributes no scenario
/// identity and no slice binding. That is exactly right here: this is a chapter-wide invariant
/// rather than a specification of one slice, so it belongs to no slice on the Event Model and the
/// curated model has no place to declare 36 identities for it. Adding <c>[BobcatFeature]</c> would
/// make every pair an orphan identity.
/// </para>
/// <para>
/// It still uses <see cref="CritterCrushSpec"/> for the host and the store vocabulary — an ordinary
/// xUnit test is free to, and the decorated steps simply report nothing outside a scenario.
/// </para>
/// <para>
/// <b>Why it earns 30 seconds.</b> It catches, as a class, the four guard defects the review found
/// one at a time — each of which let an event reach a closed or unconfirmed appointment and moved a
/// counter that should not have moved. Removing any one guard fails it with the offending pair
/// named: <c>[proposed + noshow] awaiting=1 confirmed=-1 closed=1</c>.
/// </para>
/// </remarks>
[Collection(CritterCrushHost.CollectionName)]
public class AppointmentQueueInvariants(CritterCrushHost host) : CritterCrushSpec(host)
{
    public static TheoryData<string, string> Pairs()
    {
        var data = new TheoryData<string, string>();
        foreach (var state in new[] { "proposed", "confirmed", "completed", "cancelled", "noshow", "requested" })
        foreach (var cmd in new[] { "confirm", "request", "reschedule", "complete", "cancel", "noshow" })
            data.Add(state, cmd);
        return data;
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public async Task the_queue_buckets_always_sum_to_one_and_never_go_negative(string state, string cmd)
    {
        var id = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var shelter = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 10, 1, 15, 0, 0, TimeSpan.Zero);
        var later = at.AddDays(4);

        var history = new List<object>
        {
            new HomeCheckAppointmentProposed(owner, shelter, AppointmentKind.HomeCheck, id, at)
        };
        if (state is "confirmed" or "completed" or "noshow" or "requested")
            history.Add(new AppointmentConfirmed(owner, shelter, at));
        if (state == "completed") history.Add(new AppointmentCompleted(owner, shelter, at.AddHours(1)));
        if (state == "cancelled") history.Add(new AppointmentCancelled(owner, shelter, false, "withdrew"));
        if (state == "noshow") history.Add(new AppointmentNoShowRecorded(owner, shelter, at));
        if (state == "requested") history.Add(new AppointmentRescheduleRequested(owner, shelter, later, "busy"));

        await GivenEvents<Appointment>(id, history.ToArray());

        var (body, route) = cmd switch
        {
            "confirm"    => ((object)new ConfirmAppointment(id), "/api/scheduling/confirmappointment"),
            "request"    => (new RequestReschedule(id, later, "busy"), "/api/scheduling/requestreschedule"),
            "reschedule" => (new RescheduleAppointment(id, later), "/api/scheduling/rescheduleappointment"),
            "complete"   => (new CompleteAppointment(id), "/api/scheduling/completeappointment"),
            "cancel"     => (new CancelAppointment(id, "withdrew"), "/api/scheduling/cancelappointment"),
            _            => (new RecordAppointmentNoShow(id), "/api/scheduling/recordappointmentnoshow")
        };

        await WhenPosted(body, route);

        var q = await ThenReadModel<AppointmentsQueue>(shelter);
        var sum = q.AwaitingConfirmation + q.Confirmed + q.Closed;
        Assert.True(sum == 1 && q.AwaitingConfirmation >= 0 && q.Confirmed >= 0 && q.Closed >= 0,
            $"[{state} + {cmd}] awaiting={q.AwaitingConfirmation} confirmed={q.Confirmed} closed={q.Closed}");
    }
}
