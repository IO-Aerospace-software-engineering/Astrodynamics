// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace IO.Astrodynamics.Tests.ApiConventions;

/// <summary>
/// The two diagnostic ids of the library's obsolete members (#668: D1 rule 9, resolution C15, D2 N5;
/// <see cref="ObsoleteDiagnostics"/>) and their page, docs/reference/obsolete-members.md:
/// <list type="bullet">
/// <item>every <see cref="ObsoleteAttribute"/> of the public surface carries IOA0001 or IOA0002 and the page's URL format,
/// with a message;</item>
/// <item>an IOA0001 member is a warning hidden from IntelliSense (<c>[EditorBrowsable(Never)]</c>), an IOA0002 member an
/// error;</item>
/// <item>the page has a section per id, at the anchor the URL format names, whose table lists exactly the members of
/// that id.</item>
/// </list>
/// The one obsolete member that predates the ids, <c>Star.PropagationOutput</c> (#668, 11.0.0, before L0), is the
/// allow-list <see cref="WithoutId"/>: the D1 amendments of the re-baseline (2026-11-19, not yet answered) give it
/// IOA0001.
/// </summary>
public class ObsoleteMembersTests
{
    private static readonly string Page = Path.Combine(AppContext.BaseDirectory, "Docs", "pages", "reference", "obsolete-members.md");

    /// <summary>The obsolete members without an id, each until a decision gives it one; the list may only shrink.</summary>
    internal static readonly IReadOnlySet<string> WithoutId = new HashSet<string>(StringComparer.Ordinal)
    {
        "Star.PropagationOutput"
    };

    // A type's name without its namespace, nested types with a dot
    private static string TypeName(Type type) =>
        type.DeclaringType == null ? PublicSurface.SimpleName(type) : $"{TypeName(type.DeclaringType)}.{PublicSurface.SimpleName(type)}";

    /// <summary>A member as the page's tables name it: "Type", "Type.Member", or "Type.new" for its constructors.</summary>
    internal static string Display(MemberInfo member) => member switch
    {
        Type t => TypeName(t),
        ConstructorInfo c => $"{TypeName(c.DeclaringType!)}.new",
        _ => $"{TypeName(member.DeclaringType!)}.{member.Name}"
    };

    /// <summary>The obsolete types and members of the public surface, with their attribute.</summary>
    internal static IEnumerable<(MemberInfo Member, ObsoleteAttribute Attribute)> ObsoleteMembers()
    {
        foreach (var type in PublicSurface.Types)
        {
            if (type.GetCustomAttribute<ObsoleteAttribute>(false) is { } typeAttribute)
            {
                yield return (type, typeAttribute);
            }

            foreach (var member in PublicSurface.Members(type))
            {
                // The compiler marks obsolete the constructors of a type with required members, for older compilers
                // (CompilerFeatureRequired("RequiredMembers")): not an obsolete member of the library
                if (member.GetCustomAttribute<ObsoleteAttribute>(false) is { } attribute &&
                    !member.IsDefined(typeof(System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute), false))
                {
                    yield return (member, attribute);
                }
            }
        }
    }

    /// <summary>Every violation of the convention, as "rule&lt;TAB&gt;member".</summary>
    internal static IEnumerable<string> Violations(IReadOnlyDictionary<string, IReadOnlySet<string>> pageTables)
    {
        var listed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (member, attribute) in ObsoleteMembers())
        {
            var name = Display(member);
            var id = attribute.DiagnosticId;
            if (id == null && WithoutId.Contains(name))
            {
                continue;
            }

            if (id != ObsoleteDiagnostics.ReplacedEntryPoint && id != ObsoleteDiagnostics.Trap)
            {
                yield return $"id\t{name}: DiagnosticId {id ?? "null"}, not IOA0001 or IOA0002";
                continue;
            }

            if (attribute.UrlFormat != ObsoleteDiagnostics.UrlFormat)
            {
                yield return $"url\t{name}: UrlFormat {attribute.UrlFormat ?? "null"}, not ObsoleteDiagnostics.UrlFormat";
            }

            if (string.IsNullOrWhiteSpace(attribute.Message))
            {
                yield return $"message\t{name}: no message naming the replacement or the fix";
            }

            if (id == ObsoleteDiagnostics.ReplacedEntryPoint)
            {
                if (attribute.IsError)
                {
                    yield return $"severity\t{name}: IOA0001 is a warning";
                }

                if (member.GetCustomAttribute<EditorBrowsableAttribute>(false)?.State != EditorBrowsableState.Never)
                {
                    yield return $"hidden\t{name}: IOA0001 needs [EditorBrowsable(EditorBrowsableState.Never)]";
                }
            }
            else if (!attribute.IsError)
            {
                yield return $"severity\t{name}: IOA0002 is an error (error: true)";
            }

            listed.Add($"{id} {name}");
            if (!pageTables.TryGetValue(id, out var rows) || !rows.Contains(name))
            {
                yield return $"page\t{name}: not in the table of {id} on obsolete-members.md";
            }
        }

