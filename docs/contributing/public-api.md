---
title: Public API
---

# Public API

The public surface of `IO.Astrodynamics` is named and documented by rules the build and the tests check. They come
from the API ergonomics review of 11.0.0
([#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), design PR
[#671](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/pull/671), decisions D1 and D2). This page
says what each check asks of a pull request, and how a change of the surface is documented.

| Check | Where | Fails |
|---|---|---|
| XML documentation | `GenerateDocumentationFile` | The build, on a cref it cannot resolve or that names several overloads, a param tag of no parameter, a parameter without a tag beside others with one, or a malformed comment (CS0419, CS1570, CS1572, CS1573, CS1574); a member without a comment (CS1591) is a warning |
| Naming (D2, N1 to N3) | `NamingRuleTests`, `NamingRuleBaseline.txt` | The tests, on a new name that breaks the rules (a namespace, a type, a member, each overload of a method, a parameter), or a baseline entry no longer broken |
| Options (D1) | `OptionsConventionTests`, `OptionsConventionBaseline.txt` | The tests, on a new options type or parameter that breaks the rule, a new method or constructor with two bool parameters or more, a new double named `Step`, `Duration` or `Tolerance` without the `Seconds` suffix (the six tolerances that are no time, an angle, a matrix test and the residuals of `NewtonRaphson`, exempt by their key), two options types whose property of one name differs in type or default, or a baseline entry no longer broken |
| Members in the docs' tables | `DocumentationTableMembersTests`, `DocumentationTableMembersBaseline.txt` | The tests, on a backticked member of a table that is not on the public surface: an internal member only after the word "internal" in its cell, and any member in a column headed Implementation (the provenance page's pointers to the code) |
| CA naming rules | `AnalysisModeNaming` | Nothing yet: warnings until the renaming lot of 11.0.0 makes them errors |

The tests live in `IO.Astrodynamics.Tests/ApiConventions/`. Each baseline lists the violations of the day the check
landed, one per line in ordinal order, and may only shrink: a test fails on a violation that is not in its baseline, and
on a baseline line that is no longer a violation, and its message gives the lines to fix or to remove. A method or a
constructor is keyed by its signature (`Spacecraft.GetTotalIsp()`), a parameter by its method's signature and its name
(`LegendreFunctions.ComputeGeodesyNormalizedLegendreTable(int, double, double, double[][], double[][]):P`), so a new
overload of a name in the baseline is a new violation.

The library builds with the warnings of the lots to come: CS1591 (a public member without a comment) and the CA naming
rules (`AnalysisModeNaming` All).

The surface itself is not tracked in files: no list of the public signatures is kept in the repository (decided by S.
Guillet, 2026-11-19). The reference pages of `docs/` and the release notes ([Versioning](versioning.md)) are the record
of what each version adds, changes and removes.

## Adding a public member

1. Name it by D2. N1: PascalCase types and members, camelCase parameters, no underscore, an acronym of two letters in
   capitals (`TT`, `JD`) and a longer one as a word (`Tle`, `Utc`, `Icrf`, `SpiceApi`). N2: an enum member or a static
   instance of its own type whose whole name is a SPICE, IAU/IERS or CCSDS symbol keeps the standard's spelling
   (`Frame.ICRF`, `Aberration.LT`, `CcsdsTimeSystem.UTC`); the list is closed, in `NamingRuleTests.StandardSymbols`, and
   a new symbol is added there with its source. N3: full English words, one term per concept (`Sidereal`, `Derivative`,
   `Tsiolkovsky`, `Max`/`Min`, `epoch` for a `Time` parameter), none of the deny-list (`Relationnal`, `Sideral`,
   `Derivation`, `Rigth`, `Oject`, `Dictionnary`, `Tsiolkovski`, `Bissection`).
2. A setting of an operation goes in its options record (D1): a sealed record of `init` properties with a
   `public static X Default { get; } = new();` equal to `new()`, taken last as `X options = null`, before out
   parameters and a `CancellationToken`; values only, never an integrator, a force or a delegate.
3. Document it in the code: a `<summary>`, a `<param>` for each parameter, the exceptions it throws.
4. Document it in `docs/`: its page of the reference (a member a table cites must exist), and its entry under
   **Added** in the release notes of the coming version ([Versioning](versioning.md)).

## Renaming, replacing or removing a member

11.0.0 is a major version: a member renamed or replaced is renamed or removed outright, in the pull request that
replaces it (decided by S. Guillet, 2026-11-19, amending D1's rule 9 and D2's N5). No `[Obsolete]` forwarder keeps the
old name, and no member is kept as an error-level trap: nothing of the old surface stays in the code.

The pull request instead:

1. lists the change in the release notes ([Versioning](versioning.md)): under **Changed**, an entry marked
   **Breaking** with its **Migration** line (the old name, the new one, what a caller writes instead), or under
   **Removed** with what replaces the member;
2. updates the reference pages of `docs/` that cite the member, and the examples (the C# blocks are compiled);
3. removes the baseline lines of the old name, which the tests then report as stale.
