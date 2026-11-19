---
title: Public API
---

# Public API

The public surface of `IO.Astrodynamics` is tracked, named and documented by rules the build and the tests check. They
come from the API ergonomics review of 11.0.0
([#668](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/issues/668), design PR
[#671](https://github.com/IO-Aerospace-software-engineering/Astrodynamics/pull/671), decisions D1 and D2). This page
says what each check asks of a pull request.

| Check | Where | Fails |
|---|---|---|
| Declared surface | `PublicAPI.Shipped.txt`, `PublicAPI.Unshipped.txt` (RS0016, RS0017) | The build, on a public or protected member missing from the files, or listed and gone |
| XML documentation | `GenerateDocumentationFile` | The build, on a cref it cannot resolve or that names several overloads, a param tag of no parameter, a parameter without a tag beside others with one, or a malformed comment (CS0419, CS1570, CS1572, CS1573, CS1574); a member without a comment (CS1591) is a warning |
| Naming (D2, N1 to N3) | `NamingRuleTests`, `NamingRuleBaseline.txt` | The tests, on a new name that breaks the rules, or a baseline entry no longer broken |
| Options (D1) | `OptionsConventionTests`, `OptionsConventionBaseline.txt` | The tests, on a new options type or parameter that breaks the rule, or a baseline entry no longer broken |
| Obsolete members | `ObsoleteMembersTests`, [Obsolete Members](../reference/obsolete-members.md) | The tests, on an `[Obsolete]` without its id or its row on the page |
| Members in the docs' tables | `DocumentationTableMembersTests`, `DocumentationTableMembersBaseline.txt` | The tests, on a backticked member of a table that does not exist |
| CA naming rules | `AnalysisModeNaming` | Nothing yet: warnings until the renaming lot of 11.0.0 makes them errors |

The tests live in `IO.Astrodynamics.Tests/ApiConventions/`. Each baseline lists the violations of the day the check
landed, one per line in ordinal order, and may only shrink: a test fails on a violation that is not in its baseline, and
on a baseline line that is no longer a violation, and its message gives the lines to fix or to remove.

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
3. Document it: a `<summary>`, a `<param>` for each parameter, the exceptions it throws.
4. Declare it. The build fails with RS0016 until the member is in `PublicAPI.Unshipped.txt`; the analyzer's code fix
   writes the line (in an IDE, or `dotnet format analyzers IO.Astrodynamics/IO.Astrodynamics.csproj --diagnostics RS0016
   --severity warn`). A member removed or changed is declared the same way: the line of the old signature in
   `PublicAPI.Shipped.txt` gets its `*REMOVED*` twin in `PublicAPI.Unshipped.txt` (RS0017 names it), and a changed
   signature gets the new line too. The review reads the diff of `PublicAPI.Unshipped.txt` as the change of the
   surface.
5. Write its entry in the release notes ([Versioning](versioning.md)) and its page in `docs/`; a member a table cites
   must exist.

## Making a member obsolete

A member is made obsolete with one of the two ids of [Obsolete Members](../reference/obsolete-members.md), whose
constants and help link are the internal `ObsoleteDiagnostics`; there is no third kind.

- **IOA0001, a replaced entry point.** The member keeps working and forwards to its replacement, bit for bit; a test
  shows the two give the same results. It is a warning, hidden from IntelliSense, removed in the next major version:

  ```text
  [Obsolete("Use Replacement(...).", DiagnosticId = ObsoleteDiagnostics.ReplacedEntryPoint, UrlFormat = ObsoleteDiagnostics.UrlFormat)]
  [EditorBrowsable(EditorBrowsableState.Never)]
  ```

  D2's N5 says which renames get one: a member shipped in the last stable release (10.1.0 for 11.0.0), renamed by more
  than case, neither virtual nor an interface member; every other rename is hard.
- **IOA0002, a trap.** A member whose silent use gives wrong results is an error whose message says what goes wrong and
  what to write instead, listed under **Breaking** in the release notes:

  ```text
  [Obsolete("What goes wrong. Write ... instead.", error: true, DiagnosticId = ObsoleteDiagnostics.Trap, UrlFormat = ObsoleteDiagnostics.UrlFormat)]
  ```

Then add the member's row to the table of its id on [Obsolete Members](../reference/obsolete-members.md) (`Type.Member`,
`Type.Member(...)` or `new Type(...)` for a constructor, its replacement or fix, the version), list it under
**Deprecated** in the release notes, and leave its line in `PublicAPI.*.txt`: an obsolete member is still public.
`ObsoleteMembersTests` fails on a member without an id, the URL format or a message, an IOA0001 member that is an error
or visible to IntelliSense, an IOA0002 member that is a warning, and a member and a row that do not match.

The repository's own code does not call a replaced entry point: `Directory.Build.props` makes IOA0001 an error in every
project of the solution, and a test of a forwarder disables it around its call (`#pragma warning disable IOA0001`).

## At a release

The tag of a stable version ships what `PublicAPI.Unshipped.txt` declares. The release PR, before its merge:

1. moves every line of `PublicAPI.Unshipped.txt` into `PublicAPI.Shipped.txt`: an added line is inserted in its order,
   a `*REMOVED*` line deletes its twin from `PublicAPI.Shipped.txt` and goes;
2. leaves `PublicAPI.Unshipped.txt` empty;
3. builds: RS0016 and RS0017 must stay silent, the files then holding exactly the surface of the tag.

A prerelease (a tag with a hyphen) moves nothing: `PublicAPI.Shipped.txt` holds the last stable release, the reference
of the forwarders of N5. `PublicAPI.Shipped.txt` was seeded from the tag `10.1.0` by the analyzer's own code fix; 11.0.0
is the first release to move its lines.
