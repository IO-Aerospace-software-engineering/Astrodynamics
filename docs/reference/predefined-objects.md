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
| `Frame.GCRF` | Geocentric realization of ICRS with IAU frame bias |
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
