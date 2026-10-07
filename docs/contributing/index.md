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

The native C++ layer is a thin, stable bridge to CSPICE. It accepts fixes and performance or
stability improvements, but not new API surface: new capabilities belong in the .NET layer.

## What A Good Contribution Looks Like

- A feature branch, and a pull request that explains the why rather than restating the diff.
- Unit tests for the new behaviour, including edge and error cases. Coverage is expected to stay
  above 95 percent.
- Numerical work validated against an authoritative source. Say which one and quote the residuals;
  a conformance case is better still, see [Validation](../guides/validation.md).
- Tests that hold on one platform only, or take minutes (bit-for-bit goldens, statistical validations), carry
  `[Trait("Category", "Validation")]`. The CI runs them on Linux only; on Windows or macOS, run
  `dotnet test --filter "Category!=Validation"`. A golden is recaptured only for an intended change of results, which
  the pull request states: set `IO_ASTRODYNAMICS_GOLDEN_CAPTURE` to the `Data/Golden` directory of the test project and
  run `PropagationGoldenTests`.
- XML documentation comments on every public member, and a documentation update when the public
  API changes.
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
