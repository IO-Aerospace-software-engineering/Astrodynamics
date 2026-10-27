# Occultations and eclipses

`FindWindowsOnOccultationConstraint` finds the windows when a front body occults a target (the back body) as an
observer sees them. It is a method of the target (`CelestialItem`, the observer as argument) and of a `Site` (the site
as observer). An eclipse is the occultation of the Sun.

```csharp
var earth = new CelestialBody(PlanetsAndMoons.EARTH);
var sun = new CelestialBody(Stars.Sun);
// spacecraft: propagated, from a TLE, or from the kernels
var options = new OccultationSearchOptions { FrontFrame = new Frames.Frame("IAU_EARTH") };
var penumbra = sun.FindWindowsOnOccultationConstraint(window, spacecraft, ShapeType.Ellipsoid, earth, ShapeType.Ellipsoid,
    OccultationType.Any, Aberration.None, TimeSpan.FromSeconds(60.0), options);
var umbra = sun.FindWindowsOnOccultationConstraint(window, spacecraft, ShapeType.Ellipsoid, earth, ShapeType.Ellipsoid,
    OccultationType.Full, Aberration.None, TimeSpan.FromSeconds(60.0), options);
```

## Types

The types are those of CSPICE's `gfoclt`:

| Type | Condition |
|------|-----------|
| `Full` | the target is hidden: its disc lies within the front body's |
| `Annular` | the front body's disc lies within the target's |
| `Partial` | the front body hides part of the target's limb |
| `Any` | any of the three |
| `None` | the rest of the search window, the complement of `Any` |

A point target can only be hidden (`Full` or `Any`), a point front body can only transit (`Annular` or `Any`); the other
types give no window. Both bodies cannot be points.

## Shapes and frames

A body modeled as `ShapeType.Ellipsoid` is the triaxial ellipsoid of its radii (`CelestialBody.Radii`, the
`BODYnnn_RADII` of the kernels) along the axes of a body-fixed frame: the body's own frame by default (for the Earth,
`ITRF93` when `earth_assoc_itrf93.tf` is loaded, `IAU_EARTH` otherwise), or `OccultationSearchOptions.FrontFrame` and
`TargetFrame`, the `fframe` and `bframe` of `gfoclt`. A sphere (the Sun and the Moon of `pck00011`) needs no frame.

## Two paths

- **CSPICE.** When the observer and the target are in the kernels and one of them is a spacecraft (or, for a site, the
  target is a spacecraft of the kernels), `gfoclt` searches, with its convergence tolerance of 1 µs.
- **Managed.** Otherwise the library searches: a propagated spacecraft, a spacecraft from a TLE, a site, two natural
  bodies. It reads the states and the orientations (the kernels through the shared lock of `SpiceAPI`), never writes a
  kernel, and keeps no state, so searches on separate propagations may run concurrently.

The managed search follows the method of CSPICE's `zzocced`: a linear map turns one ellipsoid into the unit sphere,
keeping every incidence of the apparent discs, and three continuous margins measure the overlap of the discs and the
containment of each in the other. Brent's method locates each zero of a margin to
`OccultationSearchOptions.ConvergenceTolerance` (1 µs by default, `gfoclt`'s). A window that holds at the start or the
end of the search window, or whose transition lies within the tolerance of them, starts or ends there; no state outside
the search window is read.

**Step.** The margins are sampled at every step; between three samples that show an extreme not reaching zero, the
extreme is searched, so an event shorter than the step is found as long as the step is at most half the time between
the extremes of the margins. For the eclipses of a satellite, a quarter of the period finds a penumbra of a few
seconds. `gfoclt` asks the step to be shorter than every event and every gap.

**Aberration.** `None`, or the light time (`LT`, `CN`, `XLT`, `XCN`) as `gfoclt`: each body's position and frame at its
corrected epoch. The stellar aberration of the `+S` corrections is ignored, as `gfoclt` does.

## Accuracy

Against `gfoclt` converged to 1e-9 s (`tools/occultation_reference`), on Linux x64 (2026-10-27):

| Scenario | Worst edge |
|----------|-----------:|
| LEO, GEO eclipse season, grazing LEO, LRO behind the Moon, solar eclipses from the Earth and from DSS-13 and DSS-65 (geometric, 100 ns tolerance) | 1.2e-7 s |
| The same at the default tolerance, against `gfoclt` at its default | 7.0e-7 s |
| Eclipse 001 with the light time (`LT`, `CN`, `XLT`, `XCN`) | 1.3e-6 s |
| Eclipse 001 of a propagated spacecraft, against GMAT R2026a's `EclipseLocator` (epochs to the millisecond) | 4.8e-4 s |

The last row includes the difference of the Earth GM of the two trajectories (the kernel's, GMAT's 398600.4415
km³/s²), 3.2e-4 s after 12 h.

`CelestialItem.IsOcculted(by, from, aberration)` gives the type at one instant with the same geometry.

Before 10.2.0 the managed search modeled the bodies as spheres of their equatorial radius, sampled a boolean condition
and refined it to about 1 s: its windows were off by up to 0.16 s and could start before the search window.