        foreach (var (id, rows) in pageTables)
        {
            foreach (var row in rows.Where(r => !listed.Contains($"{id} {r}")))
            {
                yield return $"page\t{row}: listed under {id} on obsolete-members.md, but no member of the library carries {id}";
            }
        }
    }

    /// <summary>
    /// The members each section of the page lists: the first backticked name of each row of the section's table,
    /// "Type.Member", "Type.Member(...)" or "new Type(...)", by <see cref="Display"/>'s form.
    /// </summary>
    internal static IReadOnlyDictionary<string, IReadOnlySet<string>> PageTables(IEnumerable<string> lines)
    {
        var tables = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        HashSet<string> current = null;
        foreach (var line in lines)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                current = null;
                foreach (var id in new[] { ObsoleteDiagnostics.ReplacedEntryPoint, ObsoleteDiagnostics.Trap })
                {
                    if (line.StartsWith($"## {id}", StringComparison.Ordinal))
                    {
                        current = new HashSet<string>(StringComparer.Ordinal);
                        tables[id] = current;
                    }
                }

                continue;
            }

            if (current == null || !line.StartsWith("| `", StringComparison.Ordinal))
            {
                continue;
            }

            var cell = line[3..line.IndexOf('`', 3)];
            int parenthesis = cell.IndexOf('(');
            if (parenthesis >= 0)
            {
                cell = cell[..parenthesis];
            }

            current.Add(cell.StartsWith("new ", StringComparison.Ordinal) ? cell[4..] + ".new" : cell);
        }

        return tables;
    }

    [Fact]
    public void ObsoleteMembers_CarryTheirIdAndAreListedOnThePage()
    {
        var violations = Violations(PageTables(File.ReadAllLines(Page))).ToList();
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void Ids_AreTheDesignsAndLinkToTheirSectionOfThePage()
    {
        Assert.Equal("IOA0001", ObsoleteDiagnostics.ReplacedEntryPoint);
        Assert.Equal("IOA0002", ObsoleteDiagnostics.Trap);
        // The site of mkdocs.yml (site_url) and the page's address under it (use_directory_urls), the anchor the id itself
        Assert.Equal("https://docs.io-aerospace.org/reference/obsolete-members/#{0}", ObsoleteDiagnostics.UrlFormat);
        var text = File.ReadAllText(Page);
        foreach (var id in new[] { ObsoleteDiagnostics.ReplacedEntryPoint, ObsoleteDiagnostics.Trap })
        {
            Assert.Contains($"{{ #{id} }}", text);
        }

        var tables = PageTables(File.ReadAllLines(Page));
        Assert.Equal(new[] { "IOA0001", "IOA0002" }, tables.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void AllowList_HoldsOnlyObsoleteMembersWithoutId()
    {
        var withoutId = ObsoleteMembers().Where(m => m.Attribute.DiagnosticId == null).Select(m => Display(m.Member)).ToHashSet();
        Assert.All(WithoutId, name => Assert.Contains(name, withoutId));
    }

    [Fact]
    public void PageTables_ReadTheFirstNameOfEachRowOfEachSection()
    {
        var tables = PageTables([
            "## IOA0001: replaced entry point { #IOA0001 }", "| Member | Replacement | Since |", "|---|---|---|",
            "| `Spacecraft.Propagate(Window, ...)` | `Spacecraft.Propagate(Window, PropagationOptions)` | 11.0.0 |",
            "| `new Spacecraft(int, string, ...)` | x | 11.0.0 |", "## IOA0002: trap { #IOA0002 }",
            "| `Matrix.Set` | `Matrix.With` | 11.0.0 |", "## For contributors", "| `Not.Read` | | |"
        ]);
        Assert.Equal(new[] { "Spacecraft.Propagate", "Spacecraft.new" }.OrderBy(x => x), tables["IOA0001"].OrderBy(x => x));
        Assert.Equal(new[] { "Matrix.Set" }, tables["IOA0002"]);
    }
}
