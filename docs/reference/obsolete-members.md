---
title: Obsolete Members
---

# Obsolete Members

A member of IO.Astrodynamics that is obsolete carries one of two diagnostic ids, so that the compiler names the id and
links to its section of this page. Each section lists the members of its id, with what replaces them.

| Id | Kind | Severity | Removed |
|---|---|---|---|
| [IOA0001](#IOA0001) | A replaced entry point | Warning | In the next major version (12.0.0 for the members obsoleted in 11.x) |
| [IOA0002](#IOA0002) | A trap: a member whose silent use gives wrong results | Error | In the next major version |

An application that cannot migrate at once suppresses a warning by its id, `<NoWarn>$(NoWarn);IOA0001</NoWarn>` in its
project file or `#pragma warning disable IOA0001` around a call. An IOA0002 error cannot be suppressed that way: the
member's message names the fix.

## IOA0001: replaced entry point { #IOA0001 }

The member has a replacement that does the same thing: it is marked `[Obsolete]` with the id IOA0001 and
`[EditorBrowsable(EditorBrowsableState.Never)]`, so IntelliSense no longer offers it, and it forwards to its
replacement, its results bit for bit those of the replacement. The warning names the replacement. The member is removed
in the next major version.

| Member | Replacement | Since |
|---|---|---|

No member carries IOA0001 yet.

## IOA0002: trap { #IOA0002 }

The member compiles and runs but gives results its caller does not expect: it is marked `[Obsolete]` with the id
IOA0002 at the error level, and its message says what goes wrong and what to write instead. Its binaries keep running
until the next major version removes it; source code must change. Each one is listed under Breaking in the
[changelog](../contributing/versioning.md).

| Member | Fix | Since |
|---|---|---|

No member carries IOA0002 yet.

## For contributors

How a member is made obsolete, and how the tests check it, is on the
[Public API](../contributing/public-api.md#making-a-member-obsolete) page.
