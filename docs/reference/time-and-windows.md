# Time & Windows

## Time

`Time` represents an instant with explicit time-frame conversion support.

### Static Constants

| Constant | Description |
|----------|-------------|
| `Time.J2000` | J2000 epoch (January 1, 2000 12:00 TDB) |
| `Time.J2000TDB` | J2000 in TDB frame |
| `Time.J2000UTC` | J2000 in UTC frame |

### Constructors

| Constructor | Description |
|-------------|-------------|
| `Time(int year, int month, int day, ...)` | Create from date components (hour, minute, second, ms, us optional; defaults to TDB) |
| `Time(DateTime dateTime, ITimeFrame frame)` | Create from DateTime with specified frame |
| `Time(string timeString)` | Parse from string (e.g., `"2024-06-21T12:00:00.000 UTC"`) |

### Static Factories

| Method | Description |
|--------|-------------|
| `Create(double secondsFromJ2000, ITimeFrame frame)` | Create from J2000 seconds in a given frame |
| `Create(int year, int doy, double sec)` | Create from year, day-of-year, seconds-of-day |
| `CreateTDB(double secondsFromJ2000)` | Create TDB time from J2000 seconds |
| `CreateUTC(double secondsFromJ2000)` | Create UTC time from J2000 seconds |
| `CreateFromJD(double julianDate, ITimeFrame frame)` | Create from Julian Date |

### Conversion Methods

