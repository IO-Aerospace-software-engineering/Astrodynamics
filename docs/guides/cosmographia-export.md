# Cosmographia Export

Propagated trajectories can be exported as SPICE SPK kernels for visualization in [JPL Cosmographia](https://naif.jpl.nasa.gov/naif/cosmographia.html) or other SPICE-compatible tools.

## Workflow

1. **Propagate** the spacecraft trajectory using `CentralBodyPropagator` or any other propagator.
2. **Write the ephemeris** to an SPK file using `Spacecraft.WriteEphemeris()`.
3. **Load the SPK** in Cosmographia (or another SPICE viewer) alongside the standard planetary kernels.

## Example

```csharp
// Propagate
var solution = propagator.Propagate();

// Export trajectory as SPK kernel
spacecraft.WriteEphemeris(new FileInfo("output/mission.bsp"));
```

The exported SPK uses the spacecraft's NAIF ID and covers the propagated time window. Cosmographia can then render the trajectory relative to any loaded celestial body.

!!! info "Kernel compatibility"
    Ensure the Cosmographia session loads the same planetary and leap-second kernels used during propagation so that frames and epochs are consistent.

## See Also

- [Spacecraft](../reference/spacecraft.md)
- [Kernel Management](kernel-management.md)
