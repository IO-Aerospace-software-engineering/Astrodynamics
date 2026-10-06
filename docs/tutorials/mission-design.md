# Mission Design

This tutorial covers higher-level mission design tools: the Lambert solver for
transfer orbit computation, launch window analysis, the Mission/Scenario
simulation framework, spacecraft rendezvous sequences, and kernel-backed
spacecraft for archived missions.

## Lambert solver

The Lambert problem finds the transfer orbit connecting two position vectors
in a given flight time. IO.Astrodynamics provides a solver that handles both
short-way and long-way transfers with multi-revolution solutions.

```csharp
var solver = new LambertSolver();

// Solve for the transfer from departure to arrival position
var result = solver.Solve(
    shortWay: false,       // long-way transfer
    departure,             // StateVector at departure
    arrival,               // StateVector at arrival
    earth,                 // central body
    nRevolutions: 0);      // zero extra revolutions

// Extract the zero-revolution solution
var solution = result.GetZeroRevolutionSolution();

Console.WriteLine($"Departure V: {solution.DepartureVelocity.Magnitude():F2} m/s");
Console.WriteLine($"Arrival   V: {solution.ArrivalVelocity.Magnitude():F2} m/s");
```

The solver returns departure and arrival velocity vectors. Subtract the
spacecraft's current velocity to obtain the required delta-v at each end.

### Multi-revolution solutions

For longer flight times, multiple revolution solutions may exist:

```csharp
var result = solver.Solve(false, departure, arrival, earth, nRevolutions: 2);

foreach (var sol in result.Solutions)
{
    Console.WriteLine($"Revolutions: {sol.Revolutions}, " +
                      $"DV: {sol.TotalDeltaV:F2} m/s");
}
```

## Launch window computation

The `Launch` class computes launch windows that place a vehicle into (or near)
a target orbit from a given launch site, accounting for Earth rotation, site
latitude constraints, and lighting conditions.

```csharp
var launch = new Launch(
    launchSite,              // Site object for the pad
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
// Create mission
var mission = new Mission("LunarExplorer");

// Define scenario with time window
var scenario = new Scenario("Phase1", mission, new Window(start, end));

// Add celestial bodies to the environment
scenario.AddCelestialItem(earth);
scenario.AddCelestialItem(moon);

// Add spacecraft (with maneuvers already configured)
scenario.AddSpacecraft(spacecraft);

// Run simulation
var summary = await scenario.SimulateAsync(
    includeAtmosphericDrag: false,
    includeSolarRadiationPressure: false,
    TimeSpan.FromSeconds(1.0));
```

`SimulateAsync` propagates all spacecraft in the scenario, applies maneuvers,
and returns a summary with ephemeris data.

### Choosing the integrator

By default, `SimulateAsync` integrates with Velocity-Verlet, using `propagatorStepSize` as step. Pass a factory to
use another integrator, for example the adaptive RK7(8):

```csharp
var summary = await scenario.SimulateAsync(
    includeAtmosphericDrag: false,
    includeSolarRadiationPressure: false,
    TimeSpan.FromSeconds(10.0),
    () => new RK78Integrator(absoluteTolerance: 1e-11, relativeTolerance: 1e-11));
```

The factory is called once per spacecraft and must return a new instance each time, because the propagator adds
the force model of each spacecraft to its integrator. `propagatorStepSize` remains the step of the propagated
states. See [Integrators](../reference/integrators.md#choosing-the-integrator).

### Multiple scenarios

A mission can contain sequential or alternative scenarios:

```csharp
var mission = new Mission("TransferStudy");

var nominalScenario = new Scenario("Nominal", mission, nominalWindow);
nominalScenario.AddSpacecraft(spacecraft);

var backupScenario = new Scenario("Backup", mission, backupWindow);
backupScenario.AddSpacecraft(spacecraftBackup);

// Simulate each independently
var nominalResult = await nominalScenario.SimulateAsync(false, false, TimeSpan.FromSeconds(10));
var backupResult = await backupScenario.SimulateAsync(false, false, TimeSpan.FromSeconds(10));
```

## Spacecraft rendezvous

A rendezvous sequence chains multiple maneuvers to match the target orbit.
The typical sequence is: plane alignment, apsidal alignment, phasing, then
orbit circularization.

```csharp
// Build the maneuver chain
var maneuver1 = new PlaneAlignmentManeuver(
    new Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero,
    targetOrbit, engine);

maneuver1
    .SetNextManeuver(new ApsidalAlignmentManeuver(
        new Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero,
        targetOrbit, engine))
    .SetNextManeuver(new PhasingManeuver(
        new Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero,
        targetOrbit, 3, engine))
    .SetNextManeuver(new ApogeeHeightManeuver(
        new Time(DateTime.MinValue, TimeFrame.TDBFrame), TimeSpan.Zero,
        targetOrbit, engine));

// Arm the maneuver chain on the spacecraft
spacecraft.SetStandbyManeuver(maneuver1);

// Propagate --- maneuvers fire automatically via event detection
var scenario = new Scenario("Rendezvous", mission, window);
scenario.AddCelestialItem(earth);
scenario.AddSpacecraft(spacecraft);
var result = await scenario.SimulateAsync(false, false, TimeSpan.FromSeconds(1));
```

Each maneuver in the chain fires at the correct orbital location determined by
its g-function. See the [Maneuvers tutorial](maneuvers.md) for the g-function
reference table.

### Adding attitude maneuvers

Insert attitude maneuvers before impulse burns to orient the spacecraft:

```csharp
var attitude = new ProgradeAttitude(
    earth, new Time(DateTime.MinValue, TimeFrame.TDBFrame),
    TimeSpan.FromMinutes(5), engine);

attitude.SetNextManeuver(maneuver1);
spacecraft.SetStandbyManeuver(attitude);
```

Attitude maneuvers execute immediately when the chain reaches them, then
pass control to the next impulse maneuver in the chain.

## Kernel-backed spacecraft

For archived missions with SPICE kernels available, create a spacecraft
directly from its NAIF ID. The framework reads ephemeris, attitude, and
instrument data from loaded kernels:

```csharp
var lro = new Spacecraft(
    naifId: -85,
    name: "LRO",
    epoch: new Time(2024, 6, 1, 12, 0, 0));

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
