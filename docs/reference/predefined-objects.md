# Predefined Objects

The framework provides predefined constants for solar system bodies and reference frames to avoid recreating standard NAIF object mappings in application code.

## SolarSystemObjects.PlanetsAndMoons

### Planets

| Constant | Description |
|----------|-------------|
| `MERCURY` | Mercury (NAIF ID) |
| `VENUS` | Venus |
| `EARTH` | Earth (NAIF ID) |
| `EARTH_BODY` | Earth as `CelestialBody` instance |
| `MARS` | Mars |
| `JUPITER` | Jupiter |
| `SATURN` | Saturn |
| `URANUS` | Uranus |
| `NEPTUNE` | Neptune |

### Earth System

| Constant | Description |
|----------|-------------|
| `MOON` | Moon (NAIF ID) |
| `MOON_BODY` | Moon as `CelestialBody` instance |

### Mars System

| Constant | Description |
|----------|-------------|
| `PHOBOS` | Phobos |
| `DEIMOS` | Deimos |

### Outer Planet Moons

The module also provides NAIF IDs for the complete catalog of known moons:

- **Jupiter**: `IO`, `EUROPA`, `GANYMEDE`, `CALLISTO`, `AMALTHEA`, `HIMALIA`, and 50+ additional moons
- **Saturn**: `MIMAS`, `ENCELADUS`, `TETHYS`, `DIONE`, `RHEA`, `TITAN`, `IAPETUS`, `PHOEBE`, `HYPERION`, and 50+ additional moons
- **Uranus**: `ARIEL`, `UMBRIEL`, `TITANIA`, `OBERON`, `MIRANDA`, and 20+ additional moons
- **Neptune**: `TRITON`, `NEREID`, and additional moons

All constants are `NaifObject` instances. Create a `CelestialBody` from any of them:

```csharp
var titan = new CelestialBody(PlanetsAndMoons.TITAN);
```

## SolarSystemObjects.Barycenters

| Constant | Description |
|----------|-------------|
| `SOLAR_SYSTEM_BARYCENTER` | Solar system barycenter (NAIF 0) |
| `MERCURY_BARYCENTER` | Mercury system barycenter |
| `VENUS_BARYCENTER` | Venus system barycenter |
| `EARTH_BARYCENTER` | Earth-Moon barycenter |
| `MARS_BARYCENTER` | Mars system barycenter |
| `JUPITER_BARYCENTER` | Jupiter system barycenter |
| `SATURN_BARYCENTER` | Saturn system barycenter |
| `URANUS_BARYCENTER` | Uranus system barycenter |
| `NEPTUNE_BARYCENTER` | Neptune system barycenter |
| `PLUTO_BARYCENTER` | Pluto system barycenter |

## Data of the static objects

`EARTH_BODY`, `MOON_BODY`, `SUN_BODY` and the ten barycentres are built when their class is first read without any read
of the data provider: their NAIF identifier, their name and the predicates (`IsBarycenter`, `IsPlanet`, ...) need no
data. Their other members, the GM, the mass, `InitialOrbitalParameters` (at J2000 TDB in ECLIPJ2000, about the
barycentre of their system) and, for a body, the radii, flattening, J2 to J4, frame and sphere of influence, are read
from the provider at the first read of one of them, exactly as `new CelestialBody(naifObject)` or `new Barycenter(id)`
reads them. A read the provider cannot answer throws its exception (`ArgumentException` naming `naifId` for missing
information, `InvalidOperationException` for a missing state) and caches nothing: the next read tries again, on the
provider set at its time (`Configuration.Instance.SetDataProvider`), and succeeds once that provider has the data, the
same provider completed or another one set since. Once its data are read, an object keeps the provider it read them
from, for its data and for its ephemeris, as an object built by a constructor keeps the provider of its construction:
a provider set later does not reach it. The data read are published once, with no lock held across the provider call, so a read
never waits for another thread (#623). Before 11.0.0 the type initializers read the provider: a missing datum threw a
`TypeInitializationException` kept for the life of the process, and every later use of the class failed.

An object keeps its data only when the provider gives the data the kernels give each of them: a GM (on SPICE
`BODYnnn_GM`; for the solar system barycentre the sum of `BODY1_GM` to `BODY10_GM`) and, for `EARTH_BODY`, `MOON_BODY` and
`SUN_BODY`, radii (`BODYnnn_RADII`). SPICE names the bodies without kernels and answers a GM or radii of 0 for a
variable no loaded kernel defines: a read before `LoadKernels`, or with a GM kernel and no PCK, throws
`InvalidOperationException` naming the missing variable and keeps nothing, so that the read after `LoadKernels` gives
the kernel values. Before the third round of #668 such a read kept the zeros for the life of the process (a GM of 0,
radii of 0, J2 NaN, the built-in frame `IAU_EARTH` for the Earth, no orbit). The orbit is not required: one the
provider cannot give at the first complete read (an SPK loaded after the GM and radii kernels) stays null, as for a body
the kernels do not chain to its barycentre: read a static object's data after `LoadKernels`.

## SolarSystemObjects.Stars

| Constant | Description |
|----------|-------------|
| `Sun` | Sun (NAIF 10) |
| `SUN_BODY` | Sun as `CelestialBody` / `Star` instance |

## Predefined Frames

### Common Frames (`Frame`)

| Constant | Description |
|----------|-------------|
| `Frame.ICRF` | International Celestial Reference Frame |
| `Frame.TEME` | True Equator Mean Equinox (SGP4 output frame) |
| `Frame.ECLIPTIC_J2000` | Ecliptic plane at J2000 epoch |
| `Frame.ECLIPTIC_B1950` | Ecliptic plane at B1950 epoch |
| `Frame.GALACTIC_SYSTEM2` | Galactic coordinate system II |
| `Frame.B1950` | Mean equator and equinox of B1950 |
| `Frame.FK4` | Fundamental Katalog 4 frame |

### Earth Orientation Frames

!!! note
    These frames are static members of `Frame`, like the ones above. `Frame.TIRS` uses
    `NullEop`; construct `new TirsFrame(eop)` to supply real Earth orientation parameters.

| Constant | Description |
|----------|-------------|
| `Frame.GCRF` | ICRS axes centred on the Earth: the axes of `Frame.ICRF`, the identity rotation, no frame bias |
| `Frame.CIRS` | Celestial intermediate reference system (CIO-based precession-nutation) |
| `Frame.TIRS` | Terrestrial intermediate reference system (adds Earth rotation angle) |

## Example

```csharp
var earth = PlanetsAndMoons.EARTH_BODY;
var moon = new CelestialBody(PlanetsAndMoons.MOON);

var state = moon.GetEphemeris(epoch, earth, Frame.ICRF, Aberration.None)
    .ToStateVector();
```

## See Also

- [Celestial Bodies](celestial-bodies.md)
- [Frames & Orientation](frames.md)
- [Enumerations](enumerations.md)
