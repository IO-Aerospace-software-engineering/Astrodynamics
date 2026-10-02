# Spacecraft

`Spacecraft` represents a vehicle with mass properties, instruments, engines, fuel tanks, and either a propagated or kernel-backed ephemeris.

## Standard Constructor

Creates a spacecraft for numerical propagation.

```csharp
var spacecraft = new Spacecraft(
    naifId: -1001, name: "MySat",
    mass: 500.0, maximumOperatingMass: 2000.0,
    clock: new Clock("OnboardClk", 256),
    initialOrbitalParameters: orbit,
    sectionalArea: 10.0, dragCoeff: 2.2,
    cosparId: "2024-001A", solarRadiationCoeff: 1.5);
```

## Kernel-Backed Constructor

Creates a spacecraft whose ephemeris comes from loaded SPK/CK kernels. Use for archived missions (LRO, Cassini, JWST) or when using a spacecraft as an observer without propagation.

```csharp
var lro = new Spacecraft(naifId: -85, name: "LRO",
    epoch: new Time(2024, 6, 1, 12, 0, 0), mass: 1018.0, sectionalArea: 12.0);
```

| Parameter | Default | Description |
|-----------|---------|-------------|
| `epoch` | `Time.J2000TDB` | Epoch for initial state lookup |
| `mass` | `0.0` | Dry mass (kg) |
| `maximumOperatingMass` | `double.MaxValue` | Maximum operating mass (kg) |
| `clock` | Auto-created | A new `Clock` named `"{name}_CLK"` |
| `sectionalArea` | `1.0` | Mean sectional area (m2) |
| `dragCoeff` | `2.2` | Drag coefficient |
| `solarRadiationCoeff` | `1.0` | Solar radiation pressure coefficient |

Kernel-backed spacecraft: `IsSpiceBacked` returns `true`. Calling `Propagate()` throws `InvalidOperationException`.

## Properties

| Property | Description |
|----------|-------------|
| `NaifId` | NAIF ID (negative) |
| `DryOperatingMass` | Mass without fuel (kg) |
| `MaximumOperatingMass` | Maximum operating mass (kg) |
| `SectionalArea` | Cross-sectional area for drag and SRP (m2) |
| `DragCoefficient` | Cd (default 2.2) |
| `SolarRadiationCoeff` | Cr (default 1.0) |
| `CosparId` | COSPAR international designator |
| `HardBodyRadius` | Hard-body radius for conjunction assessment (m) |
| `BodyFront` / `BodyRight` / `BodyUp` | Instance body axes (override static defaults) |
| `Instruments` | Read-only collection of instruments |
| `Engines` | Read-only collection of engines |
| `FuelTanks` | Read-only collection of fuel tanks |
| `Payloads` | Read-only collection of payloads |
| `IsSpiceBacked` | Whether ephemeris comes from SPICE kernels |
| `Frame` | Spacecraft body-fixed reference frame |

## Static Body Axes

Default body-frame directions used by all attitude maneuvers when no instance override is set.

| Field | Direction | Vector |
|-------|-----------|--------|
| `Spacecraft.Front` | Forward | +Y |
| `Spacecraft.Back` | Aft | -Y |
| `Spacecraft.Right` | Starboard | +X |
| `Spacecraft.Left` | Port | -X |
| `Spacecraft.Up` | Zenith-facing | +Z |
| `Spacecraft.Down` | Nadir-facing | -Z |

Instance-level overrides can be set via constructor parameters (`bodyFront`, `bodyRight`, `bodyUp`). Custom axes must be orthogonal and right-handed.

## Methods

| Method | Description |
|--------|-------------|
| `AddCircularInstrument(...)` | Add circular FOV instrument |
| `AddRectangularInstrument(...)` | Add rectangular FOV instrument |
| `AddEngine(Engine)` | Add propulsion engine |
| `AddFuelTank(FuelTank)` | Add fuel tank |
| `AddPayload(Payload)` | Add payload |
| `GetTotalMass()` | Total mass including fuel |
| `SetStandbyManeuver(Maneuver, Time?)` | Set maneuver to execute |
| `Propagate(Window, IEnumerable, bool, bool, TimeSpan)` | Propagate orbit |
| `GetEphemeris(Time, ILocalizable, Frame, Aberration)` | Get state relative to observer |
| `WriteEphemeris(FileInfo)` | Write ephemeris to SPK file |
| `ToOpm(...)` | Export to CCSDS OPM |

## Components

### Engine

| Property | Description |
|----------|-------------|
| `Name` | Engine name |
| `Model` | Engine model identifier |
| `SerialNumber` | Serial number |
| `ISP` | Specific impulse (s) |
| `FuelFlow` | Fuel mass flow rate (kg/s) |
| `Thrust` | Computed thrust (N) |
| `FuelTank` | Associated fuel tank |

```csharp
var engine = new Engine("MainEngine", "RS-25", "SN001",
    isp: 366.0, fuelFlow: 0.5, fuelTank: tank);
```

### FuelTank

| Property | Description |
|----------|-------------|
| `Name` | Tank name |
| `Model` | Tank model identifier |
| `SerialNumber` | Serial number |
| `Capacity` | Maximum capacity (kg) |
| `InitialQuantity` | Initial fuel quantity (kg) |
| `Quantity` | Current fuel quantity (kg) |

```csharp
var tank = new FuelTank("MainTank", "FT-100", "SN001",
    capacity: 1000.0, initialQuantity: 800.0);
```

### Instrument

| Property | Description |
|----------|-------------|
| `NaifId` | NAIF instrument ID |
| `Name` | Instrument name |
| `Model` | Instrument model identifier |
| `FieldOfView` | Field of view angle (rad) |
| `Shape` | `InstrumentShape` (Circular, Rectangular, Elliptical) |
| `Boresight` | Boresight direction in instrument frame |
| `RefVector` | Reference vector in instrument frame |
| `Orientation` | Instrument orientation relative to spacecraft |

| Method | Description |
|--------|-------------|
| `IsInFOV(Time, ILocalizable, Aberration)` | Check if target is in field of view |
| `FindWindowsInFieldOfViewConstraint(...)` | Find visibility windows |
| `GetBoresightInSpacecraftFrame()` | Transform boresight to spacecraft body frame |
| `GetRefVectorInSpacecraftFrame()` | Transform reference vector to spacecraft body frame |

### Payload

```csharp
var payload = new Payload("Camera", 25.0, "SN-42");
```

## See Also

- [Numerical Propagation](../tutorials/numerical-propagation.md)
- [Maneuvers](../tutorials/maneuvers.md)
- [Attitudes](attitudes.md)
- [OPM](opm.md)
