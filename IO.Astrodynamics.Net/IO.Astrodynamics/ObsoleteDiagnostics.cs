// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics;

/// <summary>
/// The diagnostic ids of the library's obsolete members (#668: D1 rule 9, resolution C15 and D2 N5), listed with their
/// members on docs/reference/obsolete-members.md, which <see cref="UrlFormat"/> points to.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ReplacedEntryPoint"/> (IOA0001) marks an entry point that a new one replaces: a warning, with
/// <c>[EditorBrowsable(EditorBrowsableState.Never)]</c>, that forwards to its replacement bit for bit and is removed in the
/// next major version (12.0.0 for the members obsoleted in 11.x):
/// </para>
/// <code>
/// [Obsolete("Use NewMember(...).", DiagnosticId = ObsoleteDiagnostics.ReplacedEntryPoint, UrlFormat = ObsoleteDiagnostics.UrlFormat)]
/// [EditorBrowsable(EditorBrowsableState.Never)]
/// </code>
/// <para>
/// <see cref="Trap"/> (IOA0002) marks a trap, a member whose silent use gives wrong results: an error whose message names the
/// fix, listed under Breaking in the changelog:
/// </para>
/// <code>
/// [Obsolete("What goes wrong. Write ... instead.", error: true, DiagnosticId = ObsoleteDiagnostics.Trap, UrlFormat = ObsoleteDiagnostics.UrlFormat)]
/// </code>
/// <para>
/// The constants are internal: an application suppresses a diagnostic by its id (<c>&lt;NoWarn&gt;IOA0001&lt;/NoWarn&gt;</c> or
/// <c>#pragma warning disable IOA0001</c>). <c>ObsoleteMembersTests</c> checks every obsolete member of the public surface
/// against these rules and the page.
/// </para>
/// </remarks>
internal static class ObsoleteDiagnostics
{
    /// <summary>The id of a replaced entry point: a warning, hidden from IntelliSense, removed in the next major version.</summary>
    internal const string ReplacedEntryPoint = "IOA0001";

    /// <summary>The id of a trap: an error whose message names the fix.</summary>
    internal const string Trap = "IOA0002";

    /// <summary>
    /// The help link of both ids: the section of docs/reference/obsolete-members.md that lists the members of the id
    /// (the compiler replaces {0} with the id, the anchor of its section).
    /// </summary>
    internal const string UrlFormat = "https://docs.io-aerospace.org/reference/obsolete-members/#{0}";
}
