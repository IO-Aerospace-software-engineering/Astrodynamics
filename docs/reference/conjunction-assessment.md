# Conjunction Assessment

!!! note
    `ConjunctionAssessment` and its companion types live in the `IO.Astrodynamics.SSA` namespace.

`ConjunctionAssessment` is the main SSA facade for screening, analyzing, and evaluating conjunction events between a protected spacecraft and candidate secondary objects.

## Covariance at TCA

The covariance of each participant at the time of closest approach (TCA) is resolved once, at the final TCA, never
during the search, in this order:

1. **Propagated** (`CovarianceSource.Propagated`). The library propagates the participant in RK7(8), its initial
   `StateVector` carries a covariance at the start of that propagation, and the analysis computes a probability of
   collision. The participant's propagation is either the `PropagationSolution` passed to the analysis or, for a
   `Spacecraft` passed as an `ILocalizable`, its last propagation.
    - When that propagation already propagated the covariance (`CovariancePropagationMode.Covariance`, for example a
      `Scenario` or a `Spacecraft.Propagate` with `CovariancePropagationOptions`), P is read from it at TCA.
    - Otherwise the variational equations are **replayed along the steps of that propagation**, from its start to the
      last TCA of the analysis, without a second propagation: with the step controlled on the state only, the replay
      gives the values of a propagation run with the covariance from the start, bit for bit (`CovarianceReplayTests`).
      Your solution is not changed.
    - P = Φ P0 Φᵀ + Ψc Pp Ψcᵀ + Q at TCA, with the options of `ConjunctionAnalysisOptions.CovariancePropagation`
      (see [Covariance Propagation](covariance-propagation.md)). Its age is zero, it is never stale, and the state at TCA
      retained by the `EncounterCase` carries it.
2. **Provided at the encounter** (`ProvidedAtEncounter`): the state of the participant at TCA carries a covariance,
   for a hand-built `ILocalizable` whose ephemeris gives one (age zero).
3. **Initial state held in RTN** (`InitialStateHeldInRtn`), the fallback for a participant that the library does not
   propagate in RK7(8) (Velocity-Verlet, not propagated and so two-body, TLE, SPICE), for an initial covariance at another
   epoch than the start of the propagation, and for every participant when `CovariancePropagation` is null. The
   covariance of the initial `StateVector` is rotated into RTN with the initial state, then back into the inertial frame
   with the state at TCA. It is **not propagated**: its shape relative to the orbit is kept, its size does not grow. Its
   age is reported, and `EncounterQualityFlags.StaleCovarianceUsed` is raised beyond
   `ConjunctionAnalysisOptions.StaleCovarianceThreshold` (60 s by default).
4. **None**: `MissingProtectedCovariance` or `MissingSecondaryCovariance`, and `CovarianceUnavailable` when both are
   missing.

The source is reported for each participant in `EncounterState.ProtectedCovariance` and `SecondaryCovariance`
(`ParticipantCovariance`), with `ProcessNoiseApplied`, false for a propagated covariance without process noise: a purely
linear covariance, often optimistic. `Screen` never propagates the covariance: it ranks candidates with the covariance
held in RTN.

!!! note "Change of results in 10.2.0"
    The covariance propagation is on by default (`CovariancePropagationMode.Covariance`, no process noise, no consider
    parameter): the Pc of `Analyze` and `AnalyzeAll` changes for a participant propagated in RK7(8) with an initial
    covariance, and becomes the Pc of the propagated covariance. Set
    `ConjunctionAnalysisOptions.CovariancePropagation = null` to restore the covariance held in RTN. On the two-body
    conjunction of the tests, 24 h after the initial states (σ 50 to 300 m and 0.03 to 0.3 m/s), the in-track σ at TCA
    is 21.7 km propagated against 232 m held in RTN, and the Pc 2.0e-6 against 1.9e-4.

### Non-linearity index

The covariance is propagated linearly. For each propagated covariance, the analysis reports a non-linearity index at TCA,
`ParticipantCovariance.NonlinearityIndex`, the index of Junkins and Singla (J. L. Junkins, P. Singla, *How nonlinear is
it? A tutorial on nonlinearity of orbit and attitude dynamics*, J. Astronaut. Sci. 52, 2004; citation to be verified):

ν(t) = maxᵢ ‖Φ(t, t0; x̄0 + δxᵢ) − Φ(t, t0; x̄0)‖_F / ‖Φ(t, t0; x̄0)‖_F,

