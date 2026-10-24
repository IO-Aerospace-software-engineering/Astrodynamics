---
title: Covariance Propagation
---

# Covariance Propagation

This tutorial takes a state with its covariance to a covariance at another epoch, then to a collision probability. Along
the way it reads the state transition matrix Φ and the sensitivity Ψ to Cd and Cr, adds process noise and consider
parameters, gives an impulsive maneuver its execution error, remediates a covariance that rounding made indefinite, and
exports the encounter as a CDM.

The [Covariance Propagation](../reference/covariance-propagation.md) reference gives every option, unit and exception;
the [Covariance Provenance](../reference/covariance-propagation-provenance.md) page gives the equations, their
references and their tests. The code of this tutorial runs as a smoke test against the packed NuGet package
(`IO.Astrodynamics.SmokeTests/CovariancePropagationTutorialSmokeTests.cs`).

## What you choose first

- **The integrator: RK7(8).** The variational equations are integrated with the state by `RK78Integrator` only; any
  other integrator, Velocity-Verlet included, throws `NotSupportedException` at activation. Velocity-Verlet stays the
  default integrator everywhere, so the integrator is always given explicitly here.
- **The mode.** `StateTransitionOnly` gives Φ and Ψ; `Covariance` gives Φ, Ψ and the covariance P. The covariance
  propagation is off by default: without it, an initial covariance is not propagated and costs nothing.
