# Stars

## Star

A star of a catalog: its position at the catalog epoch, its proper motion and its parallax, moved along a straight line
at constant speed to any epoch. `Star` derives from `CelestialBody`; its NAIF identifier is 1 000 000 000 plus its
catalog number.

### Constructor

```csharp-signature
Star(int catalogNumber, string name, double mass, string spectralType, double visualMagnitude, double parallax,
    Equatorial equatorialCoordinatesAtEpoch, double declinationProperMotion, double rightAscensionProperMotion,
    double declinationSigma, double rightAscensionSigma, double declinationSigmaProperMotion,
    double rightAscensionSigmaProperMotion, Time epoch)
```

| Parameter | Unit and convention |
|-----------|---------------------|
| `parallax` | Arcseconds. `Distance` is 1/`parallax` parsecs (`Constants.Parsec2Meters`) |
| `equatorialCoordinatesAtEpoch` | Right ascension and declination in ICRF at the catalog epoch, radians; its distance is not read |
| `declinationProperMotion` | μδ = dδ/dt, radians per Julian year of TDB |
| `rightAscensionProperMotion` | μα* = (dα/dt) cos δ, a true angle, radians per Julian year of TDB: Gaia's `pmra`, Hipparcos's μα* |
| `declinationSigma` | σδ at the catalog epoch, radians |
| `rightAscensionSigma` | σα* = σα cos δ, a true angle, radians (Gaia's `ra_error`) |
| `declinationSigmaProperMotion` | σμδ, radians per Julian year of TDB |
| `rightAscensionSigmaProperMotion` | σμα*, a true angle, radians per Julian year of TDB (Gaia's `pmra_error`) |
| `epoch` | The catalog epoch, in any time frame: read in its own frame and converted to TDB (#709, decided by S. Guillet, 2026-11-16) |

The proper motion in right ascension and its sigmas are true angles, the convention of the Hipparcos and Gaia catalogs, so
a catalog record converts by its units alone. Before 11.0.0 the proper motion was added to the right ascension, so it was
dα/dt (SOFA's convention, `iauStarpm`, Note 4), which nothing documented: a catalog star given its μα* moved cos δ0 times
too fast in right ascension, 77.9 times for Polaris (#718).

### Properties

| Property | Description |
|----------|-------------|
| `CatalogNumber`, `SpectralType`, `VisualMagnitude` | As given |
| `Parallax` | Arcseconds |
| `Distance` | The distance from the solar system barycenter at the catalog epoch, meters: 1/`Parallax` parsecs |
| `Epoch` | The catalog epoch, as given |
| `EquatorialCoordinatesAtEpoch` | The catalog right ascension and declination, radians |
| `RightAscensionProperMotion`, `DeclinationProperMotion` | μα* and μδ, radians per Julian year of TDB |
| `RightAscensionSigma`, `DeclinationSigma`, `RightAscensionSigmaProperMotion`, `DeclinationSigmaProperMotion` | σα*, σδ, σμα*, σμδ |
| `IsPropagated` | True once `Propagate`, `PropagateAsync` or `Scenario.SimulateAsync` stored states |

### Methods

| Method | Description |
|--------|-------------|
| `GetEquatorialCoordinates(Time)` | The right ascension in [0, 2π), the declination in [−π/2, π/2] and the distance at the epoch (see [The model](#the-model)) |
| `GetRightAscensionSigma(Time)`, `GetDeclinationSigma(Time)` | √(σ² + (dt σμ)²), radians, not reduced (#709); the right ascension's a true angle |
| `GetEphemeris(Time, ILocalizable, Frame, Aberration)` | The star from an observer at the epoch: a direction, no light time on the star whatever the correction, the stellar aberration of the `+S` corrections ([Occultations](occultations.md)) |
| `Propagate(Window, TimeSpan)`, `PropagateAsync(Window, TimeSpan)` | The states every step from the start of the window, replacing those held |
| `WriteEphemeris(FileInfo)` | The SPK of the last propagation |

## The catalog epoch and dt

dt is the Julian years of TDB (365.25 days of 86 400 s) from the catalog epoch to the epoch, both converted to TDB, from
the exact difference of their ticks, as SOFA's `iauStarpm` takes TDB dates (Note 1): the same instant gives the same
position in every time frame, and a leap second between two UTC times counts (#709). A catalog epoch is given as a TDB
time: J2016.0 is `Time.CreateTDB(5844.0 * 86400.0)` (Gaia DR3), J1991.25 `Time.CreateTDB(-3195.9375 * 86400.0)`
(Hipparcos), J2000.0 `Time.J2000TDB`.

## The model

A star moves along a straight line at constant speed in ICRF, the space motion of SOFA's `iauStarpm` (release
2023-10-11, `external-lib/sofa-src/starpm.c`, Note 5) with a radial velocity of 0. With x = μα* dt and y = μδ dt, its
position in catalog distances is

    p(dt) = u0 + x e_α + y e_δ

u0 the catalog direction, e_α = (−sin α0, cos α0, 0) and e_δ = (−sin δ0 cos α0, −sin δ0 sin α0, cos δ0) its unit
tangents toward increasing right ascension and declination. With e = cos δ0 − y sin δ0:

- α = α0 + atan2(x, e), reduced to [0, 2π);
- δ = atan2(sin δ0 + y cos δ0, √(x² + e²));
- the distance is `Distance` · √(1 + x² + y²), as `iauStarpm`'s parallax decreases.

The form divides by no cos δ0: it holds at a pole (where dα/dt is undefined and the star moves along e_α) and crosses a
pole by itself. It is geometric at any speed: the synthetic stars of the tests, 0.1 rad/yr in each coordinate at 2.6 pc (about 1.2 c), move
along their line, where `iauStarpv` would freeze them (its speed limit of 0.5 c). A star without proper motion, and every
star at its catalog epoch, is its catalog, bit for bit.

Before 11.0.0 the angles moved linearly, α0 + μα dt and δ0 + μδ dt, at the catalog distance (#718): fed dα/dt, Barnard's
star left the straight line by 0.33 mas after 10 years, 34 mas after 100 and 9.4″ after 1000, Polaris by 4.2 mas after 100
years, and a star near a pole by 1.26° after 1000 years.

### Accuracy

Against `iauStarpm` (rv = 0, fed dα/dt = μα*/cos δ0), `tools/sofa_reference/starpm_ref.c`, Linux x64, 2026-11-16, over
Barnard's star (Hipparcos I/311 and Gaia DR3), Polaris (I/311) and Barnard's motion at ±89.5°, which crosses a pole:

| dt | Direction | Distance (relative) |
|---:|----------:|--------------------:|
| 1 yr | 1.8e-17 rad | 7.6e-9 |
| 10 yr | 1.8e-17 rad | 7.6e-8 |
| 100 yr | 2.4e-14 rad | 7.6e-7 |
| 1000 yr | 2.4e-10 rad | 7.6e-6 |

After 1 and 10 years both round the right ascension to the same double. After 100 and 1000 years the difference is
`iauStarpm`'s light-time and relativistic terms of the observed place, growing as dt⁴; the distance's is `iauStarpv`'s
inertial radial velocity −v_t²/(2c) for an observed radial velocity of 0, 7.6e-8 relative per decade for Barnard's star.
Against the same closed form evaluated in long double, every star of the harness, the poles and the synthetic stars
included: 7.8e-16 rad in direction (the resolution of a right ascension near 4.7 rad), 2.2e-16 relative in length. The
thresholds of `StarTests` are twice the Linux worst, provisional, to be set again from the Windows x64 and macOS arm64 CI
logs; after 1 and 10 years, where the Linux worst lies below one ulp of the right ascension, the direction threshold is
twice the resolution of the returned angles, 1.9e-15 rad (one ulp of a right ascension below 2π, 8.9e-16 rad, and one of
a declination, 2.2e-16 rad), so that another platform's one-ulp difference passes.

### Limits

- **No radial velocity.** The model is `iauStarpm`'s at rv = 0, and the distance comes from the catalog parallax and the
  straight line. Barnard's star's −110.47 km/s (Gaia DR3) would move it by 64 mas after 10 years, 6.5″ after 100 and
  11.4′ after 1000 (`iauStarpm` with and without it), more than the straight line corrects (34 mas after 100 years),
  and change its parallax after 100 years from 0.548303″ to 0.551722″. A radial velocity input would be a new
  constructor parameter, and the defect fixes of 11.0.0 keep the existing signatures (#668, decided by S. Guillet,
  2026-11-14): whether `Star` takes one is S. Guillet's choice, open in the PR of #714 to #718.
- **The state's velocity is zero.** `GetGeometricStateRelativeTo`, `Propagate` and the SPK of `WriteEphemeris` carry the
  moving position and a zero velocity.
- **A catalog position is the direction of the light that reaches the barycenter at its epoch** (as SOFA's `iauPmsafe`
  reads a catalog): no light time is applied to a star, whatever the correction ([Occultations](occultations.md)).

## From a catalog record

A Gaia DR3 record converts by its units alone: `ra` and `dec` from degrees to radians, `pmra` (μα*) and `pmdec` from
mas/yr to rad/yr, `parallax` from mas to arcseconds, `ra_error` and `dec_error` from mas to radians, the epoch J2016.0
TDB. Barnard's star, Gaia DR3 4472832130942575872 (VizieR I/355, read on 2026-11-16, to be verified by S. Guillet):

```csharp
const double deg = Math.PI / 180.0;
const double mas = Math.PI / 180.0 / 3600.0 / 1000.0;
var j2016 = Time.CreateTDB(5844.0 * 86400.0);
var barnard = new Star(4472832, "Barnard", 2.9e29, "M4V", 9.5, 546.9759 / 1000.0,
    new Equatorial(4.73942005111 * deg, 269.44850252544 * deg, j2016),
    10362.394 * mas, -801.551 * mas, 0.0, 0.0, 0.0, 0.0, j2016);
var in2100 = barnard.GetEquatorialCoordinates(new Time(2100, 1, 1));
```

Its coordinates are those of `iauStarpm` with rv = 0 to the figures of [Accuracy](#accuracy)
(`StarTests.GetEquatorialCoordinates_FromAGaiaDr3Record_IsIauStarpm`). A Hipparcos record (I/239, μα* as `pmRA`; the new
reduction I/311, whose `pmRA` is read here as μα* too, to be verified by S. Guillet) converts the same way, at the epoch
J1991.25 TDB.
