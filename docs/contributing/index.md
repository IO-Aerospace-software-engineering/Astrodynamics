---
title: Contributing
---

# Contributing

IO.Astrodynamics is free and open source under
[LGPL-3.0-or-later](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/blob/main/LICENSE),
published as a single package, [`IO.Astrodynamics`](https://www.nuget.org/packages/IO.Astrodynamics).

!!! info "One edition since 10.0.0"
    Up to 9.x there was a second, proprietary package, `IO.Astrodynamics.Pro`. It is discontinued:
    all of its code is now part of `IO.Astrodynamics`, under the same licence. See
    [Versioning](versioning.md) for the migration notes.

## Where To Contribute

Everything happens in the
[GitHub repository](https://github.com/IO-Aerospace-software-engineering/Astrodynamics): issues,
feature discussions and pull requests. The whole codebase is open to contributions, including the
adaptive integrator, the Earth orientation frames, the SSA module and the CCSDS message support.

The native C++ layer is a thin bridge to CSPICE. New capabilities belong in the .NET layer; the C++ layer
changes when a technical or ergonomic reason calls for it (a CSPICE call the .NET layer cannot make well, an
error a proxy drops, performance), with its P/Invoke declarations in the same pull request.

## What A Good Contribution Looks Like

- A feature branch, and a pull request that explains the why rather than restating the diff.
- Unit tests for the new behaviour, including edge and error cases. Coverage is expected to stay
  above 95 percent.
- Numerical work validated against an authoritative source. Say which one and quote the residuals;
  a conformance case is better still, see [Validation](../guides/validation.md).
- Expected values from SPICE or another independent reference, never a snapshot of the code under test.
  Thresholds are set from a measurement on Linux, Windows and macOS arm64, and never relaxed to make a test pass.
- Tests that hold on one platform only, or take minutes (bit-for-bit goldens, statistical validations), carry
  `[Trait("Category", "Validation")]`. The CI runs them on Linux only; on Windows or macOS, run
  `dotnet test --filter "Category!=Validation&Category!=LongValidation"`. Validations and studies of
  minutes each carry `[Trait("Category", "LongValidation")]` instead, with `[LongValidationFact]` or
  `[LongValidationTheory]`: they are skipped unless `IO_ASTRODYNAMICS_LONG_VALIDATION` is 1, so neither a plain
  `dotnet test`, an IDE, the CI nor the CD runs them. The manual workflow `long-validation.yml` sets the variable (about
  40 min on the runner: run 43603489238, 2026-11-14); run it before each release, and on a pull request that changes the dynamics these tests measure. A golden is recaptured only for an intended change of results, which
  the pull request states: set `IO_ASTRODYNAMICS_GOLDEN_CAPTURE` to the `Data/Golden` directory of the test project and
  run `PropagationGoldenTests`.
- XML documentation comments on every public member, and a documentation update when the public
  API changes.
- A public member named and documented as [Public API](public-api.md) says: the build fails on a comment the
  compiler cannot read, and the tests of `IO.Astrodynamics.Tests/ApiConventions/` fail on a new name that breaks the
  naming rules, a member that breaks the options rule (an options type or parameter, two bool parameters, a time in
  double without `Seconds`), and a member a table of the documentation cites that is not public (#668). A member
  renamed, replaced or removed goes outright, without an `[Obsolete]` forwarder, and its change is listed in the
  release notes ([Versioning](versioning.md)) with its migration.
- Each C# block of `DEVELOPER_GUIDE.md` is compiled: it is the region `guide:<n>` of a method of
  `IO.Astrodynamics.Tests/Documentation/DeveloperGuideExamples.cs` (n its rank among the guide's csharp blocks, the
  excerpt of `Constants` excepted), and `DeveloperGuideExamplesTests` fails when a block and its region differ. Change
  the block and its region together; a block that continues an earlier one names its inputs in its first comment, and
  the method declares them (#668).
- So is each csharp block of the pages of `docs/` and of `README.md` (the changelog aside): it is the region
  `<page>:<n>` of a method of the page's file under `IO.Astrodynamics.Tests/Documentation/Pages/`
  (`docs/tutorials/tle-and-omm.md` in `Tutorials/TleAndOmmPage.cs`, n its rank among the page's compiled csharp
  blocks), whose parameters are the inputs its first comment names (`// Inputs: ...`), and
  `DocumentationPagesExamplesTests` fails when a block and its region differ, when a page with csharp blocks has no such
  file, or when a block of a page that declares using directives, whether the block declares some or not, does not
  compile with its own directives, the earlier blocks' of its page and a console application's implicit ones alone, as
  a reader who copies it compiles it (#716); a page that declares no directive gives the reader none and is not compiled
  this way. A signature listing
  or a fragment that is not a statement is fenced `csharp-signature`, highlighted as C# (`mkdocs.yml`) and checked
  against the library's public members by reflection (#705); the one exception is the excerpt of `Constants` that opens
  `docs/standards-and-units.md`, fenced `csharp`, with no region (so region 1 of that page is its second csharp block)
  and checked by value against the library, as the excerpt of the developer guide.
- Microsoft C# conventions, SOLID and DRY, methods that do one thing.

The full guidelines live in
[CONTRIBUTING.md](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/blob/main/CONTRIBUTING.md),
alongside the
[code of conduct](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/blob/main/CODE_OF_CONDUCT.md).
Security reports follow
[SECURITY.md](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/blob/main/SECURITY.md)
rather than the public issue tracker.

## Supporting The Project

Keeping a .NET-first astrodynamics toolkit current with SPICE releases, kernel updates and
cross-platform packaging takes real effort. You can help through
[GitHub Sponsors](https://github.com/sponsors/IO-Aerospace-software-engineering), by backing a
specific issue, or by contributing kernel sets, scenarios and documentation.

If you need guarantees rather than best effort, see [Services](services.md).
