// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace IO.Astrodynamics.Tests.Documentation;

/// <summary>
/// The csharp blocks of a Markdown page and the regions of the C# file that compiles them (#668): read by
/// <see cref="DeveloperGuideExamplesTests"/> for DEVELOPER_GUIDE.md and <see cref="ClaudeMdExamplesTests"/> for the two
/// CLAUDE.md files, so that a page's blocks are compared with code the compiler checks.
/// </summary>
internal static class DocumentationCodeBlocks
{
    private static readonly Regex UsingDirective = new(@"^using [A-Za-z0-9_.]+( = [A-Za-z0-9_.]+)?;$");

    /// <summary>The csharp blocks of a Markdown file, in order.</summary>
    internal static List<List<string>> Blocks(string markdownPath)
    {
        var lines = File.ReadAllLines(markdownPath);
        var blocks = new List<List<string>>();
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() != "```csharp")
            {
                continue;
            }

            var block = new List<string>();
            for (i++; lines[i].Trim() != "```"; i++)
            {
                block.Add(lines[i]);
            }

            blocks.Add(block);
        }

        return blocks;
    }

    /// <summary>The regions <c>#region &lt;prefix&gt;:&lt;n&gt;</c> of a C# file, by n.</summary>
    internal static SortedDictionary<int, List<string>> Regions(string sourcePath, string prefix)
    {
        var lines = File.ReadAllLines(sourcePath);
        var regions = new SortedDictionary<int, List<string>>();
        var header = new Regex($@"^#region {Regex.Escape(prefix)}:(\d+)$");
        for (int i = 0; i < lines.Length; i++)
        {
            var match = header.Match(lines[i].Trim());
            if (!match.Success)
            {
                continue;
            }

            var region = new List<string>();
            for (i++; lines[i].Trim() != "#endregion"; i++)
            {
                region.Add(lines[i]);
            }

            regions.Add(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), region);
        }

        return regions;
    }

    /// <summary>The leading using directives of a block, which the compiled file holds once, and the rest of the block.</summary>
    internal static (List<string> Usings, List<string> Code) SplitUsings(List<string> block)
    {
        var usings = new List<string>();
        int start = 0;
        while (start < block.Count && (UsingDirective.IsMatch(block[start].Trim()) || block[start].Trim().Length == 0))
        {
            if (block[start].Trim().Length > 0)
            {
                usings.Add(block[start].Trim());
            }

            start++;
        }

        return (usings, block.Skip(start).ToList());
    }

    /// <summary>Indentation normalized: the common indentation removed, the trailing blanks and blank edge lines dropped.</summary>
    internal static List<string> Normalize(IEnumerable<string> lines)
    {
        var list = lines.Select(l => l.TrimEnd()).ToList();
        while (list.Count > 0 && list[0].Length == 0) list.RemoveAt(0);
        while (list.Count > 0 && list[^1].Length == 0) list.RemoveAt(list.Count - 1);
        int indent = list.Where(l => l.Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        return list.Select(l => l.Length >= indent ? l[indent..] : l).ToList();
    }

    /// <summary>
    /// Asserts that blocks <paramref name="first"/> onwards equal the regions 1, 2, ... of <paramref name="regions"/>,
    /// their using directives aside (each naming a namespace of the library or of the base class library), and that there
    /// are as many regions as such blocks. <paramref name="label"/> names the page in the messages.
    /// </summary>
    internal static void AssertBlocksEqualRegions(string label, List<List<string>> blocks, int first, SortedDictionary<int, List<string>> regions)
    {
        int count = blocks.Count - first;
        Xunit.Assert.True(count == regions.Count, $"{label}: {count} blocks, {regions.Count} regions");
        Xunit.Assert.Equal(Enumerable.Range(1, count), regions.Keys);
        var namespaces = typeof(IO.Astrodynamics.Constants).Assembly.GetTypes().Select(t => t.Namespace).Where(n => n != null).ToHashSet();
        for (int n = 1; n <= count; n++)
        {
            var (usings, code) = SplitUsings(blocks[first + n - 1]);
            foreach (var directive in usings)
            {
                var name = directive["using ".Length..^1];
                Xunit.Assert.True(namespaces.Contains(name) || name.StartsWith("System", StringComparison.Ordinal), $"{label} block {n}: {directive}");
            }

            var expected = Normalize(code);
            var actual = Normalize(regions[n]);
            for (int i = 0; i < System.Math.Min(expected.Count, actual.Count); i++)
            {
                Xunit.Assert.True(expected[i] == actual[i], $"{label} block {n}, line {i + 1}: the page has \"{expected[i]}\", the region \"{actual[i]}\"");
            }

            Xunit.Assert.True(expected.Count == actual.Count, $"{label} block {n}: {expected.Count} lines on the page, {actual.Count} in the region");
        }
    }
}
