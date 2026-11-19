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
/// The members the tables of the documentation cite exist and are public (R05 of #668: backticked table members checked
/// against the public surface). The pages' code blocks are compiled by <c>DocumentationPagesExamplesTests</c>, but a
/// table names members in prose, where a renamed or never-written member stays unseen (<c>ApogeeRadius()</c> in
/// keplerian-elements.md, #662). Each backticked name of a table row of docs/ (the changelog aside, whose rows name the
/// members of former versions) and README.md that reads as a member is looked up by reflection on the public surface:
/// the exported types of the library and their public members and the protected members of the types a caller can
/// derive from (<see cref="PublicSurface.IsVisible"/>), and the public test
/// classes the provenance page names. An internal type or member (not a private one) resolves only where its table cell
/// says so before it, "the internal <c>EvaluateVariational</c>, <c>EvaluateCovariance</c>", so a member made internal
/// by a later lot fails where a cell cites it as public; a column headed Implementation, the provenance page's pointers
/// to the code of each formula (file and member), may name any member of the library or the tests, private ones
/// included (<see cref="Access"/>).
/// <list type="bullet">
/// <item><c>Type.Member</c>, <c>Type.Member(...)</c> or <c>Type.Nested.Member</c>, Type by its simple or its full name:
/// the member must exist on the type or its base types or interfaces, as a method, property, field, event, nested type
/// or enum value; a chain past a member, <c>Frame.ICRF.Name</c>, goes on through the member's type (a property's or
/// field's type, a method's return type);</item>
/// <item><c>Member(...)</c> or <c>Type(...)</c> alone: a type, or a member of the surface, of that name must exist (a
/// name in small letters or capitals, <c>pxform(...)</c>, <c>SPICE(INVALIDELLIPSE)</c>, is a function of SPICE or of a
/// formula);</item>
/// <item><c>new Type(...)</c>: the type must exist with a constructor;</item>
/// <item><c>X.Member</c> where X is no type of the library: X must be a type of .NET (<see cref="DotNetTypes"/>), a tool
/// the pages compare with (<see cref="OtherTools"/>) or a member of the surface heading a chain
/// (<c>InitialOrbitalParameters.Observer</c>), the chain then followed through that member's type, or the name is
/// reported (a type that was renamed or never existed).</item>
/// </list>
/// A single name without parentheses (<c>Frame</c>, <c>TEME</c>, <c>J2000</c>) reads as a value as often as a type, a
/// file name as a file, and neither is checked. The arguments of a call are not checked. The baseline
/// DocumentationTableMembersBaseline.txt lists today's citations that fail, which the documentation PRs fix; it may only
/// shrink.
/// </summary>
public class DocumentationTableMembersTests
{
    /// <summary>What a citation may name: the public surface, internal members too, or any member (a code pointer).</summary>
    public enum Access
    {
        Public,
        Internal,
        Any
    }

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

    private static readonly Regex InternalWord = new(@"\binternal\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// The backticked spans of the table rows of a page, outside code blocks, each with what it may name: an internal
    /// member where the word "internal" comes before it in its cell, any member, private ones included, in a column headed
    /// Implementation (the provenance page's pointers to the code of each formula, file and member), the public surface
    /// otherwise.
    /// </summary>
    internal static IEnumerable<(string Span, Access Access)> TableCitations(IEnumerable<string> lines)
    {
        var rows = lines.Select(l => l.Trim()).ToList();
        bool fenced = false;
        var headers = new List<string>();
        for (int i = 0; i < rows.Count; i++)
        {
            var line = rows[i];
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                fenced = !fenced;
                continue;
            }

            if (fenced || !line.StartsWith('|') || IsSeparator(line))
            {
                continue;
            }

            var cells = Cells(line).ToList();
            if (i + 1 < rows.Count && IsSeparator(rows[i + 1]))
            {
                headers = cells.Select(c => c.Trim()).ToList();
            }

            for (int c = 0; c < cells.Count; c++)
            {
                bool implementation = c < headers.Count && headers[c].Equals("Implementation", StringComparison.OrdinalIgnoreCase);
                foreach (Match match in Backticks.Matches(cells[c]))
                {
                    yield return (match.Groups[1].Value.Trim(), implementation ? Access.Any
                        : InternalWord.IsMatch(Backticks.Replace(cells[c][..match.Index], "")) ? Access.Internal
                        : Access.Public);
                }
            }
        }
    }

    private static bool IsSeparator(string line) => Regex.IsMatch(line, @"^\|[\s\-:|]+\|?$");

    // The cells of a table row, split at the pipes outside backticks (`|g|` stays in its cell)
    private static IEnumerable<string> Cells(string row)
    {
        var cell = new System.Text.StringBuilder();
        bool code = false;
        foreach (char c in row)
        {
            if (c == '`')
            {
                code = !code;
            }

            if (c == '|' && !code)
            {
                yield return cell.ToString();
                cell.Clear();
                continue;
            }

            cell.Append(c);
        }

        yield return cell.ToString();
    }

