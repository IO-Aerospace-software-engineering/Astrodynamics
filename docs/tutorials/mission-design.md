# Mission Design

This tutorial covers higher-level mission design tools: the Lambert solver for
transfer orbit computation, launch window analysis, the Mission/Scenario
simulation framework, spacecraft rendezvous sequences, and kernel-backed
spacecraft for archived missions.

## Lambert solver

The Lambert problem finds the transfer orbit connecting two position vectors
in a given flight time. IO.Astrodynamics provides a solver of prograde or retrograde
transfers, from zero to a given number of full revolutions, each multi-revolution
count with its two branches (`LambertBranch.Left` and `Right`).

```csharp
// Inputs: departure and arrival (StateVector), earth (CelestialBody)
var solver = new LambertSolver();

// Solve for the transfer from departure to arrival position
var result = solver.Solve(
    isRetrograde: false,   // prograde transfer
    departure,             // StateVector at departure
    arrival,               // StateVector at arrival
    earth,                 // central body
    maxRevolution: 0);     // zero full revolutions

// Extract the zero-revolution solution
var solution = result.GetZeroRevolutionSolution();

Console.WriteLine($"Departure V: {solution.V1.Magnitude():F2} m/s");
Console.WriteLine($"Arrival   V: {solution.V2.Magnitude():F2} m/s");
```

The solver returns departure and arrival velocity vectors. Subtract the
spacecraft's current velocity to obtain the required delta-v at each end.

### Multi-revolution solutions

For longer flight times, multiple revolution solutions may exist:

```csharp
// Inputs: solver (LambertSolver), departure and arrival (StateVector), earth (CelestialBody)
var result = solver.Solve(false, departure, arrival, earth, maxRevolution: 2);

foreach (var sol in result.Solutions)
{
    Console.WriteLine($"Revolutions: {sol.Revolutions} {sol.Branch}, " +
                      $"DV: {sol.DeltaV:F2} m/s");
}
```

## Launch window computation

The `Launch` class computes launch windows that place a vehicle into (or near)
a target orbit from a given launch site, accounting for Earth rotation, site
latitude constraints, and lighting conditions.

```csharp
// Inputs: launchSite (LaunchSite), recoverySite (Site), targetOrbit (OrbitalParameters), searchWindow (Window)
var launch = new Launch(
    launchSite,              // LaunchSite of the pad
    recoverySite,            // Site for abort/recovery
    targetOrbit,             // desired orbital parameters
    Constants.CivilTwilight, // minimum solar elevation for launch
    true);                   // ascending node launch

var windows = launch.FindLaunchWindows(searchWindow);

foreach (var w in windows)
{
    Console.WriteLine($"Launch at {w.InertialAzimuth * Constants.Rad2Deg:F1} deg " +
                      $"from {w.Window.StartDate} to {w.Window.EndDate}");
}
```

Each `LaunchWindow` includes the inertial launch azimuth, the non-inertial
(ground-track) azimuth, and the insertion state vector.

## Mission and Scenario

The `Mission` and `Scenario` classes provide a top-level simulation container.
A mission groups one or more scenarios; each scenario defines a time window,
celestial environment, and spacecraft to propagate.

```csharp
// Inputs: start and end (Time), earth and moon (CelestialBody), spacecraft (Spacecraft)
// Create mission
var mission = new Mission("LunarExplorer");

// Define scenario with time window
var scenario = new Scenario("Phase1", mission, new Window(start, end));

// Add celestial bodies to the environment: one per NAIF identifier (a second body of an identifier is ignored when its
// models equal those of the first, and throws ArgumentException otherwise)
scenario.AddCelestialItem(earth);
scenario.AddCelestialItem(moon);

// Add spacecraft (with maneuvers already configured)
scenario.AddSpacecraft(spacecraft);

// Run simulation
var summary = scenario.Simulate();
```

