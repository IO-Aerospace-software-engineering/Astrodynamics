# Command-Line Tool

`astro` is the command-line tool of IO.Astrodynamics: ephemerides, orientations, window searches, conversions of orbits,
frames and times, and propagations, from SPICE kernels. It ships as a .NET global tool:

```bash
dotnet tool install -g IO.Astrodynamics.CLI
astro --help
```

Its grammar is one for every command (R29 of [#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668),
11.0.0): the objects of a command are its positional arguments, everything else is an option.

```bash
astro ephemeris MOON EARTH -K kernels --begin 2024-01-01T00:00:00Z --end 2024-01-02T00:00:00Z --step 12h --aberration LT+S
astro ephemeris -1000 EARTH -K kernels -K out --begin 0 --end 3600 --step 60s --format csv
astro propagate Sat -K kernels --output out --from keplerian --orbit "6800km 0.001 51.6deg 0 0 0" --center EARTH --epoch 0 --frame ICRF --begin 0 --end 86400 --drag
```

## Kernels

`-K` (`--kernels`) gives a kernel file, a meta-kernel or a directory of kernels; it is repeatable, and the paths of the
environment variable `ASTRO_KERNELS`, separated by the path separator of the platform (`:`, `;` on Windows), are loaded
first, then those of `-K` in order. A command that reads the kernels without any, or with a path that does not exist,
or a file SPICE refuses, ends with exit code 3. `time-converter` reads no kernel.

## Objects, values, epochs and steps

- **Objects.** A NAIF identifier (`399`, `-1000`, a negative number given bare) or a name SPICE knows, built in or given
  by the loaded kernels (`EARTH`, `Moon`, `DSS-13`, `SOLAR SYSTEM BARYCENTER`), read by `SpiceApi.TryGetNaifId`
  (`bodn2c_c`, choice 12 of #668). An identifier is not checked: the spacecraft a propagation wrote has no name.
- **Values.** A number in invariant culture, a dot as decimal separator and no thousands separator, finite, in SI units and
  radians, or followed by a unit of its dimension: `km` for a length, `km/s` for a speed, `deg` for an angle
  (`400000km`, `7.5km/s`, `51.6deg`); `m`, `m/s`, `rad`, `kg` and `m2` are read too.
- **Steps.** `[d.]hh:mm:ss[.f]`, or a number and `s`, `min`, `h` or `d` (`60s`, `1.5min`, `12h`, `1d`), above zero. A bare
  number has no unit, and is refused: it was a number of days before 11.0.0.
- **Epochs.** An ISO 8601 date or a day of year with an optional time, then `Z`, an offset or a scale tag
  (`2024-01-01T21:00:00Z`, `2024-001T21:00:00 UTC`), seconds from J2000 with an optional tag (`144521.0 UTC`), or a
  Julian date (`2460310.5 JD UTC`); the tags are UTC, TT, TDT, TDB, TAI and GPS, TDB when none is given (the grammar of
  `Time.Parse`, [time and windows](time-and-windows.md)).
- **Frames.** ICRF or J2000, in any case, is `Frame.ICRF`, written `J2000`; any other name is a frame of the library
  (TEME, CIRS, TIRS, GCRF) or of the loaded kernels, checked once they are loaded.
- **Enumerations.** The aberration, the relation, the coordinate system and coordinate, the shapes, the occultation and the
  illumination angle are read by their SPICE name (`LT+S`, `<=`, `RA/DEC`, `RIGHT ASCENSION`) or their member name
  (`LtS`, `LessThanOrEqual`, `RaDec`, `RightAscension`), in any case.

## Output

`--format` chooses the output: `text`, the default, writes the lines of the library's `ToString`; `csv` writes the rows
of the commands that write rows (`ephemeris`, `orientation`, the window searches), a header of field names with their
unit (`epoch,frame,x_m,y_m,z_m,vx_m_s,vy_m_s,vz_m_s`) then one line per row; `json` writes the record of the commands
that write one (`celestial-body-info`, `sub-point`, `angular-separation`, `frame-converter`,
`orbital-parameters-converter`), an object of the same named fields, a value the body has not (J2 to J4) `null`. Every real
is written in its shortest round-trip form, read back bit for bit, an epoch as its label and scale.

The epochs written are in the scale of the input epoch, that of `--begin` for a window (UTC labels for a `Z` input);
`--time-scale tdb|tt|tai|utc|gps` writes another. A label converted to another scale is the nearest tick of its instant,
which can sit one tick off it.

The standard output holds the data only. Each error is one line on the standard error, `Error: ` and its message;
`--verbose` adds its trace.

| Exit code | Meaning |
|---|---|
| 0 | Success, and the help and the version |
| 1 | An error of the library or of SPICE: a read outside the coverage of the kernels, a search SPICE cannot run |
| 2 | An argument the command cannot read: an unknown command, option, object, frame, value or unit, a missing argument, two options that exclude each other |
| 3 | The kernels: none given, a path that does not exist, a file SPICE refuses |

## Propagation

`propagate` runs the library's propagation with its defaults (choice 12 of #668): RK7(8) at 1e-9 with outputs every 60 s
(`--step`), a drag coefficient of 2.2, and with `--drag` NRLMSISE-00 at nominal space weather for the Earth; Mars takes its
standard atmosphere, and the drag about another centre is refused. The Earth carries EGM2008 to `--degree` (10 by
default, 100 at most). It writes the kernels of the spacecraft, NAIF identifier -1000, and a Cosmographia scenario in
`--output`, and prints the identifier and the directory. `--integrator vv --step 1s --cd 0.3` gives the settings of the
command before 11.0.0, with `--atmosphere standard` for a run with `--drag` about the Earth, whose SPK it writes byte for
byte. An atmosphere is read by the drag only: `--atmosphere` without `--drag`, or about another centre than the Earth, is
an input error, and so are `--drag` about another centre than the Earth and Mars, a `--body` that is the centre, a mass
or an area of 0 or less, a negative drag coefficient, a name that is not a portable file name (a `/`, `\`, `:` or another character
Windows refuses, a trailing dot or space, a device name such as CON or NUL; the rule of the
[Cosmographia export](../guides/cosmographia-export.md#scenario-export), #799) and a name that cannot name a kernel
variable (a blank, a quote, more than 32 bytes with its frame), all refused before the propagation.
The export refuses, after the propagation, an `--output` whose previous export SPICE holds: the kernels given with `-K`
above or inside it, or a meta-kernel that names its files (decision 103 of #668,
[Cosmographia export](../guides/cosmographia-export.md#scenario-export)); give another directory.

## Known limitations

The window searches are those of CSPICE (`gfdist`, `gfposc`, `gfoclt`, `gftfov`, `gfilum`), on objects of the kernels;
the managed searches of the library, the conjunction assessment and the CDM have no command.

## Migrating from 10.x

11.0.0 keeps no positional form of 10.x but the aliases of `time-converter` (`-t -u -l -j -e -d`). Each command of 10.x
and its form in 11.0.0 (`k` the kernels, `B` and `E` the window, `T` an epoch):

```text
ephemeris k 301 399 B E 00:01:00 ICRF LT -k
ephemeris 301 399 -K k --begin B --end E --step 1min --aberration LT --as keplerian

orientation k 399 B E 00:01:00 ITRF93
orientation 399 -K k --begin B --end E --step 1min --frame ITRF93

sub-point k 301 399 T LT -d
sub-point 301 399 -K k --epoch T --aberration LT --as planetodetic        (-c: --as cartesian)

angular-separation k 399 301 10 T
angular-separation 399 301 10 -K k --epoch T

celestial-body-info k 399
celestial-body-info 399 -K k

frame-converter k ICRF ITRF93 T
frame-converter ICRF ITRF93 -K k --epoch T

orbital-parameters-converter k 399 "a e i raan aop m" T0 ICRF -k --to-state-vector T ECLIPJ2000
orbital-parameters-converter -K k --from keplerian --orbit "a e i raan aop m" --center 399 --epoch T0 --frame ICRF --as state --target-epoch T --target-frame ECLIPJ2000

orbital-parameters-converter k 399 "<line 1>,<line 2>" T0 ICRF -t --to-keplerian
orbital-parameters-converter -K k --from tle --orbit "<line 1>" --orbit "<line 2>" --as keplerian

propagate k Sat 399 "x y z vx vy vz" T0 ICRF B E out -r -a -n 20 100 1 0.3 10 301
propagate Sat -K k --output out --from state --orbit "x y z vx vy vz" --center 399 --epoch T0 --frame ICRF --begin B --end E --srp --drag --degree 20 --mass 100 --area 1 --body 10 --body 301 --integrator vv --step 1s --cd 0.3 --atmosphere standard

propagate k Sat 301 "x y z vx vy vz" T0 ICRF B E out
propagate Sat -K k --output out --from state --orbit "x y z vx vy vz" --center 301 --epoch T0 --frame ICRF --begin B --end E --integrator vv --step 1s --cd 0.3

find-windows-from-distance-constraint k 399 301 "<" 400000000 B E 1.00:00:00 LT
find-windows-from-distance-constraint 399 301 -K k --relation "<" --value 400000km --begin B --end E --step 1d --aberration LT

find-windows-from-coordinate-constraint k 10 399 RA/DEC DECLINATION "=" 0 B E 20.00:00:00 NONE 0 ICRF
find-windows-from-coordinate-constraint 10 399 -K k --system RA/DEC --coordinate DECLINATION --relation "=" --value 0 --begin B --end E --step 20d

find-windows-from-occultation-constraint k 399 10 Ellipsoid 301 Ellipsoid ANY B E 01:00:00 LT
find-windows-from-occultation-constraint 399 10 301 -K k --target-shape ellipsoid --front-shape ellipsoid --occultation any --begin B --end E --step 1h --aberration LT

find-windows-from-FOV-constraint k -172 -172600 399 ITRF93 Ellipsoid B E 01:00:00 LT
find-windows-from-fov-constraint -172 -172600 399 -K k --target-frame ITRF93 --target-shape ellipsoid --begin B --end E --step 1h --aberration LT

find-windows-from-illumination-constraint k 10 399 10 ITRF93 "0.0349 0.8378 0" Incidence "<" 1.585 B E 04:30:00 CN+S
find-windows-from-illumination-constraint 10 399 10 -K k --fixed-frame ITRF93 --site "0.0349 0.8378 0" --angle incidence --relation "<" --value 1.585 --begin B --end E --step 4.5h --aberration CN+S
```

Beyond the grammar, a step needs a colon or a unit (`60` was 60 days), the numbers are read in invariant culture,
the epochs are written in the scale of the input (`--time-scale tdb` for the TDB labels of 10.x), the exit codes are 0,
1, 2 and 3 (129 for an unknown option and for the help before), no banner precedes the data, and `propagate` runs
with the library's defaults (`--integrator vv --step 1s --cd 0.3` for the run of 10.x, with `--atmosphere standard` for
a run with `--drag` about the Earth). A positional argument a command does not take is an input error.

## Commands

The help of each command, as `astro <command> --help` writes it (generated from the tool and compared with it by
`CliPageTests`).

<!-- generated: astro --help -->
```text
Usage: astro [command]

The command-line tool of IO.Astrodynamics: ephemerides, orientations, window searches, conversions and propagations from SPICE kernels

Commands:
  ephemeris                                    The states of a target relative to an observer over a window, as state vectors, Keplerian or equinoctial elements
  sub-point                                    The sub-observer point of an object on a celestial body, in planetocentric, planetodetic or planetocentric cartesian coordinates
  angular-separation                           The angle between two objects seen from an observer, in rad
  orientation                                  The orientations of a celestial body or a spacecraft relative to a frame over a window: the rotation from the frame and the angular velocity
  find-windows-from-coordinate-constraint      The windows where a coordinate of a target seen from an observer meets a relation (CSPICE gfposc)
  find-windows-from-distance-constraint        The windows where the distance of a target from an observer meets a relation (CSPICE gfdist)
  find-windows-from-occultation-constraint     The windows where a front body occults a target seen from an observer (CSPICE gfoclt)
  find-windows-from-fov-constraint             The windows where a target is in the field of view of an instrument of a spacecraft (CSPICE gftfov)
  find-windows-from-illumination-constraint    The windows where an illumination angle at a site of a target body, seen from an observer, meets a relation (CSPICE gfilum)
  orbital-parameters-converter                 Converts an orbit to a state vector, Keplerian or equinoctial elements, at an epoch and in a frame
  frame-converter                              The orientation of a frame relative to another at an epoch: the rotation from the first and the angular velocity
  time-converter                               Converts an epoch to a time scale (tdb, tt, tai, utc, gps or local) and a format (iso, jd or seconds from J2000)
  celestial-body-info                          The information of a celestial body: identifier, name, mass, GM, fixed frame, radii, flattening, J2 to J4
  propagate                                    Propagates a spacecraft about its centre and writes its kernels and a Cosmographia scenario

Options:
  -h, --help    Show help message
  --version     Show version
```

### ephemeris

<!-- generated: astro ephemeris --help -->
```text
Usage: astro ephemeris [--kernels <path>...] [--begin <epoch>] [--end <epoch>] [--step <step>] [--aberration <correction>] [--time-scale <tdb|tt|tai|utc|gps>] [--format <text|csv|json>] [--verbose] [--frame <frame>] [--as <state|keplerian|equinoctial>] [--help] target observer unexpected0 ... unexpectedN

The states of a target relative to an observer over a window, as state vectors, Keplerian or equinoctial elements

Arguments:
  0: target        Target: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  1: observer      Observer: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  2: unexpected    Nothing: a value with spaces is quoted, everything but the objects is an option

Options:
  -K, --kernels <path>...               A kernel, a meta-kernel or a directory of kernels; repeatable, loaded in order after the paths of the environment variable ASTRO_KERNELS (separated by the path separator of the platform)
  --begin <epoch>                       Start of the window: an ISO 8601 date or a day of year with an optional time, then Z, an offset or a scale tag (2024-01-01T21:00:00Z, 2024-001T21:00:00 UTC), seconds from J2000 with an optional tag (144521.0 UTC), or a Julian date (2460310.5 JD UTC); the tags are UTC, TT, TDT, TDB, TAI and GPS, TDB when none is given (Required)
  --end <epoch>                         End of the window, in the same forms (Required)
  --step <step>                         Step: [d.]hh:mm:ss[.f], or a number and s, min, h or d (60s, 12h) (Required)
  --aberration <correction>             Aberration correction: NONE, LT, LT+S, CN, CN+S, XLT, XLT+S, XCN or XCN+S, in any case (Default: NONE)
  --time-scale <tdb|tt|tai|utc|gps>     Scale of the epochs written: the scale of the input epoch by default (that of --begin for a window)
  --format <text|csv|json>              Output format: text (default), csv for the commands that write rows, json for those that write one record
  --verbose                             Write the trace of an error after its message
  --frame <frame>                       Frame of the states: ICRF (written J2000) or a frame of the library or of the loaded kernels (Default: ICRF)
  --as <state|keplerian|equinoctial>    Kind of the states written: state, keplerian or equinoctial (Default: state)
  -h, --help                            Show help message
```

### sub-point

<!-- generated: astro sub-point --help -->
```text
Usage: astro sub-point [--kernels <path>...] [--epoch <epoch>] [--aberration <correction>] [--format <text|csv|json>] [--verbose] [--as <planetocentric|planetodetic|cartesian>] [--help] object body unexpected0 ... unexpectedN

The sub-observer point of an object on a celestial body, in planetocentric, planetodetic or planetocentric cartesian coordinates

Arguments:
  0: object        Object projected, a celestial body or a spacecraft: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  1: body          Celestial body onto which the object is projected: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  2: unexpected    Nothing: a value with spaces is quoted, everything but the objects is an option

Options:
  -K, --kernels <path>...                         A kernel, a meta-kernel or a directory of kernels; repeatable, loaded in order after the paths of the environment variable ASTRO_KERNELS (separated by the path separator of the platform)
  --epoch <epoch>                                 Epoch: an ISO 8601 date or a day of year with an optional time, then Z, an offset or a scale tag (2024-01-01T21:00:00Z, 2024-001T21:00:00 UTC), seconds from J2000 with an optional tag (144521.0 UTC), or a Julian date (2460310.5 JD UTC); the tags are UTC, TT, TDT, TDB, TAI and GPS, TDB when none is given (Required)
  --aberration <correction>                       Aberration correction: NONE, LT, LT+S, CN, CN+S, XLT, XLT+S, XCN or XCN+S, in any case (Default: NONE)
  --format <text|csv|json>                        Output format: text (default), csv for the commands that write rows, json for those that write one record
  --verbose                                       Write the trace of an error after its message
  --as <planetocentric|planetodetic|cartesian>    Coordinates written: planetocentric, planetodetic, or the planetocentric cartesian position on the surface (Default: planetocentric)
  -h, --help                                      Show help message
```

### angular-separation

<!-- generated: astro angular-separation --help -->
```text
Usage: astro angular-separation [--kernels <path>...] [--epoch <epoch>] [--aberration <correction>] [--format <text|csv|json>] [--verbose] [--help] observer first second unexpected0 ... unexpectedN

The angle between two objects seen from an observer, in rad

Arguments:
  0: observer      Observer: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  1: first         First object: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  2: second        Second object: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  3: unexpected    Nothing: a value with spaces is quoted, everything but the objects is an option

Options:
  -K, --kernels <path>...      A kernel, a meta-kernel or a directory of kernels; repeatable, loaded in order after the paths of the environment variable ASTRO_KERNELS (separated by the path separator of the platform)
  --epoch <epoch>              Epoch: an ISO 8601 date or a day of year with an optional time, then Z, an offset or a scale tag (2024-01-01T21:00:00Z, 2024-001T21:00:00 UTC), seconds from J2000 with an optional tag (144521.0 UTC), or a Julian date (2460310.5 JD UTC); the tags are UTC, TT, TDT, TDB, TAI and GPS, TDB when none is given (Required)
  --aberration <correction>    Aberration correction: NONE, LT, LT+S, CN, CN+S, XLT, XLT+S, XCN or XCN+S, in any case (Default: NONE)
  --format <text|csv|json>     Output format: text (default), csv for the commands that write rows, json for those that write one record
  --verbose                    Write the trace of an error after its message
  -h, --help                   Show help message
```

### orientation

<!-- generated: astro orientation --help -->
```text
Usage: astro orientation [--kernels <path>...] [--begin <epoch>] [--end <epoch>] [--step <step>] [--time-scale <tdb|tt|tai|utc|gps>] [--format <text|csv|json>] [--verbose] [--frame <frame>] [--help] object unexpected0 ... unexpectedN

The orientations of a celestial body or a spacecraft relative to a frame over a window: the rotation from the frame and the angular velocity

Arguments:
  0: object        Celestial body, or spacecraft read from its CK: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  1: unexpected    Nothing: a value with spaces is quoted, everything but the objects is an option

Options:
  -K, --kernels <path>...              A kernel, a meta-kernel or a directory of kernels; repeatable, loaded in order after the paths of the environment variable ASTRO_KERNELS (separated by the path separator of the platform)
  --begin <epoch>                      Start of the window: an ISO 8601 date or a day of year with an optional time, then Z, an offset or a scale tag (2024-01-01T21:00:00Z, 2024-001T21:00:00 UTC), seconds from J2000 with an optional tag (144521.0 UTC), or a Julian date (2460310.5 JD UTC); the tags are UTC, TT, TDT, TDB, TAI and GPS, TDB when none is given (Required)
  --end <epoch>                        End of the window, in the same forms (Required)
  --step <step>                        Step: [d.]hh:mm:ss[.f], or a number and s, min, h or d (60s, 12h) (Required)
  --time-scale <tdb|tt|tai|utc|gps>    Scale of the epochs written: the scale of the input epoch by default (that of --begin for a window)
  --format <text|csv|json>             Output format: text (default), csv for the commands that write rows, json for those that write one record
  --verbose                            Write the trace of an error after its message
  --frame <frame>                      Reference frame: ICRF (written J2000) or a frame of the library or of the loaded kernels (Default: ICRF)
  -h, --help                           Show help message
```

### find-windows-from-coordinate-constraint

<!-- generated: astro find-windows-from-coordinate-constraint --help -->
```text
Usage: astro find-windows-from-coordinate-constraint [--system <system>] [--coordinate <coordinate>] [--relation <relation>] [--value <value>] [--kernels <path>...] [--begin <epoch>] [--end <epoch>] [--step <step>] [--aberration <correction>] [--time-scale <tdb|tt|tai|utc|gps>] [--format <text|csv|json>] [--verbose] [--adjust <value>] [--frame <frame>] [--help] observer target unexpected0 ... unexpectedN

The windows where a coordinate of a target seen from an observer meets a relation (CSPICE gfposc)

Arguments:
  0: observer      Observer: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  1: target        Target: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  2: unexpected    Nothing: a value with spaces is quoted, everything but the objects is an option

Options:
  --system <system>                    Coordinate system: RECTANGULAR, RA/DEC, GEODETIC or PLANETOGRAPHIC, in any case (the member name RaDec read too) (Required)
  --coordinate <coordinate>            Coordinate of the system: X, Y, Z, ALTITUDE, DECLINATION, LATITUDE, LONGITUDE, RADIUS, RANGE or RIGHT ASCENSION, in any case (the member name RightAscension read too) (Required)
  --relation <relation>                Relation: <, >, =, <=, >=, !, ABSMIN, ABSMAX, LOCMIN or LOCMAX, in any case (<= and >= are searched as < and >, whose windows are closed; ! is the window less the instants of =) (Required)
  --value <value>                      Value of the relation: a length in m (or km) for X, Y, Z, ALTITUDE, RADIUS and RANGE, an angle in rad (or deg) otherwise (Required)
  -K, --kernels <path>...              A kernel, a meta-kernel or a directory of kernels; repeatable, loaded in order after the paths of the environment variable ASTRO_KERNELS (separated by the path separator of the platform)
  --begin <epoch>                      Start of the window: an ISO 8601 date or a day of year with an optional time, then Z, an offset or a scale tag (2024-01-01T21:00:00Z, 2024-001T21:00:00 UTC), seconds from J2000 with an optional tag (144521.0 UTC), or a Julian date (2460310.5 JD UTC); the tags are UTC, TT, TDT, TDB, TAI and GPS, TDB when none is given (Required)
  --end <epoch>                        End of the window, in the same forms (Required)
  --step <step>                        Step: [d.]hh:mm:ss[.f], or a number and s, min, h or d (60s, 12h) (Required)
  --aberration <correction>            Aberration correction: NONE, LT, LT+S, CN, CN+S, XLT, XLT+S, XCN or XCN+S, in any case (Default: NONE)
  --time-scale <tdb|tt|tai|utc|gps>    Scale of the epochs written: the scale of the input epoch by default (that of --begin for a window)
  --format <text|csv|json>             Output format: text (default), csv for the commands that write rows, json for those that write one record
  --verbose                            Write the trace of an error after its message
  --adjust <value>                     Adjustment of ABSMIN and ABSMAX: the windows where the value is within it of the absolute extremum, in the unit of --value (Default: 0)
  --frame <frame>                      Frame of the coordinates: ICRF (written J2000) or a frame of the kernels (Default: ICRF)
  -h, --help                           Show help message
```

### find-windows-from-distance-constraint

<!-- generated: astro find-windows-from-distance-constraint --help -->
```text
Usage: astro find-windows-from-distance-constraint [--relation <relation>] [--value <distance>] [--kernels <path>...] [--begin <epoch>] [--end <epoch>] [--step <step>] [--aberration <correction>] [--time-scale <tdb|tt|tai|utc|gps>] [--format <text|csv|json>] [--verbose] [--help] observer target unexpected0 ... unexpectedN

The windows where the distance of a target from an observer meets a relation (CSPICE gfdist)

Arguments:
  0: observer      Observer: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  1: target        Target: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  2: unexpected    Nothing: a value with spaces is quoted, everything but the objects is an option

Options:
  --relation <relation>                Relation: <, >, =, <=, >=, !, ABSMIN, ABSMAX, LOCMIN or LOCMAX, in any case (<= and >= are searched as < and >, whose windows are closed; ! is the window less the instants of =) (Required)
  --value <distance>                   Distance of the relation, in m (or km) (Required)
  -K, --kernels <path>...              A kernel, a meta-kernel or a directory of kernels; repeatable, loaded in order after the paths of the environment variable ASTRO_KERNELS (separated by the path separator of the platform)
  --begin <epoch>                      Start of the window: an ISO 8601 date or a day of year with an optional time, then Z, an offset or a scale tag (2024-01-01T21:00:00Z, 2024-001T21:00:00 UTC), seconds from J2000 with an optional tag (144521.0 UTC), or a Julian date (2460310.5 JD UTC); the tags are UTC, TT, TDT, TDB, TAI and GPS, TDB when none is given (Required)
  --end <epoch>                        End of the window, in the same forms (Required)
  --step <step>                        Step: [d.]hh:mm:ss[.f], or a number and s, min, h or d (60s, 12h) (Required)
  --aberration <correction>            Aberration correction: NONE, LT, LT+S, CN, CN+S, XLT, XLT+S, XCN or XCN+S, in any case (Default: NONE)
  --time-scale <tdb|tt|tai|utc|gps>    Scale of the epochs written: the scale of the input epoch by default (that of --begin for a window)
  --format <text|csv|json>             Output format: text (default), csv for the commands that write rows, json for those that write one record
  --verbose                            Write the trace of an error after its message
  -h, --help                           Show help message
```

### find-windows-from-occultation-constraint

<!-- generated: astro find-windows-from-occultation-constraint --help -->
```text
Usage: astro find-windows-from-occultation-constraint [--target-shape <point|ellipsoid>] [--front-shape <point|ellipsoid>] [--occultation <full|annular|partial|any>] [--kernels <path>...] [--begin <epoch>] [--end <epoch>] [--step <step>] [--aberration <correction>] [--time-scale <tdb|tt|tai|utc|gps>] [--format <text|csv|json>] [--verbose] [--help] observer target front unexpected0 ... unexpectedN

The windows where a front body occults a target seen from an observer (CSPICE gfoclt)

Arguments:
  0: observer      Observer: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  1: target        Target occulted: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  2: front         Front body, which occults the target: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  3: unexpected    Nothing: a value with spaces is quoted, everything but the objects is an option

Options:
  --target-shape <point|ellipsoid>            Shape of the target: point or ellipsoid (Required)
  --front-shape <point|ellipsoid>             Shape of the front body: point or ellipsoid (Required)
  --occultation <full|annular|partial|any>    Occultation: full, annular, partial or any (Required)
  -K, --kernels <path>...                     A kernel, a meta-kernel or a directory of kernels; repeatable, loaded in order after the paths of the environment variable ASTRO_KERNELS (separated by the path separator of the platform)
  --begin <epoch>                             Start of the window: an ISO 8601 date or a day of year with an optional time, then Z, an offset or a scale tag (2024-01-01T21:00:00Z, 2024-001T21:00:00 UTC), seconds from J2000 with an optional tag (144521.0 UTC), or a Julian date (2460310.5 JD UTC); the tags are UTC, TT, TDT, TDB, TAI and GPS, TDB when none is given (Required)
  --end <epoch>                               End of the window, in the same forms (Required)
  --step <step>                               Step: [d.]hh:mm:ss[.f], or a number and s, min, h or d (60s, 12h) (Required)
  --aberration <correction>                   Aberration correction: NONE, LT, LT+S, CN, CN+S, XLT, XLT+S, XCN or XCN+S, in any case (Default: NONE)
  --time-scale <tdb|tt|tai|utc|gps>           Scale of the epochs written: the scale of the input epoch by default (that of --begin for a window)
  --format <text|csv|json>                    Output format: text (default), csv for the commands that write rows, json for those that write one record
  --verbose                                   Write the trace of an error after its message
  -h, --help                                  Show help message
```

### find-windows-from-fov-constraint

<!-- generated: astro find-windows-from-fov-constraint --help -->
```text
Usage: astro find-windows-from-fov-constraint [--target-frame <frame>] [--target-shape <point|ellipsoid>] [--kernels <path>...] [--begin <epoch>] [--end <epoch>] [--step <step>] [--aberration <correction>] [--time-scale <tdb|tt|tai|utc|gps>] [--format <text|csv|json>] [--verbose] [--help] observer instrument target unexpected0 ... unexpectedN

The windows where a target is in the field of view of an instrument of a spacecraft (CSPICE gftfov)

Arguments:
  0: observer      Spacecraft that carries the instrument: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  1: instrument    Instrument: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  2: target        Target: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  3: unexpected    Nothing: a value with spaces is quoted, everything but the objects is an option

Options:
  --target-frame <frame>               Body-fixed frame of the target (ITRF93, IAU_MOON, ...), which orients its ellipsoid (Required)
  --target-shape <point|ellipsoid>     Shape of the target: point or ellipsoid (Required)
  -K, --kernels <path>...              A kernel, a meta-kernel or a directory of kernels; repeatable, loaded in order after the paths of the environment variable ASTRO_KERNELS (separated by the path separator of the platform)
  --begin <epoch>                      Start of the window: an ISO 8601 date or a day of year with an optional time, then Z, an offset or a scale tag (2024-01-01T21:00:00Z, 2024-001T21:00:00 UTC), seconds from J2000 with an optional tag (144521.0 UTC), or a Julian date (2460310.5 JD UTC); the tags are UTC, TT, TDT, TDB, TAI and GPS, TDB when none is given (Required)
  --end <epoch>                        End of the window, in the same forms (Required)
  --step <step>                        Step: [d.]hh:mm:ss[.f], or a number and s, min, h or d (60s, 12h) (Required)
  --aberration <correction>            Aberration correction: NONE, LT, LT+S, CN, CN+S, XLT, XLT+S, XCN or XCN+S, in any case (Default: NONE)
  --time-scale <tdb|tt|tai|utc|gps>    Scale of the epochs written: the scale of the input epoch by default (that of --begin for a window)
  --format <text|csv|json>             Output format: text (default), csv for the commands that write rows, json for those that write one record
  --verbose                            Write the trace of an error after its message
  -h, --help                           Show help message
```

### find-windows-from-illumination-constraint

<!-- generated: astro find-windows-from-illumination-constraint --help -->
```text
Usage: astro find-windows-from-illumination-constraint [--fixed-frame <frame>] [--site <"longitude latitude altitude">] [--angle <phase|incidence|emission>] [--relation <relation>] [--value <angle>] [--kernels <path>...] [--begin <epoch>] [--end <epoch>] [--step <step>] [--aberration <correction>] [--time-scale <tdb|tt|tai|utc|gps>] [--format <text|csv|json>] [--verbose] [--adjust <angle>] [--help] observer target source unexpected0 ... unexpectedN

The windows where an illumination angle at a site of a target body, seen from an observer, meets a relation (CSPICE gfilum)

Arguments:
  0: observer      Observer: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  1: target        Target body of the site: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  2: source        Illumination source: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  3: unexpected    Nothing: a value with spaces is quoted, everything but the objects is an option

Options:
  --fixed-frame <frame>                     Body-fixed frame of the target (ITRF93, IAU_MOON, ...) (Required)
  --site <"longitude latitude altitude">    Planetodetic coordinates of the site: longitude and latitude in rad (or deg), altitude in m (or km) (Required)
  --angle <phase|incidence|emission>        Illumination angle: phase, incidence or emission (Required)
  --relation <relation>                     Relation: <, >, =, <=, >=, !, ABSMIN, ABSMAX, LOCMIN or LOCMAX, in any case (<= and >= are searched as < and >, whose windows are closed; ! is the window less the instants of =) (Required)
  --value <angle>                           Angle of the relation, in rad (or deg) (Required)
  -K, --kernels <path>...                   A kernel, a meta-kernel or a directory of kernels; repeatable, loaded in order after the paths of the environment variable ASTRO_KERNELS (separated by the path separator of the platform)
  --begin <epoch>                           Start of the window: an ISO 8601 date or a day of year with an optional time, then Z, an offset or a scale tag (2024-01-01T21:00:00Z, 2024-001T21:00:00 UTC), seconds from J2000 with an optional tag (144521.0 UTC), or a Julian date (2460310.5 JD UTC); the tags are UTC, TT, TDT, TDB, TAI and GPS, TDB when none is given (Required)
  --end <epoch>                             End of the window, in the same forms (Required)
  --step <step>                             Step: [d.]hh:mm:ss[.f], or a number and s, min, h or d (60s, 12h) (Required)
  --aberration <correction>                 Aberration correction: NONE, LT, LT+S, CN, CN+S, XLT, XLT+S, XCN or XCN+S, in any case (Default: NONE)
  --time-scale <tdb|tt|tai|utc|gps>         Scale of the epochs written: the scale of the input epoch by default (that of --begin for a window)
  --format <text|csv|json>                  Output format: text (default), csv for the commands that write rows, json for those that write one record
  --verbose                                 Write the trace of an error after its message
  --adjust <angle>                          Adjustment of ABSMIN and ABSMAX: the windows where the value is within it of the absolute extremum, in the unit of --value (Default: 0)
  -h, --help                                Show help message
```

### orbital-parameters-converter

<!-- generated: astro orbital-parameters-converter --help -->
```text
Usage: astro orbital-parameters-converter [--as <state|keplerian|equinoctial>] [--from <state|keplerian|equinoctial|tle>] [--orbit <values>...] [--center <body>] [--epoch <epoch>] [--frame <frame>] [--kernels <path>...] [--time-scale <tdb|tt|tai|utc|gps>] [--format <text|csv|json>] [--verbose] [--target-epoch <epoch>] [--target-frame <frame>] [--help] unexpected0 ... unexpectedN

Converts an orbit to a state vector, Keplerian or equinoctial elements, at an epoch and in a frame

Arguments:
  0: unexpected    Nothing: a value with spaces is quoted, everything but the objects is an option

Options:
  --as <state|keplerian|equinoctial>          Kind of the orbit written: state, keplerian or equinoctial (Required)
  --from <state|keplerian|equinoctial|tle>    Kind of the orbit given: state, keplerian, equinoctial or tle (Required)
  --orbit <values>...                         The values of the orbit, in one quoted argument or several: state "x y z vx vy vz" (m and m/s, or km and km/s), keplerian "a e i raan aop m" (m or km, rad or deg), equinoctial "p f g h k l0" (m or km, rad or deg for l0); tle: --orbit "<line 1>" --orbit "<line 2>" (Required)
  --center <body>                             Centre of motion: a NAIF identifier or a name SPICE knows; not given for a TLE
  --epoch <epoch>                             Epoch of the orbit: an ISO 8601 date or a day of year with an optional time, then Z, an offset or a scale tag (2024-01-01T21:00:00Z, 2024-001T21:00:00 UTC), seconds from J2000 with an optional tag (144521.0 UTC), or a Julian date (2460310.5 JD UTC); the tags are UTC, TT, TDT, TDB, TAI and GPS, TDB when none is given; not given for a TLE
  --frame <frame>                             Frame of the orbit: ICRF (written J2000) or a frame of the library or of the loaded kernels; not given for a TLE
  -K, --kernels <path>...                     A kernel, a meta-kernel or a directory of kernels; repeatable, loaded in order after the paths of the environment variable ASTRO_KERNELS (separated by the path separator of the platform)
  --time-scale <tdb|tt|tai|utc|gps>           Scale of the epochs written: the scale of the input epoch by default (that of --begin for a window)
  --format <text|csv|json>                    Output format: text (default), csv for the commands that write rows, json for those that write one record
  --verbose                                   Write the trace of an error after its message
  --target-epoch <epoch>                      Epoch of the orbit written, by the two-body motion (SGP4 for a TLE): the epoch of the orbit given by default
  --target-frame <frame>                      Frame of the orbit written: ICRF (written J2000) or a frame of the library or of the loaded kernels (Default: ICRF)
  -h, --help                                  Show help message
```

### frame-converter

<!-- generated: astro frame-converter --help -->
```text
Usage: astro frame-converter [--kernels <path>...] [--epoch <epoch>] [--time-scale <tdb|tt|tai|utc|gps>] [--format <text|csv|json>] [--verbose] [--help] from to unexpected0 ... unexpectedN

The orientation of a frame relative to another at an epoch: the rotation from the first and the angular velocity

Arguments:
  0: from          Frame converted from: ICRF (written J2000) or a frame of the library or of the loaded kernels (Required)
  1: to            Frame converted to, as the first (Required)
  2: unexpected    Nothing: a value with spaces is quoted, everything but the objects is an option

Options:
  -K, --kernels <path>...              A kernel, a meta-kernel or a directory of kernels; repeatable, loaded in order after the paths of the environment variable ASTRO_KERNELS (separated by the path separator of the platform)
  --epoch <epoch>                      Epoch: an ISO 8601 date or a day of year with an optional time, then Z, an offset or a scale tag (2024-01-01T21:00:00Z, 2024-001T21:00:00 UTC), seconds from J2000 with an optional tag (144521.0 UTC), or a Julian date (2460310.5 JD UTC); the tags are UTC, TT, TDT, TDB, TAI and GPS, TDB when none is given (Required)
  --time-scale <tdb|tt|tai|utc|gps>    Scale of the epochs written: the scale of the input epoch by default (that of --begin for a window)
  --format <text|csv|json>             Output format: text (default), csv for the commands that write rows, json for those that write one record
  --verbose                            Write the trace of an error after its message
  -h, --help                           Show help message
```

### time-converter

<!-- generated: astro time-converter --help -->
```text
Usage: astro time-converter [--to-tdb] [--to-utc] [--to-local] [--to-julian] [--to-seconds-from-j2000] [--to-date-time] [--to <String>] [--format <String>] [--verbose] [--help] epoch unexpected0 ... unexpectedN

Converts an epoch to a time scale (tdb, tt, tai, utc, gps or local) and a format (iso, jd or seconds from J2000)

Arguments:
  0: epoch         Epoch: an ISO 8601 date or a day of year with an optional time, then Z, an offset or a scale tag (2024-01-01T21:00:00Z, 2024-001T21:00:00 UTC), seconds from J2000 with an optional tag (144521.0 UTC), or a Julian date (2460310.5 JD UTC); the tags are UTC, TT, TDT, TDB, TAI and GPS, TDB when none is given; an epoch with a space is quoted (Required)
  1: unexpected    Nothing: an epoch with a space is quoted ("2024-01-01T00:00:00 TAI")

Options:
  -t, --to-tdb                   Convert to TDB (alias of --to tdb)
  -u, --to-utc                   Convert to UTC (alias of --to utc)
  -l, --to-local                 Convert to the local time of the machine, ISO only (alias of --to local)
  -j, --to-julian                Write the Julian date (alias of --format jd)
  -e, --to-seconds-from-j2000    Write the seconds from J2000 of the target scale (alias of --format seconds)
  -d, --to-date-time             Write the ISO 8601 calendar (alias of --format iso)
  --to <String>                  Target scale: tdb, tt, tai, utc, gps or local (tdb by default)
  --format <String>              Output format: iso, jd or seconds (iso by default)
  --verbose                      Write the trace of an error after its message
  -h, --help                     Show help message
```

### celestial-body-info

<!-- generated: astro celestial-body-info --help -->
```text
Usage: astro celestial-body-info [--kernels <path>...] [--format <text|csv|json>] [--verbose] [--help] body unexpected0 ... unexpectedN

The information of a celestial body: identifier, name, mass, GM, fixed frame, radii, flattening, J2 to J4

Arguments:
  0: body          Celestial body: a NAIF identifier (399, -1000) or a name SPICE knows, built in or given by the loaded kernels (EARTH, DSS-13) (Required)
  1: unexpected    Nothing: a value with spaces is quoted, everything but the objects is an option

Options:
  -K, --kernels <path>...     A kernel, a meta-kernel or a directory of kernels; repeatable, loaded in order after the paths of the environment variable ASTRO_KERNELS (separated by the path separator of the platform)
  --format <text|csv|json>    Output format: text (default), csv for the commands that write rows, json for those that write one record
  --verbose                   Write the trace of an error after its message
  -h, --help                  Show help message
```

### propagate

<!-- generated: astro propagate --help -->
```text
Usage: astro propagate [--output <directory>] [--from <state|keplerian|equinoctial|tle>] [--orbit <values>...] [--center <body>] [--epoch <epoch>] [--frame <frame>] [--kernels <path>...] [--begin <epoch>] [--end <epoch>] [--step <step>] [--verbose] [--integrator <rk78|vv>] [--drag] [--srp] [--atmosphere <nrlmsise00|standard>] [--degree <degree>] [--mass <mass>] [--area <area>] [--cd <coefficient>] [--body <body>...] [--help] name unexpected0 ... unexpectedN

Propagates a spacecraft about its centre and writes its kernels and a Cosmographia scenario

Arguments:
  0: name          Name of the spacecraft (Required)
  1: unexpected    Nothing: a value with spaces is quoted, everything but the objects is an option

Options:
  --output <directory>                        Directory of the kernels and the Cosmographia scenario written (Required)
  --from <state|keplerian|equinoctial|tle>    Kind of the orbit given: state, keplerian, equinoctial or tle (Required)
  --orbit <values>...                         The values of the orbit, in one quoted argument or several: state "x y z vx vy vz" (m and m/s, or km and km/s), keplerian "a e i raan aop m" (m or km, rad or deg), equinoctial "p f g h k l0" (m or km, rad or deg for l0); tle: --orbit "<line 1>" --orbit "<line 2>" (Required)
  --center <body>                             Centre of motion: a NAIF identifier or a name SPICE knows; not given for a TLE
  --epoch <epoch>                             Epoch of the orbit: an ISO 8601 date or a day of year with an optional time, then Z, an offset or a scale tag (2024-01-01T21:00:00Z, 2024-001T21:00:00 UTC), seconds from J2000 with an optional tag (144521.0 UTC), or a Julian date (2460310.5 JD UTC); the tags are UTC, TT, TDT, TDB, TAI and GPS, TDB when none is given; not given for a TLE
  --frame <frame>                             Frame of the orbit: ICRF (written J2000) or a frame of the library or of the loaded kernels; not given for a TLE
  -K, --kernels <path>...                     A kernel, a meta-kernel or a directory of kernels; repeatable, loaded in order after the paths of the environment variable ASTRO_KERNELS (separated by the path separator of the platform)
  --begin <epoch>                             Start of the propagation: an ISO 8601 date or a day of year with an optional time, then Z, an offset or a scale tag (2024-01-01T21:00:00Z, 2024-001T21:00:00 UTC), seconds from J2000 with an optional tag (144521.0 UTC), or a Julian date (2460310.5 JD UTC); the tags are UTC, TT, TDT, TDB, TAI and GPS, TDB when none is given (Required)
  --end <epoch>                               End of the propagation, in the same forms (Required)
  --step <step>                               Output step: [d.]hh:mm:ss[.f], or a number and s, min, h or d (Default: 60s)
  --verbose                                   Write the trace of an error after its message
  --integrator <rk78|vv>                      Integrator: rk78, RK7(8) at 1e-9 (default), or vv, Velocity-Verlet at 1 s
  --drag                                      Atmospheric drag, about the Earth or Mars
  --srp                                       Solar radiation pressure
  --atmosphere <nrlmsise00|standard>          Atmosphere of the Earth for --drag: nrlmsise00, NRLMSISE-00 at nominal space weather (default), or standard, the U.S. Standard Atmosphere 1976; Mars takes its standard atmosphere
  --degree <degree>                           Degree of the geopotential of the Earth (EGM2008), 10 by default, 100 at most
  --mass <mass>                               Mass of the spacecraft, kg (Default: 100)
  --area <area>                               Cross-sectional area of the spacecraft, m2 (Default: 1)
  --cd <coefficient>                          Drag coefficient (2.2 by default)
  --body <body>...                            A celestial body whose gravity acts as a third body, in addition to the centre: a NAIF identifier or a name SPICE knows; repeatable
  -h, --help                                  Show help message
```
