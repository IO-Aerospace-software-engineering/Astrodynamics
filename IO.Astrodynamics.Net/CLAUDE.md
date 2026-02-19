# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

IO.Astrodynamics.Net is a .NET 8/10 astrodynamics framework for orbital mechanics calculations, ephemeris computations, and space mission planning. It consists of:

- **IO.Astrodynamics**: Core framework library with orbital mechanics algorithms
- **IO.Astrodynamics.CLI**: Command-line interface tool (`astro`) for astrodynamics operations
- **IO.Astrodynamics.Tests**: Unit tests using xUnit framework
- **IO.Astrodynamics.CLI.Tests**: CLI tests
- **IO.Astrodynamics.Performance**: Performance benchmarking using BenchmarkDotNet

## Key Commands

### Build
```bash
dotnet build IO.Astrodynamics.sln
```

### Test
```bash
# Run all tests
dotnet test

# Run specific project tests
dotnet test IO.Astrodynamics.Tests/IO.Astrodynamics.Tests.csproj
dotnet test IO.Astrodynamics.CLI.Tests/IO.Astrodynamics.CLI.Tests.csproj

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run a single test by fully qualified name
dotnet test --filter "FullyQualifiedName~IO.Astrodynamics.Tests.ClassName.TestName"

# Run tests matching a pattern
dotnet test --filter "DisplayName~TLE"
```

### CLI Tool
```bash
# Build and run the CLI
dotnet run --project IO.Astrodynamics.CLI -- [command]

# Install CLI globally
dotnet tool install --global --add-source ./IO.Astrodynamics.CLI/bin/Debug IO.Astrodynamics.CLI
```

## Architecture

### Core Components

