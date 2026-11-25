# Cosmographia Export

Propagated trajectories can be exported as SPICE SPK kernels for visualization in [JPL Cosmographia](https://naif.jpl.nasa.gov/naif/cosmographia.html) or other SPICE-compatible tools.

## Workflow

1. **Propagate** the spacecraft trajectory using `CentralBodyPropagator` or any other propagator.
2. **Write the ephemeris** to an SPK file using `Spacecraft.WriteEphemeris()`.
3. **Load the SPK** in Cosmographia (or another SPICE viewer) alongside the standard planetary kernels.

## Example

```csharp
// Inputs: propagator (CentralBodyPropagator), spacecraft (Spacecraft)
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
`astro propagate` exports the same way ([command-line tool](../reference/cli.md)).

The export checks its names first: each spacecraft, instrument, site and star name, and the export directory
`<Mission>_<Scenario>` the mission and scenario names form, make directories and files, so each is a portable file name,
the same on Linux, macOS and Windows: not empty, none of `/ \ : * ? " < > |` nor a control character, not `.` or `..`, no
trailing dot or space, and not a device name Windows reserves (CON, PRN, AUX, NUL, COM1 to COM9, LPT1 to LPT9, in any case,
alone or before a dot). A name that is not throws `InvalidOperationException` before anything is deleted or written
([#799](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/799),
[#822](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/822); before 11.0.0 a name holding a `/`
was written into nested directories, and a scenario named `/..` made the export delete the whole output directory). The
constructors of the objects do not check it: rename the object before the export.

The export then writes its kernels and its catalog into a new scratch directory, and replaces the previous export only
once everything is written: a writer that throws (a kernel variable SPICE cannot read, a spacecraft without stored states
or attitude record, a star of fewer than three stored states) or the catalog that throws leaves the previous export as it
was (#799; before 11.0.0 the previous export was deleted first). A failure while the previous export is deleted or the new
one copied, a full disk for instance, leaves part of either. The scratch directory is deleted in every case, and the
export leaves the kernels SPICE holds as they were: the clock and frame kernels it loads while it writes a CK are unloaded
after.

The layout adds to the output directory `/<Mission>_<Scenario>/Spacecrafts/<name>/Orientation/<name>.ck` for the CK of
a spacecraft, usually the longest kernel: 31 characters plus the mission and scenario names and twice the spacecraft
name. SPICE takes a kernel path of at most 255 characters, so the export throws `ArgumentException` (parameter
`outputDirectory`) for a directory whose kernels would be longer, before it deletes or writes anything
([#673](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/673)); choose a shorter directory.
The scratch directory is a new directory of the system temporary directory (`Path.GetTempPath()`: `TMPDIR` on Linux and
macOS, `TEMP` on Windows), where the export loads the clock and frame kernels of each spacecraft; their paths there, `<temp>/<6 to 12 characters>/Spacecrafts/<name>/Orientation/<name>.ck` for the CK, are
checked too, and the export throws `ArgumentException` without a parameter name when the temporary directory is the
longer one: choose a shorter temporary directory.

!!! info "Kernel compatibility"
    Ensure the Cosmographia session loads the same planetary and leap-second kernels used during propagation so that frames and epochs are consistent.

## See Also

- [Spacecraft](../reference/spacecraft.md)
- [Kernel Management](kernel-management.md)
