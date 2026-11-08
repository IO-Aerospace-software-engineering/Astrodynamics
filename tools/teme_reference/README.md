# TEME reference

`teme_ref.py` gives the references of the managed TEME frame (`IO.Astrodynamics/Frames/TemeFrame.cs`, #451): for a LEO
and a GEO state at 13 epochs from 1980 to 2026, the epochs of the TEME cases of the conformance suite, the state of
Vallado et al. (2006) appendix C and the SGP4 states of five TLEs (Vallado's code, python `sgp4` 2.27, WGS-72 old,
AFSPC mode), the ICRF state by GMAT R2026a (TEME → EarthICRF, run by the script) and by ERFA's CIP route with
IAU 2006/2000A and IAU 2006/2000B (UT1 = UTC), and ERFA's GMST82. It writes
`IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/TemeReference/teme_reference.json`, read by `TemeFrameTests`,
`TLETests.ToStateVector` and `ScenarioTests.PropagateSpacecraftFromTLE`. Developer tool, run by hand; the build and the tests never run it.

```bash
python3 -I tools/teme_reference/teme_ref.py <GMAT R2026a bin directory> <scratch directory> \
    IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/SolarSystem \
    > IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/TemeReference/teme_reference.json
```

Requires pyerfa 2.0.1.5, sgp4 2.27, spiceypy 8.3.0 (CSPICE N0067, which converts the instants and gives DSS-63's state) and numpy. GMAT's EOP file does not enter its TEME → EarthICRF (measured with five
variants of the file, 2026-11-07, lot 5 of #457).
