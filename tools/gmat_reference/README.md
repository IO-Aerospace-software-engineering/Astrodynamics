# GMAT reference

The comparison of IO.Astrodynamics with GMAT, a third-party implementation, for the state transition matrix Φ, the
sensitivity Ψ to Cd and Cr, the propagated covariance P, the process noise Q and the partials of the forces (H2 of
phase 2, feature 1; design approved in
[#408](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/pull/408)).

These are developer tools, run by hand or by the manual workflow `gmat-reference.yml`. The build and the test suite
never run GMAT: what it writes is committed in `IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/GmatReference`, and
the tests (`GmatReferenceTests`, `GmatPartialsTests`, category `Validation`) compare the library with it.

## GMAT

GMAT R2026a, the official Ubuntu x64 build of the GMAT project on SourceForge,
<https://sourceforge.net/projects/gmat/files/GMAT/GMAT-R2026a/> (`GmatConsole` built on 2026-03-30):

| Archive | SHA-256 | SHA-1 (as SourceForge publishes it) | MD5 (same) |
|---|---|---|---|
| `gmat-ubuntu-x64-R2026a.tar.gz` | `fe124b4a606b2e3b704a6fbb1c37b87598d5df0d18cb661c304b5f60074a7754` | `15bb6057a25853ea68f2c0c781e030d4beae3348` | `ec496a0319657fa2c079d7e996b9b1ed` |
| `GMAT-datafiles-R2026a.tar.gz` | `6c0bae7229b3333f8ca20fbf68861f97e26c8fd1f8dcb5fb5855183f594c89a8` | `e52bf5a1008c6cb4f8ab5d8c122e5fd12662d963` | `5d9c5dfb25f24a498709f6f2d177abac` |

The data files of the second archive are those of the `data/` directory of the build, apart from graphics files; the
build alone is enough. `GmatConsole` needs no system library beyond Ubuntu 24.04; the plugins that do not load (MATLAB,
OpenFrames, Python 3.9, 3.10 and 3.14) are not used. The partials use GMAT's own Python API, shipped with the build
(`bin/gmatpy`), with the Python 3 of the system; the PyPI package `gmatpy` is a different, third-party one.

## Files

| File | What it does |
|---|---|
| `generate_scripts.py` | Writes the GMAT scripts of the cases G1 to G6 from `cases.json` into `scripts/`: the nominal propagation with Φ, the 12 to 14 propagations of GMAT's own central differences, the density along the drag case, the covariance and the process noise |
| `scripts/` | The scripts it wrote, committed; `@SPK@`, `@COF@` and `@OUT@` are replaced by `run_gmat.sh` |
| `gmat_partials.py` | GMAT's A matrix of each force at the four states of the force partials (B7), through GMAT's Python API |
| `egm2008_to_cof.py` | Converts the EGM2008 field of the library into GMAT's `.cof` format, with the GM and the radius of the library |
| `check_cof.py` | Checks that the committed `.cof` is that conversion, byte for byte |
| `run_gmat.sh` | Runs all of it and collects the reports in the test data |
| `compare_run.sh` | Checks that a run wrote the committed reports, partials and logs, both ways |

In the test data:

| File | Written by |
|---|---|
| `cases.json` | `GmatCasesFileTests`, from the reference cases of the library (`ReferenceCases`, `PartialsTestCases`) |
| `EGM2008_to70_TideFree_IO.cof` | `egm2008_to_cof.py` |
| `G1/` to `G6/` | GMAT: one report (`.txt`) and one console log (`.log`) per script |
| `partials/` | `gmat_partials.py`: one line of 42 numbers per state and force |

## Running

From the repository root, with GMAT R2026a extracted in `<GMAT>` (the directory that holds `bin/`):

```bash
# 1. Only when the reference cases change: write cases.json again from the library
mkdir -p /tmp/gmat-cases
IO_ASTRODYNAMICS_GMAT_CASES_CAPTURE=/tmp/gmat-cases dotnet test \
    IO.Astrodynamics.Net/IO.Astrodynamics.Tests/IO.Astrodynamics.Tests.csproj \
    --filter "FullyQualifiedName~GmatCasesFileTests"   # fails on purpose, having written the file
cp /tmp/gmat-cases/cases.json IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/GmatReference/

# 2. Only when the field or the Earth of the cases changes: convert EGM2008 again
python3 -I -B tools/gmat_reference/egm2008_to_cof.py \
    IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/SolarSystem/EGM2008_to70_TideFree \
    IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/GmatReference/EGM2008_to70_TideFree_IO.cof \
    398600435507022.6 6378136.6 70     # the GM and the radius of cases.json

# 3. Run GMAT (about 5 min) and compare
tools/gmat_reference/run_gmat.sh <GMAT>
dotnet test IO.Astrodynamics.Net/IO.Astrodynamics.Tests/IO.Astrodynamics.Tests.csproj -c Release \
    --filter "FullyQualifiedName~Variational.External" --logger "console;verbosity=detailed"
```

`run_gmat.sh <GMAT> <directory>` writes the reports into another directory, to compare a new run with the committed
one: `tools/gmat_reference/compare_run.sh IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/GmatReference <directory>`,
as the workflow `gmat-reference.yml` does.

## Alignment of the models

What GMAT is given, so that the comparison measures the computation and not the models (design of #408; the measured
departures are in `docs/guides/validation.md`):

- **Frame.** The state is given in GMAT's `EarthICRF`, whose axes are those of SPICE `J2000`: the Sun of `de440s.bsp` in
  `EarthICRF` equals SPICE to 2e-16. GMAT integrates in `EarthMJ2000Eq`, which it turns from ICRF by a slowly varying
  rotation (2.1e-7 rad in 2021, drifting by 9.3e-10 rad over the day of the cases). The scripts report in `EarthMJ2000Eq`, and the tests
  turn every report to ICRF by the constant rotation R0 of the start. Taking the coordinates of `J2000` as those of
  `EarthMJ2000Eq`, as first designed, left the third bodies and the Sun 2.1e-7 rad away (31 km for the Sun), visible in
  their partials (2e-7 to 5e-7).
- **Geopotential.** The EGM2008 field of the library, converted to `.cof` with the GM and the reference radius of the
  library; `Earth.Mu` the same GM.
- **Earth orientation.** GMAT's own (IAU-1976/FK5 with its EOP file); from `EarthICRF`, its Earth-fixed frame is within
  about 2.6e-8 rad of the library's `ITRF93` at the epoch of the cases.
- **Third bodies.** The same SPK through SPICE, the GM of the library; the barycentres of the planets as user-defined
  bodies on their NAIF identifiers.
- **SRP.** Spherical model, the flux at 1 AU (`L☉ / (4π AU²)`) and the AU of the library, the Earth and Sun radii of its
  PCK. GMAT's Φ leaves out the derivative of the shadow function, which the library keeps: GMAT's own warning in the
  console logs of G5, against §4.2.4 and §4.2.5 of its Mathematical Specification.
- **Drag.** NRLMSISE-00 with constant F10.7, F10.7A and Kp (ap 4). The densities of the two tools agree to 5.0e-6
  along the drag case since #411 and #409 (2.8 % before).
- **Integrator.** PrinceDormand78 at the fixed step of each case: GMAT propagates the covariance at a fixed step only, and
  resolves Φ best there. GMAT adds its discrete process noise at every integration step, so the noise cases sweep the
  step itself.