the δxᵢ being the twelve points ±√λₖ eₖ on the principal axes of P0, its 1σ boundary. It is zero for a linear flow
and grows as the tangent map changes across the initial uncertainty. Φ at the twelve points comes from twelve
propagations of the state transition matrix from the perturbed initial states to the last TCA, with the forces and
settings of the participant's propagation: the dominant cost of the guard, which `ComputeNonlinearityIndex = false`
turns off. It is not computed for a propagation with maneuvers (null), nor for the avoidance options.
`EncounterQualityFlags.NonlinearCovariancePropagation` is raised above `NonlinearityIndexThreshold`.

**Cost** (`ConjunctionCovarianceCostTests`, Release, Intel Xeon 2.1 GHz, 4 cores, 2026-10-17, informative). Two
participants of R2 (LEO 400 km, EGM2008 10×10, Sun, Moon, drag), each propagated over one day in RK7(8) at 1e-11 in
0.67 s; `Analyze` over a 2 h window 23 h after the start: 0.55 s with the covariance held in RTN, 1.5 s with the replay
of both participants, 10.1 s with the non-linearity index (24 STM propagations of 23 h). `EvaluateAvoidance` with the
default magnitudes and directions: 36 options in 21.7 s, about 0.6 s per option propagated with the covariance from its
burn to the end of the window (12 h). Turn the index off (`ComputeNonlinearityIndex = false`) when the cost matters.

**Measurement of the threshold** (`FosterMonteCarloValidationTests`, `LongValidation`, 2026-10-17). Two-body
encounters, the secondary crossing at 70° and passing 151 m from the protected object at TCA, both initial covariances
isotropic in RTN (σ 100 m and 0.1 m/s, then scaled), combined hard-body radius 500 m. Foster with the covariance
propagated by RK7(8), against a Monte Carlo of N pairs of initial states, each propagated exactly in the two-body problem
to its own closest approach; agreement when Foster lies in the 99 % Clopper–Pearson interval of the Monte Carlo
(C. J. Clopper, E. S. Pearson, *Biometrika* 26, 1934; to be verified).

| TCA after the initial states | P0 | ν | Foster | Monte Carlo (hits, N) | 99 % interval | |
|---|---|---|---|---|---|---|
| 6 h | × 1 | 9.5e-4 | 3.359e-2 | 3.357e-2 (335 708, 1e7) | [3.342e-2, 3.372e-2] | agrees |
| 24 h | × 1 | 3.6e-3 | 8.624e-3 | 8.605e-3 (86 051, 1e7) | [8.530e-3, 8.681e-3] | agrees |
| 72 h | × 1 | 1.1e-2 | 2.908e-3 | 2.920e-3 (29 200, 1e7) | [2.876e-3, 2.964e-3] | agrees |
| 6 h | × 10 | 8.2e-3 | 1.345e-3 | 1.331e-3 (13 313, 1e7) | [1.302e-3, 1.361e-3] | agrees |
| 24 h | × 10 | 3.8e-2 | 3.010e-4 | 2.989e-4 (2 989, 1e7) | [2.850e-4, 3.133e-4] | agrees |
| 72 h | × 10 | 0.12 | 1.380e-4 | 1.432e-4 (1 432, 1e7) | [1.336e-4, 1.532e-4] | agrees |
| 7 days | × 10 | 0.23 | 4.143e-5 | 4.077e-5 (4 077, 1e8) | [3.914e-5, 4.244e-5] | agrees |
| 72 h | × 30 | 0.33 | 1.664e-5 | 1.740e-5 (1 740, 1e8) | [1.634e-5, 1.850e-5] | agrees |
| 7 days | × 30 | 0.75 | 4.779e-6 | 4.680e-6 (468, 1e8) | [4.142e-6, 5.267e-6] | agrees |
| 72 h | × 100 | 1.04 | 1.513e-6 | 1.310e-6 (131, 1e8) | [1.034e-6, 1.635e-6] | agrees |
| 7 days | × 100 | 1.94 | 4.319e-7 | 4.000e-7 (40, 1e8) | [2.559e-7, 5.936e-7] | agrees |

Foster agrees with the Monte Carlo in every case, up to ν = 1.94, although the in-track spread reaches 28 000 km and
the distribution of the positions at TCA is far from Gaussian: on these encounters, the probability depends on the
density near the encounter point, which the linear covariance keeps, not on the curvature of the distant spread. The
measurement does not find where Foster departs. The default threshold, 0.75, is the largest index at which the
agreement is established with a few hundred hits (the interval within ±13 %); beyond it the Monte Carlo resolves the
probability only to ±50 % or worse. `NonlinearCovariancePropagation` therefore marks a covariance beyond the measured
domain, not a measured failure. The measurement covers two-body crossing encounters only: low relative velocities
(`LowRelativeVelocityEncounter`), drag and the other forces are outside it.

