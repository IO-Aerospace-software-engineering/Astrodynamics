# SPICE reference harness

A small C program that calls CSPICE to produce reference values for the managed tests. Like the
[SOFA harnesses](../sofa_reference/README.md), it is a developer tool, run by hand when a reference value is needed:
the build and the test suite never run it, and what it prints is committed as literals in the tests.

CSPICE is already vendored in `external-lib` (`cspice.a` and the headers in `includeLinux`), so no extra download is
needed. From the repository root:

```bash
gcc -O2 -I external-lib/includeLinux tools/spice_reference/spice_ref.c external-lib/cspice.a -lm -o spice_ref
./spice_ref IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/SolarSystem
```

| Program | What it does | Consumed by |
|---|---|---|
| `spice_ref.c` | Applies the SPICE state transformation matrix (`sxform_c`) between ICRF and ITRF93 to the states of the tests below, and derives the launch azimuths from the SPICE speed of the launch sites. Prints JSON. | The expected velocities in `StateVectorTests.ToNonInertialFrame` and `ScenarioTests.PropagateSite`, the non-inertial azimuths and insertion velocities in `LaunchTests` |

The library goes from ICRF to a rotating frame through a quaternion, the angular velocity of `xf2rav_c` and
`v' = R v - (R ω) × r'`. The harness applies the 6x6 matrix `[[R, 0], [dR/dt, R]]` directly, an independent path:
both agree to about 1e-13 m/s on these states. The inputs of the harness (epochs, launch parameters) are the values the
library computes for each test, which the frame transformation does not change. The body-fixed positions of the sites
come from their geodetic coordinates by `georec_c` on the `pck00011` ellipsoid (since #411; they used to be the
positions the library computed, with the site of `ScenarioTests.PropagateSite`, at 1000 m of altitude, 1.15 m away).
`SiteTests.WriteEphemeris`, the same site, takes the same values.
