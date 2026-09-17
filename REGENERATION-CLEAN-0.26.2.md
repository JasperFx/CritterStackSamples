# Clean regeneration on Bobcat 0.26.2

A fresh clone of `origin/main`, the curated model + plan + manifest carried in as INPUTS, the
scaffolder run once, its output dropped in **untouched**, and then only the decisions filled.

**78 specs green in 52s. Zero structural edits.**

## The four shapes that needed rewriting last time, straight out of the generator

```
Identity<IShelterEvent>(x => x.ShelterId);                                       # bobcat#349
Identity<IOwnerEvent>(x => x.OwnerId);
public override AppointmentsQueue Evolve(AppointmentsQueue snapshot, Guid id, IEvent e)   # bobcat#351
public override MyAppointments   Evolve(MyAppointments   snapshot, Guid id, IEvent e)
[ReadModel] on Validate : 0                                                      # bobcat#345
empty response records  : 0                                                      # bobcat#346
```

No signature, routing rule or attribute the scaffolder emitted had to be replaced. Everything
removed from a scaffolded file was a `TODO` comment or a `throw new NotImplementedException`.

## What still needed hand work, and neither is a scaffolder defect

### 1. Ten endpoints bound `X?` because the model declared one 404 — NOW FIXED IN THE MODEL

| | scaffolded | final |
|---|---|---|
| `[WriteModel]` nullable | **12** | 2 |
| `[WriteModel]` required | 1 | **11** |

The model declares `refusedWith: status: 404` on **one** slice, so the scaffolder binds the other
twelve nullable — correctly, given what it was told. Two of those genuinely are creating slices
(`ApplyToVolunteer`, `RequestHomeCheck`) where null is the expected state. The other ten were
flipped to non-nullable by hand.

**Resolved.** The other ten are declared now, and the numbers moved exactly as predicted:

| | one 404 declared | eleven declared |
|---|---|---|
| `[WriteModel]` nullable | 12 | **2** |
| `[WriteModel]` required | 1 | **11** |
| scenarios | 42 | **52** |
| suite | 78 green | **88 green** |

The two that stay nullable are the genuinely creating slices, `ApplyToVolunteer` and
`RequestHomeCheck`, where null is the expected state and non-null is the refusal. Every scaffolded
signature now matches what had been hand-edited, and the only remaining difference is cosmetic — a
`Validate` that reads just the aggregate drops the unused `command` parameter.

Falsified rather than assumed: making one write model nullable again fails exactly its own 404
scenario and nothing else, which is Wolverine ceasing to emit the guard.

The original note, kept because it is the general lesson:

**This was a model gap, not a scaffolder gap, and it was the single biggest remaining source of hand
editing.** bobcat#337 made a 404 declarable and the payoff scales with how many you declare:
declaring all eleven removes ten edits and the nullable/non-nullable judgement with them. Worth
doing before the next pass — the scaffolder already turns a declared 404 into a required parameter
AND prose explaining there is no guard to write.

### 2. `[property: Identity]` on seven command records

Scaffolded: 0. The stream identity is a field of the command (`ApplicantOwnerId`, `HomeCheckId`),
and nothing in the curated format says which. The scaffold's own comment points at it —
`A computed stream id belongs on the request record: [Identity] public Guid ...Id => ...;` — so it
is documented rather than automated. Probably genuinely hard to derive; worth a look at whether the
model could state it.

### 3. Status vocabularies are new files

`AppointmentKind.cs` and the `*Status` constants are hand-written, because the curated format knows
only `string, Guid, bool, int, decimal, DateTimeOffset`. A format limitation propagating into the
domain, as noted in the 2026-09-17 handoff.

## The interceptors

93 step calls rendered, **zero** carrying an unbound placeholder, and no BOBCAT027 in the build.
That warning is what makes this checkable — it names the method, the placeholder, and the parameters
the method actually has. Reading `BobcatStepInterceptors.g.cs` is still the confirmation, not the
first move.
