# Geodetic reference harness

A small C program that produces the reference values of the geodetic (planetodetic) coordinates used to check
`Planetocentric.ToPlanetodetic` and `Planetodetic.ToPlanetocentric`
([#411](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/411)). It is a developer tool, run by
hand; the build and the test suite never run it. What it prints is committed as a JSON data file.

It calls two independent implementations, both already vendored, so no download is needed:

- CSPICE N0067 (`external-lib/cspice.a`): `georec_c` (geodetic to body-fixed Cartesian) and `recgeo_c` (back);
- IAU SOFA, release 2023-10-11 (`external-lib/sofa-src`): `iauGd2gce` and `iauGc2gde`, the latter after T. Fukushima,
  "Transformation from Cartesian to geodetic coordinates accelerated by Halley's method", *J. Geodesy* 79, 689–693,
  2006.

The grid: the Earth, Mars and the Moon with the radii of `pck00011.tpc` (the Moon a sphere), and the prolate comet
103P/Hartley 2 (1000041, radii 0.34, 0.34 and 1.16 km, f = −2.41); 6 longitudes; 16 latitudes, the poles and 1e-3°
from them included; 10 altitudes from −5 km to 384 400 km (depths below a tenth of the smaller radius left out, where
the geodetic coordinates of a point are not unique): 3744 points. For each point, the Cartesian position from both
libraries, and the geodetic coordinates computed back from the SPICE position by both; SOFA rejects a negative
flattening, so the comet has SPICE only. Then 292 points inside the evolute of the meridian ellipse, near the centre
(from 0 to half the equatorial radius), with their SPICE geodetic coordinates and the position SPICE gives back from
them.

Build and run from the repository root:

```bash
gcc -O2 -I external-lib/includeLinux -I external-lib/sofa-src \
    tools/geodetic_reference/geodetic_ref.c external-lib/sofa-src/*.c external-lib/cspice.a -lm -o geodetic_ref
./geodetic_ref > geodetic_reference.json
```

Measured on 2026-10-19 (Linux, gcc), over the 2880 points:

| Comparison | Largest departure |
|---|---|
| Cartesian position, SPICE against SOFA | 4.4e-16 relative |
| Geodetic latitude, SPICE against SOFA | 3.3e-16 rad from −5 km to 5 km; 1.3e-11 rad at 800 km and 1.5e-10 rad at 35 786 km, both on Mars at 51.6° (SOFA's single Halley step) |
| Altitude, SPICE against SOFA | 1.2e-7 m (at 384 400 km) |
| SPICE round trip, geodetic to Cartesian and back | 5.6e-16 rad, 1.2e-7 m (at 384 400 km); 1.0e-15 rad on the prolate comet |
| SPICE inside the evolute, recgeo_c then georec_c | 1.9e-12 of the equatorial radius |

Two more programs give the reference values of the unit tests that the conversions change. Build each the same way,
with `external-lib/cspice.a` alone:

| Program | What it prints | Consumed by |
|---|---|---|
| `dss13_topo.c` | The Moon from DSS-13 in `DSS-13_TOPO` (`earth_topo_201023.tf`, built on the WGS84 spheroid) and in the frame the library builds from the geodetic coordinates of the station on the `pck00011` ellipsoid; the angles of the Sun and of the Moon to that zenith; the geodetic coordinates of the station | `SiteTests.GetHorizontalCoordinates*`, `SiteTests.Illumination*` |
| `test_values_ref.c` | `recgeo_c` of the planetocentric point of `PlanetocentricTests`, `georec_c` and `reclat_c` of the geodetic point of `PlanetodeticTests`, and the geodetic coordinates of the sub-Moon point at J2000 TDB with the kernels of the CLI tests | `PlanetocentricTests.ToPlanetodetic`, `PlanetodeticTests.ToPlanetocentric`, `EphemerisTests.SubPointPlanetodetic` |

The library agrees with the frame it builds to the last digits (117.89632135112616° in both, DSS-13, 2000-01-01); it
departs from `DSS-13_TOPO` by up to 6.5e-6° in azimuth, the difference between the two ellipsoids (9.5e-7° in the
latitude of the station).
