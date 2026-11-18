# CK segment priority reference (#724)

`ck_priority_ref.c` writes the clock and two CKs of one spacecraft and prints, as JSON, what CSPICE N0067's `ckgp_c`
reads on them, loaded in each order. Its output is the reference of
`SpiceApiTest.ReadOrientation_FollowsTheCkSegmentPriority` (`IO.Astrodynamics.Tests/SpiceApiTest.cs`), which reads the
kernels through `SpiceAPI.ReadOrientation` on every platform, and they are read by
`SpiceApiTest.ReadOrientation_InAnotherFrame_FollowsTheCkSegmentPriority` (Validation) against `ckgp_c` and `ckgpav_c`
in ECLIPJ2000.

## What it writes

In the output directory (`IO.Astrodynamics.Tests/Data/CkPriorityReference`), for the spacecraft -472401 CKPRIO:

| File | Content |
|---|---|
| `ckprio.tsc` | its clock, as the library's `ClockTemplate.tsc` (type 1, 65 536 ticks a second) |
| `ckprio_rates.bc` | a type 3 CK with angular velocity: a rotation about z at 1e-3 rad/s, a record every 100 s over [0, 1000] s TDB |
| `ckprio_norates.bc` | a type 3 CK without angular velocity (`ckw03_c`, avflag false): a rotation about x at 2e-3 rad/s, a record every 50 s over [300, 700] s TDB |

Each record is the SPICE quaternion (cos(wt/2), sin(wt/2) a) of the rotation of angle wt about the axis a, which the
linear interpolation of a type 3 segment reproduces between records, so that the closed form is a second reference.

## What it prints

`ckgp_c` at the epochs 0, 50, ..., 1000 s, 299.5 s and 700.5 s TDB with a tolerance of 1 s (65 536 ticks), in J2000:
`rates_last` with the CK with rates loaded last (its segment has the higher priority everywhere), `norates_last` with
the CK without rates loaded last (its segment has the higher priority over [299, 701] s, within the tolerance, so that
299.5 s and 700.5 s take its records at 300 s and 700 s). Each entry: `epoch`, `et` (the TDB seconds of the record,
`sct2e_c` of `ckgp_c`'s clkout) and `cmat` (the C-matrix, J2000 to the spacecraft frame, row-major), 17 significant
digits.

## Build and run

From the repository root, with the vendored CSPICE (N0067):

```sh
gcc -O2 -I external-lib/includeLinux tools/ck_priority_reference/ck_priority_ref.c external-lib/cspice.a -lm -o ck_priority_ref
./ck_priority_ref IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/CkPriorityReference \
    > IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/CkPriorityReference/ck_priority_reference.json
```

Two runs give byte-identical kernels and JSON.
