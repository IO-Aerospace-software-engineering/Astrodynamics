---
title: Covariance Propagation
---

# Covariance Propagation

The RK7(8) integrator can propagate, with the state, the state transition matrix Φ, the sensitivity Ψ of the state to the
force parameters Cd and Cr, and the covariance P of the state. The values are read afterwards from the
`PropagationSolution`, at any epoch of the propagation.

**Namespace:** `IO.Astrodynamics.Propagator.Variational`

It is off by default. A propagation without it evaluates no partial derivative, and its results and cost do not change,
even when the initial state carries a covariance ([see below](#an-initial-covariance-without-activation)).

## Example

```csharp
var propagator = new CentralBodyPropagatorBuilder(window, spacecraft, new RK78Integrator(1e-11, 1e-11), TimeSpan.FromSeconds(60))
    .WithPerturbingBodies(new CelestialItem[] { moon, sun })   // the central body, the observer of the spacecraft, is added
    .IncludeAtmosphericDrag()
    .WithCovariancePropagation(new CovariancePropagationOptions
    {
        Mode = CovariancePropagationMode.Covariance,
        ProcessNoise = new RtnProcessNoise(1e-12, 1e-11, 1e-12),
        ConsiderParameters = new ConsiderParameters(dragCoefficientSigma: 0.1)
    })
    .Build();

var solution = propagator.Propagate();
Matrix phi = solution.GetStateTransitionMatrix(tca);  // Φ(tca, t0), 6×6
Matrix p = solution.GetCovariance(tca);               // 6×6, inertial frame of the propagation
```

The initial covariance P0 is that of the initial orbital parameters of the spacecraft (`StateVector.Covariance`), given at
the start of the window. This example runs as a smoke test against the packed NuGet package
(`IO.Astrodynamics.SmokeTests/CovariancePropagationSmokeTests.cs`).

## Activation

| Entry point | Member |
|-------------|--------|
| Builder | `CentralBodyPropagatorBuilder.WithCovariancePropagation(CovariancePropagationOptions)` |
| Propagator | `CentralBodyPropagator(window, spacecraft, integrator, celestialBodies, includeAtmosphericDrag, includeSolarRadiationPressure, deltaT, covariancePropagation)` and `CentralBodyPropagator(window, spacecraft, integrator, deltaT, covariancePropagation)` |
| Spacecraft | `Spacecraft.Propagate(window, celestialBodies, integrator, includeAtmosphericDrag, includeSolarRadiationPressure, propagatorStepSize, covariancePropagation)` |
| Batch | `PropagationTask.CovariancePropagation { get; init; }`, with an `IntegratorFactory` that creates an `RK78Integrator` |
| Scenario | `Scenario.SimulateAsync(includeAtmosphericDrag, includeSolarRadiationPressure, propagatorStepSize, integratorFactory, covariancePropagation)` |

- The integrator must be an `RK78Integrator`. Any other, Velocity-Verlet included, throws `NotSupportedException`.
  Velocity-Verlet stays the default integrator everywhere.
- Null options mean "off" in the constructors, `Spacecraft.Propagate`, `PropagationTask` and `SimulateAsync`;
  `WithCovariancePropagation` takes a non-null value and may be called once.
- The options are checked at activation (construction, `Build`, `Propagate`), never during the integration.
- In a `Scenario`, the options apply to every spacecraft. In covariance mode, the states that each spacecraft stores
  carry their covariance; a spacecraft without a valid initial covariance makes the simulation throw, naming it. The
  matrices themselves are read from the `PropagationSolution` of `Spacecraft.Propagate`, since `SimulateAsync` returns
  a summary.

## Options

`CovariancePropagationOptions` is a record; every member has a default.

| Member | Type | Default | Meaning |
|--------|------|---------|---------|
| `Mode` | `CovariancePropagationMode` | `StateTransitionOnly` | `StateTransitionOnly`: Φ and Ψ. `Covariance`: Φ, Ψ and P |
| `ProcessNoise` | `RtnProcessNoise` | `null` | State noise compensation, white acceleration noise in RTN (covariance mode only) |
| `ConsiderParameters` | `ConsiderParameters` | `null` | Standard deviations of Cd and Cr taken as consider parameters (covariance mode only) |
| `IncludeInStepControl` | `bool` | `false` | Whether the error of Φ and Ψ takes part in the step-size control |

- `RtnProcessNoise(radial, transverse, normal)`: the spectral densities `q_R`, `q_T`, `q_N`, in m²/s³, finite and
  non-negative.
- `ConsiderParameters(dragCoefficientSigma, reflectivityCoefficientSigma)`: standard deviations, dimensionless, finite and
  non-negative; null for a coefficient that is not a consider parameter.
- With `IncludeInStepControl = false`, the steps are controlled on the state only, so the trajectory is the same, bit for
  bit, with and without the variational equations. With `true`, the steps change, and so does the trajectory, within the
  tolerance of the integrator.
- Without `IncludeInStepControl`, Ψ carries an integration error that the control of the state does not see, largest
  for Cr across the shadow crossings of SRP. In an eclipsing LEO 400 × 1000 km over 6 h, with a maneuver, drag and SRP,
  at a tolerance of 1e-12, the error of Ψ_Cr over the arc, relative to the largest Ψ_Cr of the arc, was 6.0e-5 to
  1.9e-3 depending on where the steps fell (the same physics in J2000 and in ECLIPJ2000), against 2.6e-5 at most at
  1e-14. Relative to the local Ψ_Cr, it reaches 1.5e-2 at 1e-12 just after the first shadow exit, where Ψ_Cr is still
  below a millimetre, but also 1.2e-2 at a later crossing (4.45 h), where Ψ_Cr is 0.28 m; at 1e-14, 1.6e-4 at that later
  crossing. With the option, at 1e-12, Ψ_Cr is within 7.1e-8 (J2000) and 3.4e-8 (ECLIPJ2000) of the same propagation at 1e-13, relative to the local
  Ψ_Cr, over the whole arc. When Ψ_Cr matters (Cr as a consider parameter, in an orbit with eclipses), use the option: a
  tighter tolerance reduces the error about a hundredfold, not below 1e-4 at every crossing. In the same
  case, Φ and Ψ_Cd stay below 2.2e-8 and 7.6e-7 (at 5.123 h; measured 2026-10-21).
- `IncludeInStepControl` has no effect with a fixed-step `RK78Integrator(fixedStepSize)`, which controls no step.
- With `IncludeInStepControl`, the step follows the error of Φ and Ψ, so it needs partial derivatives that are smooth
  along the trajectory. The density gradient of the drag partials is taken in geodetic coordinates, without rounding
  noise of its own (dρ/dh in closed form for the US 1976 and Mars atmospheres, the derivatives of NRLMSISE-00 in its
  inputs): in dense air the steps stay ordinary. Over 2 min of flight on a circular orbit with drag alone (A/m =
  0.1 m²/kg): US 1976 at 200 km, 6, 7 and 10 steps at 1e-11, 1e-12 and 1e-13; NRLMSISE-00 at 200 km (on its 200 km
  cutoff), 6, 26 and 321; at 400 km, 11 at 1e-13; 2 steps without the option. Before [#420](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/420) the same cases took 107 290
  steps or did not finish. On a whole propagation (R2: LEO 400 km, EGM2008 10×10, Sun, Moon, NRLMSISE-00 drag, one
  day, with Ψ_Cd; Linux, Release, best of 3, measured 2026-10-21): without the option, the same steps and 15 % (1e-12)
  to 22 % (1e-13) more time than before, for the two more model evaluations of each drag partials call; with the
  option, 1 084 steps in 2.15 s at 1e-12 against 1 247 in 1.95 s before, and 1 681 steps in 3.27 s at 1e-13 against
  38 678 in 60.6 s.
- Through the SRP penumbra, the partials take the analytic derivative of the shadow function, so the steps stay
  ordinary with the option on an eclipsing orbit too
  ([#423](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/423)). R3 (Sun-synchronous 700 km,
  eclipses on every revolution, Cr, one day; Linux, Release, 2026-10-22, with the shadow fraction of
  [#434](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/434), and with the constants of the
  geopotential of [#410](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/410), 2026-10-23)
  takes 1 645, 2 214, 2 970 and 4 639 steps at 1e-11, 1e-12, 1e-13 and 1e-14 with the option, against 746, 1 005, 1 331
  and 1 807 without it; R4 (GEO in eclipse season) 256 and 466 at 1e-13 and 1e-14 (1 657 to 4 604 and 258, 464 before
  #410; 1 692 to 4 757, and 252 and 458, before #434). Before #423 the central differences of the acceleration carried the
  rounding of the shadow fraction, which the step of the velocity rows of Φ followed: R3 took 1 947, 4 379 and 23 967
  steps, 21 596 of them in its 265 s of penumbra at 1e-13, and stopped on the guard below at 13.3 h at 1e-14 (on 100
  rejections at 15.0 h before [#420](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/420)); R4
  took 2 402 steps at 1e-14. Without the option the trajectory does not change, and Φ and Ψ move only through the steps
  that touch the penumbra (R3 at 1e-13 after one day: Φ by 2.6e-9, Ψ_Cr by 8.7e-9).
- When the step collapses all the same (a force whose partials are not smooth at the tolerance), RK7(8) stops with an
  `InvalidOperationException` after 10 000 consecutive accepted steps within 100 s of flight, a mean step below 10 ms,
  instead of running until the memory is exhausted. The steps are counted in disjoint blocks of 10 000 from the start
  of the segment, so a collapse is caught after 10 000 to 20 000 steps. Its message names the option, the epoch, the
  mean and last step and the errors of Y and of the state, and the remedies for the error that held the step: relax
  the tolerance or set `IncludeInStepControl` to false when it is that of Y, relax the tolerance when it is that of
  the state (a force that is not smooth itself). No legitimate propagation measured takes 10 000 steps with the option:
  the closest before [#423](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/423), R3 at 1e-13,
  took 10 000 consecutive steps over 5 947 s at its shadow crossings (a mean of 0.595 s, 60 times the threshold), and
  1 000 over 0.174 s; it now takes 2 970 steps a day (2 972 before #410), and at 1e-14 its shortest 1 000 consecutive steps cover
  17 666 s (16 368 s before #434). The threshold is 50 times above the mildest collapse measured before
  [#420](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/420). Without the option the guard
  does not apply.
- In SSB mode (initial orbital parameters relative to a barycenter or a star), the columns of Y are scaled by |r0|
  relative to the barycenter, about 1.5e11 m for a spacecraft near the Earth: with the option the step control then
  fails, with or without drag ([#389](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/389)).
- The RTN axes of the process noise are those of the state relative to the central body of the propagation. When the
  central body is the Sun (initial orbital parameters relative to a star or a barycenter), they are heliocentric, even
  for a spacecraft near a planet.

### Columns of Ψ

The columns of Ψ follow the forces of the propagation: Cd when atmospheric drag is present, then Cr when a radiation force
is (solar radiation, albedo or thermal radiation pressure, which share Cr). `PropagationSolution.SensitivityParameters`
lists them in order. A consider parameter needs its force.

### Exceptions at activation

| Case | Exception |
|------|-----------|
| The integrator is not an `RK78Integrator` | `NotSupportedException` |
| `Mode` is not a defined value | `ArgumentOutOfRangeException` (when the options are built) |
| `ProcessNoise` or `ConsiderParameters` with `StateTransitionOnly` | `ArgumentException`: they act on P only |
| A consider parameter without its force | `ArgumentException` |
| Covariance mode, and the initial orbital parameters carry no covariance, carry it at another epoch than the start of the window, or carry one that is not 6×6, finite, symmetric and positive semi-definite (one that rounding made indefinite: see [below](#an-initial-covariance-indefinite-from-rounding)) | `InvalidOperationException` |

## Results

On `PropagationSolution`:

| Member | Returns | Units |
|--------|---------|-------|
| `CovariancePropagation` | The options of the propagation; null when off | — |
| `SensitivityParameters` | The columns of Ψ, in order | — |
| `ExtendedCovarianceParameters` | The consider parameters, in the order of the rows and columns 7 and after of the extended covariance | — |
| `GetStateTransitionMatrix(Time)` | Φ(t, t0), 6×6 | position-position and velocity-velocity blocks dimensionless; ∂r/∂v0 in s; ∂v/∂r0 in 1/s |
| `GetParameterSensitivity(Time)` | Ψ(t), 6×p | m and m/s per unit of the coefficient |
| `GetCovariance(Time)` | P(t), 6×6 | m², m²/s, m²/s² |
| `GetExtendedCovariance(Time)` | (6 + c)×(6 + c) | P as above; state-parameter blocks in m and m/s; parameter block dimensionless |
| `GetStateVector(Time)` | The state of `InterpolateAt`, with its covariance in covariance mode | m, m/s |

- Rows and columns are ordered x, y, z, vx, vy, vz, in the frame of the initial orbital parameters, which is the
  inertial frame of the propagation (ICRF or another), relative to its central body. Moving the origin from the observer of the initial state to the central body is a translation by a known
  ephemeris, of Jacobian I, which leaves P0 unchanged.
- Inside an integration step, a value comes from a shortened RK7(8) step from the start of that step, at the accuracy of
  the integrator; at a step boundary, the stored value.
- At an impulsive maneuver, the value after it, its execution error included.
- In covariance mode, each output state (`StateVectors`) carries its P in `StateVector.Covariance`; the first one is P0,
  symmetrized. The output state comes from the same shortened step as without the covariance, so it is the same, bit for
  bit.

| Case | Exception |
|------|-----------|
| The epoch is outside the propagation (an epoch up to one tick, 100 ns, after its end reads the values at the end) | `ArgumentOutOfRangeException` |
| The propagation did not integrate the matrices (no activation, another integrator, a solution built by hand) | `InvalidOperationException` |
| `GetCovariance` or `GetExtendedCovariance` without the covariance mode | `InvalidOperationException` |
| `GetParameterSensitivity` without drag or radiation force | `InvalidOperationException` |
| `GetExtendedCovariance` without consider parameters | `InvalidOperationException` |
| `GetStateVector` on a solution built by hand | `InvalidOperationException` |

Each call allocates its result. The propagation itself allocates nothing per step or per stage; in covariance mode, each
output state allocates its covariance matrix only.

### Cost of the output states

Inside an integration step, a value costs one shortened RK7(8) step from the start of that step: 13 evaluations of the
forces for a state (`InterpolateAt`), and 13 evaluations of the forces and 13 of their partial derivatives for Φ, Ψ or P
(the getters above). In covariance mode, every output state (`StateVectors`) carries its P, so it is computed the second
way, and a fine output grid makes the propagation markedly slower than one of the state transition matrix only. Measured
on a LEO 400 km over one day, with NRLMSISE-00 drag, the Moon and the Sun, RK7(8) at 1e-10 (about 500 steps; Release,
informative):

| Outputs every | Geopotential | Without | State transition matrix only | Covariance |
|---------------|--------------|---------|------------------------------|------------|
| 600 s | J2 | 70 ms | 331 ms | 473 ms |
| 600 s | 20×20 | 110 ms | 474 ms | 544 ms |
| 60 s | J2 | 235 ms | 432 ms | 1325 ms |
| 60 s | 20×20 | 279 ms | 668 ms | 1495 ms |

When P is needed at a few epochs only (a time of closest approach, for example), a coarse output step with
`GetCovariance` or `GetStateVector` at those epochs costs less. A `Scenario` in covariance mode stores every output state
with its P, so it pays this cost.

## Equations

With x = (r, v), a = a(r, v, t; p), G = ∂a/∂r, D = ∂a/∂v, A = [[0, I], [G, D]] and B = [0 ; I]:

| Quantity | Equation | Reference |
|----------|----------|-----------|
| State transition matrix | dΦ/dt = A Φ, Φ(t0) = I | Montenbruck and Gill, *Satellite Orbits*, 2000, chapter 7 |
| Sensitivity to the parameters | dΨ/dt = A Ψ + B ∂a/∂p, Ψ(t0) = 0 | Montenbruck and Gill, op. cit., chapter 7 |
| Process-noise covariance | dQ/dt = A Q + Q Aᵀ + B Qc Bᵀ, Q(t0) = 0 | Gelb, *Applied Optimal Estimation*, 1974, chapter 4; Tapley, Schutz and Born, *Statistical Orbit Determination*, 2004, §4.9 |
| RTN noise | Qc = R_rtnᵀ diag(q_R, q_T, q_N) R_rtn, at each stage state | Tapley, Schutz and Born, op. cit., §4.9 |
| Propagated covariance | P = Φ P0 Φᵀ + Ψc Pp Ψcᵀ + Q, then (P + Pᵀ) / 2 | Tapley, Schutz and Born, op. cit., chapter 4 |
| Extended covariance | [[P, Ψc Pp], [Pp Ψcᵀ, Pp]] | Tapley, Schutz and Born, op. cit., chapter 4 |
| Across segments | Φ = Φ_seg Φ_e, Ψ = Φ_seg Ψ_e + Ψ_seg, Q = Φ_seg Q_e Φ_segᵀ + Q_seg | Chain rule of the flow |
| Execution error at a burn | Q⁺ = Q⁻ + B L_e Bᵀ, L_e of Gates | Gates, JPL TR 32-504, 1963, §II and §III |

Ψc holds the columns of Ψ of the consider parameters and Pp = diag(σ²). The variational equations use the stages of the
state step, so they add no approximation beyond the integrator. The [provenance page](covariance-propagation-provenance.md)
gives the implementation, the tests and the citation status of each equation.

## Modeling Assumptions

- **Linear propagation.** P is the linear propagation of the deviations, valid while they stay small against the
  non-linearities of the dynamics. Measured against a Monte Carlo of 5000 runs, with P0 of 100 m and 0.1 m/s on each
  RTN axis in LEO (R1 two-body at 700 km, R2 at 400 km with drag, R3 sun-synchronous with SRP):
  - For about one revolution, P is the covariance of the deviations along every direction of the state, and the
    distribution is Gaussian with covariance P. The ratios of the sample variance to the variance of P stay within
    the sampling noise up to 1 h to 2.2 h depending on the case, and the Kolmogorov-Smirnov test of the Mahalanobis
    distances passes up to 1.67 h to 2.33 h.
  - Beyond about one revolution, P is only the linear covariance. Along one direction of the state the sample
    variance grows away from P: about 1.15 times at one revolution, 350 to 610 times at 6 h, 1.3e6 to 2.4e6 times at
    24 h (2.2 and 87 with Cd sampled). Every element of P in inertial axes still agrees with the sample covariance
    within the noise of its estimator up to 24 h, but that noise is set by the along-track variance, which hides the
    thin directions of P.
  - The position, what a collision probability projects on the encounter plane, holds longer: its variances agree
    with the Monte Carlo up to 6 h. At 24 h one direction of the position has 3.3 to 5.0 times the variance of P (the
    radial variance alone 1.08 to 1.17 times). For this P0 a collision probability computed with P more than a few
    hours before the encounter therefore rests on a covariance that underestimates the spread in part of the
    encounter plane; how much this moves the probability is measured with the conjunction analysis (step 10).
  - The departure is the curvature of the orbit. The along-track spread lies on the curved orbit, so the samples sit
    below the straight line of the linear model, by a mean radial offset of about −E[δT²] / (2 r): −104 m at 24 h for
    R1, against −102 m predicted. P is very thin across that curve, so a few metres of curvature move the
    Mahalanobis distances far beyond their spread.
  - The curvature term scales with the along-track variance over the radius, σ_T² / r, and σ_T grows mainly with the
    velocity uncertainty (0.1 m/s here): a smaller P0 should delay the departure, which has not been measured.
  - Figures and tests: [Validation](../guides/validation.md#state-transition-matrix-and-covariance).
- **Open-loop maneuvers.** An impulsive maneuver applies its nominal ΔV at its epoch; its firing time is not
  differentiated with respect to the state, so Φ and Ψ are continuous at the burn. That is the covariance of a burn
  executed as commanded, with its execution error (Gates). A maneuver that retargets its ΔV from the state it finds,
  as `ApogeeHeightManeuver` does at its perigee, removes part of the deviation: in the eclipsing LEO case of the
  validation, Φ after the burn differs from that of the retargeted maneuver by 0.23 to 0.48 per block (Φ against the
  open-loop reference: 2.9e-8).
- **P0 at the start of the window.** The propagator does not carry a covariance from another epoch.
- **No process noise by default.** Without it, P = Φ P0 Φᵀ + Ψc Pp Ψcᵀ ignores the unmodelled accelerations and is
  usually optimistic.
- **Constant consider parameters**, independent of the initial state and of the noise.

### RTN axes of the process noise along an arc

The propagated Q integrates the noise with the RTN axes of each stage state, so the axes turn with the orbit along the
arc. A filter that applies the discrete form of Tapley §4.9 between measurements, `Q = [[Δt³/3 Qc, Δt²/2 Qc], [Δt²/2 Qc,
Δt Qc]]`, holds them fixed at the start of the arc. The difference is first order in the arc, since the axes turn at the
orbital rate: in LEO 400 km with `q_T` alone, 0.8 % over 10 s, 4.8 % over 60 s, 9.5 % over 120 s. With the same Qc held
fixed in inertial axes, the two agree to second order (`RtnProcessNoiseTests`). Neither form is wrong: they model
different noises, and the comparison of a propagated covariance with a filter covariance should take this into account.

GMAT R2026a applies that discrete form at every integration step. Over one day, its Q departs from the propagated one by
2.35e-4 at a step of 20 s, then by half as much at each halving of the step, and its first-order limit is the propagated
Q to 1e-8 (LEO 700 km two-body) and 2e-8 (LEO 400 km, EGM2008 10×10, Sun, Moon): the propagated Q is the limit of the
discrete form along the arc ([Validation](../guides/validation.md#against-gmat-h2)).

## Execution Error of an Impulsive Maneuver

`ImpulseManeuver.ExecutionError` (`ManeuverExecutionError`, namespace `IO.Astrodynamics.Maneuver`) describes the error of
the burn by the model of Gates, four independent zero-mean Gaussian errors:

| Parameter | Error | Unit |
|-----------|-------|------|
| `ResolutionSigma` (σ_r) | Fixed magnitude error, along ΔV | m/s |
| `ShutoffSigma` (σ_s) | Magnitude error proportional to \|ΔV\| | dimensionless |
| `AutopilotSigma` (σ_a) | Fixed pointing error, on each axis perpendicular to ΔV | m/s |
| `PointingSigma` (σ_p) | Pointing error proportional to \|ΔV\|, on each axis perpendicular to ΔV | rad |

```csharp
var maneuver = new ApogeeHeightManeuver(earth, epoch, TimeSpan.Zero, 7000000.0, engine)
{
    ExecutionError = new ManeuverExecutionError
    {
        ResolutionSigma = 0.01, ShutoffSigma = 0.002, AutopilotSigma = 0.005, PointingSigma = 0.001
    }
};
```

- L_e = (σ_s² − σ_p²) v vᵀ + (σ_r² − σ_a²) v vᵀ / V² + (σ_p² V² + σ_a²) I, with v the commanded ΔV and V = |v|: along ΔV
  σ_r² + σ_s² V², across it σ_a² + σ_p² V² on each axis.
- A ΔV of exactly zero adds no error: the engine burns nothing, and the model is not defined there. Any non-zero ΔV takes
  the whole model, the fixed terms included.
- The error acts only in covariance mode, added to the velocity covariance at the burn. Otherwise it is ignored and costs
  nothing. The trajectory always follows the commanded ΔV.

## An Initial Covariance Without Activation

When the initial orbital parameters carry a covariance and the propagation is not activated, the covariance stays on the
initial orbital parameters, the output states carry none, and no partial derivative is evaluated: the propagation costs
and allocates the same as without the initial covariance, and gives the same states bit for bit
(`CovariancePropagationApiTests.WithoutActivation_*`).

## An Initial Covariance Indefinite From Rounding

The propagation checks the initial covariance P0 on its correlation matrix C = D^(-1/2) P0 D^(-1/2), D = diag(P0), so
that the check does not depend on the units: each asymmetry within 1e-12 of √(P_ii P_jj), the smallest eigenvalue of C
at least −1e-12 of its trace. A covariance that fails is rejected with `InvalidOperationException`, never corrected:
the user asks for this covariance to be propagated, and `PropagationSolution` has no channel to report a correction.
Rounded values can make a covariance indefinite, for example a correlation of −1 written as −1.0001 in a message.

`StateVector.WithRemediatedCovariance()` remediates such a covariance, explicitly, before the propagation. It is the
remediation of the [conjunction analysis](conjunction-assessment.md#covariance-at-tca), the same code and the same
tolerances: eigenvalue clipping (D. T. Hall, M. D. Hejduk, L. C. Johnson, *Remediating non-positive definite state
covariances for collision probability estimation*, AAS 17-567, 2017; confirmed by S. Guillet, 2026-10-18), on the
correlation matrix:

- C' = V max(Λ, 0) Vᵀ, the negative eigenvalues of C = V Λ Vᵀ set to zero, then P'_ij = C'_ij √(P_ii P_jj);
- the covariances of a zero variance set to zero;
- the result symmetric to the bit and accepted by the check of the propagation.

```csharp
var remediation = state.WithRemediatedCovariance();
if (remediation.Remediated)
    Console.WriteLine($"Smallest eigenvalue of the correlation matrix: {remediation.SmallestCorrelationEigenvalue:E3}");

var spacecraft = new Spacecraft(-1001, "SAT", 100.0, 1000.0, clock, remediation.State);
var solution = spacecraft.Propagate(window, bodies, new RK78Integrator(), false, false, TimeSpan.FromSeconds(60.0),
    new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance });
```

| Covariance of the state | Result |
|------|--------|
| Accepted by the propagation | `Remediated` false, `State` the state itself, its covariance untouched |
| Symmetric within the tolerance, finite, non-negative variances, not positive semi-definite | `Remediated` true, `State` a new state with the same position, velocity, observer, epoch and frame, and the remediated covariance |
| None, not 6×6, a value that is not finite, a negative variance, an asymmetry beyond the tolerance | `InvalidOperationException`, with the reason: rounding does not explain it |

- `SmallestCorrelationEigenvalue` is that of the correlation matrix of the covariance given, over its positive
  variances: negative when it was remediated. The remediation has no bound on this depth: a correlation of −1.0001
  changes the covariance by 7.1e-5 of √(P_ii P_jj), one of −10 by 5.5 times it, both remediated. A caller who wants a
  bound tests this value ([#443](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/443) will set
  one for the analysis and this method together).
- The clipping is made in the frame of the state, the frame in which the propagation takes P0, and depends on the
  axes: the same covariance clipped in RTN comes out different, by 4.2e-5 of √(P_ii P_jj) for a correlation of −1.0001.
  A covariance known in RTN (a CDM) is given to the state in its inertial frame (`RotateCovarianceFromRtn`), then
  remediated there, as the analysis does.
- The conjunction analysis, given the remediated state, propagates the same covariance bit for bit as when it
  remediates the original state itself, but does not raise `EncounterQualityFlags.CovarianceRemediated`: the covariance
  it is given is valid.
- One 6×6 eigendecomposition, about 18 µs and 7.6 kB, once before the propagation.

## Ephemeris Files

The SPK written by `Spacecraft.WriteEphemeris` holds positions and velocities only: the covariance is not written. The
ephemeris queries of the spacecraft (`GetEphemeris`, `GetGeometricStateRelativeTo`, a Lagrange interpolation of the stored
states) and SPICE reading the SPK carry no covariance. The covariance is read from the `PropagationSolution` and from its
output states. An OEM export with covariance is planned for a later release.

## In the Conjunction Analysis

`ConjunctionAssessment.Analyze` and `AnalyzeAll` propagate the covariance of a participant propagated in RK7(8) whose
initial state vector carries a covariance at the start of the propagation: they read it from the solution in covariance
mode, or replay the variational equations along the steps of a solution propagated without the covariance (without the
variational equations, or with the state transition matrix only), up to the last TCA
(`ConjunctionAnalysisOptions.CovariancePropagation`, on by default). The replay gives the values of a propagation run
with the covariance from the start, bit for bit, and does not change the solution. See
[Conjunction Assessment](conjunction-assessment.md#covariance-at-tca) and, for the avoidance options propagated from the
covariance at their burn, [Avoidance Studies](avoidance.md).

## Thread Safety

The evaluations of a solution (`InterpolateAt` and the getters above) run under a lock of the solution. They evaluate the
forces, so they must not run concurrently with a propagation or an evaluation that shares a `CelestialBody` with a
geopotential. See [Thread Safety](../guides/thread-safety.md).

## See Also

- [Covariance Provenance](covariance-propagation-provenance.md)
- [Propagators](propagators.md)
- [Propagator Builder](propagator-builder.md)
- [Integrators](integrators.md)
- [Impulse Maneuvers](impulse-maneuvers.md)
- [StateVector](state-vector.md)
