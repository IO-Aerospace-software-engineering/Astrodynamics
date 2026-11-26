# Occultations and eclipses

`FindOccultationWindows` finds the windows when a front body occults a target (the back body) as an observer sees
them. It is a method of the observer (`WindowSearches`, an extension of every `ILocalizable`), its settings one
`OccultationSearchOptions`. An eclipse is the occultation of the Sun.

```csharp
// Inputs: window (Window), spacecraft (Spacecraft)
var earth = new CelestialBody(PlanetsAndMoons.EARTH);
var sun = new CelestialBody(Stars.Sun);
// spacecraft: propagated, from a TLE, or from the kernels
var options = new OccultationSearchOptions { Step = TimeSpan.FromSeconds(60.0), FrontFrame = new Frame("IAU_EARTH") };
var penumbra = spacecraft.FindOccultationWindows(window, sun, earth, options);
var umbra = spacecraft.FindOccultationWindows(window, sun, earth, options with { OccultationType = OccultationType.Full });
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

`OccultationSearchOptions.TargetShape` and `FrontShape` default (null) to an ellipsoid for a body with radii and to a
point otherwise, and `OccultationType` to `Any`. A body modeled as `ShapeType.Ellipsoid` is the triaxial ellipsoid of its radii (`CelestialBody.Radii`, the
`BODYnnn_RADII` of the kernels) along the axes of a body-fixed frame: the body's own frame by default (for the Earth,
`ITRF93` when `earth_assoc_itrf93.tf` is loaded, `IAU_EARTH` otherwise), or `OccultationSearchOptions.FrontFrame` and
`TargetFrame`, the `fframe` and `bframe` of `gfoclt`. A sphere (the Sun and the Moon of `pck00011`) needs no frame.

## Two paths

- **CSPICE.** When the observer, the target and the front body are in the kernels, the observer is not a site and the
  observer or the target is a spacecraft, `gfoclt` searches, with its convergence tolerance of 1 µs (a
  `ConvergenceTolerance` given there throws `ArgumentException`), each body with the shape given (before 11.0.0 a point
  front body reached it as an ellipsoid, and the search failed: #496).
- **Managed.** Otherwise the library searches: a propagated spacecraft, a spacecraft from a TLE, a site observer, two
  natural bodies. A site observer is the library's whatever the site (lot L6 of #668, choice 4): before 11.0.0
  `Site.FindWindowsOnOccultationConstraint` gave a site, a spacecraft target and a front body of the kernels to
  `gfoclt`, whose edges differ from the library's by up to 5.0e-7 s geometric and 6.2e-4 s with the light time (LRO
  behind the Moon from DSS-13; see Accuracy). It reads the states and the orientations (the kernels through the shared lock of `SpiceApi`), never writes a
  kernel, and keeps no state, so searches on separate propagations may run concurrently.

The managed search follows the method of CSPICE's `zzocced`: a linear map turns one ellipsoid into the unit sphere,
keeping every incidence of the apparent discs, and three continuous margins measure the overlap of the discs and the
containment of each in the other. Brent's method locates each zero of a margin to
`OccultationSearchOptions.ConvergenceTolerance` (1 µs when null, `gfoclt`'s). A window that holds at the start or the
end of the search window, or whose transition lies within the tolerance of them, starts or ends there; no state outside
the search window is read.

**Step.** The margins are sampled at every step; between three samples that show an extreme not reaching zero, the
extreme is searched, so an event shorter than the step is found as long as the step is at most half the time between
the extremes of the margins. For the eclipses of a satellite, a quarter of the period finds a penumbra of a few
seconds. `gfoclt` asks the step to be shorter than every event and every gap.

`OccultationSearchOptions.Step` is required, `ArgumentException` (`options`) naming it when null, but on the library's
search of a propagated spacecraft or a spacecraft from a TLE, observer or target, whose centre of propagation is the
front body (decision 106 of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668),
decided by S. Guillet, 2026-11-25). There the period P is the smallest period of its orbit about that body over the
search window (the osculating period at the window start and after each impulse inside it; the mean motion of a TLE):
a null step is P/16, capped at half the search window, and a step above P/2 throws `ArgumentOutOfRangeException`
(`options`). The 16 eclipses of an equatorial LEO day at the equinox are found at P/16 and at P/2, within 2.0e-7 s of
a 1 min step. Everywhere else, the CSPICE path, a natural body or a hyperbolic orbit included, the step given is used
as it is.

**Labels.** The windows are labelled in the scale of the search window's start (lot L6 of #668): an end at a bound of
the search window is that bound, every other end the same instant in that scale, but an end inside a UTC leap second,
which no UTC label holds, keeps its TDB label. Both paths sample and solve in TDB seconds. On a UTC or TT search window
`Window.Length` counts TT seconds (#508); before 11.0.0 both paths returned TDB windows.

**Resolution.** The search for an extreme between samples is Brent's `localmin`, whose abscissa tolerance is
√ε |x| + τ/3 (|x| up to the step h from the middle of its bracket, τ the convergence tolerance), and it stops only at a
margin strictly past zero. A window or a gap narrower than about 2 √ε h + 2τ/3 can therefore be missed: 2.5 µs at a
1 min step, 19 µs at 10 min, 41 µs at 22.5 min with τ = 1 µs; the bound scales with
`OccultationSearchOptions.ConvergenceTolerance`. A tangency between two samples (an extreme exactly at zero) is not
found. On a closed-form margin of period 5400 s, every window was found down to 0.3 µs at a 1 min step and 10 µs at 10
and 22.5 min, every gap down to 0.3 µs, 1 µs and 10 µs (#668, `OccultationFinderResolutionTests`, Linux x64).

**Aberration.** `None`, or the light time (`LT`, `CN`, `XLT`, `XCN`) as `gfoclt`: each body's position and frame at its
corrected epoch. The stellar aberration of the `+S` corrections is ignored, as `gfoclt` does. A `Star` is a direction,
read at the epoch whatever the correction, as the [field-of-view search](spacecraft.md) reads it: its catalog position
moved by its proper motion to the epoch is the direction of the light that reaches the barycenter then (as SOFA's
`iauPmsafe` reads a catalog, confirmed by S. Guillet, 2026-11-11), so no light time is applied to it (#533). Before, it was read
at t − lt, 65 years at 20 pc: a proper motion of 0.1″/yr moved a lunar occultation by 9.6 s, and from a site the Earth was
read there too, before the start of de440s beyond about 54 pc, which threw once the reads of #454 threw.

## Accuracy

Against `gfoclt` converged to 1e-9 s (`tools/occultation_reference`), the first four rows the same on Linux x64, Windows
x64 and macOS arm64 (CI run 43773937161, 2026-11-15):

| Scenario | Worst edge |
|----------|-----------:|
| LEO, GEO eclipse season, grazing LEO, LRO behind the Moon, solar eclipses from the Earth and from DSS-13 and DSS-65 (geometric, 100 ns tolerance) | 1.2e-7 s |
| The same at the default tolerance, against `gfoclt` at its default | 6.0e-7 s |
| Eclipse 001 with the light time (`LT`, `CN`, `XLT`, `XCN`) | 1.3e-6 s |
| Eclipse 001 of a propagated spacecraft, against GMAT R2026a's `EclipseLocator` (epochs to the millisecond) | 4.8e-4 s |
| A star at 20 and 100 pc behind the Moon from DSS-13, with and without μα* = 0.1″/yr of proper motion, `None`, `LT`, `CN`, `XLT`, `XCN`, against the closed form of `tools/occultation_reference/star_occultation_reference.py` (2026-11-11; its star moved along its straight line since #718, 2026-11-16, Linux x64: 1.91e-6 s for 0.1″/yr, its directions 1.30e-16 rad) | 2.38e-6 s (1.43e-6 s without `XLT` and `XCN`) |

With the light time, a spacecraft of the kernels taking part, the library's search and `gfoclt` differ by more:
LRO behind the Moon over 12 h at a 60 s step, the same windows with each edge within 5.0e-7 s geometric, 3.7e-4 s with
`LT`, 1.2e-4 s with `CN` and 6.2e-4 s with `XLT` from DSS-13 (3.5e-4 s, 1.2e-4 s and 5.9e-4 s from the Earth, `gfoclt`
searching there). The states agree (`GetEphemeris` equals `spkezr` to 0 m); at the library's edges the margin is below
1e-12 rad, at `gfoclt`'s about 4e-10 rad, so the two light-time models differ, and which one is right is open
([#828](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/828), under **Known limitations** of
11.0.0).

The last row includes the difference of the Earth GM of the two trajectories (the kernel's, GMAT's 398600.4415
km³/s²), 3.2e-4 s after 12 h; measured again with the TT time argument of the propagation
([#501](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/501)), 4.83e-4 s (the three platforms, CI
run 43773937161, 2026-11-15).
The references of `tools/occultation_reference` are two-body SPKs propagated by `conics_c` over ET, TDB seconds about
the Earth: they depart from the time argument of the library by the change of TDB − TT, at most 3.6e-6 s over their 12 h,
within the bounds of the tests (5.0e-5 s and more), and are kept as they are.

`CelestialItem.IsOcculted(by, from, aberration)` gives the type at one instant with the same geometry.

Before 11.0.0 the managed search modeled the bodies as spheres of their equatorial radius, sampled a boolean condition
and refined it to about 1 s: its windows were off by up to 0.16 s and could start before the search window.