- **The tolerance.** Φ, Ψ and P come from the stages of the state step, at the accuracy of the integrator.
- **`IncludeInStepControl` when Ψ_Cr matters.** By default the steps are controlled on the state only, so the trajectory
  is the same with and without the variational equations; Ψ then carries an integration error that the control does
  not see, largest for Cr across the shadow crossings of SRP (up to 3.5e-3 of its largest value over the arc at 1e-12 in
  an eclipsing LEO). When Cr is a consider parameter in an orbit with eclipses, set it: the step then follows Φ and Ψ too
  ([Options](../reference/covariance-propagation.md#options)).

## The case

A LEO at 400 km, with the EGM2008 geopotential to degree 10, NRLMSISE-00 drag, SRP, the Moon and the Sun:

```csharp
SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));

var epoch = new Time(2021, 3, 20, 12, 0, 0);
var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch,
    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
    new Nrlmsise00Model(SpaceWeather.Nominal));
var moon = new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, epoch);
var sun = new CelestialBody(Stars.Sun);
var bodies = new CelestialItem[] { earth, moon, sun };
```

## A state with its covariance

The initial covariance P0 is carried by the initial state vector of the spacecraft (`StateVector.Covariance`), 6×6, in
m², m²/s and m²/s², rows and columns x, y, z, vx, vy, vz in the frame of the state, at the start of the propagation
window. A covariance known in RTN, from an orbit determination or a CDM, is rotated to that frame first:

```csharp
var orbit = new KeplerianElements(6778137.0, 0.0005, 51.6 * Constants.Deg2Rad, 20.0 * Constants.Deg2Rad,
    15.0 * Constants.Deg2Rad, 0.0, earth, epoch, Frame.ICRF).ToStateVector();

double[] sigma = { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 };   // R, T, N (m), then their rates (m/s)
var rtn = new Matrix(6, 6);
for (int i = 0; i < 6; i++)
    rtn.Set(i, i, sigma[i] * sigma[i]);
rtn.Set(1, 3, -0.9 * sigma[1] * sigma[3]);   // along-track position and radial velocity
rtn.Set(3, 1, -0.9 * sigma[1] * sigma[3]);

var initialState = new StateVector(orbit.Position, orbit.Velocity, earth, epoch, Frame.ICRF,
    orbit.RotateCovarianceFromRtn(rtn));

var spacecraft = new Spacecraft(-1001, "SAT", 100.0, 1000.0, new Clock("SAT_CLK", 65536), initialState,
    sectionalArea: 10.0, dragCoeff: 2.2, solarRadiationCoeff: 1.5);
var window = new Window(epoch, epoch.AddHours(6.0));
var t = epoch.AddHours(6.0);
```

## The state transition matrix only

```csharp
var stm = spacecraft.Propagate(window, bodies, new RK78Integrator(1e-11, 1e-11), true, true,
    TimeSpan.FromSeconds(600.0),
    new CovariancePropagationOptions { Mode = CovariancePropagationMode.StateTransitionOnly });

Matrix phi = stm.GetStateTransitionMatrix(t);   // Φ(t, t0), 6×6
Matrix psi = stm.GetParameterSensitivity(t);    // Ψ(t), 6×2: ∂x/∂Cd, ∂x/∂Cr
Console.WriteLine(string.Join(", ", stm.SensitivityParameters));   // DragCoefficient, ReflectivityCoefficient
```

- Φ(t, t0) maps a deviation of the initial state to a deviation at t: its position and velocity blocks are
  dimensionless, ∂r/∂v0 is in s, ∂v/∂r0 in 1/s.
- The columns of Ψ follow the forces of the propagation, Cd with drag then Cr with a radiation force, in the order of
  `SensitivityParameters`; Ψ is in m and m/s per unit of the coefficient.
- Inside an integration step, a value comes from a shortened RK7(8) step from the start of that step; any epoch of the
  window can be read.

The same options are taken by the builder (`CentralBodyPropagatorBuilder.WithCovariancePropagation`), the
`CentralBodyPropagator` constructors, `PropagationTask.CovariancePropagation` and `Scenario.SimulateAsync`
([Activation](../reference/covariance-propagation.md#activation)).

## The covariance

```csharp
var options = new CovariancePropagationOptions
{
    Mode = CovariancePropagationMode.Covariance,
    ProcessNoise = new RtnProcessNoise(radial: 1e-12, transverse: 1e-11, normal: 1e-12),   // m²/s³
    ConsiderParameters = new ConsiderParameters(dragCoefficientSigma: 0.1, reflectivityCoefficientSigma: 0.05),
    IncludeInStepControl = true   // Ψ_Cr through the shadow crossings
};
var solution = spacecraft.Propagate(window, bodies, new RK78Integrator(1e-11, 1e-11), true, true,
    TimeSpan.FromSeconds(600.0), options);

Matrix p = solution.GetCovariance(t);                 // 6×6, m², m²/s, m²/s²
StateVector state = solution.GetStateVector(t);       // the state, with P in state.Covariance
Matrix extended = solution.GetExtendedCovariance(t);  // 8×8: x, y, z, vx, vy, vz, Cd, Cr
```

P = Φ P0 Φᵀ + Ψc Pp Ψcᵀ + Q, in the inertial frame of the propagation:

- **Process noise** (`RtnProcessNoise`): a white acceleration noise of spectral densities q_R, q_T, q_N (m²/s³) along
  the RTN axes of the state, the state noise compensation of Tapley, Schutz and Born §4.9. It stands for the forces the
  model leaves out; without it P is the linear propagation of P0 alone, usually optimistic.
- **Consider parameters** (`ConsiderParameters`): the standard deviations of Cd and Cr, dimensionless. They are not
  estimated; their uncertainty widens P through Ψ. A consider parameter needs its force (drag for Cd, a radiation
  force for Cr). `GetExtendedCovariance` adds their rows and columns, in the order of `ExtendedCovarianceParameters`.
- **Output states**: in covariance mode each state of `StateVectors` carries its P. Each one inside a step costs 13
  evaluations of the forces and of their partial derivatives, so a fine output grid costs more than the propagation
  itself ([Cost](../reference/covariance-propagation.md#cost)); when P is needed at a few epochs, take a coarse
  output step and read `GetCovariance` there.

## An impulsive maneuver with its execution error

`ImpulseManeuver.ExecutionError` describes the error of the burn by the model of Gates: a fixed and a proportional error
of the magnitude, along ΔV, and a fixed and a proportional pointing error, across it.

```csharp
var tank = new FuelTank("TANK", "model", "sn1", 50.0, 50.0);
var engine = new Engine("ENGINE", "model", "sn1", 300.0, 1.0, tank);
var maneuvering = new Spacecraft(-1002, "SAT2", 100.0, 1000.0, new Clock("SAT2_CLK", 65536), initialState,
    sectionalArea: 10.0, dragCoeff: 2.2, solarRadiationCoeff: 1.5);
maneuvering.AddFuelTank(tank);
maneuvering.AddEngine(engine);
var raise = new ApogeeHeightManeuver(earth, epoch.AddHours(1.0), TimeSpan.Zero, 6878137.0, engine)
{
    ExecutionError = new ManeuverExecutionError
    {
        ResolutionSigma = 0.005,   // m/s, along ΔV
        ShutoffSigma = 0.001,      // proportional to |ΔV|
        AutopilotSigma = 0.002,    // m/s, across ΔV
        PointingSigma = 0.001      // rad, proportional to |ΔV|
    }
};
maneuvering.SetStandbyManeuver(raise);

var withBurn = maneuvering.Propagate(window, bodies, new RK78Integrator(1e-11, 1e-11), true, true,
    TimeSpan.FromSeconds(600.0), new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance });

Time burn = raise.ThrustWindow.Value.StartDate;
Matrix before = withBurn.GetCovariance(burn.AddSeconds(-1.0));
Matrix after = withBurn.GetCovariance(burn);   // the value after the burn, its execution error included
```

- The error is added to the velocity covariance at the burn, in covariance mode only; the trajectory follows the
  commanded ΔV.
- The maneuver is open loop for the covariance: the commanded ΔV is applied as computed on the nominal trajectory, and
  its firing time is not differentiated. A maneuver that retargets its ΔV from the state it finds, as
  `ApogeeHeightManeuver` does at its perigee, removes part of the deviation in reality: in the eclipsing LEO case of the
  validation, Φ after the burn differs from that of the retargeted maneuver by 0.23 to 0.48 per block
  ([Modeling Assumptions](../reference/covariance-propagation.md#modeling-assumptions)).

## A covariance indefinite from rounding

The propagation checks P0 on its correlation matrix and rejects, with `InvalidOperationException`, a covariance that
is not positive semi-definite; it never corrects it. Rounded values can make a covariance indefinite, a correlation of −1
written as −1.0001 for example. `StateVector.WithRemediatedCovariance()` remediates it explicitly, before the
propagation, by the eigenvalue clipping of the conjunction analysis (Hall, Hejduk and Johnson, AAS 17-567):

```csharp
var rounded = new Matrix(6, 6);
for (int i = 0; i < 3; i++)
{
    rounded.Set(i, i, 100.0 * 100.0);
    rounded.Set(i + 3, i + 3, 0.1 * 0.1);
}
rounded.Set(1, 3, -1.0001 * 100.0 * 0.1);   // a correlation of −1 written as −1.0001
rounded.Set(3, 1, -1.0001 * 100.0 * 0.1);
var indefinite = new StateVector(initialState.Position, initialState.Velocity, earth, epoch, Frame.ICRF, rounded);

var remediation = indefinite.WithRemediatedCovariance();
if (remediation.Remediated)
    Console.WriteLine($"Smallest eigenvalue of the correlation matrix: {remediation.SmallestCorrelationEigenvalue:E3}");

var remediated = new Spacecraft(-1003, "SAT3", 100.0, 1000.0, new Clock("SAT3_CLK", 65536), remediation.State,
    sectionalArea: 10.0, dragCoeff: 2.2, solarRadiationCoeff: 1.5);
var fromRemediated = remediated.Propagate(window, bodies, new RK78Integrator(1e-11, 1e-11), true, true,
    TimeSpan.FromSeconds(600.0), new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance });
```

A covariance the propagation already accepts comes back as it is (`Remediated` false). One that rounding does not
explain (a negative variance, an asymmetry beyond the tolerance, a value that is not finite) throws. The remediation has
no bound on the depth of the defect: test `SmallestCorrelationEigenvalue` if you want one
([#443](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/443)).

## To a collision probability

`ConjunctionAssessment.Analyze` propagates to the time of closest approach (TCA) the covariance of each participant that
the library propagated in RK7(8) and whose initial state carries a covariance at the start of that propagation. The
propagation need not carry the variational equations: the analysis replays them along its steps, up to the TCA, and
gives the values of a propagation with the covariance, bit for bit. Two participants in the two-body problem, the
secondary passing 200 m from the protected one 24 h after the start (the case of the validation):

```csharp
var start = new Time(2024, 3, 1, 0, 0, 0, frame: TimeFrame.TDBFrame);
var tca = start.AddHours(24.0);
var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, start);
// protectedOrbit, a LEO at 700 km; secondaryOrbit, 120 m radial and 160 m normal from it at tca, its velocity turned
// by 70° about the radial direction; each with a covariance in RTN, as above (see the smoke test for the geometry)
var protectedCraft = new Spacecraft(-2001, "PROTECTED", 100.0, 1000.0, new Clock("P_CLK", 65536),
    WithRtnCovariance(protectedOrbit, new[] { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 }), sectionalArea: 10.0);
var secondaryCraft = new Spacecraft(-2002, "SECONDARY", 100.0, 1000.0, new Clock("S_CLK", 65536),
    WithRtnCovariance(secondaryOrbit, new[] { 80.0, 300.0, 60.0, 0.3, 0.08, 0.05 }), sectionalArea: 10.0);

// Both propagated in RK7(8), without the variational equations
var propagation = new Window(start, start.AddHours(26.0));
foreach (var craft in new[] { protectedCraft, secondaryCraft })
    craft.Propagate(propagation, new CelestialItem[] { earth }, new RK78Integrator(1e-12, 1e-12), false, false,
        TimeSpan.FromSeconds(60.0));

var profile = new ProtectedSpacecraftProfile(protectedCraft);
var window = new Window(tca.AddHours(-1.0), tca.AddHours(1.0));
var encounter = ConjunctionAssessment.Analyze(profile, secondaryCraft, window);
var held = ConjunctionAssessment.Analyze(profile, secondaryCraft, window,
    new ConjunctionAnalysisOptions { CovariancePropagation = null });

var source = encounter.EncounterState.ProtectedCovariance.Source;   // CovarianceSource.Propagated
```

- `EncounterState.ProtectedCovariance` and `SecondaryCovariance` give the source of each covariance
  (`CovarianceSource`): `Propagated`, `ProvidedAtEncounter`, `InitialStateHeldInRtn` (the participant is not propagated
  in RK7(8): Velocity-Verlet, TLE, SPICE) or `None`; with the age and whether a process noise was applied.
- `CovariancePropagation = null` gives the former behaviour: the initial covariance held fixed in RTN up to the TCA,
  its size not growing, flagged `StaleCovarianceUsed`. On this case (miss distance 151 m) the propagated covariance gives
  a Pc of 2.0e-6 where the covariance held in RTN gave 1.9e-4: the along-track uncertainty grown over 24 h spreads the
  probability, two orders of magnitude lower here.
- The analysis takes its own options for the propagated covariance (`ConjunctionAnalysisOptions.CovariancePropagation`,
  covariance mode by default, no process noise, no consider parameters): set them there to add process noise or consider
  parameters to the covariance at TCA.

## The CDM

```csharp
var cdm = encounter.ToCdm(new CdmExportOptions { Originator = "MyOpsCenter", MessageId = "CDM-001" });
cdm.WriteToFile("encounter.cdm.xml");
```

A propagated covariance is exported with `COVARIANCE_METHOD` `CALCULATED`, the force model of the propagation
(`GRAVITY_MODEL`, `ATMOSPHERIC_MODEL`, `N_BODY_PERTURBATIONS`, `SOLAR_RAD_PRESSURE`) and, with consider parameters,
the `CDRG_*` and `CSRP_*` rows from the extended covariance at TCA; comments give the source of the covariance and the
process noise ([CDM](../reference/cdm.md)). A covariance held in RTN is stale and its export is refused unless
`CdmExportOptions.AllowStaleCovariance` is set.

## Domain of validity

P is the **linear** propagation of the deviations. Measured against a Monte Carlo of 5000 runs with P0 of 100 m and
0.1 m/s on each RTN axis in LEO ([Validation](../guides/validation.md#state-transition-matrix-and-covariance)):

- for about one revolution, P is the covariance of the deviations along every direction of the state, and the
  distribution is Gaussian with covariance P;
- the position, what a collision probability projects on the encounter plane, holds up to about 6 h;
- beyond, the curvature of the orbit bends the along-track spread: at 24 h one direction of the position has 3.3 to 5.0
  times the variance of P, and along one direction of the state the departure is far larger.

For an encounter a day or more after the covariance epoch, the linear covariance underestimates the spread in part of
the encounter plane. `ConjunctionAnalysisOptions.ComputeNonlinearityIndex` flags a propagation that has left what the
linearization assumes; it is off by default, for its cost, and validated on two-body crossing encounters at high relative
velocity only. Confirm a Pc that matters by a Monte Carlo of your own
([Conjunction Assessment](../reference/conjunction-assessment.md#covariance-at-tca)).

## See Also

- [Covariance Propagation](../reference/covariance-propagation.md): options, results, units, exceptions, cost
- [Covariance Provenance](../reference/covariance-propagation-provenance.md): equations, references, tests
- [Conjunction Assessment tutorial](conjunction-assessment.md) and
  [reference](../reference/conjunction-assessment.md)
- [Validation](../guides/validation.md#state-transition-matrix-and-covariance)