    private static Assembly TestAssembly => typeof(DocumentationTableMembersTests).Assembly;

    // A type a table may cite: exported; cited as internal, any type but a private nested one; in an Implementation
    // column, any type the compiler did not generate
    private static bool TypeAccessible(Type type, Access access)
    {
        if (type.Name.Contains('<'))
        {
            return false;
        }

        if (type.IsVisible || access == Access.Any)
        {
            return true;
        }

        for (var t = type; access == Access.Internal && t != null; t = t.DeclaringType)
        {
            if (t.IsNestedPrivate || t.IsNestedFamANDAssem)
            {
                return false;
            }
        }

        return access == Access.Internal;
    }

    // A member a table may cite: of the surface (PublicSurface.IsVisible); cited as internal, any member but a private
    // one; in an Implementation column, any member the compiler did not generate
    private static bool MemberAccessible(MemberInfo member, Access access)
    {
        if (member is Type nested)
        {
            return TypeAccessible(nested, access);
        }

        if (member.Name.Contains('<') || !TypeAccessible(member.DeclaringType!, access))
        {
            return false;
        }

        if (PublicSurface.IsVisible(member) || access == Access.Any)
        {
            return true;
        }

        return access == Access.Internal && member switch
        {
            FieldInfo f => !f.IsPrivate,
            MethodBase m => !m.IsPrivate,
            PropertyInfo p => p.GetAccessors(true).Any(a => !a.IsPrivate),
            EventInfo e => e.AddMethod is { IsPrivate: false },
            _ => false
        };
    }

    // The types of the library and of the tests (the provenance page names test classes) a table may cite
    private static Type[] CitableTypes(Access access) =>
        PublicSurface.Library.GetTypes().Concat(TestAssembly.GetTypes()).Where(t => TypeAccessible(t, access)).ToArray();

    private static readonly Dictionary<Access, Lazy<ILookup<string, Type>>> TypesBySimpleName =
        Enum.GetValues<Access>().ToDictionary(a => a,
            a => new Lazy<ILookup<string, Type>>(() => CitableTypes(a).ToLookup(PublicSurface.SimpleName, StringComparer.Ordinal)));

    // The members of the library a name cited alone or heading a chain may be, by name (the test classes' members only
    // through their class)
    private static readonly Dictionary<Access, Lazy<ILookup<string, MemberInfo>>> MembersByName =
        Enum.GetValues<Access>().ToDictionary(a => a, a => new Lazy<ILookup<string, MemberInfo>>(() => LibraryMembers(a)));

    private static ILookup<string, MemberInfo> LibraryMembers(Access access) =>
        PublicSurface.Library.GetTypes().Where(t => TypeAccessible(t, access))
            .SelectMany(t => t.GetMembers(PublicSurface.Declared))
            .Where(m => m is not Type && MemberAccessible(m, access))
            .ToLookup(m => m.Name, StringComparer.Ordinal);

    private static readonly Lazy<HashSet<string>> Namespaces = new(() =>
        PublicSurface.Types.Select(t => t.Namespace).Where(n => n != null)
            .SelectMany(n => Enumerable.Range(1, n!.Split('.').Length).Select(k => string.Join(".", n.Split('.').Take(k))))
            .ToHashSet(StringComparer.Ordinal));

    // The members and nested types of a name on a type, its base types and its interfaces that a table may cite
    private static IEnumerable<MemberInfo> FindMembers(Type type, string name, Access access)
    {
        var found = new List<MemberInfo>();
        for (var t = type; t != null; t = t.BaseType)
        {
            found.AddRange(t.GetMember(name, PublicSurface.Declared).Where(m => MemberAccessible(m, access)));
        }

        found.AddRange(type.GetInterfaces().Where(i => TypeAccessible(i, access))
            .SelectMany(i => i.GetMember(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)));
        if (type.IsInterface)
        {
            found.AddRange(typeof(object).GetMember(name));
        }

        return found;
    }

    // The type a chain goes on through after a member: a nested type itself, a property's or field's type, a method's
    // return type, a nullable's underlying type
    private static IEnumerable<Type> ValueTypes(MemberInfo member)
    {
        var type = member switch
        {
            Type nested => nested,
            PropertyInfo p => p.PropertyType,
            FieldInfo f => f.FieldType,
            MethodInfo m => m.ReturnType,
            EventInfo e => e.EventHandlerType,
            _ => null
        };
        if (type == null || type == typeof(void))
        {
            yield break;
        }

        yield return Nullable.GetUnderlyingType(type) ?? type;
    }

    // Whether the segments from the given one resolve, member after member, from one of the types
    private static bool ResolvesFrom(IEnumerable<Type> types, IReadOnlyList<string> segments, int from, Access access)
    {
        var current = types.Distinct().ToList();
        for (int k = from; k < segments.Count; k++)
        {
            var members = current.SelectMany(t => FindMembers(t, segments[k], access)).ToList();
            if (members.Count == 0)
            {
                return false;
            }

            current = members.SelectMany(ValueTypes).Distinct().ToList();
        }

        return true;
    }

