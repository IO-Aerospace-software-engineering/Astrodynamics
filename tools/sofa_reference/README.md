# SOFA reference harnesses

Small C programs that call the IAU SOFA library to produce the reference values used to validate
the managed IAU 2006/2000A implementation in `IO.Astrodynamics/Frames/`. They are developer tools,
run by hand when the model changes or a new reference epoch is needed. Nothing in the build or the
test suite depends on them: the values they print are committed as literals in the tests.

SOFA is already vendored in this repository, so no extra download is needed.

```bash
gcc -O2 -I external-lib/includeLinux \
    tools/sofa_reference/sofa_ref.c external-lib/sofa-src/*.c -lm -o sofa_ref
./sofa_ref
```

| Program | What it does | Consumed by |
|---|---|---|
| `sofa_ref.c` | Prints CIP X/Y, CIO locator s, ERA, TIO locator s', dpsi/deps and the full rotation matrices at several epochs, as JSON | The expected values in `IO.Astrodynamics.Tests/Frame/Iau2006ModelTests.cs`, `GcrfFrameTests.cs`, `CirsFrameTests.cs` and `TirsFrameTests.cs` |
| `extract_coefficients.c` | Emits the 77 IAU 2000B luni-solar nutation terms and the IAU 2006 precession data **as C# source** | `IO.Astrodynamics/Frames/Iau2006NutationData.cs` was generated from it |
| `extract_nut00b.c` | Evaluates `iauNut06a` and the precession angles at chosen epochs, for cross-checking the 2000B truncation | The accuracy tolerances in the frame tests |

The managed implementation is a reimplementation from the IERS Conventions 2010, not a port of
SOFA; these harnesses only provide the numbers it is checked against. `sofa_ref.c` at J2000.0 TT
prints `cip_x_rad = -2.69463795685740364e-05`, which is the literal asserted in
`Iau2006ModelTests.CipXYAtJ2000MatchesSofaWithinNutationAccuracy`.
