# SOFA reference harnesses

Small C programs that call the IAU SOFA library to produce the reference values and coefficient
tables used by the managed IAU 2006 / 2000B implementation in `IO.Astrodynamics/Frames/`. They are
developer tools, run by hand when the model changes or a new reference epoch is needed. The build
and the test suite never run them: what they print is committed, as literals in the tests, as a JSON
data file, or as generated C# source.

SOFA (release 2023-10-11) is already vendored in `external-lib/sofa-src`, so no extra download is
needed. Every harness builds the same way, from the repository root:

```bash
gcc -O2 -I external-lib/includeLinux \
    tools/sofa_reference/sofa_ref.c external-lib/sofa-src/*.c -lm -o sofa_ref
./sofa_ref
```

| Program | What it does | Consumed by |
|---|---|---|
| `sofa_ref.c` | Prints CIP X/Y, CIO locator s, ERA, TIO locator s', dpsi/deps, the fundamental arguments and the full rotation matrices at seven named epochs, as JSON | The expected values in `IO.Astrodynamics.Tests/Frame/Iau2006ModelTests.cs`, `GcrfFrameTests.cs`, `CirsFrameTests.cs`, `TirsFrameTests.cs` and `FrameChainIntegrationTests.cs` |
| `sofa_ref.c --sweep` | Prints CIP X/Y, CIO locator s, ERA and the GCRS-to-TIRS matrix on 300 epochs from 1990 to 2040, at varying times of day, with UT1 = UTC | `IO.Astrodynamics.Tests/Data/SofaReference/iau2006_sweep.json`, read by `Iau2006SofaSweepTests` |
| `extract_coefficients.c` | Emits the 77 IAU 2000B luni-solar nutation terms as C# source | `IO.Astrodynamics/Frames/Iau2006NutationData.cs` was generated from it |
| `extract_s06.c` | Parses the IAU 2006 CIO locator series (66 terms) from `external-lib/sofa-src/s06.c`, checks the parsed series against `iauS06` from 1990 to 2040, and only then emits it as C# source | `IO.Astrodynamics/Frames/Iau2006CioLocatorData.cs`: `./extract_s06 external-lib/sofa-src/s06.c > IO.Astrodynamics.Net/IO.Astrodynamics/Frames/Iau2006CioLocatorData.cs` |
| `extract_nut00b.c` | Evaluates `iauNut06a`, `iauNut00b`, and `iauNut00b` with the P03 adjustments, plus the precession angles, at seven epochs | `Iau2006ModelTests.NutationMatchesSofaNut00bWithP03Adjustments` and the accuracy tolerances in the frame tests |

The managed implementation follows the IERS Conventions 2010. Its coefficient tables come from the
SOFA sources through these harnesses, and its results are checked against SOFA; the code itself is
not a port of SOFA. `sofa_ref.c` at J2000.0 TT prints `cip_x_rad = -2.69463795685740364e-05`, which
is the literal asserted in `Iau2006ModelTests.CipXYAtJ2000MatchesSofaWithinNutationAccuracy`.
