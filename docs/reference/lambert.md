# Lambert Solver

The Lambert solver computes transfer orbits between two positions and a time of flight, enabling interplanetary and rendezvous mission design.

## LambertSolver

`LambertSolver` finds departure and arrival velocities that satisfy the Lambert boundary-value problem. It supports multi-revolution transfers via Battin, Lagrange, and Lancaster formulations with Halley root-finding.

### Solve Method

```csharp
public LambertResult Solve(
    bool isRetrograde,
    OrbitalParameters initialOrbitalParameters,
    OrbitalParameters targetObjectOrbitalParameters,
    CelestialItem centerOfMotion,
    ushort maxRevolution)
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `isRetrograde` | `bool` | Whether the transfer is retrograde |
| `initialOrbitalParameters` | `OrbitalParameters` | Spacecraft state at departure |
| `targetObjectOrbitalParameters` | `OrbitalParameters` | Target state at arrival |
| `centerOfMotion` | `CelestialItem` | Central body for the transfer |
| `maxRevolution` | `ushort` | Maximum number of full revolutions |

## LambertResult

| Member | Description |
|--------|-------------|
| `Solutions` | `IReadOnlyCollection<LambertSolution>` of all computed transfer solutions |
| `MaxRevolutions` | Maximum revolutions used |
| `GetZeroRevolutionSolution()` | Get the direct (zero-revolution) solution |
| `GetMultiRevolutionSolutions(uint n)` | Get solutions for a given revolution count |

## LambertSolution

Each solution contains the velocity vectors and delta-V information for one transfer arc.

| Property | Type | Description |
|----------|------|-------------|
| `V1` | `Vector3` | Departure velocity (m/s) |
| `V2` | `Vector3` | Arrival velocity (m/s) |
| `DeltaV1` | `Vector3` | Required delta-V at departure |
| `DeltaV2` | `Vector3` | Required delta-V at arrival |
| `DeltaV` | `double` | Total delta-V magnitude (m/s) |
| `Revolutions` | `uint` | Number of full revolutions |
| `Branch` | `LambertBranch?` | `Left` or `Right` branch (null for direct transfer) |
| `Iterations` | `uint` | Number of solver iterations |

## Example

```csharp
var solver = new LambertSolver();
var result = solver.Solve(
    isRetrograde: false,
    initialOrbitalParameters: departureState,
    targetObjectOrbitalParameters: arrivalState,
    centerOfMotion: earth,
    maxRevolution: 0);

var solution = result.GetZeroRevolutionSolution();
var transferOrbit = new StateVector(
    departureState.ToStateVector().Position, solution.V1,
    earth, departureState.Epoch, Frame.ICRF);

Console.WriteLine($"Total Delta-V: {solution.DeltaV:F1} m/s");
```

## See Also

- [StateVector](state-vector.md)
- [Impulse Maneuvers](impulse-maneuvers.md)
- [Mission Design](../tutorials/mission-design.md)
