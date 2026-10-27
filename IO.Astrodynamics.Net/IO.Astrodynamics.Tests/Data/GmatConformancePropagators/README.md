# GMAT runs of the conformance propagators 001 to 003

GMAT R2026a's propagations of the cases `propagator` 001, 002 and 003 of the conformance suite with EGM2008 applied
to the whole field, its GM and radius in the `.cof` header, which GMAT applies to the central term and to the
harmonics. They are the runs `icrf` of the study of the pre-existing conformance cases (2026-10-27), the goldens that
decision 17 of [#457](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/457) gives those
cases. `GmatConformancePropagatorTests` compares the library with them, with
`GeopotentialCentralTerm.ModelGravitationalParameter` (decision 18 of #457).

These files are not written by `tools/gmat_reference`, and `gmat-reference.yml` does not run them: they are copied from
the study and kept here as they were run.

## Files

One directory per case:

| File | What it is |
|---|---|
| `icrf.script` | The GMAT script as run |
| `icrf.log` | GMAT's console log of the run |
| `propagator_24h_*_output.txt` | GMAT's report: the state at the start and after 86400 s (EarthICRF, km, km/s) |

The reports are byte for byte those of the study. In the scripts and the logs, the absolute paths of the machine of the
study are replaced by the placeholders of `tools/gmat_reference/run_gmat.sh`: `@SPK@` for the SPK, `@COF@` for the
gravity file, `@OUT@` for the directory of the run.

SHA-256 of the files of the study, before the paths were replaced:

| Case | `icrf.script` | `icrf.log` | Report |
|---|---|---|---|
| 001 | `2812e7e520b0bb53a5f2373166db6c8f7fb3a85c76217f55fdd2ed6205fba324` | `91a4d438eef592155f7236c8e85c7e3d3e3815351d0bc9e62e5e80e686776ec3` | `f89a775307e7e65d907e64b562de1acff570a541e90528a63990436578009dea` |
| 002 | `c815aafea64aed8f0be9ae8b65053cb7ef1682b3c6fa6687227a1ecad006b75a` | `07c8ff1a3be3fac8b8c8db406def35feee1666131d424f076c611216907b746b` | `9cf2e885783771e83dfe8617fb8414d765713c388fa96081601a791890368767` |
| 003 | `d21eb8570b55c24fea257ec58ca0edc4afcf44af652f59c0c403fdd93ffc2ddc` | `9aa18728e1effb583bfd4ab46d1f9965ba39bd2139a0d078c0a887cbd7566d3a` | `04845ad4caebb027635aca5bc94f34191b7de87339cd74958ad571b1163514b9` |

## What GMAT was given

- **GMAT.** R2026a, the official Ubuntu x64 build (`GmatConsole` built on 2026-03-30, as its log shows), the build of
  `tools/gmat_reference/README.md`.
- **Ephemerides.** `@SPK@` is the conformance suite's `data/de440s.bsp`, byte for byte the `de440s.bsp` of this test
  data (SHA-256 `c1c7feeab882263fc493a9d5a5b2ddd71b54826cdf65d8d17a76126b260a49f2`).
- **Gravity field.** `@COF@` is `EGM2008_model.cof` of the study (SHA-256
  `acedd3e7b9d2428ab8fc6c4b7a23f0861341182bc405591529090f1c8cf7a1c7`), converted from the suite's
  `data/EGM2008_to70_TideFree`, byte for byte the `EGM2008_to70_TideFree` of this test data (SHA-256
  `e5bc64f18f8dbf2c7a86d2144e8d772ea032779031c427c549e5f348815f3e00`). Its header carries the GM and the radius of
  EGM2008, 3.986004415e14 m³/s² and 6378136.3 m, and its coefficients are those of the file, unscaled.
  `tools/gmat_reference/egm2008_to_cof.py` writes the same field when it is given the model's GM twice:

  ```bash
  python3 -I -B tools/gmat_reference/egm2008_to_cof.py \
      IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/SolarSystem/EGM2008_to70_TideFree EGM2008_model.cof \
      398600441500000 398600441500000 6378136.3 70
  ```

  Its 2553 `RECOEF` lines are identical to those of the study's file; the header prints the same three numbers with
  more digits, and the comment lines differ (checked on 2026-10-27).
- **Forces.** Earth as central body with EGM2008 to degree and order 10 (001, 003) or 70 (002); the Moon and the Sun
  as point masses, and for 002 Mercury, Venus and the barycentres of Mars to Pluto. No drag, no SRP.
- **Integrator.** PrinceDormand78, accuracy 1e-13, steps of at most 1 s (001, 003) or 60 s (002).
- **States.** Given and reported in EarthICRF. 001 and 002 start from the Cartesian states of their inputs; 003 from
  Keplerian elements, which GMAT converts with its own `Earth.Mu` (the script does not set it), so the test starts the
  library from the Cartesian state that GMAT reports at the start.

## GMAT's convention

GMAT's force model takes the GM of the `.cof` header for the central term, not `Earth.Mu`. The study ran each case
again with `Earth.Mu = 398600.4355070226` (the kernel GM) and the same `.cof`: the final states of 001 and 002 are bit
for bit unchanged (003 moves, through the conversion of its elements). With the kernel GM in the header instead (the
coefficients scaled by GM_model / GM_kernel), their largest components move by 15.5 m and 6.9 m: the central term.
- **Epochs.** UTC; the propagation stops after `ElapsedSecs = 86400`, no leap second within.
