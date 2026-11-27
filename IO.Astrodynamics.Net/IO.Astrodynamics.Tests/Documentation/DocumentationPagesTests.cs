// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace IO.Astrodynamics.Tests.Documentation;

/// <summary>
/// Rules that the csharp blocks of the pages an application's developer reads (docs/ and README.md) keep, checked on
/// their text (#668, third round), a quick check beside the compiled ones: the blocks of docs/ and README.md are
/// compiled regions too since #705 (<see cref="DocumentationPagesExamplesTests"/>), as those of the two CLAUDE.md files
/// (<see cref="ClaudeMdExamplesTests"/>). DEVELOPER_GUIDE.md is a pointer to the site since lot L9 of #668, its content
/// moved to the pages.
/// </summary>
public class DocumentationPagesTests
{
    private static readonly string Docs = Path.Combine(AppContext.BaseDirectory, "Docs");

    // The changelog, whose blocks keep the code of former versions (its migration notes show the removed static class
    // Frames of 9.x, "Frames.CIRS", as it was written then)
    private static readonly string Changelog = Path.Combine("pages", "contributing", "versioning.md");

    // The pages, by their path under the output's Docs directory, the changelog aside
    public static IEnumerable<object[]> Pages() =>
        Directory.GetFiles(Path.Combine(Docs, "pages"), "*.md", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(Docs, p))
            .Where(p => p != Changelog)
            .Append("README.md")
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => new object[] { p });

    private static readonly Regex StringLiteral = new("@?\"(?:[^\"\\\\]|\\\\.)*\"");

    // The code of a block line: string literals and the line comment removed
    private static string Code(string line)
    {
        var code = StringLiteral.Replace(line, "\"\"");
        int comment = code.IndexOf("//", StringComparison.Ordinal);
        return comment < 0 ? code : code[..comment];
    }

    /// <summary>
    /// The first segments of the library's namespaces below <c>IO.Astrodynamics</c>, <c>Math</c> aside: in an application's
    /// namespace <c>Math.PI</c> is <c>System.Math</c>.
    /// </summary>
    private static HashSet<string> LibrarySubNamespaces() =>
        typeof(IO.Astrodynamics.Constants).Assembly.GetTypes()
            .Select(t => t.Namespace)
            .Where(n => n != null && n.StartsWith("IO.Astrodynamics.", StringComparison.Ordinal))
            .Select(n => n["IO.Astrodynamics.".Length..].Split('.')[0])
            .Where(s => s != "Math")
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// No csharp block of a page names a library type through a namespace relative to <c>IO.Astrodynamics</c>, as
    /// <c>Frames.Frame.ICRF</c>: a <c>using IO.Astrodynamics;</c> directive imports the types of that namespace, not its
    /// sub-namespaces, so such a name resolves only inside <c>IO.Astrodynamics</c> (the library and its tests) and an
    /// application's compiler reports CS0103 (C# language specification, §14.5 "Using directives"). Red on 608bcf6: 14
    /// such names in eight pages of docs/ (get-started, the orbit-creation and ephemeris-queries tutorials, the
    /// force-models, celestial-bodies, state-vector, occultations and spacecraft references).
    /// </summary>
    [Theory]
    [MemberData(nameof(Pages))]
    public void CodeBlocks_NameNoTypeThroughARelativeNamespace(string page)
    {
        var relative = new Regex($@"(?<![\w.])({string.Join("|", LibrarySubNamespaces().Select(Regex.Escape))})\.[A-Z]\w*");
        var faults = new List<string>();
        foreach (var block in DocumentationCodeBlocks.Blocks(Path.Combine(Docs, page)))
        {
            foreach (var line in block)
            {
                if (line.TrimStart().StartsWith("using ", StringComparison.Ordinal))
                {
                    continue;
                }

                faults.AddRange(relative.Matches(Code(line)).Select(m => $"{m.Value} in \"{line.Trim()}\""));
            }
        }

        Assert.True(faults.Count == 0, $"{page}: {string.Join("; ", faults)}");
    }

    // The arguments of the call whose opening parenthesis is at open, split at the commas of its own level
    private static List<string> Arguments(string code, int open)
    {
        var arguments = new List<string>();
        int depth = 0, start = open + 1;
        for (int i = open; i < code.Length; i++)
        {
            char c = code[i];
            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}')
            {
                depth--;
                if (depth == 0)
                {
                    arguments.Add(code[start..i].Trim());
                    return arguments;
                }
            }
            else if (c == ',' && depth == 1)
            {
                arguments.Add(code[start..i].Trim());
                start = i + 1;
            }
        }

        throw new InvalidOperationException($"No closing parenthesis after {code[open..System.Math.Min(code.Length, open + 80)]}");
    }

    private static readonly Regex ZeroSeparation = new(@"^(minimumVectorSeparation\s*:\s*)?0(\.0*)?[dD]?$");

    /// <summary>
    /// No <c>TriadAttitude</c> of a page passes a minimum vector separation of 0, the last parameter of each of its
    /// constructors: the rule is 5 degrees at least between the body vectors and between the reference vectors
    /// (<c>TriadAttitude.DefaultMinimumSeparation</c>), and 0 turns off the guard on collinear reference vectors in
    /// <c>ComputeOrientation</c>, whose cross product is then the zero vector. Red on 608bcf6: the second TRIAD of the
    /// maneuvers tutorial (Front to prograde, Up to the Moon) passed <c>engine, 0.0</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(Pages))]
    public void TriadAttitudes_KeepAMinimumSeparation(string page)
    {
        var faults = new List<string>();
        foreach (var block in DocumentationCodeBlocks.Blocks(Path.Combine(Docs, page)))
        {
            var code = string.Join("\n", block.Select(Code));
            foreach (Match match in Regex.Matches(code, @"\bnew TriadAttitude\s*\("))
            {
                var last = Arguments(code, match.Index + match.Length - 1)[^1];
                if (ZeroSeparation.IsMatch(last))
                {
                    faults.Add($"{match.Value}... {last})");
                }
            }
        }

        Assert.True(faults.Count == 0, $"{page}: {string.Join("; ", faults)}");
    }
}
