# Time & Windows

## Time

`Time` is an instant: a calendar (`DateTime`, to the tick of 100 ns) in a time scale (`Scale`, a `TimeScale`). The scale
is a required argument of every constructor and factory (D3 of #668, approved by S. Guillet, 2026-11-13, amended 2026-11-19): before 11.0.0 a
calendar without a frame was TDB, 69.18 s from the UTC most callers meant.

### Time Scales

| Member | Description |
|--------|-------------|
| `TimeScale.TDB` | Barycentric Dynamical Time, the ephemeris time of SPICE (ET); 0, so `default(Time)` is 0001-01-01T00:00:00 TDB |
| `TimeScale.TT` | Terrestrial Time, TAI + 32.184 s (TDT before 11.0.0) |
| `TimeScale.TAI` | International Atomic Time |
| `TimeScale.UTC` | Coordinated Universal Time, TAI less the leap seconds |
| `TimeScale.GPS` | GPS time, TAI − 19 s |

A local time is not a scale: its instant depends on the zone of the machine. The calendar of an instant in the zone of
the machine is `t.ToUTC().DateTime.ToLocalTime()`.

### Static Constants

| Constant | Description |
|----------|-------------|
| `Time.J2000TDB` | J2000, 2000-01-01T12:00:00 TDB |
| `Time.J2000UTC` | 2000-01-01T12:00:00 UTC |
| `Time.UtcNow` | The clock of the machine, in UTC |

### Constructors

| Constructor | Description |
|-------------|-------------|
| `Time(DateTime dateTime, TimeScale scale)` | The calendar in the scale. A `DateTime` of kind `Local` throws `ArgumentException` (give its universal time with `TimeScale.UTC`), and so does one of kind `Utc` with another scale than UTC; before 11.0.0 the frame replaced the kind silently |
| `Time(int year, int month, int day, TimeScale scale)` | Midnight of the day in the scale |
| `Time(int year, int month, int day, int hour, int minute, int second, TimeScale scale)` | The calendar to the second |
| `Time(int year, int month, int day, int hour, int minute, int second, int millisecond, TimeScale scale)` | The calendar to the millisecond |
| `Time(int year, int month, int day, int hour, int minute, int second, int millisecond, int microsecond, TimeScale scale)` | The calendar to the microsecond |

A scale outside the enumeration throws `ArgumentOutOfRangeException`.

### Parsing and Formatting

`Time` is `ISpanParsable<Time>` and `ISpanFormattable`, on one grammar (D3.4 of #668):

- an ISO 8601 date `yyyy-MM-dd` or a CCSDS day of year `yyyy-DDD`, then optionally `T` (or a space) and `hh:mm`,
  `hh:mm:ss` or `hh:mm:ss.f…` (a fraction of up to 7 digits exact, a longer one rounded to the tick as `DateTime`
  parsing rounds it);
- then `Z` or an offset `±hh:mm`, which give the UTC instant (never the machine's local time), or the tag of a scale:
  `UTC`, `TT` (or `TDT`), `TDB`, `TAI` or `GPS`, in any case, the space before it optional;
- or a Julian date, `JD n TAG` or `n JD TAG`, its digits read exactly to the nearest tick (ties to even).

| Method | Description |
|--------|-------------|
| `Parse(string s)` | The time a tagged string writes; a string without a tag, `Z` or offset throws `FormatException` |
| `Parse(string s, TimeScale scale)` | The same, `scale` the scale of an untagged string; a tag of the same scale is accepted (`TDT` for TT, `Z` or an offset for UTC), and a tag, `Z` or offset of another scale contradicts it and throws `FormatException` (D3.4) |
| `TryParse(string s, out Time result)`, `TryParse(string s, TimeScale scale, out Time result)` | The same, false instead of an exception; they never throw |
| `ToString()` | The calendar in the round-trip form of `DateTime` (`O`), then `Z` for UTC or ` TDB`, ` TT`, ` TAI`, ` GPS`; `Parse` reads it back exactly (`EqualsExact`). TT is written `TT` (`TDT` before 11.0.0) |
| `ToString(string format, IFormatProvider provider)` | `null`, `G`, `O` or `o` give `ToString()`; any other format is a custom `DateTime` format of the calendar followed by the tag (`Z` for UTC, ` TDB`, ` TT`, ` TAI`, ` GPS` otherwise): `$"{t:yyyy-MM-dd}"` is `2024-06-21 TDB`. A standard format (one letter: `u` and `R` write their own UTC designator) and a zone specifier (`z`, `zz`, `zzz`, `K`: the machine's offset for a calendar of another scale than UTC) throw `FormatException`, the scale being the tag; a quoted or escaped letter is a literal (D3.5) |

`Parse` throws `FormatException` only (a Julian date beyond every `Time` included, however many digits), and refuses a second 60 (a `Time` holds no leap second: give a UTC leap second in
TAI), a field out of range, a tag after `Z` or an offset, and the forms `Time(string)` read through `DateTime.TryParse`
before 11.0.0 (`06/21/2024 12:00`, RFC 1123, a `GMT` suffix). The CCSDS readers (OPM, OMM, CDM) use the strict branch of
the same grammar: `T` and the seconds required, no tag (#619).

### Static Factories

| Method | Description |
|--------|-------------|
| `Create(double secondsFromJ2000, TimeScale scale)` | The calendar of 2000-01-01T12:00:00 of the scale plus the seconds, to the nearest tick |
| `FromDayOfYear(int year, int dayOfYear, double seconds, TimeScale scale)` | A day of year (1 to 365, or 366 in a Gregorian leap year) of a year (1 to 9999) and the seconds of that day (finite, in [0, 86400)), to the nearest tick, in the scale; `ArgumentOutOfRangeException` names an argument out of its range (#680). It replaces Create(year, doy, sec), which was UTC |
| `CreateTDB(double secondsFromJ2000)` | Create TDB time from J2000 seconds, to the nearest tick |
| `CreateUTC(double secondsFromJ2000)` | Create UTC time from J2000 seconds, to the nearest tick |
| `CreateFromJD(double julianDate, TimeScale scale)` | Create from Julian Date, J2000 plus the nearest tick of the days `julianDate` − 2451545.0 (#637); a non-finite date throws `ArgumentOutOfRangeException` (#680) |

A span of seconds becomes the nearest tick (100 ns) of the exact value of its double, ties to even, in `Create`,
`CreateTDB`, `CreateUTC`, `AddSeconds`, `AddMilliseconds`, `AddMicroseconds` and the TDB − TT offset of the TDB
conversions ([#625](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/625)); a value that is
not finite, or whose ticks do not fit in a `long`, throws `ArgumentOutOfRangeException`. Before 11.0.0 these truncated
toward zero: up to a tick early after J2000 and late before it, so that a spacecraft built at the first instant of its
SPK read 84 ns before its coverage. The rounding is symmetric, so a span and its opposite give a time back.

What a double can hold limits both directions:

- **From 2017 to 2028, and from 1971 to 1983** (|ET| from 2^29 s to 2^53 ticks) an ulp of the ET is coarser than a tick: an ET of SPICE comes back from
  `TimeSpanFromJ2000()` exactly, but a tick made seconds and back is not always that tick. The double nearest a tick can
  be up to 0.6 tick from it, and for about 16 % of ticks its nearest tick is the neighbour. So the epochs of SPICE are not
  passed through seconds again where the tick asked is known: `SpiceAPI.ReadEphemeris(Time …)` and the initial state of
  a kernel-backed spacecraft carry the epoch asked, as `SpiceDataProvider.GetEphemeris` does, and an end of a
  CSPICE search (`FindWindowsOn…Constraint` of `SpiceAPI`) at an end of its search window is that end. Where SPICE
  gives the epoch itself (the grid of `ReadEphemeris(Window …)` and `ReadOrientation`, a transition inside a window, the
  element conversions of `SpiceAPI`), the label is the nearest tick of its double, within a tick of any tick it came from.
- **From 1983 to 2017** (|ET| below 2^29 s) a tick is coarser than an ulp, so no tick holds every ET: an ET comes back
  within half a tick, and a tick made seconds and back is that tick. The nearest tick of a coverage bound (`spkcov`, `pckcov`) lies outside the
  coverage for about a third of the bounds, at either end (on the double SPICE receives, 50 000 bounds per range: starts
  30 % from 2000 to 2017 and 37 % from 1990 to 2000, ends the same). The truncation kept every start from 1983 to J2000 and
  every end from 2000 to 2017 inside, and put 80 % of the starts from 2000 to 2017 and 87 % of the ends from 1990 to 2000
  outside, so a read at a bound that worked before 11.0.0 can now throw (since #454), and the reverse. Read from the
  first tick inside: one tick later at a start, one tick earlier at an end. The coverage of ITRF93 in the test kernels starts at
  −43135.816087188054 s, whose nearest tick is 11.9 ns before it (the truncation gave the tick 88.1 ns after it).

The same rule holds for `AddDays`, `AddHours`, `AddMinutes` and `CreateFromJD`, with the ticks per day, hour and
minute (the days of `CreateFromJD` from J2000, `julianDate − 2451545.0`, are exact from JD 1225772.5 to 4903090 by
Sterbenz's lemma), which throw for a non-finite argument (#680: NaN returned the epoch, or J2000 for `CreateFromJD`),
and for the spans the library computes outside `Time` (internal `Time.SecondsToSpan`, which keeps the exception types
of `TimeSpan.FromSeconds`, `ArgumentException` for NaN and `OverflowException` for an infinity or from 922 337 203 685 s,
the bound of `SecondsToTicks`, 0.48 s below the 922 337 203 685.477 s `TimeSpan.FromSeconds` accepted):
`OrbitalParameters.Period()`, `CelestialBody.SideralRotationPeriod` and `TrueSolarDay`, the
offsets of the launch windows after the first (`Launch`: half the sidereal period in seconds plus ±2 δL/ω, rounded
once), the thrust window of an impulse (`Tsiolkovski.DeltaT`), the
hold of `PhasingManeuver`, the step, candidate, seed, secant and subdivision epochs of the conjunction search and the
period of the element conversions of `SpiceAPI`
([#637](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/637), decided by S. Guillet,
2026-11-17). Before 11.0.0 they truncated toward zero, a tick early for about half of the values (8 733 of 20 000
random spans, 16 of 23 orbital periods, 43 of 80 sidereal periods of the Earth and the Moon, 40 of 65 launch window
offsets). `TimeSpan.FromSeconds`
of a constant that a tick holds exactly (60 s, 32.184 s, 19 s, whole leap seconds) is not concerned.

### Time Scales

The conversions between the scales are managed and read no kernel:

- **TAI − UTC** comes from the leap second table of the library (internal `TimeScaleConversions`): 10 s from 1972-01-01 to 37 s from 2017-01-01, its last
  entry, each offset counting from 00:00:00 UTC of its date. Before 1972 it is a fixed 9 s, as in the NAIF LSK (the USNO
  table `tai-utc.dat`, which GMAT reads, has offsets that drift from 1961 to 1971 and none before 1961). A leap second
  announced after 2017-01-01 needs a new entry in the table.
- **TT** (`ToTT()`) is TAI + 32.184 s, and **GPS** is TAI − 19 s.
- **TDB − TT** is the one-term series 0.001658 sin(g + 0.0167 sin g) s, with g = 357.528° + 35999.05° T and T the Julian
  centuries from J2000 of the TT time converted to TDB, or of the TDB time converted to TT, rounded to the nearest tick.
  It is a short series, like those of the LSK and of GMAT. Measured from 1972 to 2100 (TT instants every 0.05 day, 935 041
  epochs, [#455](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/455)), the series is within 1.006 µs (rms 0.709 µs) of the LSK formula that SPICE `unitim` evaluates,
  1.6570e-3 sin(M + 1.671e-2 sin M) s with M = 6.239996 + 1.99096871e-7 t (t the TT seconds from J2000), and within
  1.055 µs (rms 0.710 µs) once rounded to the tick; it is within 37.34 µs of the full Fairhead & Bretagnon series (ERFA
  `dtdb`, rms 17.11 µs). Over the 6 679 epochs every 7 days of #455, it is within 0.232 µs of GMAT R2026a's series.

CSPICE is given the managed TDB as its ephemeris time (`ToTDB()`). The NAIF LSK kernel, which CSPICE needs loaded, does
not enter the managed conversions.

### Conversion Methods

| Method | Description |
|--------|-------------|
| `ConvertTo(TimeScale scale)` | Convert to any scale; to the scale of the time, the time itself ([#510](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/510)) |
| `ToTDB()` | Convert to TDB |
| `ToUTC()` | Convert to UTC |
| `ToTAI()` | Convert to TAI |
| `ToTT()` | Convert to TT (ToTDT before 11.0.0) |
| `ToGPS()` | Convert to GPS time |
| `ToJulianDate()` | Get the Julian date |

### Arithmetic Methods

| Method | Description |
|--------|-------------|
| `Add(TimeSpan)` | Add a duration |
| `AddYears(int)` | Add calendar years |
| `AddMonths(int)` | Add calendar months |
| `AddDays(double)` | Add days, to the nearest tick (#637); a non-finite argument throws `ArgumentOutOfRangeException` (#680) |
| `AddHours(double)` | Add hours, to the nearest tick (#637); a non-finite argument throws `ArgumentOutOfRangeException` (#680) |
| `AddMinutes(double)` | Add minutes, to the nearest tick (#637); a non-finite argument throws `ArgumentOutOfRangeException` (#680) |
| `AddSeconds(double)` | Add seconds, to the nearest tick |
| `AddMilliseconds(double)` | Add milliseconds, to the nearest tick |
| `AddMicroseconds(double)` | Add microseconds, to the nearest tick |
| `AddTicks(long)` | Add ticks |

### Utility Methods

| Method | Description |
|--------|-------------|
| `TimeSpanFromJ2000()` | Get TimeSpan from J2000 epoch |
| `Centuries()` | Get Julian centuries from J2000 |

### Operators

| Operator | Description |
|----------|-------------|
| `Time + TimeSpan` | Add duration to time: calendar arithmetic in the scale of the time, so across a leap second a UTC time and the time a span after it are the span plus the leap second apart, `(t + d) - t` is `d + 1 s` |
| `Time - TimeSpan` | Subtract duration from time, calendar arithmetic as `+` |
| `Time - Time` | Duration between two instants: the difference of the calendars in TDB, TT, TAI and GPS, through TAI for UTC times and across scales, so that leap seconds count ([#508](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/508)) |
| `<`, `>`, `<=`, `>=` | Comparison of the instants, by their TAI ticks, within a scale and across scales (`CompareTo`) |
| `==`, `!=`, `Equals` | The same instant, whatever the scales: `CompareTo` returns 0 ([#512](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/512)) |
| `EqualsExact(Time other)` | The same label: the same calendar, to the tick, in the same scale |
| `Time.ExactComparer` | The equality of labels (`EqualsExact`), for a hash collection that keeps the labels apart |

### Equality and Order

Two times are equal when they are the same instant, whatever their scales (#512, decided by S. Guillet, 2026-11-17; the
TAI tick confirmed as the key by S. Guillet, 2026-11-19, D3 as amended): `==`, `!=`, `Equals` and `GetHashCode` follow
`CompareTo`, the one order of times, which compares their ticks converted to TAI. So `utc == utc.ToTDB()`, both hash
alike, and a `Dictionary`, a `HashSet`, `Distinct` or `IndexOf` keyed by times finds the same instant in another scale,
as `SortedSet` did. Within UTC, TAI, TT and GPS the order is that of the calendars; within TDB too for times two ticks or
more apart, but two consecutive TDB ticks can convert to one TAI tick (TDB − TT, rounded to the tick, gains one there,
about every 300 s near its steepest), and then they are one instant, equal to each other and to that TAI time, so that
equality stays transitive. A UTC time is never equal to a TAI time in an inserted leap second (UTC 23:59:60 has no
`Time`). The default time is 0001-01-01T00:00:00 TDB, the earliest TDB calendar.

To tell two labels of one instant apart, use `EqualsExact` (the calendar and the scale), or `Time.ExactComparer` for a
`Dictionary` or a `HashSet`. Within TDB, `-` still counts the ticks of the calendars, so two equal consecutive TDB ticks
differ by one tick. The library's caches whose values carry their key as a label (the orientation a frame keeps from
its last read, #541, the orientations of `TLEPropagator`, the range rates of the conjunction search) are keyed by label,
an epoch in another scale read again, bit for bit as before. Before 11.0.0 equality was the record's, on the calendar
and the frame, while `CompareTo` compared through TAI across frames: the same instant in two frames compared to 0 and
was not equal.

### Example

```csharp
var t1 = new Time(2024, 6, 21, 12, 0, 0, TimeScale.UTC);
var t2 = Time.Parse("2024-06-21T12:00:00.000 UTC");
var t3 = Time.Parse("2024-173T12:00:00Z");
var t4 = Time.Parse("JD 2460483.0 UTC");
var doy = Time.FromDayOfYear(2024, 173, 43200.0, TimeScale.UTC);
bool sameLabel = t1.EqualsExact(t2) && t1.EqualsExact(t3) && t1.EqualsExact(t4) && t1.EqualsExact(doy); // true
var tdb = t1.ToTDB();
bool sameInstant = tdb == t1; // true: one instant in two scales
string text = $"{tdb:yyyy-MM-dd HH:mm:ss}"; // 2024-06-21 12:01:09 TDB
var later = t1.AddDays(1.0);
var duration = later - t1;
var fromJD = Time.CreateFromJD(2451545.0, TimeScale.TDB);
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
| `==`, `!=`, `Equals` | The same two ends, instants compared whatever their scales (#512): a window and the window of the same instants in TDB are equal; `Length` does not take part |
| `Length` | Window duration, `EndDate - StartDate`: 121 s for the two UTC minutes around the leap second of 2016-12-31; the length given to `Window(Time, TimeSpan)`, whose `EndDate` is `start + length` in calendar arithmetic, so a UTC window built from a length across a leap second keeps that length while `EndDate - StartDate`, and a propagation over it, count the leap second |
| `Merge(Window)` | Merge two overlapping windows |
| `Intersects(Window)` / `Intersects(Time)` | Check overlap |
| `GetIntersection(Window)` | Get overlapping portion |

### Example

```csharp
var window = new Window(new Time(2024, 1, 1, TimeScale.TDB), new Time(2024, 1, 31, TimeScale.TDB));
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

The maneuver chain counts the same seconds (#547): about the Earth the thrust and maneuver windows of an impulse are
counted in TT seconds from the time argument of its event, and the hold of an attitude in TT seconds from its
activation; their ends, TDB labels, are the epochs of TT₀ + τ. About any other body they are TDB arithmetic on the
labels, bit for bit as before 11.0.0. An activation at the end of a hold that ends on an output is then that output's
epoch ([Attitudes](attitudes.md#the-attitude-timeline)).

### The Length of a Window

The window has the TT seconds between its two instants, whatever their tags: exact in ticks between UTC, TAI, TT and GPS
tags, through IO's TDB − TT series for a TDB tag.

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

A solution is read at an epoch in any scale
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

On an ellipse, `EpochAtMeanAnomaly(m)` is the next passage at m modulo 2π, at or after the epoch, whatever the turn of
m and of the mean anomaly at the epoch, so `ToStateVector(trueAnomaly)` is the next passage at that true anomaly; on an
open orbit it is the single passage, before the epoch when m is behind. Before 11.0.0 it added 2π at most once: from a
`StateVector`, whose mean anomaly is in (−π, π), the passage came one period late, and at e = 0.999999, where
`KeplerianElements` keeps its mean anomaly unreduced, it could be in the past
([#474](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/474)).

## See Also

- [Standards & Units](../standards-and-units.md)
