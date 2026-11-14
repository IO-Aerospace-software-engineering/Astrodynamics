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
      Your solution is not changed. A propagation with the state transition matrix only
      (`CovariancePropagationMode.StateTransitionOnly`) is replayed in the same way: its Φ are not read, so the
      covariance at TCA takes the options of the analysis, the process noise and the consider parameters included.
    - P = Φ P0 Φᵀ + Ψc Pp Ψcᵀ + Q at TCA, with the options of `ConjunctionAnalysisOptions.CovariancePropagation`
      (see [Covariance Propagation](covariance-propagation.md)). Its age is zero, it is never stale, and the state at TCA
      retained by the `EncounterCase` carries it.
    - An initial covariance that rounding made indefinite (symmetric, finite, non-negative variances, but not positive
      semi-definite, as from the rounded values of a CDM) is **remediated** before its propagation, by eigenvalue
      clipping (D. T. Hall, M. D. Hejduk, L. C. Johnson, *Remediating non-positive definite state covariances for
      collision probability estimation*, AAS 17-567, 2017; confirmed by S. Guillet, 2026-10-18): the negative eigenvalues of its
      correlation matrix are set to zero, so that the result does not depend on the units, and
      `EncounterQualityFlags.CovarianceRemediated` is raised (decision of S. Guillet, 2026-10-18). A propagation by
      `CentralBodyPropagator` still rejects such a covariance: `StateVector.WithRemediatedCovariance()` applies the same
      remediation beforehand (see [Covariance Propagation](covariance-propagation.md#an-initial-covariance-indefinite-from-rounding));
      the analysis then gives the same covariance bit for bit, without the flag.
2. **Provided at the encounter** (`ProvidedAtEncounter`): the state of the participant at TCA carries a covariance,
   for a hand-built `ILocalizable` whose ephemeris gives one (age zero).
3. **Initial state held in RTN** (`InitialStateHeldInRtn`), the fallback for a participant that the library does not
   propagate in RK7(8) (Velocity-Verlet, not propagated and so two-body, TLE, SPICE), for an initial covariance at another
   epoch than the start of the propagation, for one that rounding does not explain (a negative variance, a value that is
   not finite: the analysis does not throw, it holds that covariance in RTN as before 10.2.0-preview-1), and for every
   participant when `CovariancePropagation` is null. The
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

!!! note "Change of results in 10.2.0-preview-1"
    The covariance propagation is on by default (`CovariancePropagationMode.Covariance`, no process noise, no consider
    parameter): the Pc of `Analyze` and `AnalyzeAll` changes for a participant propagated in RK7(8) with an initial
    covariance, and becomes the Pc of the propagated covariance. Set
    `ConjunctionAnalysisOptions.CovariancePropagation = null` to restore the covariance held in RTN. On the two-body
    conjunction of the tests, 24 h after the initial states (σ 50 to 300 m and 0.03 to 0.3 m/s), the in-track σ at TCA
    is 21.7 km propagated against 232 m held in RTN, and the Pc 2.0e-6 against 1.9e-4.

### Non-linearity index

The covariance is propagated linearly. For each propagated covariance, the analysis reports a non-linearity index at TCA,
`ParticipantCovariance.NonlinearityIndex`, the index of Junkins and Singla (J. L. Junkins, P. Singla, *How nonlinear is
it? A tutorial on nonlinearity of orbit and attitude dynamics*, J. Astronaut. Sci. 52, 2004; confirmed by S. Guillet, 2026-10-18):

ν(t) = sup over the 1σ boundary of P0 of ‖Φ(t, t0; x̄0 + δx) − Φ(t, t0; x̄0)‖_F / ‖Φ(t, t0; x̄0)‖_F.

It is zero for a linear flow and grows as the tangent map changes across the initial uncertainty. The supremum is taken
over the whole boundary, δx = P0^(1/2) u with ‖u‖ = 1, on the first-order variation of Φ: with the columns sₖ of the
symmetric square root P0^(1/2), M = [(Φ(x̄0 + sₖ) − Φ(x̄0 − sₖ)) / 2]ₖ (36 × 6) and ν = σ_max(M) / ‖Φ(x̄0)‖_F, the
largest singular value of M. It does not depend on the basis: P0^(1/2) is unique even when P0 has repeated eigenvalues,
and any other orthonormal basis of the boundary changes M into M Q, of the same singular values (H3 review of step 10,
decision of S. Guillet, 2026-10-18: the twelve points ±√λₖ eₖ on the principal axes, used before, depended on the axes
the eigensolver returned within a repeated eigenspace, by up to ±6 % on the case that set the threshold). The
second-order part of the variation cancels in the central differences. Φ at the twelve points ±sₖ comes from twelve
propagations of the state transition matrix from the perturbed initial states to the last TCA, with the forces and
settings of the participant's propagation: its cost is that of the guard. It is **off by default**: set
`ComputeNonlinearityIndex = true` to compute it (decision of S. Guillet, 2026-10-18, after the measurement below, where
Foster agreed with the Monte Carlo far beyond the threshold). It is not computed (null) for a propagation of more than one
segment (with maneuvers, or with an event that restarts the integration), nor for the avoidance options. `EncounterQualityFlags.NonlinearCovariancePropagation` is raised above
`NonlinearityIndexThreshold`.

What the operator can do with it, and its limits:

- read ν for each participant at TCA: near 0 the propagation is close to linear; the larger it is, the further the
  covariance has left what the linearization assumes;
- the flag means *beyond the validated domain*, not *wrong Pc*: Foster stayed right up to ν = 1.42 in the measurement;
- that domain is two-body crossing encounters at high relative velocity. Slow encounters, drag and the other forces are
  not covered, and the index does not necessarily detect where Foster would be wrong there; confirm a Pc that matters by a
  Monte Carlo of your own ([#403](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/403) is the
  measurement of those encounters).

**Cost** (`ConjunctionCovarianceCostTests`, Release, Intel Xeon 2.1 GHz, 4 cores, 2026-10-17, informative). Two
participants on a 700 km sun-synchronous orbit in the force model of R2 (EGM2008 10×10, Sun, Moon, NRLMSISE-00 drag),
crossing 23 h after the start, each propagated over one day in RK7(8) at 1e-11 in
0.67 s; `Analyze` over a 2 h window 23 h after the start: 0.55 s with the covariance held in RTN, 1.5 s with the replay
of both participants, 10.1 s with the non-linearity index (24 STM propagations of 23 h). `EvaluateAvoidance` with the
default magnitudes and directions, on the TCA found in a 12 h screening window, 12.3 h after the start (lead times up to
0.32 h, the shortest below the minimum lead time skipped): 36 options in 21.7 s, about 0.6 s per option propagated with
the covariance from its burn to the end of the window (about 12 h). The index is off by default for that cost. These
figures were measured before the changes of the force models of #420, #437 and #410; measured again on 2026-10-24 with
BenchmarkDotNet (`ConjunctionCovarianceBenchmarks`, the same pair and window, library at `deb26b9`), `Analyze` takes
0.83 ± 0.08 s with the covariance held in RTN and 1.71 ± 0.08 s with the replay, 2.1 times
([Cost](covariance-propagation.md#cost)).

**Measurement of the threshold** (`FosterMonteCarloValidationTests`, `LongValidation`, 2026-10-17; ν measured again
with the definition independent of the basis on 2026-10-18, Foster and the Monte Carlo unchanged). Two-body
encounters, the secondary crossing at 70° and passing 151 m from the protected object at TCA, both initial covariances
isotropic in RTN (σ 100 m and 0.1 m/s, then scaled), combined hard-body radius 500 m. Foster with the covariance
propagated by RK7(8), against a Monte Carlo of N pairs of initial states, each propagated exactly in the two-body problem
to its own closest approach; agreement when Foster lies in the 99 % Clopper–Pearson interval of the Monte Carlo
(C. J. Clopper, E. S. Pearson, *Biometrika* 26, 1934; confirmed by S. Guillet, 2026-10-18).

| TCA after the initial states | σ scale (P0 × scale²) | ν | Foster | Monte Carlo (hits, N) | 99 % interval | |
|---|---|---|---|---|---|---|
| 6 h | × 1 | 1.3e-3 | 3.359e-2 | 3.357e-2 (335 708, 1e7) | [3.342e-2, 3.372e-2] | agrees |
| 24 h | × 1 | 5.3e-3 | 8.624e-3 | 8.605e-3 (86 051, 1e7) | [8.530e-3, 8.681e-3] | agrees |
| 72 h | × 1 | 1.6e-2 | 2.908e-3 | 2.920e-3 (29 200, 1e7) | [2.876e-3, 2.964e-3] | agrees |
| 6 h | × 10 | 1.3e-2 | 1.345e-3 | 1.331e-3 (13 313, 1e7) | [1.302e-3, 1.361e-3] | agrees |
| 24 h | × 10 | 5.3e-2 | 3.010e-4 | 2.989e-4 (2 989, 1e7) | [2.850e-4, 3.133e-4] | agrees |
| 72 h | × 10 | 0.16 | 1.380e-4 | 1.432e-4 (1 432, 1e7) | [1.336e-4, 1.532e-4] | agrees |
| 7 days | × 10 | 0.37 | 4.143e-5 | 4.077e-5 (4 077, 1e8) | [3.914e-5, 4.244e-5] | agrees |
| 72 h | × 30 | 0.47 | 1.664e-5 | 1.740e-5 (1 740, 1e8) | [1.634e-5, 1.850e-5] | agrees |
| 7 days | × 30 | 1.03 | 4.779e-6 | 4.680e-6 (468, 1e8) | [4.142e-6, 5.267e-6] | agrees |
| 72 h | × 100 | 1.37 | 1.513e-6 | 1.310e-6 (131, 1e8) | [1.034e-6, 1.635e-6] | agrees |
| 7 days | × 100 | 1.42 | 4.319e-7 | 4.000e-7 (40, 1e8) | [2.559e-7, 5.936e-7] | agrees |

Foster agrees with the Monte Carlo in every case, up to ν = 1.42, although the in-track spread reaches 28 000 km and
the distribution of the positions at TCA is far from Gaussian: on these encounters, the probability depends on the
density near the encounter point, which the linear covariance keeps, not on the curvature of the distant spread. The
measurement does not find where Foster departs. The default threshold, 1.0 (0.75 with the former points), is the largest index, rounded down, at which the
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

Each method has two families of overloads. Both refuse the protected spacecraft as its own secondary: a secondary
with the NAIF identifier of the protected spacecraft throws `ArgumentException` naming `secondaryObject`, the rule by
which `Screen` skips it ([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683): a miss
of 0 and a Pc of 0.815 before, or an empty list on two propagations).

The **hard-body radius of the secondary** is resolved before any search, from the secondary and the option of the
call (`ConjunctionAnalysisOptions.SecondaryHardBodyRadiusMeters`, `AvoidanceSearchOptions.SecondaryHardBodyRadiusMeters`,
each naming one secondary):

| Secondary | Option | Radius of the secondary |
|-----------|--------|-------------------------|
| a `Spacecraft` with `HardBodyRadius` > 0 | null | its `HardBodyRadius` |
| a `Spacecraft` with `HardBodyRadius` > 0 | the same value | that value |
| a `Spacecraft` with `HardBodyRadius` > 0 | another value | `ArgumentException` naming the secondary and the option (`secondaryObject`, `options` for `EvaluateAvoidance`) |
| a radius of 0, or not a `Spacecraft` | a value | that value |
| a radius of 0, or not a `Spacecraft` | null | 5 m |

`ScreeningOptions.DefaultSecondaryHardBodyRadiusMeters` is a fallback over the catalogue of `Screen` (decided by
S. Guillet, 2026-11-14, on [#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683)): a
candidate `Spacecraft` with `HardBodyRadius` > 0 keeps its own radius whatever the option, and a candidate that gives
none (a radius of 0, or not a `Spacecraft`) takes the option, or 5 m when it is null; no candidate throws for its radius,
so a catalogue of spacecraft with different radii is screened with one fallback. The analysis of each candidate carries
its resolved radius as its `SecondaryHardBodyRadiusMeters`, and `EvaluateAvoidance` re-analyzes a screened encounter
with it: with a null `AvoidanceSearchOptions.SecondaryHardBodyRadiusMeters` the table above applies to the radius the
screening resolved, which never conflicts (a candidate of 0 m or a body screened with a fallback of 3 m is re-analyzed
with 3 m, not the 5 m of the null row). A radius of 0 is the default of both `Spacecraft` constructors, so it cannot be
told from a radius left unset and gives none; a fallback of 0 m keeps every such candidate at 0 m.

An option that is NaN, infinite or negative throws `ArgumentOutOfRangeException` naming it. Before 11.0.0 the option,
5 m by default, was used whatever the radius of the secondary
([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683): a secondary of 3 m beside a
protected spacecraft of 10 m gave a combined radius of 15 m and a Pc 33 % high, 3.6145e-4 for 2.7147e-4).

- **`ILocalizable`** overloads for generic participants. States are sampled from each participant's ephemeris: SPICE data for kernel-backed objects, the stored states of a spacecraft that was already propagated, otherwise the initial orbit (Keplerian, or SGP4 for a TLE).
    - The observer of the analysis is the centre of the protected spacecraft's initial orbital parameters, so its RTN axes are those of its orbit about that centre. A kernel-backed spacecraft reads them about the Earth unless its constructor's `centerOfMotion` says otherwise ([#620](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/620); the Solar System barycentre before 11.0.0).
    - A window that leaves the stored states of a propagated spacecraft, protected or secondary, throws `ArgumentException` rather than extrapolating them ([#527](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/527)); a read that the loaded kernels do not cover throws `InvalidOperationException` with SPICE's message ([#454](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/454)).
    - Without a range-rate zero crossing in the window, `Analyze` and `AnalyzeAll` return the window midpoint; [#621](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/621) decides that case.
    - These overloads always run the sampled search, even for two participants propagated in RK7(8): the search on the polynomials of their steps is not inferred in 11.0.0. Pass their `PropagationSolution` for it (D4 of the API review of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668)).
- **`PropagationSolution`** overloads when both trajectories were already propagated, reusing dense output. Each trajectory must be that of its participant: a solution propagated for another spacecraft (compared by reference, the instance that was propagated, so another instance with the same NAIF identifier too) throws `ArgumentException` naming `protectedTrajectory` or `secondaryTrajectory`; a solution built by hand (`new PropagationSolution()`) states no spacecraft and is accepted ([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683): another spacecraft's trajectory gave a miss of 0 and a Pc of 0.676, a swapped pair the covariances of the other objects). The TCA is labelled in the time frame of the window start, as on the sampled path, never a window end for an interior minimum: with a `BisectionToleranceSeconds` below a tick (the default), the tick of that frame nearer the zero of the range rate; with a tolerance of a tick or more, the middle of the last bracket (see the options below) converted to that frame, or the neighbouring tick toward the zero where the range rate is smaller in magnitude, one tick at most from that middle; a TDB window is unchanged bit for bit (#683: the TDB of the step that opened its bracket, in 455 of 540 UTC windows measured, the frame of the window in the other 85, the same instant to a tick). When both trajectories share the observer and the frame, the close approaches are searched on the cubic polynomials of their accepted steps, then each TCA is refined on the range rate of the states of the trajectories (`PropagationSolution.InterpolateAt`), at the accuracy of the integrator for RK7(8) trajectories.
    - The local minima are selected on the polynomials, which costs no force evaluation. Only the refinement uses the states: inside a step of an RK7(8) trajectory, each state is one RK7(8) step, 13 force evaluations.
    - The refinement starts from two secants of the range rate near the minimum, each checked by a bracket, then bisects down to one tick (100 ns), the resolution of the epochs, and returns the tick of that last bracket nearer the zero: the one where the range rate is smaller in magnitude, the earlier one on a tie, so within half a tick of the zero of a range rate linear over the tick, and a zero exactly on a tick is that tick ([#624](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/624); before 11.0.0 the earlier tick, up to one tick before the zero, a zero exactly on a tick one tick early). The sampled search of the `ILocalizable` overloads bisects each change of sign of its samples the same way, from the range rates it already holds. The refinement ends on the same epoch as a bisection between the neighbouring candidates, with about 7 states per trajectory and per minimum instead of about 33. When the secants cannot be checked, the range rate at the seed says on which side its zero lies (positive: before it; negative: after it; zero: the seed itself); it is read on that side 1 ms from the seed, then at distances doubling up to 1.024 s and growing by 1 s after that, up to the neighbouring candidate, until it changes sign, and that bracket is bisected. This reaches a zero the Hermite has left behind its seed while the candidates on both sides have the sign of the seed (two minima of the distance per orbit with a maximum between them, at the default steps of `new RK78Integrator()`: a seed 1.6 s past the minimum, its range rate and those of its neighbours positive). The reads are 1 s apart at most, the resolution of this search: an interval of the other sign at least 1 s wide holds a read, however far from the seed, and a narrower one may be stepped over. Distances doubling all the way stepped over a shallow minimum next to a fold of the range rate (2.9e-7 m deep, the range rate positive over 1.6 s only, read negative 4.096 and 8.192 s after its seed). They cost at most 11 + ⌈D − 1.024⌉ states per trajectory for a neighbouring candidate D seconds away (110 at 100 s, against 18 with distances doubling), for a seed the secants cannot bracket only. Their reach is the neighbouring candidate, with `ReadRangeRateOverWindow` whatever `MaximumEventSearchStep` ([#628](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/628), round 1 of its review): a reach capped at 60 s read 29 to 33 % fewer states at fixed 600 s steps for one trajectory and the default steps for the other, but lost minima these reads find: a positive interval of the range rate 2.3 to 43 s wide, 73 to 87 s past its seed, between two reads over the window up to 60 s apart (the pass with two minima per orbit at fixed 600 s steps, y0 = 1501 to 1502.33 m, the minimum 1.1 µm to 1.5 mm deep), lost in 21 to 135 of 843 windows where the search up to the neighbouring candidate lists it. When the range rate keeps its sign up to the neighbouring candidate, it bisects between the neighbouring candidates, moving the bracket up to twice to the next candidate when the minimum on the states lies past one of them. Past that, it follows the range rate over the candidates, on the side where the states place the minimum (on both sides when the seed sits on a maximum of the distance on the states), to the nearest pair of candidates where it goes from negative to zero or positive, and bisects there; a side that reaches a window end without that change holds no minimum. A seed that none of these brackets is no close approach: every TCA of this path is a zero of the range rate going from negative to positive, and a maximum of the distance seeded by the error of the Hermite on a slow pass (50 m along-track, a drift of 100 m across the plane, relative speeds below 0.12 m/s) is not listed. The zeros the seeds reach give one TCA per pair of consecutive candidates: a zero in the pair of an earlier one is the same minimum, reached from another seed where the range rate at the level of its noise changes sign more than once (a drift of 1 m: zeros spread over about 1e-4 s). With `ReadRangeRateOverWindow`, the reads over the window below add theirs one per pair of reads, so a pair of candidates may then hold two TCAs (two minima within one long step).
    - By default the seeds alone are refined, as before #628, bit for bit ([#628](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/628), decision 54 of [#457](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/457), S. Guillet, 2026-11-11): on the 149 scenarios of the probe of #628 and of its review, 141 837 windows, every list of `AnalyzeAll` and every result of `Analyze` is that of main before #628 (b237eec2), and so are the SPKs. Every TCA listed is a minimum of the states: a zero of the range rate from negative to positive, inside the window, in order, none twice (measured against CSPICE's `gfdist_c` on the windows of the passes below). A minimum that no minimum of the cubic Hermite seeds may be lost, in two forms: two minima within one long step with a maximum between them, the Hermite one minimum only; or a minimum between two candidates where the range rate closes at one and recedes at the next, with no seed near, at long (300 s and more) or mixed steps. They were measured on slow co-orbital passes only, two minima per orbit at relative speeds below 1 m/s, the two minima centimetres apart in distance: lost in 10 to 341 of 843 to 1084 windows per pass, against a scan of the range rate of the states (the figures below). No loss was measured on fast passes, and the screening and the avoidance options use the sampled search, which this does not concern. `Analyze` then returns another minimum or a window end. Over the 141 837 windows, in the 5314 where it differs from the result with `ReadRangeRateOverWindow` by more than 1 µm: another minimum in 4039, farther by 1.7 mm to 9.6 cm (median 3.5 cm; 504.8366 m for the missed minimum's 504.8015 m on the window [97.86, 1507.86] s of the pass at y0 = 1495 m, 60 s and default steps); a window end in 1275, farther in 845 by 1.5 mm to 1.46 m (median 4.5 cm; 504.6578 m for 504.6428 m on the window [6969.70, 7089.70] s at y0 = 1500 m, default and 60 s steps; 1.46 m at fixed 600 s steps for one trajectory and the default for the other, y0 = 1490 m), and closer in 430, by up to 1.36 m (an end is no close approach, and the option returns the minimum inside, farther than that end). Ask for `ReadRangeRateOverWindow` on slow co-orbital formations propagated at long or mixed steps, where such a minimum matters; on a fast pass it costs about 6 times the search (cost below) for no measured gain.
    - With `ReadRangeRateOverWindow = true`, the range rate of the states is also read over the whole window, not only near the seeds ([#628](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/628)): at every candidate, and between two candidates more than `MaximumEventSearchStep` (60 s by default) apart at the fewest epochs evenly spaced that leave no gap wider by more than one tick, as the sampled search subdivides its intervals but for that tick: about the Earth a step of 60 TT seconds spans 60 s and one tick of its TDB labels when TDB − TT increases, and a strict comparison read it at its middle, one RK7(8) step more each time (32 reads in 3 h at 60 s steps from 2026-01-20, review round 3 of #628). Each change from negative to zero or positive between two consecutive reads that holds no TCA yet is bisected to the tick nearer its zero, one TCA per pair of reads. It finds the minima the Hermite does not seed: with mixed steps (the default for one trajectory and 60 s for the other), the range rate closing at one candidate and receding at the next with no seed near (lost by default, as before #628, against a scan of the range rate of the states, in 10 to 95 of 911 to 1084 windows of the passes with two minima per orbit at y0 = 1490, 1495, 1497, 1499 and 1500 m, `Analyze` then returning a window end, 504.6578 m against the minimum's 504.6428 m, or another minimum); and with long steps, one step holding a minimum, a maximum and a second minimum, its Hermite one minimum only (at fixed 600 s steps, the minima at t0 + 1317.45 s and 1526.28 s of the pass with two minima per orbit, the first lost by default; at fixed 300 s or 600 s steps, or 600 s for one trajectory and the default for the other, a minimum lost in 116 to 341 of 843 to 1084 windows). With the option, none is lost on these windows at the passes measured: y0 = 1480, 1490, 1495, 1497, 1499 and 1500 m at mixed default and 60 s steps; 1480, 1486, 1490, 1492, 1495 to 1499 and 1500 m at fixed 300 s steps; 1480, 1481, 1483, 1485, 1486, 1488, 1490, 1492, 1494, 1495, 1498, 1499, 1499.5 and 1500 m at fixed 600 s steps; 1480, 1486, 1490, 1495 to 1497, 1499 and 1500 m at 600 s for one trajectory and the default for the other. At fixed 600 s steps with y0 = 1496 and 1497 m some still are, the limit below. Each epoch is read once, the window ends included. The minima the seeds reach are those of the default, bit for bit: on the 81 scenarios of the probe of #628 and of its review (618 to 1292 windows each), every TCA listed by default is listed at the same epoch, but at the level of noise of the slow passes below; the reads add a minimum where the seeds reached none. The results with the option are those of 5c6ed81f, the commit before decision 54, bit for bit on the 149 scenarios.
    - Limit of the reads over the window: they separate two changes of sign of the range rate when the interval of each sign between them is wider than their spacing. An interval of either sign narrower than the spacing of the reads, a positive one between two closing reads or a negative one between a zero found from a seed and a later minimum in the same pair of reads (a pair that holds a zero is not bisected again), is found when the 1 s reads of a seed reach it (from a seed whose secants cannot be checked and whose range rate points toward it, up to its neighbouring candidate, the first change of sign there), and is otherwise not guaranteed to be found, however wide the interval of the other sign next to it. A seed whose two secants are checked reads nothing between itself and their zero: an interval of the other sign between them, however wide against 1 s, is read only by the reads over the window. The secants extrapolate from 1 ms about the seed and their bracket of one tick on each side is checked, so this takes a range rate linear to about 1e-10 per second over the tens of seconds to that zero, as a constructed one; an orbital range rate curves by about its mean motion, 1e-3 per second, and its secants are checked a fraction of a second from the seed only. The shallow minimum next to a fold of the range rate (2.9e-7 m deep, positive over about 1 s) is found when a seed lies before it, as at 60 s steps and at the default steps for both trajectories at y0 = 1502.358 m, and lost without one: in 203 of 843 windows at mixed default and 60 s steps (y0 = 1502.358 m and 1502.359 m), in 205 at the default steps (y0 = 1502.3591 m), and in 200 at 60 s steps when it is narrower than 1 s (0.787 s at y0 = 1502.35938 m). The same holds for two minima within one long step: the reads separate them when the interval of each sign between them is wider than `MaximumEventSearchStep`, and a narrower one is found only when the 1 s reads of a seed reach it. At fixed 600 s steps, y0 = 1501 to 1502.33 m, the range rate is positive over 2.3 to 43 s after the first minimum, which the seed reaches, and negative over about 83 s before the second, which the reads reach: by default the second is lost in 165 to 170 of 843 windows, with the option in none. At y0 = 1497 m the step from t0 + 1200 s to 1800 s holds the minima at 1373.31 s (502.851 m) and 1483.67 s (502.824 m), the range rate positive over 27.9 s between them, narrower than the reads 60 s apart, which both close around it; the step's only seed refines to the second, and the first is lost in 152 of 948 windows (341 by default), the whole window included (137 of them hold the second, which `Analyze` returns, the closer; in the 15 others, ending before it, `AnalyzeAll` lists nothing for the first); at 1496 m (positive over 46.1 s) in 1 of 948 (309 by default). A smaller `MaximumEventSearchStep` resolves the intervals wider than it: at 10 s, the first minimum at y0 = 1497 m is lost in none of the 948 windows; at 1 s, the fold of mixed default and 60 s steps, positive over 2.35 s, in none of the 843 windows. Only reads about 1 s apart over the whole window would resolve every fold, at about one state per second, which the search does not do by default.
    - At the level of the noise of the states, below about 1e-8 m/s of range rate (a slow pass whose distance is flat to 1e-6 m), the zeros of the states and of their SPK lie up to seconds apart and the range rate of the states changes sign more than once, in different pairs of reads: the minima listed follow that noise (two zeros 0.34 s apart for one minimum of CSPICE, an end of a window without a minimum; with `ReadRangeRateOverWindow`, a zero that the reads reach first in place of the one a seed reaches by default, at range rates of 1e-12 to 1e-13 m/s, in 26 windows ending within 0.35 s of the start of 618 to 1292 windows per scenario, by up to 1.2e-2 s). The rule is not changed for them: a rule merging the zeros by depth would also drop the shallow minimum next to the fold, 2.9e-7 m deep, on which the states and their SPK agree to 3e-5 s at 60 s steps.
    - A close approach is an interior local minimum of the distance ([#621](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/621)): `AnalyzeAll` lists the interior minima only, an empty list when the window holds none, and `Analyze` returns the closest of them when there is one. A window end, where the distance grows into the window, is never preferred to an interior minimum; without one, `Analyze` returns that end. Whether a minimum lies inside the window is decided on the range rate of the states, not on the cubic Hermite of the stored steps, whose minima only seed the search: they miss the zero of the range rate by the Hermite error ([#363](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/363), below 1 µs on a crossing at 60 s steps, 12 to 212 µs at the default steps of `new RK78Integrator()`, up to milliseconds). A window end is a seed too, bracketed when the range rate says the minimum lies inside (negative at the start, zero or positive at the end); a seed whose range-rate zero lies outside the window is dropped. A window end is never listed, not even a zero of the range rate exactly there, the distance decreasing to it, or growing from it at the start: `AnalyzeAll` lists nothing for it, and `Analyze` returns that end itself, never the tick before it. Without a seed inside, when the distances of the candidates all lie within the flatness tolerance of the seeds (a slow pass, a window of a few ticks about a minimum), a range rate negative at the start and positive at the end still holds a minimum inside, bisected over the window (with `ReadRangeRateOverWindow`, between the reads that bracket it, the same zero where the range rate changes sign once): `AnalyzeAll` lists it and `Analyze` returns it (before, nothing and the midpoint, 1 ms off on a pass at 0.113 m/s). A zero in the first tick of the window that the refinement rounds onto the start is the tick after the start, and a zero in the last tick that it rounds onto the end is the tick before the end, a window end being no close approach (the sampled search keeps the nearer tick, an end included); a window one tick wide holds no epoch inside, so `AnalyzeAll` lists nothing and `Analyze` returns the closer end. On this path `Analyze` never returns the midpoint. So a window ending just before a minimum lists nothing for it, and a window starting just before one lists it, however close to the end. Before 11.0.0 `AnalyzeAll` listed such an end, and `Analyze` returned it when it was closer than the interior minima. Measured against CSPICE's `gfdist_c` on the SPKs of two crossing LEO propagations, refined on the range rate of `spkezr_c`: the four minima of 3 h within 6.5e-8 s of the zero of `spkezr_c`'s range rate below the tick (1.4e-7 s before #624) and 1.1e-8 m at 60 s steps (9.0e-8 s and 2.74e-7 m at the default steps, the states inside a 234 s step against their SPK of 60 s outputs, whatever the window), and so on windows whose ends lie 1 µs to 1 ms before or after each minimum, and on windows of 2 and 3 ticks on each side of each minimum at 60 s steps (one tick on each side lies within the TCA accuracy by which the states and their SPK place the zero apart). On the slow pass, on the pass with two minima per orbit and on a pass with a shallow minimum next to a fold of the range rate, over 1187 to 1524 windows of each propagation (ends 1 to 1400 s on either side of each minimum, windows of 300 to 3000 s every 97 s, windows of 0.2 ms to 10 s about each minimum, and windows that end or start at the minimum, -10 to 10 ticks and 1 µs to 10 ms from it), `AnalyzeAll` lists the local minima of `gfdist_c`, each once, and no maximum, and `Analyze` returns the closest of them or the closer end of a window without one. The worst TCA and miss over every minimum, against the zero of `spkezr_c`'s range rate below the tick (the larger of this measure and of the review's second reader on its own windows): on the slow pass 1.5e-7 s and 3.14e-10 m at 60 s steps, 2.9e-5 s and 1.15e-6 m at the default steps (a range-rate slope of 2.6e-4 m/s per second), 5.05e-2 s and 2.1e-7 m with a drift of 1 m at the default and 30 s steps (a slope of 2.6e-6 m/s per second, the states and their SPK placing the zero apart by the difference of their range rates over it); on the pass with two minima, 6.5e-7 s to 3.3e-6 s and 4.8e-8 to 9.3e-8 m; at the fold (2.9e-7 m deep), 3.3e-5 s and 3.9e-8 m at 60 s steps and 2.1e-3 s and 9.7e-7 m at the default steps, where the states and their SPK place the zero 2.1 ms apart (the library lists it within 7.4e-6 s of the zero of the states, from a scan of their range rate). On the passes with two minima per orbit at mixed steps and at fixed 600 s steps ([#628](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/628); 1353 to 1727 windows of each propagation, and the probe of #628 on 843 to 1084 windows), with `ReadRangeRateOverWindow`, the worst TCA and miss over every minimum: 6.1e-6 s to 6.5e-5 s and 6.6e-7 m to 2.0e-6 m at mixed default and 60 s steps, 3.1e-5 s to 1.0e-4 s and 1.3e-5 m to 2.1e-5 m at fixed 600 s steps, the states of these steps against the degree-7 interpolation of their 60 s outputs in the SPK; by default the same over every minimum listed, and 3.2e-3 s and 2.3e-6 m at the fold of mixed default and 60 s steps (y0 = 1502.358 m; with the option at a `MaximumEventSearchStep` of 1 s alike) (Linux x64, provisional until the CI of the three platforms). Not listed by default: the minima of the two forms above. Not listed with the option: the fold minimum without a seed before it (the limit above, in 200 to 205 of 843 windows); the first of the two minima of one 600 s step at y0 = 1496 and 1497 m (the limit above, in 1 and 152 of 948 windows); and, with a drift of 0.1 m or less (a range rate below 1e-8 m/s), the zeros of the states several seconds from those of their SPK.
    - Cost on two RK7(8) trajectories: for two 400 km LEO trajectories over one day with EGM2008 10×10, the Moon and the Sun at the default tolerance (32 minima), `Analyze` took 15 to 20 ms, against 8 to 13 ms with the cubic Hermite states of 10.1 and 190 to 200 ms with the first version of the refinement (Release, median of 21 calls after a warm-up, Intel Xeon 2.1 GHz, 4 cores, 2026-10-11). The reads over the window of [#628](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/628), with `ReadRangeRateOverWindow`, cost one state per candidate and per sample: on two such trajectories crossing at 40° (31 minima, the default steps of about 234 s), 2063 states instead of 218 and 103 to 111 ms instead of 16.5 to 21.5 ms (about 6×) at the default `MaximumEventSearchStep` of 60 s; 1350 states and 76 to 80 ms at 120 s; 1001 states and 53 to 57 ms from 300 s on, the candidates alone, whose reads lie inside a step of the other trajectory. By default the search costs what it cost before #628, the same 31 minima: 16.5 to 21.5 ms against 16.8 to 18.9 ms for b237eec2 (Release, median of 21 calls after a warm-up, three runs of each build in turn on a quiet machine, the first configuration measured after the warm-up left out, which still carries up to 12 ms of compilation, Intel Xeon 2.1 GHz, 4 cores, 2026-11-11, after decision 54; an earlier run of the option under load gave 96 to 106 ms at 60 s and 66 to 70 ms at 120 s). At 60 s steps for both trajectories the reads add no sample, a step of 60 TT seconds that spans 60 s and one tick of its TDB labels included, and those at the candidates take the states read per window of the probe of #628 from 54 to 78 on average. A larger `MaximumEventSearchStep` trades this cost against the resolution of the reads between candidates (two minima within one long step); a smaller one resolves narrower intervals at about one state per spacing. The search from a seed reads 1 s apart up to its neighbouring candidate, as before 11.0.0: at fixed 600 s steps for one trajectory and the default steps for the other, 469 to 554 states per window of the probe of #628 with the option against 481 to 564 by default (5482 to 5646 on the whole window of 3 h); the figures of this day are the same states whether its reach is capped or not. The conformance runner's outputs are unchanged either way: with the reads over the window it took 26.7 to 27.3 s instead of 25.7 to 26.0 s; by default its 4764 deltas are those of b237eec2, bit for bit.

## ProtectedSpacecraftProfile

Packages the protected `Spacecraft` with maneuver constraints used by avoidance searches.

| Property | Description |
|----------|-------------|
| `Spacecraft` | The protected spacecraft instance |
| `ManeuverConstraints` | Delta-V and lead-time limits for avoidance searches |

## ScreeningOptions

| Property | Default | Description |
|----------|---------|-------------|
| `SampleStep` | 5 min | Coarse search cadence for each candidate pair (1 min in `ConjunctionAnalysisOptions`) |
| `MaximumEventSearchStep` | 60 s | Largest interval subdivided while refining the event detection |
| `MaxMissDistanceMeters` | 25 000 m | Filter threshold for miss distance |
| `MaxResults` | 25 | Maximum number of ranked results to return |
| `DefaultSecondaryHardBodyRadiusMeters` | null | Hard-body radius of a candidate that gives none (5 m when null); a candidate spacecraft with a radius above 0 keeps its own whatever this value (a fallback, see above, [#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683)) |
| `LowRelativeSpeedThresholdMetersPerSecond` | 1 m/s | Relative speed below which an encounter is flagged as a low-velocity case |

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
| `Epoch` | `Time` | Time of closest approach, labelled in the time frame of the window start on both paths ([#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683)); `ProtectedState` and `SecondaryState` are read at it |
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
| `CombinedHardBodyRadiusMeters` | The protected spacecraft's `HardBodyRadius` plus the radius of the secondary resolved by the table above (m) |
| `ProbabilityOfCollision` | Pc, null when no covariance is available |
| `ProjectedCovariance` | 2D encounter-plane covariance |
| `RadialSigmaMeters` / `InTrackSigmaMeters` / `CrossTrackSigmaMeters` | RTN sigmas (m) |

## ConjunctionAnalysisOptions

| Property | Default | Description |
|----------|---------|-------------|
| `SampleStep` | 1 min | Coarse search cadence |
| `MaximumEventSearchStep` | 60 s | Largest interval subdivided while refining the TCA; on two propagations with `ReadRangeRateOverWindow`, the largest spacing of the reads of the range rate over the window, one tick more allowed ([#628](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/628)); a smaller value resolves narrower intervals of the range rate at a higher cost; without it, unused on two propagations |
| `ReadRangeRateOverWindow` | false | On two propagations, also read the range rate over the whole window, every `MaximumEventSearchStep` at most, and bisect each change from negative to positive ([#628](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/628), opt-in by decision 54 of #457): finds the minima no minimum of the Hermite seeds (slow co-orbital passes at long or mixed steps), about 6 times the cost of the search on a fast pass |
| `SecondaryHardBodyRadiusMeters` | null | Hard-body radius of a secondary that gives none (5 m when null); a value that differs from the radius a secondary spacecraft gives throws (see the table above, [#683](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/683)) |
| `BisectionToleranceSeconds` | 1e-9 s | TCA refinement tolerance; the epochs have a resolution of 100 ns, which bounds the refinement below this value: below a tick the bisection ends on a bracket one tick wide and returns its tick nearer the zero, half a tick from it at most ([#624](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/624)); a tolerance of a tick or more ends on the middle of its bracket, in ticks, the middle of each step of the bisection being taken in ticks too (#624: through `TimeSpan.FromSeconds` it could fall a tick low) |
| `LowRelativeSpeedThresholdMetersPerSecond` | 1 m/s | Below this relative speed, `LowRelativeVelocityEncounter` is raised |
| `StaleCovarianceThreshold` | 60 s | Covariance age beyond which `StaleCovarianceUsed` is raised (covariance held in RTN) |
| `CovariancePropagation` | `Covariance` mode, no noise, no consider parameter | Propagation of the covariance of the participants to TCA; null for the covariance held in RTN. `StateTransitionOnly` and `IncludeInStepControl` are rejected (`ArgumentException`), as is a consider parameter whose force a propagated participant lacks |
| `ComputeNonlinearityIndex` | false | Whether the non-linearity index of each propagated covariance is computed (12 STM propagations per participant) |
| `NonlinearityIndexThreshold` | 1.0 | Index above which `NonlinearCovariancePropagation` is raised |

## Practical Limits

- Collision probability uses a 2D Foster-style encounter-plane method.
- `EncounterQualityFlags.LowRelativeVelocityEncounter` is an applicability warning that the 2D assumption may be degraded.
- The covariance is propagated to TCA for the participants the library propagates in RK7(8); the others keep their
  initial covariance held in RTN (see [Covariance at TCA](#covariance-at-tca)).
- The propagated covariance is linear: see the [non-linearity index](#non-linearity-index).
- The replay evaluates the forces and their partials of the participant's propagation: like `PropagationSolution`
  evaluations, an analysis must not run concurrently with another propagation or evaluation sharing a `CelestialBody`
  with a geopotential (see [Thread Safety](../guides/thread-safety.md)).
- Memory: to replay it, a `Spacecraft` propagated in RK7(8) keeps its last propagation (the accepted steps, the forces
  and their ephemeris cache) until it is propagated again, about 0.55 MB for one day in LEO with EGM2008 10×10, the Moon,
  the Sun and drag at 1e-11 (H3 review of step 10). A propagation by Velocity-Verlet keeps nothing beyond its output
  states.
- With both covariances, Pc is the Foster 2D probability of the combined covariance: CARA's `Pc2D`, the integral of the
  Gaussian of the encounter plane over the disc of the combined hard-body radius centred on the in-plane miss, each
  eigenvalue raised to at least (1e-4 R)² (`CovarianceRemediated` when one is raised). The integral takes the clipped
  eigenvalues and the rotation as they are, in θ along the disc (x = x_c + R sin θ), split at the Gaussian's mean and
  ±1 to 16 σ and at the chords reaching the minor axis's ±kσ, by adaptive Simpson at 1e-12 of the whole, the chord
  probability an erfc difference on the side away from zero with fdlibm's erf and erfc (#610, #611, #612). Measured
  against the exact Pc of its inputs (mpmath at 50 digits): within 2.0e-14 from 2.1e-2 to 5.1e-12, a hard-body radius much
  larger than the covariance and a covariance not positive definite included; a median of 0.2 ms a call over a random
  sweep, 2.2 ms in the deep tails (a Pc near 1e-300, 13 765 evaluations). A covariance far wider than the disc is
  limited by the noise of the integrand, the cancellation of the erfc difference, which the quadrature accepts rather
  than chase: it is bounded by about ε σ/R relative (0.05 to 1 times it as measured), within 5.0e-11 at σ = 1e6 R
  with the miss 1 σ off the major axis, and
  against Marcum's closed form (a circular covariance, the miss off the axes) 1.2e-10 at σ = 1e7 R, 2.4e-8 at 1e8 R and
  6.6e-7 at 1e10 R.
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
