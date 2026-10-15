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
  for Cr across the shadow crossings of SRP: in an eclipsing LEO over 6 h at a tolerance of 1e-12, Ψ_Cr was off by
  4.0e-5 to 1.3e-3 depending on where the steps fell, against 1.6e-5 and 3.4e-6 at 1e-14. A tighter tolerance or the
  option brings it down. In the same case, Φ and Ψ_Cd stay below 3.1e-8 and 3.4e-7.
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
| Covariance mode, and the initial orbital parameters carry no covariance, carry it at another epoch than the start of the window, or carry one that is not 6×6, finite, symmetric and positive semi-definite | `InvalidOperationException` |

## Results

On `PropagationSolution`:

| Member | Returns | Units |
|--------|---------|-------|
| `CovariancePropagation` | The options of the propagation; null when off | — |
| `SensitivityParameters` | The columns of Ψ, in order | — |
| `ConsiderParameters` | The consider parameters, rows and columns 7 and after of the extended covariance | — |
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
  non-linearities of the dynamics. Its domain of validity is measured against Monte Carlo in step 9 (F3).
- **Open-loop maneuvers.** An impulsive maneuver applies its nominal ΔV at its epoch; its firing time is not
  differentiated with respect to the state, so Φ and Ψ are continuous at the burn.
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

## Ephemeris Files

The SPK written by `Spacecraft.WriteEphemeris` holds positions and velocities only: the covariance is not written. The
ephemeris queries of the spacecraft (`GetEphemeris`, `GetGeometricStateRelativeTo`, a Lagrange interpolation of the stored
states) and SPICE reading the SPK carry no covariance. The covariance is read from the `PropagationSolution` and from its
output states. An OEM export with covariance is planned for a later release.

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
