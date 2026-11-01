# Field-of-view reference (#515, choice 9 (a))

`fov_ref.c` writes the kernels of three spacecraft and prints, as JSON, what CSPICE N0067 computes on them for the
field-of-view search of the library (`Instrument.FindWindowsInFieldOfViewConstraint`, `Instrument.IsInFOV`). Its output
is the reference of `IO.Astrodynamics.Tests/Body/FieldOfViewReferenceTests.cs`.

## What it writes

In the output directory (`IO.Astrodynamics.Tests/Data/FieldOfViewReference`):

| File | Content |
|---|---|
| `fovleo.bsp`, `fovpolar.bsp`, `fovtarget.bsp` | two-body orbits about the Earth with the kernel GM, type 5 SPKs (`spkw05_c`, a state every 60 s, conic propagation between them, exact for a two-body orbit), J2000, over [T0 − 600 s, T0 + 10 800 s + 600 s], T0 = 2026-04-01 00:00:00 TDB |
| `fovleo.bc`, `fovpolar.bc` | type 3 CKs every 10 s with their angular velocity: FOVLEO (-470101) turns about k = (sin 20°, 0, cos 20°) at one turn in 10 800 s, which the linear interpolation of a type 3 segment reproduces; FOVPOLAR (-470103) keeps the attitude of J2000 |
| `fovleo.tsc`, `fovpolar.tsc` | the clocks, as the library's `ClockTemplate.tsc` (type 1, 65 536 ticks a second) |
| `fovleo.tf`, `fovpolar.tf` | the spacecraft frames, as the library's `FrameTemplate.tf` (`<NAME>_FRAME`, identifier NAIF ID × 1000) |
| `*_instruments.tf`, `*_instruments.ti` | the instrument frames (TK, Euler angles 1-2-3 relative to the spacecraft frame, zero or about one axis only, where the library's rotation vector agrees) and their IKs (`ANGLES` class) |

The spacecraft: FOVLEO, an inclined LEO (a = 6 800 km, e = 0.001, i = 51.6°, RAAN = 30°), with 23 instruments; FOVPOLAR,
a polar LEO (a = 7 000 km, circular), with 3 instruments whose boresight is J2000's Z, so that the Earth's limb crosses
them near the pole; FOVTARGET (-470102), a LEO (a = 7 200 km, e = 0.002, i = 45°), an SPK only, a spacecraft target.

The fields: circles, ellipses with REF > CROSS, REF < CROSS and an aspect ratio of 100 (REF 0.3, CROSS 0.003 rad),
rectangles both ways and turned 30° about the boresight, a field narrower than the Earth and one that holds it, a
reference vector not perpendicular to the boresight, a single-axis orientation; narrow fields whose boresights the tool
puts on the apparent (LT+S) directions of the Moon and of the Sun at chosen epochs, narrower than the body, holding it,
or crossed by its limb; two fields crossed by a star, FOVSTAR1, a direction of J2000. Every definition is written to the
JSON with 17 significant digits, and the tests build the same instruments from it.

## What it prints

- `gftfov`: the windows of `gftfov_c` at a step of 1 s, converged to 1e-9 s (`gfstol_c`), a reference finer than the
  1 µs of the managed search and of `gftfov`'s default; key `"<instrument> <target> <POINT, or the target frame>
  <abcorr>"`, a list of [start, end] in TDB seconds from J2000. The Earth, the Moon and the Sun as points and as
  ellipsoids (ITRF93 and IAU_EARTH, MOON_ME, IAU_SUN), with NONE, LT and LT+S on every field and the six other
  corrections on a circle, a rectangle, the Moon, the Sun, DSS-13 and the spacecraft target; DSS-13, FOVTARGET, the
  Earth-Moon barycenter and L1 as points.
- `gfrfov`: the windows of `gfrfov_c` for the star, with NONE, S and XS.
- `fovtrg`, `fovray`: `fovtrg_c` (POINT) and `fovray_c` on a grid, its first epoch, its step and a string of `0` and
  `1`: every 30 s over the whole window, or every second over 200 s for the narrow fields.

One limitation of CSPICE N0067 shows here: `gftfov` with ELLIPSOID and a RECTANGLE field seen from above the pole stops
on `SPICE(INVALIDELLIPSE)` (the semi-axes `edlimb` returns for an almost circular limb are up to 1.3e-9 rad from
orthogonal, which `inelpl` rejects inside `zzelvupy`), whatever the rectangle's orientation: FOVPOLAR's rectangle has no
ELLIPSOID reference.

## Build and run

From the repository root, with the vendored CSPICE (N0067):

```sh
gcc -O2 -I external-lib/includeLinux tools/fov_reference/fov_ref.c external-lib/cspice.a -lm -o fov_ref
./fov_ref IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data \
    IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/FieldOfViewReference \
    > IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/FieldOfViewReference/fov_reference.json
```

It takes about 50 s on Linux x64. Two runs give byte-identical kernels and JSON.
