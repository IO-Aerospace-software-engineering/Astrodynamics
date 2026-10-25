// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using IO.Astrodynamics.ConformanceRunner.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// The lot-1 fixture, <c>Fixture/Lot1</c>, written by <c>tools/conformance/sync_lot1_fixture.py</c> from the
/// conformance suite: its schemas, tolerances and the cases of orbital_elements, time_conversion and
/// frame_transformation; <c>data.sha256</c>, the SHA-256 of every kernel the cases name; and in <c>data/</c> only the
/// kernels that differ from the runner's. A test writes a suite from it into a directory of its own, with the kernels
/// its cases name, taken from the fixture or from <c>Data/SolarSystem</c> once their SHA-256 has been checked.
/// </summary>
internal static class Lot1Fixture
{
    public const string Root = "Fixture/Lot1";
    public const string KernelsPath = "Data/SolarSystem";

    /// <summary>The categories of lot 1.</summary>
    public static readonly string[] Categories = { "orbital_elements", "time_conversion", "frame_transformation" };

    /// <summary>The three example cases of the design, which the suite keeps among its cases.</summary>
    public const string ElementsCase = "cases/orbital_elements/001";
    public const string TimeCase = "cases/time_conversion/001";
    public const string FrameCase = "cases/frame_transformation/003";

    private static readonly string[] Schemas = { "case.schema.json", "expected.schema.json", "runner-report.schema.json" };
    private static readonly string[] CaseFiles = { "inputs.yaml", "expected-result.json" };

    /// <summary>The case directories of the fixture, relative to its root: <c>cases/&lt;category&gt;/&lt;NNN&gt;</c>.</summary>
    public static IReadOnlyList<string> CaseDirectories()
    {
        return Categories
            .Select(category => Path.Combine(Root, "cases", category))
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.GetDirectories(directory).OrderBy(d => d, StringComparer.Ordinal))
            .Select(directory => Path.GetRelativePath(Root, directory).Replace('\\', '/'))
            .ToList();
    }

    /// <summary>
    /// Writes into <paramref name="directory"/> a suite of the fixture's schemas and tolerances, the given cases (all
    /// by default) and, in <c>data/</c>, the kernels they name.
    /// </summary>
    /// <exception cref="InvalidOperationException">A kernel is not in <c>data.sha256</c>, or its SHA-256 differs.</exception>
    public static void WriteSuite(string directory, params string[] cases)
    {
        cases = cases.Length == 0 ? CaseDirectories().ToArray() : cases;
        Directory.CreateDirectory(Path.Combine(directory, "schemas", "v1"));
        foreach (var schema in Schemas)
        {
            CopyFile(Path.Combine(Root, "schemas", "v1", schema), Path.Combine(directory, "schemas", "v1", schema));
        }

        CopyFile(Path.Combine(Root, "tolerances.yaml"), Path.Combine(directory, "tolerances.yaml"));

        var kernels = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var caseDirectory in cases)
        {
            Directory.CreateDirectory(Path.Combine(directory, caseDirectory));
            foreach (var file in CaseFiles)
            {
                CopyFile(Path.Combine(Root, caseDirectory, file), Path.Combine(directory, caseDirectory, file));
            }

            kernels.UnionWith(KernelsOf(ReadCase(Root, caseDirectory)));
        }

        var hashes = DataHashes();
        foreach (var kernel in kernels)
        {
            if (!hashes.TryGetValue(kernel, out var expected))
            {
                throw new InvalidOperationException($"{kernel} is not in {Root}/data.sha256: sync the fixture again.");
            }

            var source = File.Exists(Path.Combine(Root, "data", kernel))
                ? Path.Combine(Root, "data", kernel)
                : Path.Combine(KernelsPath, kernel);
            var actual = ContentSha256(source);
            if (actual != expected)
            {
                throw new InvalidOperationException(
                    $"{source} has the SHA-256 {actual}, the suite's file {expected}: sync the fixture again (tools/conformance/sync_lot1_fixture.py).");
            }

            Directory.CreateDirectory(Path.Combine(directory, "data"));
            CopyFile(source, Path.Combine(directory, "data", kernel));
        }
    }

    /// <summary>The SHA-256 of the kernels of <c>data.sha256</c>, by name.</summary>
    public static IReadOnlyDictionary<string, string> DataHashes()
    {
        return File.ReadAllLines(Path.Combine(Root, "data.sha256"))
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(fields => fields.Length == 2)
            .ToDictionary(fields => fields[1], fields => fields[0]);
    }

    /// <summary>
    /// The SHA-256 of a file as committed: a file without a NUL byte (a text kernel) is hashed with its CRLF read as LF,
    /// since Git converts the line endings of text files on a Windows checkout. The sync script hashes the same way.
    /// </summary>
    public static string ContentSha256(string path)
    {
        byte[] content;
        using (var stream = OpenShared(path))
        {
            content = new byte[stream.Length];
            stream.ReadExactly(content);
        }

        if (Array.IndexOf(content, (byte)0) < 0)
        {
            var normalized = new List<byte>(content.Length);
            for (int i = 0; i < content.Length; i++)
            {
                if (content[i] != (byte)'\r' || i + 1 >= content.Length || content[i + 1] != (byte)'\n')
                {
                    normalized.Add(content[i]);
                }
            }

            content = normalized.ToArray();
        }

        return Convert.ToHexStringLower(SHA256.HashData(content));
    }

    /// <summary>A case of a suite, as the runner reads its inputs.yaml.</summary>
    public static CaseInput ReadCase(string suite, string caseDirectory)
    {
        return new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build()
            .Deserialize<CaseInput>(File.ReadAllText(Path.Combine(suite, caseDirectory, "inputs.yaml")));
    }

    /// <summary>The tolerances of the fixture.</summary>
    public static ToleranceConfig ReadTolerances(string suite = Root)
    {
        return new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build()
            .Deserialize<ToleranceConfig>(File.ReadAllText(Path.Combine(suite, "tolerances.yaml")));
    }

    /// <summary>Edits the outputs of the golden of a case of a suite.</summary>
    public static void EditGolden(string suite, string caseDirectory, Action<JsonObject> edit)
    {
        var path = Path.Combine(suite, caseDirectory, "expected-result.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        edit(document["outputs"]!.AsObject());
        File.WriteAllText(path, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static IEnumerable<string> KernelsOf(CaseInput caseInput)
    {
        return caseInput.Inputs.TryGetValue("kernels", out var kernels) && kernels is List<object> list
            ? list.Select(kernel => kernel.ToString())
            : Enumerable.Empty<string>();
    }

    // A kernel SPICE has opened is read and copied without asking for exclusive access (Windows)
    private static FileStream OpenShared(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    }

    private static void CopyFile(string source, string destination)
    {
        using var input = OpenShared(source);
        using var output = File.Create(destination);
        input.CopyTo(output);
    }
}
