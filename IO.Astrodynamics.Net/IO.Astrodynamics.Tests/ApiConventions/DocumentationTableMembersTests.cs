// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace IO.Astrodynamics.Tests.ApiConventions;

/// <summary>
/// The members the tables of the documentation cite exist (R05 of #668: "backticked table members checked against D2's
/// PublicAPI files"). The pages' code blocks are compiled by <c>DocumentationPagesExamplesTests</c>, but a table names
/// members in prose, where a renamed or never-written member stays unseen (<c>ApogeeRadius()</c> in
/// keplerian-elements.md, #662). Each backticked name of a table row of docs/ (the changelog aside, whose rows name the
/// members of former versions) and README.md that reads as a member is looked up by reflection, on every type of the
/// library (the public ones are those the PublicAPI files declare, which RS0016 and RS0017 keep equal to the assembly;
/// the reference and provenance pages also name internal members, "internal <c>Time.SecondsToTicks</c>") and on the test
/// classes the provenance page names:
/// <list type="bullet">
/// <item><c>Type.Member</c>, <c>Type.Member(...)</c> or <c>Type.Nested.Member</c>, Type by its simple or its full name:
/// the member must exist on the type or its base types or interfaces, as a method, property, field, event, nested type
/// or enum value (a chain past a member, <c>Frame.ICRF.Name</c>, is checked to that member);</item>
/// <item><c>Member(...)</c> or <c>Type(...)</c> alone: a type, or a member of the library, of that name must exist (a name in small letters
/// or capitals, <c>pxform(...)</c>, <c>SPICE(INVALIDELLIPSE)</c>, is a function of SPICE or of a formula);</item>
/// <item><c>new Type(...)</c>: the type must exist with a constructor;</item>
/// <item><c>X.Member</c> where X is no type of the library: X must be a type of .NET (<see cref="DotNetTypes"/>), a tool
/// the pages compare with (<see cref="OtherTools"/>) or a member heading a chain (<c>InitialOrbitalParameters.Observer</c>),
/// or the name is reported (a type that was renamed or never existed).</item>
/// </list>
/// A single name without parentheses (<c>Frame</c>, <c>TEME</c>, <c>J2000</c>) reads as a value as often as a type, a
/// file name as a file, and neither is checked. The arguments of a call are not checked. The baseline
/// DocumentationTableMembersBaseline.txt lists today's citations that fail, which the documentation PRs fix; it may only
/// shrink.
/// </summary>
public class DocumentationTableMembersTests
{
    internal const string BaselineFile = "DocumentationTableMembersBaseline.txt";

    private static readonly string Docs = Path.Combine(AppContext.BaseDirectory, "Docs");

