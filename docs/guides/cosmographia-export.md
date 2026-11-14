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

## Scenario Export

`CosmographiaExporter.ExportAsync(scenario, outputDirectory)` writes a simulated scenario to
`<outputDirectory>/<Mission>_<Scenario>`, which it replaces: the kernels of each spacecraft (clock, frame, CK, SPK and
instruments), site and star, and the Cosmographia catalog files, among them `spice.json`, which lists the kernels.
`astro propagate` exports the same way.

The layout adds to the output directory `/<Mission>_<Scenario>/Spacecrafts/<name>/Orientation/<name>.ck` for the CK of
a spacecraft, usually the longest kernel: 30 characters with the three names. SPICE takes a kernel path of at most 255
characters, so the export throws `ArgumentException` (parameter `outputDirectory`) for a directory whose kernels would
be longer, before it deletes or writes anything
([#673](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/673)); choose a shorter directory.

!!! info "Kernel compatibility"
    Ensure the Cosmographia session loads the same planetary and leap-second kernels used during propagation so that frames and epochs are consistent.

## See Also

- [Spacecraft](../reference/spacecraft.md)
- [Kernel Management](kernel-management.md)