| Method | Description |
|--------|-------------|
| `ConvertTo(ITimeFrame frame)` | Convert to any time frame; to the frame of the time, the time itself ([#510](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/510)) |
| `ToTDB()` | Convert to TDB |
| `ToUTC()` | Convert to UTC |
| `ToTAI()` | Convert to TAI |
| `ToTDT()` | Convert to TDT |
| `ToGPS()` | Convert to GPS time |
| `ToLocal()` | Convert to local time |
| `ToJulianDate()` | Get the Julian date |

### Arithmetic Methods

| Method | Description |
|--------|-------------|
| `Add(TimeSpan)` | Add a duration |
| `AddYears(int)` | Add calendar years |
| `AddMonths(int)` | Add calendar months |
| `AddDays(double)` | Add days |
| `AddHours(double)` | Add hours |
| `AddMinutes(double)` | Add minutes |
| `AddSeconds(double)` | Add seconds |
| `AddMilliseconds(double)` | Add milliseconds |
| `AddMicroseconds(double)` | Add microseconds |
| `AddTicks(long)` | Add ticks |

### Utility Methods

| Method | Description |
|--------|-------------|
| `TimeSpanFromJ2000()` | Get TimeSpan from J2000 epoch |
| `Centuries()` | Get Julian centuries from J2000 |

### Operators

| Operator | Description |
|----------|-------------|
| `Time + TimeSpan` | Add duration to time: calendar arithmetic in the frame of the time, so across a leap second a UTC (or local) time and the time a span after it are the span plus the leap second apart, `(t + d) - t` is `d + 1 s` |
| `Time - TimeSpan` | Subtract duration from time, calendar arithmetic as `+` |
| `Time - Time` | Duration between two instants: the difference of the calendars in TDB, TT, TAI and GPS, through TAI for UTC and local times, so that leap seconds count ([#508](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/508)) |
| `<`, `>`, `<=`, `>=` | Comparison operators |

### Example

```csharp
var t1 = new Time(2024, 6, 21, 12, 0, 0);
var t2 = new Time("2024-06-21T12:00:00.000 UTC");
var tdb = t1.ToTDB();
var utc = tdb.ToUTC();
var t3 = t1.AddDays(1.0);
var duration = t3 - t1;
var fromJD = Time.CreateFromJD(2451545.0, TimeFrame.TDBFrame);
```

## Window

`Window` models a time interval with intersection and merge operations.

### Constructors

| Constructor | Description |
|-------------|-------------|
| `Window(Time start, Time end)` | Create from start and end times |
| `Window(Time start, TimeSpan duration)` | Create from start and duration |

### Properties & Methods

| Member | Description |
|--------|-------------|
| `StartDate` / `EndDate` | Interval boundaries |
| `Length` | Window duration, `EndDate - StartDate`: 121 s for the two UTC minutes around the leap second of 2016-12-31; the length given to `Window(Time, TimeSpan)`, whose `EndDate` is `start + length` in calendar arithmetic, so a UTC window built from a length across a leap second keeps that length while `EndDate - StartDate`, and a propagation over it, count the leap second |
| `Merge(Window)` | Merge two overlapping windows |
| `Intersects(Window)` / `Intersects(Time)` | Check overlap |
| `GetIntersection(Window)` | Get overlapping portion |

### Example

```csharp
var window = new Window(new Time(2024, 1, 1), new Time(2024, 1, 31));
Console.WriteLine($"Duration: {window.Length.TotalDays} days");
```

## The Time Argument of a Propagation

A numerical propagation integrates over the seconds of the dynamical time of its central body
([#501](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/501)):

- **about the Earth** (the resolved central body is NAIF 399), the TT seconds between the instants of its window. TT is
  the coordinate time of the geocentric reference system, a scaled TCG, and TDB the barycentric time of the
  ephemerides (IERS Conventions (2010), G. Petit and B. Luzum (eds.), IERS Technical Note 36, chapter 10; confirmed by S. Guillet, 2026-10-29). GMAT counts TT: GMAT R2026a evaluates the
  derivatives at the epoch plus the elapsed seconds in A1 (`src/base/forcemodel/ODEModel.cpp:3201`), and A1 is TAI
  plus a constant (`src/gmatutil/util/TimeSystemConverter.cpp:282-286`), so its seconds are TT seconds. The conformance
  suite prescribes TT (spec.md §12.1). The forces at t are those of the instant TT₀ + t: the bodies and the orientation of
  the Earth at its TDB, the drag at its UTC;
- **about any other body**, SSB mode included (a star or a barycentre resolves to the Sun), the TDB seconds of the
  window, as before 11.0.0, bit for bit.

The labels stay TDB: every segment base, stage epoch, event epoch and output epoch is a TDB `Time`, which the caches,
SPICE and the stored states read as they did. About the Earth only the instant a label names changes. `PropagationSegment.Duration`,
`AcceptedStep.CumulativeTime` and `StepSize`, the event time and the duration passed to `IIntegrator.IntegrateSegment`
count the seconds of the time argument (TT about the Earth).

### The Length of a Window

The window has the TT seconds between its two instants, whatever their tags: exact in ticks between UTC, TAI, TT, GPS
and local tags, through IO's TDB − TT series for a TDB tag.

| Window about the Earth | TT length |
|------------------------|-----------|
| A UTC day without a leap second | 86 400 s |
| The UTC day across the leap second of 2016-12-31 | 86 401 s |
| A TDB-tagged day from 2021-03-20 12:00 | 86 399.99999324768 s |

### The Outputs and the End

- **Output grid**: uniform in TT, label i the TDB label of TT₀ + i·ΔT, each output the state at its label. The outputs
  of a UTC window fall on its UTC grid; after a leap second inside the window they sit one second off the UTC minute
  marks (the grid of a UTC day across 2016-12-31 ends at 86 400 s, one second before the end). Those of a TDB-tagged
  window leave its TDB grid by the change of TDB − TT, up to 29 µs a day and 0.86 ms over 30 days: a read at a round
  TDB epoch is then interpolated, and an exact-key lookup of a `Time` misses the output epochs. The TDB gaps between
  the labels vary by a tick: an SPK of type 8 (equal steps), which the native writer chose for equal TDB steps, would
  misplace them. `Spacecraft.WriteEphemeris` writes segments of type 9 (Lagrange, unequal steps) about every central
  body, one per arc between impulses ([The stored states across an impulse](spacecraft.md#the-stored-states-across-an-impulse),
  #540); about any other body than the Earth the labels are those of before 11.0.0, bit for bit.
- **End**: when the output grid misses the length of the window by less than 1e-9 of it (three times the largest rate
  of TDB − TT), the propagation ends at the end of the window, and its last output is labelled with the end of the
  window as the caller gave it. Otherwise the propagation runs to the larger of the window and of its grid, as before.
- **Start**: the TDB label of the start, as before, 0 to 1 tick (100 ns) from the instant of a UTC start.

### The Tick

A label holds its instant to the tick of `Time`: at or before it by less than 1.0012e-7 s. Reading a solution, or
chaining propagations, through the labels a user receives therefore keeps a sub-tick time: up to 1e-7 s, about 1e-10
relative on the state transition matrix and 0.75 mm in LEO. The references of the library's own validation read at an
exact elapsed time instead.

A solution is read at an epoch in any frame
([#507](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/507)): a UTC, TAI, TT or GPS epoch
names the same instant as its TDB label, to the tick, and the bounds of the solution are read in the seconds of the time
argument, so the UTC start and end of a UTC window are inside it. Before 11.0.0 a non-TDB epoch read on a solution about
any central body was taken as a TAI difference from the TDB base, another instant by the change of TDB − TT (0.22 m after
a day in LEO), and the UTC start or end of a UTC window could compare a tick outside the solution through TAI, where the
getters threw `ArgumentOutOfRangeException`. About the Earth the TT time argument reads the TT seconds of the epoch;
about any other body the epoch is converted to TDB first (2.1 cm about the Moon in the tests of the fix).

### Velocities

About the Earth the velocities of the input and output states are per TT second, as GMAT and the suite take them. A
state from a TDB-rate source (an SPK, an OEM or OPM in TDB) differs from dx/dTT by up to 3.4e-10 relative: as an initial
condition, about 0.65 m a day along-track in LEO (computed). The velocities are not scaled.

### Two-Body Motion

`AtEpoch`, `ToKeplerianElements(epoch)`, `ToStateVector(epoch)`, `EpochAtMeanAnomaly` and `ToStateVector(trueAnomaly)`,
the time of flight of the Lambert solver and the intervals of the initial orbit determination count the same time:
TT seconds about the Earth, TDB seconds about any other body, whatever the tags of the epochs, the leap seconds between
two UTC epochs included, and none at the same instant
([#508](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/508)). A `TimeSpan` added to a TDB
epoch is a span of TDB: about the Earth, `elements.ToStateVector(elements.Epoch + elements.Period())` from a TDB epoch is
off the start by the change of TDB − TT over the period (1.2e-6 s of motion over an hour at J2000); from a TT epoch it
returns there, and from a UTC epoch away from a leap second. `Time + TimeSpan` stays calendar arithmetic on UTC: from a
UTC epoch whose period holds a leap second, `Epoch + Period()` is a period and one second of TT later, and the state
is one second of motion past the start.

## See Also

- [Standards & Units](../standards-and-units.md)