    /// <summary>The types of .NET the tables cite with a member, whose members are not checked.</summary>
    internal static readonly IReadOnlySet<string> DotNetTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "TimeSpan", "DateTime", "DateTimeKind", "Math", "Double", "double", "String", "string", "Enum", "Console",
        "Task", "Environment", "AppContext", "Path", "File", "Directory", "Assert", "Interlocked", "Volatile",
        "TimeSpan.FromSeconds", "CancellationToken", "Random", "BitConverter", "Array", "List", "Parallel",
        "Thread", "StringComparison", "MidpointRounding", "XmlReader", "XmlSchemaSet", "System"
    };

    private static readonly Regex Citation = new(
        @"^(?<new>new )?(?<path>[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*)(?<call>\([^()]*\))?$", RegexOptions.Compiled);

    /// <summary>
    /// The names of other tools the tables cite (GMAT script objects, Orekit and Hipparchus classes), and the extensions
    /// of the files they name: neither is a member of the library.
    /// </summary>
    internal static readonly IReadOnlySet<string> OtherTools = new HashSet<string>(StringComparer.Ordinal)
    {
        "Sat", "Earth", "GravityToolbox", "DormandPrince853Integrator"
    };

    private static readonly Regex FileName = new(@"\.(cpp|c|h|hpp|java|py|cs|md|yml|yaml|json|gfc|cof|tpc|tls|bsp|bpc|tf|ti|tsc|bc|txt|xml|csv|kvn|omm|opm|cdm)$",
        RegexOptions.Compiled);

    private static readonly Regex Backticks = new("`([^`]+)`", RegexOptions.Compiled);

    // The pages of docs/ and README.md, the changelog aside, by their path in the repository
    private static IEnumerable<(string Page, string Path)> Pages() =>
        Directory.GetFiles(Path.Combine(Docs, "pages"), "*.md", SearchOption.AllDirectories)
            .Select(p => ("docs/" + Path.GetRelativePath(Path.Combine(Docs, "pages"), p).Replace('\\', '/'), p))
            .Where(p => p.Item1 != "docs/contributing/versioning.md")
            .Append(("README.md", Path.Combine(Docs, "README.md")))
            .OrderBy(p => p.Item1, StringComparer.Ordinal);

    /// <summary>The backticked spans of the table rows of a page, outside code blocks.</summary>
    internal static IEnumerable<string> TableCitations(IEnumerable<string> lines)
    {
        bool fenced = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
                continue;
            }

            if (fenced || !line.StartsWith('|') || Regex.IsMatch(line, @"^\|[\s\-:|]+\|?$"))
            {
                continue;
            }

            foreach (Match match in Backticks.Matches(line))
            {
                yield return match.Groups[1].Value.Trim();
            }
        }
    }

    // The types a table may cite: every type of the library, internal ones included (the provenance and reference pages
    // name internal members, "internal Time.SecondsToTicks"), and the test classes the provenance page names
    private static readonly Lazy<Type[]> CitableTypes = new(() =>
        PublicSurface.Library.GetTypes().Concat(typeof(DocumentationTableMembersTests).Assembly.GetTypes())
            .Where(t => !t.Name.Contains('<')).ToArray());

    private static readonly Lazy<ILookup<string, Type>> TypesBySimpleName = new(() =>
        CitableTypes.Value.ToLookup(PublicSurface.SimpleName, StringComparer.Ordinal));

    // The names of the library's members, for a name cited alone (the test classes' members only through their class)
    private static readonly Lazy<HashSet<string>> MemberNames = new(() =>
        PublicSurface.Library.GetTypes().SelectMany(t => t.GetMembers(PublicSurface.Declared).Select(m => m.Name))
            .ToHashSet(StringComparer.Ordinal));

    private static readonly Lazy<HashSet<string>> Namespaces = new(() =>
        PublicSurface.Types.Select(t => t.Namespace).Where(n => n != null)
            .SelectMany(n => Enumerable.Range(1, n!.Split('.').Length).Select(k => string.Join(".", n.Split('.').Take(k))))
            .ToHashSet(StringComparer.Ordinal));

    // Whether a type, its base types or its interfaces have a member or nested type of the name, of any visibility
    private static bool HasMember(Type type, string name)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            if (t.GetMember(name, PublicSurface.Declared).Length > 0)
            {
                return true;
            }
        }

        return type.GetInterfaces().Any(i => i.GetMember(name).Length > 0) ||
               typeof(object).GetMember(name).Length > 0;
    }

    /// <summary>Why a citation does not resolve, or null when it does or is not read as a member.</summary>
    internal static string Unresolved(string span)
    {
        var match = Citation.Match(span);
        if (!match.Success)
        {
            return null;
        }

        var segments = match.Groups["path"].Value.Split('.');
        bool call = match.Groups["call"].Success;
        if (FileName.IsMatch(span) || OtherTools.Contains(segments[0]))
        {
            return null;
        }

        if (match.Groups["new"].Success)
        {
            var types = TypesBySimpleName.Value[segments[^1]].ToList();
            return types.Any(t => t.GetConstructors().Length > 0) ? null : "no type with a public constructor";
        }

        if (segments.Length == 1)
        {
            // A function of SPICE or of a formula (pxform(...), diag(R, R), SPICE(INVALIDELLIPSE)) is not a member
            var single = segments[0];
            bool function = !char.IsUpper(single[0]) || single.ToUpperInvariant() == single;
            return !call || function || MemberNames.Value.Contains(single) || TypesBySimpleName.Value.Contains(single) ||
                   DotNetTypes.Contains(single)
                ? null
                : "no member of that name";
        }

        // A full name: drop the namespace
        int first = 0;
        while (first < segments.Length - 1 && Namespaces.Value.Contains(string.Join(".", segments.Take(first + 1))))
        {
            first++;
        }

        if (first == segments.Length - 1 && !call && Namespaces.Value.Contains(string.Join(".", segments)))
        {
            return null; // a namespace
        }

        if (!char.IsUpper(segments[first][0]))
        {
            return null; // a variable of an example, as spacecraft.Propagate
        }

        var candidates = TypesBySimpleName.Value[segments[first]].ToList();
        if (candidates.Count == 0)
        {
            // .NET's types, and a member's name heading a chain (InitialOrbitalParameters.Observer), are not checked
            return DotNetTypes.Contains(segments[first]) || MemberNames.Value.Contains(segments[first])
                ? null
                : $"no type {segments[first]} in the library";
        }

        foreach (var type in candidates)
        {
            var current = type;
            bool resolved = true;
            for (int k = first + 1; k < segments.Length && resolved; k++)
            {
                if (!HasMember(current, segments[k]))
                {
                    resolved = false;
                    break;
                }

                var nested = current.GetNestedType(segments[k]);
                if (k < segments.Length - 1)
                {
                    if (nested == null)
                    {
                        // A member of a member (Frame.ICRF.Name): the rest is the member's type's, not checked
                        break;
                    }

                    current = nested;
                }
            }

            if (resolved)
            {
                return null;
            }
        }

        return "no such member";
    }

    /// <summary>Every citation that does not resolve, as "page&lt;TAB&gt;span".</summary>
    internal static IEnumerable<string> Violations() =>
        Pages().SelectMany(p => TableCitations(File.ReadAllLines(p.Path))
                .Where(span => Unresolved(span) != null)
                .Select(span => $"{p.Page}\t{span}"))
            .Distinct();

    [Fact]
    public void TableCitations_NameMembersOfTheLibrary_OrAreInTheBaseline()
    {
        Assert.NotEmpty(Pages());
        PublicSurface.AssertMatchesBaseline(BaselineFile, Violations());
    }

    [Theory]
    [InlineData("Frame.ICRF", true)]
    [InlineData("KeplerianElements.PerigeeVector()", true)]
    [InlineData("KeplerianElements.ApogeeRadius()", false)]
    [InlineData("ApogeeRadius()", false)]
    [InlineData("PerigeeVector()", true)]
    [InlineData("new Spacecraft(...)", true)]
    [InlineData("IO.Astrodynamics.TimeSystem.Time.AddSeconds", true)]
    [InlineData("IO.Astrodynamics.TimeSystem", true)]
    [InlineData("TimeSpan.FromSeconds(60)", true)]
    [InlineData("Spacecraft.Propagate", true)]
    [InlineData("Spacecraft.Teleport", false)]
    [InlineData("NoSuchType.Member", false)]
    [InlineData("spacecraft.Propagate", true)]
    [InlineData("BODY399_GM", true)]
    [InlineData("LT+S", true)]
    public void Unresolved_ReadsACitationAsTheTableTestDoes(string span, bool resolves)
    {
        Assert.Equal(resolves, Unresolved(span) == null);
    }
}
