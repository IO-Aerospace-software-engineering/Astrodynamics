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
| `ConvertTo(ITimeFrame frame)` | Convert to any time frame |
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
| `Time + TimeSpan` | Add duration to time |
| `Time - TimeSpan` | Subtract duration from time |
| `Time - Time` | Duration between two instants |
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
| `Length` | Window duration |
| `Merge(Window)` | Merge two overlapping windows |
| `Intersects(Window)` / `Intersects(Time)` | Check overlap |
| `GetIntersection(Window)` | Get overlapping portion |

### Example

```csharp
var window = new Window(new Time(2024, 1, 1), new Time(2024, 1, 31));
Console.WriteLine($"Duration: {window.Length.TotalDays} days");
```

## See Also

- [Standards & Units](../standards-and-units.md)