    /// <summary>
    /// Why a citation does not resolve among the members it may name (<see cref="Access"/>), or null when it does or is
    /// not read as a member.
    /// </summary>
    internal static string Unresolved(string span, Access access = Access.Public)
    {
        var reason = Resolve(span, access);
        if (reason != null && access == Access.Public && Resolve(span, Access.Internal) == null)
        {
            return "internal: cite it after the word internal in its cell, or cite a public member";
        }

        return reason;
    }

    private static string Resolve(string span, Access access)
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
            var types = TypesBySimpleName[access].Value[segments[^1]].ToList();
            return types.Any(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(c => MemberAccessible(c, access)))
                ? null
                : "no type with a constructor";
        }

        if (segments.Length == 1)
        {
            // A function of SPICE or of a formula (pxform(...), diag(R, R), SPICE(INVALIDELLIPSE)) is not a member
            var single = segments[0];
            bool function = !char.IsUpper(single[0]) || single.ToUpperInvariant() == single;
            return !call || function || MembersByName[access].Value.Contains(single) ||
                   TypesBySimpleName[access].Value.Contains(single) || DotNetTypes.Contains(single)
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

        var candidates = TypesBySimpleName[access].Value[segments[first]].ToList();
        if (candidates.Count > 0)
        {
            return ResolvesFrom(candidates, segments, first + 1, access) ? null : "no such member";
        }

        if (DotNetTypes.Contains(segments[first]))
        {
            return null; // .NET's types are not checked
        }

        // A member's name heading a chain (InitialOrbitalParameters.Observer): the chain goes on through its type
        var heads = MembersByName[access].Value[segments[first]].ToList();
        if (heads.Count == 0)
        {
            return $"no type or member {segments[first]} in the library";
        }

        return ResolvesFrom(heads.SelectMany(ValueTypes), segments, first + 1, access) ? null : "no such member";
    }

    /// <summary>Every citation that does not resolve, as "page&lt;TAB&gt;span".</summary>
    internal static IEnumerable<string> Violations() =>
        Pages().SelectMany(p => TableCitations(File.ReadAllLines(p.Path))
                .Where(c => Unresolved(c.Span, c.Access) != null)
                .Select(c => $"{p.Page}\t{c.Span}"))
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
    [InlineData("Frame.ICRF.Name", true)]
    [InlineData("Frame.ICRF.NoSuchThing", false)]
    // The review's probes of lot L0: private members, and chains headed by a member's name
    [InlineData("Spacecraft.ValidateBodyAxes()", false)]
    [InlineData("Spacecraft.CheckInsidePropagatedStates", false)]
    [InlineData("Radii.NoSuchThing", false)]
    [InlineData("Observer.Teleport()", false)]
    [InlineData("InitialOrbitalParameters.Observer", true)]
    // An internal member, not cited as internal
    [InlineData("Time.SecondsToTicks", false)]
    public void Unresolved_ReadsACitationAsTheTableTestDoes(string span, bool resolves)
    {
        Assert.Equal(resolves, Unresolved(span) == null);
    }

    [Theory]
    [InlineData("Time.SecondsToTicks", Access.Internal, true)]
    [InlineData("Spacecraft.ValidateBodyAxes()", Access.Internal, false)]
    [InlineData("Spacecraft.CheckInsidePropagatedStates", Access.Internal, false)]
    [InlineData("Spacecraft.ValidateBodyAxes()", Access.Any, true)]
    [InlineData("Spacecraft.NoSuchThing()", Access.Any, false)]
    [InlineData("CovarianceAssert.SymmetricPositiveSemiDefinite", Access.Public, false)]
    [InlineData("CovarianceAssert.SymmetricPositiveSemiDefinite", Access.Any, true)]
    public void Unresolved_TakesAnInternalMemberCitedAsInternal_AndAPrivateOneOnlyInAnImplementationColumn(string span, Access access,
        bool resolves)
    {
        Assert.Equal(resolves, Unresolved(span, access) == null);
    }

    [Fact]
    public void TableCitations_ReadTheWordInternalBeforeASpanInItsCell()
    {
        var citations = TableCitations([
                "| Method | Implementation | Notes |",
                "|---|---|---|",
                "| `Spacecraft.Propagate` (and the internal `EvaluateVariational`, `EvaluateCovariance`) | `Time.SecondsToTicks` | `|g|` `Time.AddSeconds` |"
            ])
            .ToList();
        Assert.Equal([("Spacecraft.Propagate", Access.Public), ("EvaluateVariational", Access.Internal),
            ("EvaluateCovariance", Access.Internal), ("Time.SecondsToTicks", Access.Any), ("|g|", Access.Public),
            ("Time.AddSeconds", Access.Public)], citations);
    }
}