**Native Interop Layer (IO.Astrodynamics/API.cs)**
- P/Invoke wrapper around native SPICE toolkit (NASA's CSPICE)
- Platform-specific libraries: `IO.Astrodynamics.dll` (Windows), `libIO.Astrodynamics.so` (Linux)
- Thread-safe access through lock synchronization

**Data Provider Pattern**
- `IDataProvider` interface with implementations:
  - `SpiceDataProvider`: Default, uses SPICE kernel files
  - `MemoryDataProvider`: In-memory for testing
- Configured via `Configuration.Instance.SetDataProvider()`

**Key Namespaces**
- `IO.Astrodynamics.Body`: Celestial bodies, spacecraft, instruments
- `IO.Astrodynamics.OrbitalParameters`: State vectors, Keplerian elements, TLE, mean/osculating elements
- `IO.Astrodynamics.OrbitalParameters.TLE`: Two-Line Element sets and OMM support
- `IO.Astrodynamics.CCSDS.OMM`: CCSDS Orbit Mean-elements Message support (read/write/validate/convert)
- `IO.Astrodynamics.CCSDS.OPM`: CCSDS Orbit Parameter Message support (read/write/validate/convert)
- `IO.Astrodynamics.Maneuver`: Lambert solvers, launch windows, maneuver planning, attitude maneuvers, IAttitudeTarget system (orbital direction targets, celestial attitude targets)
- `IO.Astrodynamics.Frames`: Reference frames and transformations
- `IO.Astrodynamics.TimeSystem`: Time frames (UTC, TDB, TAI, etc.)
- `IO.Astrodynamics.Propagator`: Orbital propagation and integration (Velocity-Verlet symplectic integrator)
- `IO.Astrodynamics.Propagator.Forces`: Force models (gravitational, atmospheric drag, solar radiation pressure)
- `IO.Astrodynamics.Atmosphere`: Atmospheric density, temperature, and pressure models for Earth and Mars
- `IO.Astrodynamics.Math`: Vectors, matrices, quaternions, Legendre functions
- `IO.Astrodynamics.Physics`: Geopotential model reader, coefficients

**External Dependencies**
- MathNet.Numerics: Linear algebra operations
- MathNet.Filtering.Kalman: Kalman filtering for state estimation
- Cocona: CLI framework (CLI project only)
- xUnit + BenchmarkDotNet: Testing and benchmarking

### Orbital Elements: Mean vs Osculating

The framework distinguishes between two types of orbital elements using `OrbitalElementsType`:

**OrbitalElementsType Enum**
- `Osculating`: Instantaneous Keplerian orbit at a specific epoch (default)
- `Mean`: Averaged elements used by analytical propagators (TLE/SGP4, OMM)

**Key Concepts**
- **Osculating elements** represent the instantaneous two-body orbit that would result if all perturbations vanished. They can be directly converted to position/velocity (StateVector).
- **Mean elements** are averaged over time to remove short-periodic variations. They require a specific propagator (SGP4/SDP4) to compute actual position/velocity.

**Important Conversion Rules**
1. **Mean elements CANNOT be directly converted to StateVector** - attempting to call `ToStateVector()` on mean KeplerianElements throws `InvalidOperationException`
2. **TLE overrides this behavior** - `TLE.ToStateVector()` uses SGP4/SDP4 propagation internally
3. **ElementsType propagates** through conversions (e.g., mean KeplerianElements → mean EquinoctialElements)

**Creating Mean Elements from OMM Data**
```csharp
// FromOMM accepts OMM native units: rev/day for mean motion, degrees for angles
var meanKep = KeplerianElements.FromOMM(
    meanMotion: 15.49309423,        // rev/day
    eccentricity: 0.0000493,
    inclination: 51.6423,           // degrees
    raan: 353.0312,                 // degrees
    argumentOfPeriapsis: 320.8755,  // degrees
    meanAnomaly: 39.2360,           // degrees
    observer: earth,
    epoch: epoch,
    frame: Frame.TEME);

// The mean motion is cached internally to preserve precision
// when converting back to TLE format
Assert.Equal(OrbitalElementsType.Mean, meanKep.ElementsType);

// This would throw InvalidOperationException:
// var sv = meanKep.ToStateVector();  // ERROR!
```

**Working with TLE**
```csharp
// Parse TLE - elements are mean by definition
var tle = new TLE("ISS",
    "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
    "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

Assert.Equal(OrbitalElementsType.Mean, tle.ElementsType);

// Get osculating state vector via SGP4 propagation
var osculating = tle.ToOsculating();           // At TLE epoch
var osculatingLater = tle.ToOsculating(epoch); // At specific epoch
Assert.Equal(OrbitalElementsType.Osculating, osculating.ElementsType);

// Get mean Keplerian elements (for creating new TLEs)
var meanKep = tle.ToMeanKeplerianElements();
Assert.Equal(OrbitalElementsType.Mean, meanKep.ElementsType);

// Create TLE from mean elements (preserves mean motion precision)
var newTle = TLE.Create(meanKep, "ISS", 25544, "98067A", 2570,
    Classification.Unclassified, bstar: 0.0001027);
```

**Mean Motion Precision Preservation**
When converting OMM → KeplerianElements → TLE, mean motion is cached to avoid precision loss from round-trip conversions:
```
OMM: meanMotion = 15.49309423 rev/day
  → FromOMM() caches original mean motion
  → MeanMotion() returns cached value (not recomputed from semi-major axis)
  → TLE output: 15.49309423 rev/day (EXACT)
```

**Osculating vs Mean Accessors in TLE**
| Accessor | Returns | Use Case |
|----------|---------|----------|
| `MeanSemiMajorAxis`, `MeanEccentricity`, etc. | Mean elements | Creating TLEs, orbit comparison |
| `SemiMajorAxis()`, `Eccentricity()`, etc. | Osculating (via SGP4) | Physical calculations |
| `ToOsculating()` | Osculating StateVector | Position/velocity calculations |
| `ToMeanKeplerianElements()` | Mean KeplerianElements | TLE creation |

### CCSDS OMM (Orbit Mean-elements Message)

The `IO.Astrodynamics.CCSDS.OMM` namespace provides full support for CCSDS Orbit Mean-elements Message format (CCSDS 502.0-B-3, NDM/XML Blue Book).

**Key Classes**
- `Omm`: Main class representing a complete OMM document
- `OmmReader`: Parses OMM from XML files/strings/streams
- `OmmWriter`: Writes OMM to XML files/strings/streams
- `OmmValidator`: Validates OMM content for CCSDS compliance
- `MeanElements`, `TleParameters`, `OmmMetadata`: Data structures

**Loading and Saving OMM**
```csharp
// Load OMM from file (with optional validation)
var omm = Omm.LoadFromFile("satellite.omm", validateSchema: true, validateContent: true);

// Access data
Console.WriteLine($"Object: {omm.ObjectName}");
Console.WriteLine($"COSPAR ID: {omm.ObjectId}");  // Format: "1998-067A"
Console.WriteLine($"Mean Motion: {omm.Data.MeanElements.MeanMotion} rev/day");

// Save OMM to file
omm.SaveToFile("output.omm", validateBeforeSave: true, wrapInNdm: true, indent: true);
```

**OMM ↔ TLE Bidirectional Conversion**
```csharp
// OMM to TLE (for SGP4 propagation)
var omm = Omm.LoadFromFile("iss.omm");
if (omm.IsTleCompatible)
{
    var tle = omm.ToTle();
    var stateVector = tle.ToStateVector();  // Propagate with SGP4
}

// TLE to OMM (for archiving/sharing)
var tle = new TLE("ISS", line1, line2);
var omm = tle.ToOmm(originator: "My Organization");
omm.SaveToFile("iss.omm");
```

**COSPAR ID Format Handling**
- OMM uses full format: `"1998-067A"` (9 chars with hyphen)
- TLE uses abbreviated format: `"98067A"` (6-8 chars)
- Conversion is automatic in `ToTle()` and `ToOmm()` methods

**TLE Precision Limitations**
When converting OMM → TLE → OMM, expect some precision loss:
- Angles: ~4 decimal places (TLE format constraint)
- BSTAR: ~6 decimal places
- Mean motion is preserved exactly (cached internally)

### CCSDS OPM (Orbit Parameter Message)

The `IO.Astrodynamics.CCSDS.OPM` namespace provides full support for CCSDS Orbit Parameter Message format (CCSDS 502.0-B-3, NDM/XML 505.0-B-3).

**Key Classes**
- `Opm`: Main class representing a complete OPM document
- `OpmReader`: Parses OPM from XML files/strings/streams
- `OpmWriter`: Writes OPM to XML files/strings/streams (with units attributes per CCSDS standard)
- `OpmValidator`: Validates OPM content for CCSDS compliance
- `OpmData`, `OpmStateVector`, `OpmKeplerianElements`, `OpmMetadata`: Data structures
- `OpmManeuverParameters`: Maneuver data (epoch, duration, delta-V, delta-mass)
- `OpmUserDefinedParameters`: Custom user-defined parameters
- `CovarianceMatrix`: 6x6 position-velocity covariance in CCSDS format

**Loading and Saving OPM**
```csharp
// Load OPM from file (with optional validation)
var opm = Opm.LoadFromFile("spacecraft.opm", validateSchema: true, validateContent: true);

// Access data
Console.WriteLine($"Object: {opm.ObjectName}");
Console.WriteLine($"COSPAR ID: {opm.ObjectId}");  // Format: "1998-067A"
Console.WriteLine($"Position: {opm.Data.StateVector.X}, {opm.Data.StateVector.Y}, {opm.Data.StateVector.Z} km");

// Save OPM to file
opm.SaveToFile("output.opm", validateBeforeSave: true, wrapInNdm: true, indent: true);
```

**OPM ↔ Spacecraft Bidirectional Conversion**
```csharp
// Spacecraft to OPM (for archiving/sharing)
var spacecraft = new Spacecraft(-1000, "ISS", 420000.0, 500000.0, clock, stateVector,
    1600.0, 2.2, "1998-067A", 1.5);
var opm = spacecraft.ToOpm(
    originator: "My Organization",
    includeKeplerianElements: true,
    includeSpacecraftParameters: true,
    includeManeuvers: true);  // Includes executed maneuvers
opm.SaveToFile("iss.opm");

// OPM to Spacecraft (for mission operations)
var opm = Opm.LoadFromFile("iss.opm");
var spacecraft = opm.ToSpacecraft(
    naifId: -1000,
    maximumOperatingMass: 500000.0,
    clock: new Clock("OnboardClock", 256),
    observer: earth);  // Optional, defaults to Earth
```

**OPM ↔ StateVector Conversion**
```csharp
// OPM to StateVector (simple conversion)
var opm = Opm.LoadFromFile("spacecraft.opm");
var stateVector = opm.ToStateVector(observer: earth);
// Units converted: km → m, km/s → m/s

// StateVector to OPM
var opm = Opm.CreateFromStateVector("SAT-1", "2024-001A", stateVector,
    originator: "Mission Control");
```

**Maneuver Integration**
```csharp
// Executed maneuvers are automatically included in ToOpm()
var spacecraft = CreateSpacecraftWithManeuvers();
// ... propagate and execute maneuvers ...

var opm = spacecraft.ToOpm(includeManeuvers: true);
// OPM will contain OpmManeuverParameters for each executed ImpulseManeuver

// Manual maneuver export from ImpulseManeuver
if (maneuver is ImpulseManeuver impulseManeuver && impulseManeuver.ThrustWindow.HasValue)
{
    var opmManeuver = impulseManeuver.ToOpmManeuverParameters(referenceFrame: "EME2000");
    // Contains: epoch, duration, delta-mass, delta-V (km/s)
}
```

**User-Defined Parameters**
```csharp
// Create OPM with custom parameters
var userParams = new OpmUserDefinedParameters(
    new Dictionary<string, string>
    {
        ["MISSION_ID"] = "STS-001",
        ["OPERATOR"] = "NASA"
    },
    comments: new[] { "Custom mission parameters" });

var data = new OpmData(stateVector, userDefinedParameters: userParams);
```

**Covariance Matrix Support**
```csharp
// OPM with covariance (units: km², km²/s, km²/s²)
var covariance = new CovarianceMatrix(
    cxX: 1.0e-6, cyX: 0.0, cyY: 1.0e-6, czX: 0.0, czY: 0.0, czZ: 1.0e-6,
    cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 1.0e-9,
    cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 1.0e-9,
    czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 1.0e-9,
    referenceFrame: "ICRF");

var data = new OpmData(stateVector, covariance: covariance);
```

**Unit Handling**
- OPM uses CCSDS units: km, km/s, deg, kg, m², km², km²/s, km²/s²
- Framework uses SI units: m, m/s, rad, kg, m²
- Conversions are automatic in `ToStateVector()`, `ToSpacecraft()`, and `ToOpm()`
- XML output includes `units` attributes on all numeric elements per CCSDS standard

### Atmospheric Modeling

The framework provides a unified atmospheric modeling system with support for multiple planets and models of varying complexity.

**Architecture**
- `IAtmosphericModel` interface: Unified API for all atmospheric models
- `IAtmosphericContext` interface: Encapsulates position, time, and environmental data
- `AtmosphericContext` record: Helper with factory methods `FromAltitude()` and `FromPlanetodetic()`
- `Atmosphere` record: Result type with Temperature, Pressure, Density, and optional Details
- `IAtmosphericDetails` interface: Marker for model-specific details
- `NrlmsiseDetails` record: NRLMSISE-00 specific data (molecular densities, exospheric temp)

All models implement `IAtmosphericModel` with these methods:
- `GetAtmosphere(context)`: Returns `Atmosphere` record with all data + optional model-specific details
- `GetTemperature(context)`: Temperature in Celsius
- `GetPressure(context)`: Pressure in kPa
- `GetDensity(context)`: Density in kg/m³

**Available Models**

**Earth Models:**
1. **EarthStandardAtmosphere** (U.S. Standard Atmosphere 1976)
   - Simple analytical model valid up to ~86 km altitude
   - Uses only altitude (ignores time and space weather)
   - Fast and suitable for preliminary analysis
   - Details property is null

2. **Nrlmsise00Model** (NRLMSISE-00)
   - Full empirical model valid from ground to exosphere (0-2000+ km)
   - Complete C# implementation of NRLMSISE-00 algorithm
   - Requires full context: altitude, position, time, and space weather data (F10.7, Ap indices)
   - Thread-safe for concurrent use
   - Returns `NrlmsiseDetails` with molecular densities (He, O, N₂, O₂, Ar, H, N, anomalous O) and exospheric temperature
   - Advanced features:
     - Configurable via `NrlmsiseFlags` for enabling/disabling variations
     - `SpaceWeather` presets: `Nominal`, `SolarMinimum`, `SolarMaximum`, `Moderate`
     - Direct access via `NRLMSISE00` class for low-level control

**Mars Models:**
- **MarsStandardAtmosphere**: Simplified analytical model using altitude only

**Usage Pattern**
```csharp
// Simple case - altitude only (standard atmosphere)
var context = AtmosphericContext.FromAltitude(10000);  // 10 km
var model = new EarthStandardAtmosphere();
var atmosphere = model.GetAtmosphere(context);
// atmosphere.Temperature, .Pressure, .Density, .Details (null for simple models)

// Full context with NRLMSISE-00
var context = AtmosphericContext.FromPlanetodetic(
    altitude: 400000,                           // 400 km
    geodeticLatitude: 45 * Constants.Deg2Rad,
    geodeticLongitude: -75 * Constants.Deg2Rad,
    epoch: new Time(2024, 6, 21, 12, 0, 0)
);
var model = new Nrlmsise00Model(SpaceWeather.Nominal);
var atmosphere = model.GetAtmosphere(context);

// Access model-specific details via pattern matching
if (atmosphere.Details is NrlmsiseDetails details)
{
    var o2Density = details.MolecularOxygenDensity;  // m⁻³
    var exoTemp = details.ExosphericTemperature;     // K
}

// Via CelestialBody (auto-selects NRLMSISE-00 for Earth with full context)
var earth = PlanetsAndMoons.EARTH_BODY;
var atm = earth.GetAtmosphere(context);  // Uses NRLMSISE-00 automatically
```

**Model Selection Guidelines**
- Use `EarthStandardAtmosphere` for quick calculations below 86 km without time-dependent effects
- Use `Nrlmsise00Model` for accurate high-altitude modeling, time-varying conditions, or space weather effects
- Use `CelestialBody.GetAtmosphere(context)` with full context for automatic NRLMSISE-00 selection
- Use `MarsStandardAtmosphere` for preliminary Mars mission analysis

### Geopotential Gravity Model

The framework supports spherical harmonic gravity modeling using EGM2008 coefficients (up to degree/order 70).

**Key Classes**
- `GeopotentialModelParameters`: Configuration (model file path + max degree)
- `GeopotentialGravitationalField`: Computes full 3D acceleration via Montenbruck & Gill formulation
- `GeopotentialModelReader`: Parses EGM2008 coefficient files
- `GeopotentialCoefficient`: Holds C_nm, S_nm coefficients for a single (n,m) pair
- `LegendreFunctions`: Geodesy-normalized associated Legendre functions and derivatives

**Conventions**
- Geodesy normalization: `sqrt((2-delta_0m)(2n+1)(n-m)!/(n+m)!)` — no Condon-Shortley phase
- Coefficients: fully-normalized C_nm and S_nm from EGM2008 (tide-free)
- Model file starts at degree 2 (degrees 0 and 1 are absent; handled as zeros)

**Usage**
```csharp
// Create Earth with degree-10 geopotential
var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, epoch,
    new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

// The propagator automatically uses the geopotential model when present
var propagator = new SpacecraftPropagator(window, spacecraft,
    [earth, PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY],
    false, false, TimeSpan.FromSeconds(1.0));
```

**Thread Safety:** `GeopotentialGravitationalField` is NOT thread-safe (pre-allocated buffers). Create separate `CelestialBody` instances for concurrent propagation.

### Force Models (Propagator Perturbations)

The `IO.Astrodynamics.Propagator.Forces` namespace provides configurable perturbation models used by the `SpacecraftPropagator`.

**Atmospheric Drag (`AtmosphericDrag`)**
- Uses **atmosphere-relative velocity**: body-centered velocity minus co-rotation (`v_rel = v_body - omega x r_body`)
- The body's angular velocity is obtained via `CelestialBody.GetOrientation(Frame.ICRF, epoch).AngularVelocity`
- **Dynamic mass**: area/mass ratio is computed per step using `Spacecraft.GetTotalMass()` (dry + fuel + payload)
- Drag coefficient default is **2.2** (appropriate for satellites in free-molecular flow)

```csharp
// Atmospheric drag requires a CelestialBody with an atmospheric model
var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, epoch,
    geopotentialParams, new EarthStandardAtmosphere());
var drag = new AtmosphericDrag(spacecraft, earth);
```

**Solar Radiation Pressure (`SolarRadiationPressure`)**
- Cannonball model: `F = (L_sun / 4*pi*c) * Cr * (A/m) * r_hat / r^2`
- Includes **reflectivity coefficient Cr** from `Spacecraft.SolarRadiationCoeff` (range 1.0–2.0, default 1.0)
- **Continuous shadow model**: uses `ShadowFraction` (two-circle overlap area) instead of binary eclipse check
  - Handles none, partial, annular, and total eclipse geometries
  - SRP is scaled by `(1 - maxShadowFraction)` across all occluding bodies
- **Dynamic mass**: area/mass ratio recomputed each step via `GetTotalMass()`
- Occluding bodies are materialized to `CelestialBody[]` (avoids IEnumerable re-evaluation)

```csharp
// SRP with Cr = 1.5
var spacecraft = new Spacecraft(-1001, "Sat", 100.0, 10000.0, clock, orbit,
    sectionalArea: 10.0, solarRadiationCoeff: 1.5);
var srp = new SolarRadiationPressure(spacecraft, [earth]);
```

**Shadow Fraction (`CelestialItem.ShadowFraction`)**
- Static method: `ShadowFraction(double angularSeparation, double backSize, double bySize)` → `[0, 1]`
- Instance method: `ShadowFraction(CelestialItem by, OrbitalParameters from, Aberration aberration)` → `[0, 1]`
- Returns 0.0 for full illumination, 1.0 for total eclipse
- Uses the standard two-circle intersection area formula for partial eclipses

### Attitude Maneuvers

The framework provides a family of attitude maneuvers for spacecraft orientation control. All inherit from the abstract `Attitude` base class.

**Available Attitude Maneuvers**
- `NadirAttitude`: Points toward nadir (center of central body)
- `ZenithAttitude`: Points toward zenith (away from central body)
- `ProgradeAttitude`: Velocity-direction pointing
- `RetrogradeAttitude`: Anti-velocity pointing
- `NormalAttitude`: Orbital normal pointing (h = r × v)
- `AntiNormalAttitude`: Anti-normal pointing (−h)
- `InstrumentPointingToAttitude`: Single-vector instrument pointing at a target
- `TriadAttitude`: Two-vector fully-constrained attitude (eliminates roll ambiguity)

**TRIAD Attitude Determination**

The `TriadAttitude` class implements the TRIAD algorithm for fully-constrained 3-DOF attitude determination using two non-collinear observation vectors. Unlike single-vector pointing (which leaves roll unconstrained), TRIAD provides a unique, unambiguous spacecraft orientation.

**Key Concepts**
- **Primary constraint**: Aligns a spacecraft body vector with the direction to a primary target
- **Secondary constraint**: Constrains roll by orienting a secondary body vector toward a secondary target (as much as possible while maintaining primary constraint)
- **Minimum vector separation**: Default 5 degrees; prevents numerical instability from near-collinear vectors

**Spacecraft Body Frame Convention**

Default body axes (static fields, backward-compatible):
- Front (+Y): `Spacecraft.Front`
- Right (+X): `Spacecraft.Right`
- Up (+Z): `Spacecraft.Up`
- Down (-Z): `Spacecraft.Down`

**Configurable Body Axes** — per-instance overrides for non-standard body frames (e.g., +X forward):
- `BodyFront`, `BodyRight`, `BodyUp` — instance properties (default to static values)
- `BodyBack`, `BodyLeft`, `BodyDown` — computed inverses
- Passed via optional constructor parameters: `bodyFront`, `bodyRight`, `bodyUp`
- Validation enforces orthogonality and right-handedness when custom axes are provided
- All attitude maneuvers use `GetBodyFront()` (protected helper on `Maneuver` base class) which reads per-instance axes with static fallback

```csharp
// Spacecraft with +X as front (e.g., matching a SPICE FK convention)
var spacecraft = new Spacecraft(-1001, "Sat", 1000, 5000, clock, orbit,
    bodyFront: Vector3.VectorX, bodyRight: Vector3.VectorY.Inverse(), bodyUp: Vector3.VectorZ);

// All attitude maneuvers automatically use BodyFront instead of Spacecraft.Front
var prograde = new ProgradeAttitude(earth, epoch, duration, engine);
// → orients spacecraft.BodyFront (+X) along velocity vector
```

**Constructor Options**

1. **Single Instrument**: Uses instrument's boresight (primary) and refVector (secondary)
```csharp
// Point camera boresight at Earth, constrain roll using camera refVector toward Sun
var attitude = new TriadAttitude(
    minimumEpoch, holdDuration,
    camera, earth, sun,  // instrument, primaryTarget, secondaryTarget
    engine);
```

2. **Dual Instruments**: Different instruments for primary and secondary pointing
```csharp
// Point nadir camera at Earth, star tracker at reference star
var attitude = new TriadAttitude(
    minimumEpoch, holdDuration,
    nadirCamera, earth,
    starTracker, referenceStar,
    engine);
```

3. **Explicit Body Vectors**: Direct spacecraft frame vectors
```csharp
// Point spacecraft Front at Moon, keep Up toward Sun
var attitude = new TriadAttitude(
    minimumEpoch, holdDuration,
    Spacecraft.Front, moon,
    Spacecraft.Up, sun,
    engine);
```

4. **IAttitudeTarget (orbital directions and/or celestial bodies)**: Uses `IAttitudeTarget` interface for polymorphic targets
```csharp
// Point prograde with solar panels toward Sun
var attitude = new TriadAttitude(
    earth, epoch, duration,
    Spacecraft.Front, OrbitalDirectionTarget.Prograde,
    Spacecraft.Up, new CelestialAttitudeTarget(sun),
    engine);

// Full LVLH attitude (nadir pointing with prograde constraint)
var lvlh = new TriadAttitude(
    earth, epoch, duration,
    Spacecraft.Down, OrbitalDirectionTarget.Nadir,
    Spacecraft.Front, OrbitalDirectionTarget.Prograde,
    engine);
```

**IAttitudeTarget System**

The `IAttitudeTarget` interface enables orbital directions and celestial bodies to be used interchangeably as TRIAD targets:
- `IAttitudeTarget.GetDirection(StateVector)` — returns a unit vector in the inertial frame
- `IAttitudeTarget.Name` — human-readable target name

**Implementations:**
- `OrbitalDirectionTarget` — computes direction from state vector (prograde, nadir, normal, etc.)
  - Predefined static instances: `OrbitalDirectionTarget.Prograde`, `.Retrograde`, `.Nadir`, `.Zenith`, `.Normal`, `.AntiNormal`
- `CelestialAttitudeTarget` — wraps `ILocalizable`, computes direction via ephemeris with `Aberration.LT`

**`OrbitalDirection` enum**: `Prograde`, `Retrograde`, `Nadir`, `Zenith`, `Normal`, `AntiNormal`

**Factory Methods**

Convenience factories for common operational attitudes:
```csharp
// LVLH: Front→Prograde, Down→Nadir
var lvlh = TriadAttitude.CreateLVLH(earth, epoch, duration, engine);

// Prograde with sun tracking: Front→Prograde, Up→Sun
var sunTrack = TriadAttitude.CreateProgradeWithSunTracking(earth, epoch, duration, sun, engine);
```

**Use Case Examples**

*Earth Observation with Sun Tracking*
```csharp
// Keep nadir sensor pointed at Earth while orienting solar panels toward Sun
var earthObservation = new TriadAttitude(
    epoch, TimeSpan.FromMinutes(30),
    Spacecraft.Down, earth,    // Primary: nadir pointing
    Spacecraft.Up, sun,        // Secondary: solar panel toward Sun
    engine);
```

*Instrument Pointing with Roll Constraint*
```csharp
// Point telescope at target star, constrain roll using reference star
var observation = new TriadAttitude(
    epoch, TimeSpan.FromHours(2),
    telescope, targetStar,
    rollSensor, referenceStar,
    engine);
```

*Plane-Change Attitude (Normal Pointing)*
```csharp
// Orient for plane-change burn: front along orbital normal
var normalAttitude = new NormalAttitude(earth, epoch, duration, engine);
```

**Instrument Enhancements**

The `Instrument` class now provides:
- `GetBoresightInSpacecraftFrame()`: Returns instrument boresight vector in spacecraft body frame
- `GetRefVectorInSpacecraftFrame()`: Returns instrument reference vector in spacecraft body frame (new in this release)

Both methods transform the instrument-frame vectors to the spacecraft body frame, enabling direct use with TRIAD constructors.

**Matrix Utility**

The `Matrix` class includes a new static method for attitude computation:
```csharp
// Create 3x3 matrix from column vectors (used in TRIAD algorithm)
var rotationMatrix = Matrix.FromColumnVectors(col0, col1, col2);
```

### Testing Approach

All test classes load SPICE kernels in constructor:
```csharp
API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
```

Test data files are in `Data/SolarSystem/` and copied to output directory.

## Development Guidelines

1. **Thread Safety**: CSPICE operations are not thread-safe - all API calls must use the shared lock object
2. **Kernel Management**: Load/unload SPICE kernels appropriately to avoid memory leaks
3. **Native Resources**: Properly free unmanaged memory returned from native calls
4. **Time Systems**: Be explicit about time frames when working with epochs
5. **Coordinate Systems**: Always specify reference frames for state vectors and transformations
6. **Mean vs Osculating Elements**: Always check `ElementsType` when working with orbital parameters:
   - Use `ToOsculating()` for TLE position/velocity calculations
   - Never call `ToStateVector()` directly on mean KeplerianElements
   - Use `TLE.Create()` only with mean elements (validates `ElementsType.Mean`)
7. **CCSDS OMM Handling**: When working with OMM files:
   - Use `Omm.LoadFromFile()` with validation for production code
   - Check `IsTleCompatible` before calling `ToTle()`
   - Use `TLE.ToOmm()` to convert TLE data for CCSDS-compliant archiving
   - COSPAR ID format conversion is automatic (OMM: "1998-067A" ↔ TLE: "98067A")
8. **CCSDS OPM Handling**: When working with OPM files:
   - Use `Opm.LoadFromFile()` with validation for production code
   - Use `Spacecraft.ToOpm()` for archiving spacecraft state with maneuvers
   - Use `Opm.ToSpacecraft()` to restore spacecraft (requires naifId, clock, maxMass)
   - Use `Opm.ToStateVector()` for simple state vector extraction
   - Unit conversions (km↔m, deg↔rad) are automatic
   - Maneuver conversion is one-way: Spacecraft→OPM (OPM maneuvers are archival only)
   - User-defined parameters support custom mission-specific data
9. **Attitude Maneuvers**: When implementing attitude control:
   - Use `TriadAttitude` for fully-constrained orientation (eliminates roll ambiguity)
   - Use single-vector attitudes (`NadirAttitude`, `ProgradeAttitude`, `NormalAttitude`, etc.) only when roll is unconstrained
   - Use `NormalAttitude` / `AntiNormalAttitude` for plane-change burns (h = r × v)
   - Ensure body vectors and reference vectors are not collinear (minimum 5 degrees separation)
   - Use `Spacecraft.Front`, `Spacecraft.Up`, etc. for standard body frame directions; use instance `BodyFront`, `BodyRight`, `BodyUp` for non-standard frames
   - Custom body axes must be orthogonal and right-handed (`BodyRight.Cross(BodyFront) == BodyUp`)
   - All attitude maneuvers use `GetBodyFront()` which reads per-instance axes with static fallback
   - Use `IAttitudeTarget` with `TriadAttitude` to mix orbital directions (`OrbitalDirectionTarget.Prograde`, etc.) and celestial targets (`CelestialAttitudeTarget`)
   - Use factory methods `TriadAttitude.CreateLVLH()` and `CreateProgradeWithSunTracking()` for common operational attitudes
   - Use `Instrument.GetBoresightInSpacecraftFrame()` and `GetRefVectorInSpacecraftFrame()` for instrument-based pointing
10. **Geopotential Gravity**: When working with spherical harmonic gravity models:
   - Pass `GeopotentialModelParameters` to `CelestialBody` constructor to enable geopotential
   - Use degree 10 for typical LEO accuracy; degree 2 for J2-only analysis
   - EGM2008 model file: `Data/SolarSystem/EGM2008_to70_TideFree` (degrees 2-70)
   - `GeopotentialGravitationalField` is NOT thread-safe — one instance per thread
   - Legendre functions use geodesy normalization without Condon-Shortley phase
   - The model file starts at degree 2; degrees 0 and 1 are handled as zeros internally
11. **Force Models (Drag & SRP)**: When working with atmospheric drag or solar radiation pressure:
   - Default drag coefficient (Cd) is **2.2** (free-molecular flow for satellites), not 0.3
   - Atmospheric drag uses **atmosphere-relative velocity** (body-centered velocity minus co-rotation)
   - SRP includes **Cr coefficient** from `Spacecraft.SolarRadiationCoeff` (default 1.0, range 1.0–2.0)
   - SRP uses **continuous shadow fraction** (partial/annular eclipses reduce SRP proportionally)
   - Both forces use **dynamic mass** via `GetTotalMass()` (accounts for fuel consumption during propagation)
   - Use `CelestialItem.ShadowFraction()` for eclipse geometry calculations

## Code Quality Standards

Per CONTRIBUTING.md:
- OOP, SOLID, and DRY principles required
- Unit test coverage must exceed 95%
- Feature branches required before merge