## ConjunctionAssessment Methods

| Method | Description |
|--------|-------------|
| `Screen(profile, secondaries, window, options)` | Rank many candidates against one protected spacecraft |
| `Analyze(profile, secondary, window)` | Return the single best conjunction in a window |
| `AnalyzeAll(profile, secondary, window)` | Return every detected conjunction in a window |
| `EvaluateAvoidance(encounter, options)` | Generate impulsive avoidance options for an encounter |

Each method has two families of overloads:

- **`ILocalizable`** overloads for generic participants. States are sampled from each participant's ephemeris: SPICE data for kernel-backed objects, the stored states of a spacecraft that was already propagated, otherwise the initial orbit (Keplerian, or SGP4 for a TLE).
- **`PropagationSolution`** overloads when both trajectories were already propagated, reusing dense output. When both trajectories share the observer and the frame, the close approaches are searched on the cubic polynomials of their accepted steps, then each TCA is refined on the range rate of the states of the trajectories (`PropagationSolution.InterpolateAt`), at the accuracy of the integrator for RK7(8) trajectories.
    - The local minima are selected on the polynomials, which costs no force evaluation. Only the refinement uses the states: inside a step of an RK7(8) trajectory, each state is one RK7(8) step, 13 force evaluations.
    - The refinement starts from two secants of the range rate near the minimum, each checked by a bracket, then bisects down to one tick (100 ns), the resolution of the epochs; it ends on the same epoch as a bisection between the neighbouring candidates, with about 7 states per trajectory and per minimum instead of about 33. When the secants cannot be checked, it bisects between the neighbouring candidates, moving the bracket at most twice to the next candidate when the minimum on the states lies past one of them.
    - Cost on two RK7(8) trajectories: for two 400 km LEO trajectories over one day with EGM2008 10×10, the Moon and the Sun at the default tolerance (32 minima), `Analyze` takes 15 to 20 ms, against 8 to 13 ms with the cubic Hermite states of 10.1 and 190 to 200 ms with the first version of the refinement (Release, median of 21 calls after a warm-up, Intel Xeon 2.1 GHz, 4 cores, 2026-10-11).

## ProtectedSpacecraftProfile

Packages the protected `Spacecraft` with maneuver constraints used by avoidance searches.

| Property | Description |
|----------|-------------|
| `Spacecraft` | The protected spacecraft instance |
| `ManeuverConstraints` | Delta-V and lead-time limits for avoidance searches |

## ScreeningOptions

| Property | Description |
|----------|-------------|
| `MaxMissDistanceMeters` | Filter threshold for miss distance |
| `MaxResults` | Maximum number of ranked results to return |

Results are ordered by descending collision probability, then by miss distance.

## EncounterCase

Top-level conjunction result packaging all assessment products.

| Property | Type | Description |
|----------|------|-------------|
| `ProtectedAsset` | `ProtectedSpacecraftProfile` | The protected spacecraft profile |
| `SecondaryObject` | `ILocalizable` | The secondary object |
| `ScreeningWindow` | `Window` | The searched window |
| `EncounterState` | `EncounterState` | Geometry and covariance at TCA |
| `CollisionRisk` | `CollisionRisk` | Collision probability products |
| `ProtectedState` / `SecondaryState` | `StateVector` | Participant states at TCA; with the covariance used when it was propagated |
| `ToCdm(CdmExportOptions)` | `Cdm` | Export to CCSDS CDM |

## EncounterState

| Property | Type | Description |
|----------|------|-------------|
| `Epoch` | `Time` | Time of closest approach |
| `MissDistanceMeters` | `double` | Miss distance (m) |
| `RelativeState` | `RelativeState` | Relative position and velocity |
| `CombinedCovarianceRtn` | `Matrix` | Combined 6x6 covariance in the protected object's RTN frame |
| `QualityFlags` | `EncounterQualityFlags` | Covariance and applicability warnings |
| `ProtectedCovarianceAge` / `SecondaryCovarianceAge` | `TimeSpan?` | Time between the epoch of the covariance used and TCA: zero when it was available at, or propagated to, TCA, null when there is none |
| `ProtectedCovariance` / `SecondaryCovariance` | `ParticipantCovariance` | Source of the covariance (`CovarianceSource`), age, `ProcessNoiseApplied`, `NonlinearityIndex` |

The constructor overload with the two `ParticipantCovariance` takes the quality flags last; the overloads with the ages
infer the source from the age (null: `None`, zero: `ProvidedAtEncounter`, otherwise `InitialStateHeldInRtn`).

