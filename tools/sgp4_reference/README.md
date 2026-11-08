# SGP4 reference

`sgp4_ref.py` gives the references of the library's SGP4 and TLE paths (lot 5 of #457: #599, #603, #600, #601, #602,
#480, #479): Vallado's code, python `sgp4` 2.27, in the configuration of CSPICE's `evsgp4_c` that the library calls,
WGS-72 "old" (`WGS72OLD`, KE = 7.43669161e-2 exactly) and the AFSPC mode (`sgp4init(..., 'a', ...)` from the fields that
`twoline2rv` reads), with CSPICE N0067 (`getelm_c`, `evsgp4_c`) as the second reference. It writes
`IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/Sgp4Reference/sgp4_reference.json`, read by `Sgp4ReferenceTests` and
`TleTextTests`:

- the 29 element sets of Vallado's `SGP4-VER.TLE` that SGP4 evaluates (28872, suborbital, and the error sets 33333 to
  33335 left out), at the 577 tsince of their rows in `tcppver.out` (both files ship with `sgp4` 2.27), with each set's
  UTC epoch in exact decimal and its `getelm_c` epoch (ET), and CSPICE's worst difference from the reference
  (7.6e-5 m, 1.3e-8 m/s);
- 06251 and the ISS of CelesTrak's GP data of 2026-11-07 hourly over a day at UTC instants, tsince the UTC days from the
  epoch in exact decimal. Not `sgp4(jd, fr)` after `sgp4init`: `sgp4init` keeps the epoch as days from 1949-12-31 in one
  double, 1.46 µs off the line's epoch for 06251 (0.1 m in LEO);
- 33334 (checksums recomputed) and 28350 after its decay, which both references refuse;
- the epoch field of line 1 of UTC instants (exact decimal, nearest 1e-8 day, a tie up, with the carry) and of TDB, TT
  and TAI instants through CSPICE's UTC.

Developer tool, run by hand; the build and the tests never run it.

```bash
python3 -I tools/sgp4_reference/sgp4_ref.py IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/SolarSystem \
    > IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/Sgp4Reference/sgp4_reference.json
```

Requires sgp4 2.27, spiceypy 8.3.0 (CSPICE N0067; the leapseconds kernel of the tests) and numpy.
