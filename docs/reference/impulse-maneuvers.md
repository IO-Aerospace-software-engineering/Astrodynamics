# Impulse Maneuvers

Impulse maneuvers model instantaneous delta-V burns triggered by g-function zero-crossings during propagation. Each maneuver defines its own event function and firing condition.

## ImpulseManeuver (Abstract)

| Member | Description |
|--------|-------------|
| `ComputeEventValue(StateVector)` | Scalar g-function evaluated at the current state; pure on every library maneuver (decision 50 of #668: before 11.0.0 a call locked the node of a `PlaneAlignmentManeuver` and wrote the intersection of an `ApsidalAlignmentManeuver`, #802), the node locked by the detector of a run |
| `ComputeDeltaV(StateVector)` | The delta-V of the impulse at the state, in the frame of the state, without executing it: the state is taken about the centre of the maneuver, as the execution takes it (about its own observer for a maneuver without one), so at one state, whatever its observer (an SSB-mode solution's states are about the Sun), it is the delta-V of the execution; pure, it touches no tank, chain, window, gate or node lock, so it is the same before and after a run; at the state of its event in a run, the delta-V the run applies (decision 50 of #668, decided by S. Guillet, 2026-11-23; before, the preview was the execution, internal `TryExecute` since then, which burnt the fuel and moved the chain of a finished run, #802). A `PlaneAlignmentManeuver` takes the node of the rule of its g-function at the state; the fuel of a delta-V is `Tsiolkovski.DeltaM` |
| `EventCrossingDirection` | `CrossingDirection` that triggers the burn |
| `CheckPreconditions(StateVector)` | Optional additional constraints (returns `true` to allow firing). When it returns `false`, the occurrence is skipped and the maneuver waits for its next crossing; if none satisfies it within the window, the maneuver is not executed (`ThrustWindow` stays null) |
| `DeltaV` | Burn delta-V vector, in the inertial frame of the propagated state: the last run's value (below) |
| `ThrustWindow` | Window of the equivalent finite burn (see below) |
| `ManeuverWindow` | Thrust window extended by the maneuver hold duration (zero or more: a negative hold throws `ArgumentOutOfRangeException`, `holdDuration`, since 11.0.0, R11 of #668) |
| `Engine` | The engine of the impulse: the one it was given, null for one the run infers, until a run writes the engine it used right after its re-arm (the given one, else the only engine of the spacecraft); the next re-arm restores the given value ([The engine and the centre](#the-engine-and-the-centre); on `Maneuver` before lot L5b, decisions 48 and 93 of #668) |
| `ManeuverCenter` | The centre of the impulse, by the same rule: the given one, else the observer of the initial orbit when it is a celestial body other than a star; required in SSB mode |
| `ExecutionError` | Optional execution error by the model of Gates (`ManeuverExecutionError`), set with an object initializer; added to the velocity covariance at the burn when the propagation propagates the covariance, ignored otherwise. See [Covariance Propagation](covariance-propagation.md#execution-error-of-an-impulsive-maneuver) |

`ManeuverExecutionError` takes its four standard deviations by name, so that two of different units cannot be swapped:
`new ManeuverExecutionError { ResolutionSigma = 0.01, PointingSigma = 0.001 }`. `ResolutionSigma` (σ_r), fixed magnitude
error, in m/s; `ShutoffSigma` (σ_s), magnitude error proportional to |ΔV|, dimensionless; `AutopilotSigma` (σ_a), fixed
pointing error, in m/s on each axis perpendicular to ΔV; `PointingSigma` (σ_p), pointing error proportional to |ΔV|, in
rad on each axis. Each finite and non-negative, zero when left out. A ΔV of
exactly zero adds no error; any non-zero ΔV takes the whole model, the fixed terms included. The trajectory follows the
commanded ΔV (open loop).

### The frame of a target orbit

`PlaneAlignmentManeuver`, `ApsidalAlignmentManeuver` and the target-orbit `PhasingManeuver` compare their target with
the integrated state, so the target is given in the frame the spacecraft is propagated in: the frame of its initial
orbit, or ICRF for a spacecraft whose initial orbit is a TLE or an osculating TEME state, which is propagated in ICRF
([Propagation Frame](propagators.md#propagation-frame)). `Spacecraft.SetManeuvers` checks every target of the plan
against that frame, `ArgumentException` (parameter `maneuvers`) for one in another frame (decision 46 of #668). The
propagation checks every target of the chain, leading and chained, when it is built, on every spacecraft and every path (`Spacecraft.Propagate`, `Scenario.Simulate`,
`BatchPropagator`, the constructors and the avoidance propagations of the conjunction analysis; `MonteCarloPropagator`
refuses a template with a plan, decision 41 of #668): a target in another frame throws `ArgumentException` (parameter
`spacecraft`) naming the maneuver, the two frames and the fix, `targetOrbit.ToFrame(Frame.ICRF)` for ICRF, before it
changes anything (the case of a spacecraft whose initial orbit's frame changed after `SetManeuvers`: the constructor
keeps its `ArgumentException`, the call receiving the faulty object, decision 94 of #668, decided by S. Guillet,
2026-11-23), and again when the propagator propagates (`PropagatorBase.Propagate`, which reads the chain of the
spacecraft again), against the frame it integrates in, the only check that throws `InvalidOperationException`, the
state of the spacecraft (#685; before 11.0.0 only a
TLE or TEME spacecraft was checked there, #675, and on another spacecraft the components of the target were read as if
they were in the integration frame: a plane change of 2° from an ECLIPJ2000 target on an ICRF spacecraft burned
2807 m/s for 263 m/s). The constructor of a maneuver checks no frame since lot L5b (decision 65 of #668, decided by
S. Guillet, 2026-11-23): the plan and the propagation are the one rule. Before, it compared the target, when the tank of
its engine was on a spacecraft, with the frame of that spacecraft's initial orbit or ICRF, a weaker rule that took a
TEME target on a TLE spacecraft the propagation then rejected. Frames are compared by name, as everywhere in the library, but for ICRF and GCRF, which the library gives the
same axes (`GcrfFrame`, the identity rotation): a GCRF target is taken on a spacecraft propagated in ICRF (a TLE or TEME
spacecraft included), and an ICRF target on one propagated in GCRF, by `SetManeuvers` and the propagation alike, the
components of the target being those the integration reads; the burn is bit for bit that of the relabelled target
(third review of #685; before, both were rejected, and the fix the message named, `targetOrbit.ToFrame(Frame.ICRF)`,
changed no number). Any other pair of frames is rejected, whatever their rotation.

## Thrust Window Convention

The burn is impulsive: the propagated state receives the whole delta-V at the event epoch `t`.
The library also reports the finite burn the impulse stands for, a burn of constant thrust F = Isp g0 q and fuel flow q.
Its duration `dt` follows from the rocket equation (engine ISP, fuel flow and total mass), and the impulse epoch is the
centroid of its acceleration, F / (m0 − q τ), which grows as the mass drops: the window is placed on
`[t - f dt, t + (1 - f) dt]` with

f = 1/x + 1/ln(1 − x) = 1/(1 − e^(−u)) − 1/u, x = Δm / m0 = 1 − e^(−u), u = Δv / (Isp g0),

x the mass fraction burned, m0 the mass before the burn
([#335](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/335), derived from
S. Guillet's remark of 2026-11-18 that the acceleration is stronger at the end of the burn than at its start, decided by
S. Guillet, 2026-11-18). f → 1/2 as x → 0 (1/2 + u/12 − u³/720 + …), 0.509 at x = 0.1, 0.557 at 0.5, 2/3 only at x ≈ 0.89, and f → 1 as x → 1. The impulse
at the centroid gives the burn's velocity change and, in a field-free space, its displacement after the burn: the
position after a burn of acceleration a(τ) gains ∫ (T − τ) a(τ) dτ = Δv (T − f dt) at T after its start, as from Δv at
f dt. `ManeuverWindow` runs from the start of the thrust window to the later of the end of the hold and the end of the
thrust window: `[t - f dt, t - f dt + max(hold, dt)]`.

The end of `ManeuverWindow` is the earliest epoch at which the next maneuver of the chain may fire, an attitude
included: an attitude chained after an impulse activates there (#539).

In a propagation about the Earth both windows, and the holds of the attitudes, count TT seconds from the time argument
of the event (#547), as the integration does (#501); about any other body, SSB mode included, TDB seconds, bit for bit
as before 11.0.0. The execution of an impulse, internal since 11.0.0 (decision 48 of #668), returns the state after the
impulse as a new state, its velocity plus ΔV, and leaves the state it is given unchanged (before 11.0.0 it added ΔV to
that state's velocity, #588). The attitude of the impulse, its front along ΔV, is in force over `[t - f dt, end of ManeuverWindow]`,
its start no earlier than the start of the propagation or the previous activation of the chain
([Attitudes](attitudes.md#the-attitude-timeline)).

Before 11.0.0 the window was `[t - 0.666 dt, t + 0.334 dt]`, two thirds of the burn before the impulse, a split
of no recorded derivation from the initial .NET implementation: the thrust window, `MAN_EPOCH_IGNITION` and the gate of
the next maneuver opened (0.666 − f) dt earlier, 1.6 s for a burn of 10 s at x = 0.1, 5.4 s for one of 50 s at x = 0.5.
The propagated state is unchanged, but where the gate binds a chained maneuver fires later.

## Minimum Epoch

`MinEpoch` is the earliest epoch at which the maneuver may fire (#569). The maneuver fires at the first crossing of
its g-function in its direction at or after the minimum epoch, searched from the integrated state there, as GMAT's
`Propagate` to the epoch followed by a `Propagate` to the apsis does:

- `VVIntegrator` and `RK78Integrator` evaluate the g-function at the minimum epoch, in the integration step that holds
  it: RK7(8) on the state of a shortened step from the start of that step (13 force evaluations), Velocity-Verlet on the
  cubic Hermite of the step (none). RK7(8) then locates the crossing between the minimum epoch and the end of the step,
  and never labels it before the minimum epoch; Velocity-Verlet fires at the end of the step that holds it, as for any
  event.
- A crossing before the minimum epoch never counts. A user `ImpulseManeuver` whose g-function has no later crossing (a
  time-like g, `t − t*`) therefore never fires when its root precedes its minimum epoch.
- A g-function exactly zero at the minimum epoch is a crossing there when the sign that follows it is the target sign,
  a minimum epoch on a step end or equal to the start of the propagation included (an apsis given by Keplerian elements
  at the start, where r·v is 0.0): RK7(8) fires at the minimum epoch; Velocity-Verlet at the first step end whose sign is
  known after it, the next one when the minimum epoch is a step end. A g-function exactly zero at the start with the
  minimum epoch before it fires at the start the same way (GMAT's convention, #580; decided by S. Guillet, 2026-11-17):
  a minimum epoch at the start and one before it give the same burn.
- A g-function exactly zero at a step end after the minimum epoch is a crossing there (#580): a time-like user impulse
  whose root is a step end of Velocity-Verlet or of fixed-step RK7(8) fires at its root.
- `PlaneAlignmentManeuver` takes the node it moves toward from the state at its minimum epoch, or at the start of the
  segment when the minimum epoch is at or before it, never a node just passed: within 1e-6 |r| of a node it moves away
  from, it waits for the next node (#581; before 11.0.0 a start less than 1e-6 |r| past a node took that node, a period
  later).
- A chained impulse is gated by the end of the previous maneuver's window, the same way, in whichever step holds it:
  an impulse, or an attitude (#539). The last leading attitude gates the first impulse by the end of its hold, and an
  attitude chained after an impulse delays the next impulse by its hold from the end of the impulse's window. About the
  Earth the end of an impulse's window is counted in TT seconds and replaces, as the gate of the next maneuver, the TDB
  end the execution raised it to (#547).
- That gate belongs to the propagation that set it (#573). `MinEpoch` reports the later of the minimum epoch the
  maneuver was given (its constructor's, which lasts; before 11.0.0 `Spacecraft.SetStandbyManeuver(maneuver, minimumEpoch)`
  could raise it: a later minimum epoch is now a new maneuver in a new plan, decision 47 of #668) and that gate, which holds after the propagation with the windows of the maneuver; the next `Spacecraft.Propagate` (and
  `Scenario.Simulate` and `BatchPropagator` through it) clears it before it runs. A spacecraft propagated again,
  from another state or over another window, so fires its maneuvers as a spacecraft never propagated would.
- With Velocity-Verlet, the decision follows its own trajectory, whose crossings lag the Keplerian epoch: with 10 s
  steps, up to 0.096 s at a node, 0.18 s at an apsis of the orbit after a Velocity-Verlet burn and 1.5 s at the
  intersection of an apsidal alignment (worsts 0.0953 s, 0.176 s and 1.44 s on Linux x64, Windows and macOS arm64). A
  minimum epoch within that lag of a crossing is decided on that trajectory.
- The maneuver detector's `Evaluate` (internal since decision 88 of #668; a custom `IIntegrator` given to the propagator reads it as an `IEventDetector`, within the run only: once the run ends it throws `InvalidOperationException` for the calls that start after the end, decision 95 of #668, a call in progress then completing, decision 98 of #668, and so it does once the detector has executed its impulse, decision 97 of #668; see [Event Detection](event-detection.md#the-maneuver-detector)) still returns +1 before the minimum epoch. A custom integrator that compares
  that value with the first g-value after the minimum epoch keeps the behaviour before 11.0.0: a false crossing at the
  minimum epoch for a positive-to-negative maneuver whose g is already negative there, and a missed negative-to-positive
  crossing in the step that holds it.

Before 11.0.0 the integrators compared that +1 with the first step end after the minimum epoch: a `PerigeeHeightManeuver`
gated where r·v < 0 fired at the minimum epoch, and an `ApogeeHeightManeuver` gated in the step of its perigee, before
it, fired one orbit later (#569).

## Constructors

Each impulse takes its target first, then the optional `engine`, `minEpoch`, `holdDuration` and `maneuverCenter`
(D1 rule 8 of #668, lot L5b, decided by S. Guillet, 2026-11-23), for instance `new ApogeeHeightManeuver(rGeo)` or
`new PerigeeHeightManeuver(rGeo, holdDuration: TimeSpan.FromMinutes(10))`. A null `engine` is the only engine of the
spacecraft and a null `maneuverCenter` the observer of its initial orbit, both inferred by the run
([The engine and the centre](#the-engine-and-the-centre)); `minEpoch` defaults to default(Time), no minimum, and
`holdDuration` to zero (every parameter after the target is optional). The target-orbit forms take no `maneuverCenter`: their centre is the observer of the target,
which must be a celestial item (`ArgumentException`, `targetOrbit`, for a site). `PhasingManeuver` takes no hold.

```csharp-signature
ApogeeHeightManeuver(double apogeeRadius, Engine engine, Time minEpoch, TimeSpan holdDuration, CelestialItem maneuverCenter)
```

```csharp-signature
ApogeeHeightManeuver(OrbitalParameters targetOrbit, Engine engine, Time minEpoch, TimeSpan holdDuration)
```

```csharp-signature
PerigeeHeightManeuver(double perigeeRadius, Engine engine, Time minEpoch, TimeSpan holdDuration, CelestialItem maneuverCenter)
```

```csharp-signature
PerigeeHeightManeuver(OrbitalParameters targetOrbit, Engine engine, Time minEpoch, TimeSpan holdDuration)
```

```csharp-signature
CombinedManeuver(double perigeeRadius, double inclination, Engine engine, Time minEpoch, TimeSpan holdDuration, CelestialItem maneuverCenter)
```

```csharp-signature
PhasingManeuver(double trueLongitude, uint revolutionNumber, Engine engine, Time minEpoch, CelestialItem maneuverCenter)
```

```csharp-signature
PhasingManeuver(OrbitalParameters targetOrbit, uint revolutionNumber, Engine engine, Time minEpoch)
```

```csharp-signature
PlaneAlignmentManeuver(OrbitalParameters targetOrbit, Engine engine, Time minEpoch, TimeSpan holdDuration)
```

```csharp-signature
ApsidalAlignmentManeuver(OrbitalParameters targetOrbit, Engine engine, Time minEpoch, TimeSpan holdDuration)
```

Radii are checked (decision 68 of #668, decided by S. Guillet, 2026-11-23): a radius of 0 or less, or not finite, throws
`ArgumentOutOfRangeException` naming it (`apogeeRadius`, `perigeeRadius`), its message "a radius from the centre ...;
for a height h pass R + h"; an apogee radius below the equatorial radius of a celestial body centre throws
`ArgumentException` in the constructor for a given centre (the target-orbit form included, naming `targetOrbit`; the
radius of a deferred body such as `PlanetsAndMoons.EARTH_BODY` is read there) and `InvalidOperationException` from the
propagator constructor and `Propagate` for a centre the run infers. A perigee inside the body stays legal (a deorbit).
`CombinedManeuver` checks its inclination in [0, π] (`ArgumentOutOfRangeException`, `inclination`). Before 11.0.0 the
radii were accepted, and 1000e3 m, an altitude given for a radius, put the apogee 5378 km under the surface.

## Concrete Maneuvers

### ApogeeHeightManeuver

Moves the apogee, up or down, to `TargetApogeeRadius` (`TargetApogee` before 11.0.0), a radius in metres from the
centre of `ManeuverCenter` (not an altitude: R + h for an altitude h), at or above its equatorial radius. Fires at perigee (r . v crosses zero from negative to positive).

### PerigeeHeightManeuver

Moves the perigee, up or down, to `TargetPerigeeRadius` (`TargetPerigeeHeight` before 11.0.0), a radius in metres from
the centre of `ManeuverCenter` (not an altitude). Fires at apogee (r . v crosses zero from positive to negative).

### PhasingManeuver

Adjusts orbital period for phasing. Fires at perigee (r . v, negative to positive).

The burn, along the velocity, puts the spacecraft on a phasing orbit after `RevolutionNumber` (N) revolutions of which
it is back at the perigee together with its target. The target is Δθ ahead of the spacecraft at the burn, the
difference of their true longitudes (Ω + ω + ν) at its epoch, beyond π a target behind. With t(Δθ) the time from the
perigee to Δθ along the current orbit by Kepler's equation (negative for a target behind), the phasing orbit has the
period T₂ = T₁ − t(Δθ) / N and the semi-major axis a₂ = (√μ T₂ / 2π)^(2/3), and ΔV = h₂ / r_p − h_target / r_p at the
perigee. The hold is set to 0.9 N T₂ by the run that executes the burn, the last run's value: 0 before the burn and
after the re-arm of the next run; the constructors take no hold (decision 51 of #668, decided by S. Guillet, 2026-11-23:
before 11.0.0 they took one that the burn replaced, and the computed hold was left for good, #784). Two constructors set
the target:

| Constructor | Target | λ_target at the burn | h_target |
|-------------|--------|----------------------|----------|
| `PhasingManeuver(targetOrbit, revolutionNumber, engine, minEpoch)` | the object on `targetOrbit`, about the maneuver center (its observer) in the frame of the spacecraft | `targetOrbit.TrueLongitude(burn epoch)` | the target orbit's |
| `PhasingManeuver(trueLongitude, revolutionNumber, engine, minEpoch, maneuverCenter)` | a virtual target on the spacecraft's current orbit (#588) | `trueLongitude`, in radians, about the maneuver centre in the frame of the spacecraft's state | the current orbit's |

With a true longitude, Δθ = λ − λ_sc(burn), and since the burn is at the perigee λ_sc(burn) is the longitude of the
perigee Ω + ω: λ − (Ω + ω) is the phase of the target ahead of the spacecraft along its orbit, taken modulo 2π. For
instance, with a perigee longitude of 80°, 110° asks to catch up a target 30° ahead, 50° to let a target 30° behind
catch up. `TargetTrueLongitude` holds the value given, NaN for the target-orbit constructor. Before 11.0.0 the
true-longitude constructor threw `NullReferenceException` at its burn (#588).

### CombinedManeuver

Combined plane and perigee change: the perigee moves, up or down, to `TargetPerigeeRadius` (`TargetPerigeeHeight`
before 11.0.0), a radius in metres from the centre of the maneuver centre (not an altitude), and the inclination to
`TargetInclination`, in [0, π]. Fires at apogee (r . v,
positive to negative) with an additional precondition that the apogee is aligned with the ascending node. Its target
orbit carries its own perigee radius, a(1 − e); before 11.0.0 it carried the current orbit's, which no result read
(#677).

### PlaneAlignmentManeuver

Changes orbital plane to match a target. Uses an adaptive crossing direction on the g-function r . h_target to fire at the nearest orbital node (ascending or descending). The detector of a run locks the node at its first evaluation (`IsAscendingNode`, `EventCrossingDirection`); the re-arm of the next run clears both, the direction back to `Any` (#784: before 11.0.0 the direction of the last run's node was left). `RelativeInclination`, and the `IntersectsP`/`IntersectsQ` of `ApsidalAlignmentManeuver`, side results of the execution that nothing read, are internal since 11.0.0 (decision 51 of #668).

### ApsidalAlignmentManeuver

Rotates the line of apsides. Fires at the nearest orbit intersection point using the g-function r . (h x p) with negative-to-positive crossing.

## Maneuver Plan

A spacecraft runs its plan, set by `Spacecraft.SetManeuvers(params Maneuver[])` in order and read back by
`Spacecraft.Maneuvers` (decision 45 of #668, decided by S. Guillet, 2026-11-23): it links each maneuver to the next
(`NextManeuver`, whose setter is internal) and the last to none, and is the one way to link a plan; no maneuver clears
it. Every propagation runs the plan from its first maneuver, whatever an earlier one executed. Before 11.0.0
`Spacecraft.SetStandbyManeuver` set the head of a chain that `Maneuver.SetNextManeuver` linked; both are removed, with
`Spacecraft.InitialManeuver`, and `Spacecraft.StandbyManeuver`, the run's own state, is internal.

`SetManeuvers` claims each maneuver for its spacecraft (decision 46): a maneuver is in one plan at a time, and it throws
`ArgumentException` (`maneuvers`), changing nothing, for a maneuver in another spacecraft's plan, a maneuver listed
twice (the chain would be a cycle), an engine that is not one of the spacecraft's `Engines` (added with `AddEngine`,
compared by reference: an engine built on the spacecraft's tank and never added, or a twin of an added engine, is
refused; decision 92, decided by S. Guillet, 2026-11-23), an engine whose fuel tank is not one of the spacecraft's, or
a target orbit in another frame than the one the spacecraft is propagated in (below). The claim is released, and the link cleared, when the
owner sets another plan without the maneuver. So the tanks and engines are added (`AddFuelTank`, `AddEngine`) and the
initial orbit set before the plan. What changes after it is checked again: a propagator built after the initial
orbit's frame changed throws `ArgumentException` (`spacecraft`), the case of `Spacecraft.Propagate`, a scenario and a
batch, which build theirs (decision 94); a propagator built before the change throws `InvalidOperationException` at
`Propagate`, through the initial-orbit guard (#783) or the re-check of the chain.

The plan of a spacecraft is not changed during a run on it (decision 96 of #668, decided by S. Guillet, 2026-11-24): a
run reads the plan as it goes, its head at the re-arm and each next maneuver when the previous one ends, so a
`SetManeuvers` from another thread, or from code the run calls (a force, a custom integrator), mixes the two plans (in
the second review of L5a, the plan [m1, m2] set again during a run of [m1, m2] left `ExecutedManeuvers` reading m1,
m1). Set the plan before the run, or once it has returned. Nothing checks the rule yet: the run guard of
[#793](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/793), after lot L5, will make
`SetManeuvers` throw `InvalidOperationException` while a run holds the spacecraft
([Thread Safety](../guides/thread-safety.md#one-run-at-a-time-per-spacecraft)).

The `Maneuver` hierarchy is closed (decision 48): its constructor is `private protected`, so a user maneuver derives
from `ImpulseManeuver` (its delta-V in the protected `Execute`, its g-function, crossing direction and preconditions) or
from `Attitude` (`ComputeOrientation`); the concrete types stay unsealed. `TryExecute`, the execution a run calls, is
internal, and so is `Engine.Ignite` (decision 49): called as previews they changed a finished run (#802). The previews are
`ComputeDeltaV` and `Attitude.ComputeStateOrientation`.

**The last run's values** (decision 52): a public member a run writes on a maneuver holds the value of the last run that
executed it, and the re-arm of the next run clears it: `DeltaV`, `FuelBurned`, `ThrustWindow`, `ManeuverWindow`,
`MinEpoch` (back to the minimum epoch the maneuver was given), the node of a `PlaneAlignmentManeuver`
(`IsAscendingNode`, `EventCrossingDirection`) and the hold of a `PhasingManeuver`. `Attitude.StateOrientation` is
removed: the attitude of a run is in the attitude record. A stopped run leaves the values it wrote up to the stop.

### The chain of a run

The maneuvers of the plan run as a chain:

1. **Leading attitudes** are laid on the attitude timeline: each activates at the later of its `MinEpoch` and the end
   of the previous one's hold, from the start of the propagation; the last one's hold gates the first impulse.
2. The first `ImpulseManeuver` is wrapped in the internal maneuver detector (`ManeuverEventDetector`, decision 88 of #668).
3. When the event fires, the detector executes the burn, and only the burn (`HandleEvent` returns no next detector).
   The propagation then lays the attitudes chained after it on the timeline, each from the end of the previous window,
   and builds the detector of the next impulse.
4. The process repeats until all maneuvers in the chain have executed. An attitude whose activation falls after the end
   of the propagation is not executed, and the chain stops there.

Every executed maneuver, attitudes included, is in `Spacecraft.ExecutedManeuvers`, once, in the order of the chain
(#570): a new read-only list at each run, replaced by a new list at each execution (decision 58 of #668), so a list held from an
earlier run keeps its members; their values (`DeltaV`, `FuelBurned`, the windows) are those of the last run. The record
of each, with the values of its run, is on the solution, `PropagationSolution.Maneuvers`, one sealed `ExecutedManeuver`
per executed maneuver (decision 57, [Propagators](propagators.md#the-executed-maneuvers)), and a scenario's
`SpacecraftSummary` is built from them (decision 59).

A chain is a list: a maneuver appears in it once, and a repeated burn is a new maneuver object (#585). Each maneuver
keeps one record (its windows, `DeltaV`, `FuelBurned`), so a maneuver met twice would lose the first. A cycle is
rejected where it is made: `SetManeuvers` throws `ArgumentException` for a maneuver listed twice and leaves the plan as
it was (before 11.0.0 `SetNextManeuver` threw for a link that closed a cycle). The walks of the chain keep their guard:
a chain that is a cycle is rejected by the construction of the propagator (`Spacecraft.Propagate` and so the scenario and the batch, a
`CentralBodyPropagator` built directly: `InvalidOperationException`, before the states of an earlier
propagation are cleared) and by `Propagate` (`InvalidOperationException`). Before 11.0.0 a cycle ended the process with
a stack overflow at the first `Spacecraft.Propagate`, or never returned through the builder of 10.x.

### The engine and the centre

A maneuver may be given no engine and no centre (decisions 61, 63 and 64 of #668, decided by S. Guillet, 2026-11-23):
the run infers them, from the values the maneuver was given, when the propagator is built and again at `Propagate`,
writing nothing, and writes the values it uses on the maneuver right after its re-arm.

- **The engine** of an impulse (`ImpulseManeuver.Engine`; an attitude has none): the one it was given, else the only
  engine of the spacecraft (`Spacecraft.Engines`, decision 92). An impulse given no engine on a spacecraft of no engine
  or of several throws `InvalidOperationException` naming them, "pass engine:".
- **The centre** of every maneuver (`ManeuverCenter`): the one it was given, else the observer of the spacecraft's
  initial orbit, as given, when it is a celestial body other than a star. Where it cannot be inferred (SSB mode: the
  observer is a star or a barycentre), the library's own law whose targets are all library `CelestialAttitudeTarget`s
  (a `TriadAttitude` of celestial targets, an `InstrumentPointingToAttitude`) takes the central body of the run, the
  Sun, and every other law requires `maneuverCenter`: the impulses, the orbital-direction attitudes in any form, a user
  `IAttitudeTarget` (or a subclass of `CelestialAttitudeTarget`) and a user `Attitude` subclass (a subclass of
  `TriadAttitude` or `InstrumentPointingToAttitude` included, whatever its targets), whose use of the centre the
  library cannot know. Without it the propagator constructor and `Propagate` throw `InvalidOperationException` (decision 99 of #668,
  decided by S. Guillet, 2026-11-24; before, such a law received a heliocentric state silently). A state read from an
  SSB-mode solution is about the Sun, so an initial orbit built from it infers the Sun.

Before a run, `Engine` and `ManeuverCenter` return the values the maneuver was given (null for an inferred one); after
it, the values the run used, the last run's values (decision 52); the re-arm of the next run restores the given ones,
and a stopped run keeps the resolved ones. A token already cancelled or an `IIntegrator.Initialize` that throws changes
neither (decisions 6 and 22 of lot L4). Every check before the write and every preview read the given values (IC-1 of
lot L5b): a maneuver a run inferred the engine of can go to another spacecraft's plan once released, an engine added
between two runs makes the second throw, and `ComputeDeltaV` and `ComputeStateOrientation` take the state about the
given centre, or about its own observer, never about the centre of the last run.

Each impulse given an engine draws on a tank of the propagated spacecraft: its `Engine.FuelTank` is one of the
spacecraft's tank instances, held by it. The same constructions and `Propagate` check it and throw
`InvalidOperationException` naming the maneuver, the engine, its tank and the spacecraft, before anything changes
(#676, #586). Before 11.0.0 an impulse on a tank added to no spacecraft threw `NullReferenceException` at its burn, and
one on another spacecraft's tank burnt that spacecraft's fuel.

## Example

```csharp
// Inputs: earth (CelestialBody), engine (Engine), spacecraft (Spacecraft), window (Window), perturbingBodies
//   (CelestialItem[])
// Raise the apogee radius to 42,164 km (meters), at the first perigee after J2000 TDB.
var maneuver = new ApogeeHeightManeuver(
    42164000.0,
    engine,
    Time.J2000TDB,
    TimeSpan.Zero,
    earth);

spacecraft.SetManeuvers(maneuver);
var solution = spacecraft.Propagate(window, new PropagationOptions { ThirdBodies = perturbingBodies });
```

## See Also

- [Event Detection](event-detection.md)
- [Attitudes](attitudes.md)
- [Propagators](propagators.md)