## ParticipantCovariance

| Property | Type | Description |
|----------|------|-------------|
| `Source` | `CovarianceSource` | `None`, `ProvidedAtEncounter`, `InitialStateHeldInRtn` or `Propagated` |
| `Age` | `TimeSpan?` | As `ProtectedCovarianceAge` |
| `ProcessNoiseApplied` | `bool` | For a propagated covariance, whether a process noise was added |
| `NonlinearityIndex` | `double?` | For a propagated covariance, ν at TCA (dimensionless); null otherwise or when not computed |

## RelativeState

| Property | Description |
|----------|-------------|
| `RelativePositionInertial` | Relative position in ICRF (m) |
| `RelativeVelocityInertial` | Relative velocity in ICRF (m/s) |
| `RelativePositionRtn` | Relative position in protected object's RTN frame (m) |
| `RelativeVelocityRtn` | Relative velocity in RTN frame (m/s) |

## CollisionRisk

| Property | Description |
|----------|-------------|
| `CombinedHardBodyRadiusMeters` | Sum of hard-body radii (m) |
| `ProbabilityOfCollision` | Pc, null when no covariance is available |
| `ProjectedCovariance` | 2D encounter-plane covariance |
| `RadialSigmaMeters` / `InTrackSigmaMeters` / `CrossTrackSigmaMeters` | RTN sigmas (m) |

## ConjunctionAnalysisOptions

| Property | Default | Description |
|----------|---------|-------------|
| `SampleStep` | 1 min | Coarse search cadence |
| `MaximumEventSearchStep` | 60 s | Largest interval subdivided while refining the TCA |
| `SecondaryHardBodyRadiusMeters` | 5 m | Secondary hard-body radius when the object does not expose one |
| `BisectionToleranceSeconds` | 1e-9 s | TCA refinement tolerance; the epochs have a resolution of 100 ns, which bounds the refinement below this value |
| `LowRelativeSpeedThresholdMetersPerSecond` | 1 m/s | Below this relative speed, `LowRelativeVelocityEncounter` is raised |
| `StaleCovarianceThreshold` | 60 s | Covariance age beyond which `StaleCovarianceUsed` is raised (covariance held in RTN) |
| `CovariancePropagation` | `Covariance` mode, no noise, no consider parameter | Propagation of the covariance of the participants to TCA; null for the covariance held in RTN. `StateTransitionOnly` and `IncludeInStepControl` are rejected (`ArgumentException`), as is a consider parameter whose force a propagated participant lacks |
| `ComputeNonlinearityIndex` | true | Whether the non-linearity index of each propagated covariance is computed |
| `NonlinearityIndexThreshold` | 0.75 | Index above which `NonlinearCovariancePropagation` is raised |

## Practical Limits

- Collision probability uses a 2D Foster-style encounter-plane method.
- `EncounterQualityFlags.LowRelativeVelocityEncounter` is an applicability warning that the 2D assumption may be degraded.
- The covariance is propagated to TCA for the participants the library propagates in RK7(8); the others keep their
  initial covariance held in RTN (see [Covariance at TCA](#covariance-at-tca)).
- The propagated covariance is linear: see the [non-linearity index](#non-linearity-index).
- The replay evaluates the forces and their partials of the participant's propagation: like `PropagationSolution`
  evaluations, an analysis must not run concurrently with another propagation or evaluation sharing a `CelestialBody`
  with a geopotential (see [Thread Safety](../guides/thread-safety.md)).
- With both covariances, Pc is the Foster 2D probability of the combined covariance.
- With only one covariance, Pc is the maximum probability over the unknown covariance (Frisbee's
  method), an upper bound, and `SingleCovarianceMaximumPcUsed` is raised.
- With no covariance, no Pc is computed (`ProbabilityOfCollision` is null) and
  `CovarianceUnavailable` is raised.

## Example

```csharp
var encounter = ConjunctionAssessment.Analyze(
    new ProtectedSpacecraftProfile(protectedSpacecraft),
    secondarySpacecraft,
    new Window(epoch, epoch.AddMinutes(10.0)));

Console.WriteLine($"Miss distance: {encounter.EncounterState.MissDistanceMeters:F1} m");
Console.WriteLine($"Pc: {encounter.CollisionRisk.ProbabilityOfCollision:E3}");
```

## See Also

- [Covariance Propagation](covariance-propagation.md)
- [Avoidance Studies](avoidance.md)
- [CDM](cdm.md)
- [Propagators](propagators.md)