`Simulate` propagates all spacecraft in the scenario, in turn, applies maneuvers,
and returns a summary with ephemeris data. Without settings it runs the defaults
of `PropagationOptions`: RK7(8) at 1e-9, an output every 60 s, the bodies of the
scenario as the third bodies of each spacecraft.
`Simulate(options, cancellationToken)` stops at the token: no summary, `IsSimulated` false, the spacecraft
propagated before the stop complete ([Propagators](../reference/propagators.md#cancellation)).

### Choosing the integrator

`PropagationOptions.Integrator` chooses the integrator of every spacecraft, for example the adaptive RK7(8) at tighter
tolerances, or Velocity-Verlet at its own step:

```csharp
// Inputs: scenario (Scenario)
var summary = scenario.Simulate(new PropagationOptions
{
    Integrator = new RK78IntegratorOptions { AbsoluteTolerance = 1e-11, RelativeTolerance = 1e-11 },
    OutputStep = TimeSpan.FromSeconds(10.0)
});
```

Each spacecraft's propagation builds its own integrator from the options, so one value serves them all. `OutputStep`
is the step of the propagated states only. See [Integrators](../reference/integrators.md#choosing-the-integrator).
(The asynchronous `SimulateAsync` of 10.x, which stepped Velocity-Verlet at the output step, was removed in 11.0.0:
`new VVIntegratorOptions { Step = step }` with `OutputStep = step` keeps its results bit for bit.)

### Multiple scenarios

A mission can contain sequential or alternative scenarios:

```csharp
// Inputs: nominalWindow and backupWindow (Window), spacecraft and spacecraftBackup (Spacecraft)
var mission = new Mission("TransferStudy");

var nominalScenario = new Scenario("Nominal", mission, nominalWindow);
nominalScenario.AddSpacecraft(spacecraft);

var backupScenario = new Scenario("Backup", mission, backupWindow);
backupScenario.AddSpacecraft(spacecraftBackup);

// Simulate each independently
var options = new PropagationOptions { OutputStep = TimeSpan.FromSeconds(10) };
var nominalResult = nominalScenario.Simulate(options);
var backupResult = backupScenario.Simulate(options);
```

## Spacecraft rendezvous

A rendezvous sequence chains multiple maneuvers to match the target orbit.
The typical sequence is: plane alignment, apsidal alignment, phasing, then
orbit circularization.

```csharp
// Inputs: targetOrbit (OrbitalParameters), engine (Engine), spacecraft (Spacecraft), mission (Mission), window
//   (Window), earth (CelestialBody)
// Build the maneuver chain
var maneuver1 = new PlaneAlignmentManeuver(
    new Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero,
    targetOrbit, engine);

maneuver1
    .SetNextManeuver(new ApsidalAlignmentManeuver(
        new Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero,
        targetOrbit, engine))
    .SetNextManeuver(new PhasingManeuver(
        new Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero,
        targetOrbit, 3, engine))
    .SetNextManeuver(new ApogeeHeightManeuver(
        new Time(DateTime.MinValue, TimeScale.TDB), TimeSpan.Zero,
        targetOrbit, engine));

// Arm the maneuver chain on the spacecraft
spacecraft.SetStandbyManeuver(maneuver1);

// Propagate --- maneuvers fire automatically via event detection
var scenario = new Scenario("Rendezvous", mission, window);
scenario.AddCelestialItem(earth);
scenario.AddSpacecraft(spacecraft);
var result = scenario.Simulate(new PropagationOptions { OutputStep = TimeSpan.FromSeconds(1) });
```

Each maneuver in the chain fires at the correct orbital location determined by
its g-function. See the [Maneuvers tutorial](maneuvers.md) for the g-function
reference table.

### Adding attitude maneuvers

Insert attitude maneuvers before impulse burns to orient the spacecraft:

```csharp
// Inputs: earth (CelestialBody), engine (Engine), maneuver1 (Maneuver), spacecraft (Spacecraft)
var attitude = new ProgradeAttitude(
    earth, new Time(DateTime.MinValue, TimeScale.TDB),
    TimeSpan.FromMinutes(5), engine);

attitude.SetNextManeuver(maneuver1);
spacecraft.SetStandbyManeuver(attitude);
```

The attitude activates at the start of the propagation and is held 5 minutes: the first
impulse maneuver of the chain fires at its first crossing after that hold. The attitude
stays in force until the impulse's own attitude takes over at the opening of its thrust
window.

## Kernel-backed spacecraft

For archived missions with SPICE kernels available, create a spacecraft
directly from its NAIF ID. The framework reads ephemeris, attitude, and
instrument data from loaded kernels:

```csharp
// Inputs: epoch (Time), moon (CelestialBody)
var lro = new Spacecraft(
    naifId: -85,
    name: "LRO",
    epoch: new Time(2024, 6, 1, 12, 0, 0, TimeScale.TDB));

// Query ephemeris directly from SPICE kernels
var state = lro.GetEphemeris(epoch, moon, Frame.ICRF, Aberration.LT);
```

This is useful for validation against published mission data, or for
computing geometry relative to operational spacecraft whose kernels are
publicly available (e.g., from NAIF's archive).

## Summary

- The **Lambert solver** finds transfer orbits between two positions with
  support for multi-revolution solutions.
- **Launch windows** account for site geometry, Earth rotation, and lighting.
- **Mission/Scenario** provides a high-level simulation container for
  organizing multi-phase campaigns.
- **Rendezvous** sequences chain plane alignment, apsidal alignment, phasing,
  and circularization maneuvers.
- **Kernel-backed spacecraft** let you query archived SPICE ephemeris for
  validation or operational analysis.
