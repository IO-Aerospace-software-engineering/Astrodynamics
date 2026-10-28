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
/// A fixture of cases of the conformance suite, <c>Fixture/Lot&lt;n&gt;</c>, written by
/// <c>tools/conformance/sync_fixture.py --lot &lt;n&gt;</c> from the suite: its schemas, tolerances and the cases of the
/// lot; <c>data.sha256</c>, the SHA-256 of every file of <c>data/</c> the cases read; and in <c>data/</c> only the files
/// that differ from the runner's kernels and from the <c>data/</c> of the earlier lots' fixtures. A test writes a suite
/// from it into a directory of its own, with the files its cases read, taken from the fixture or from those directories
/// once their SHA-256 has been checked.
/// </summary>
internal sealed class SuiteFixture
{
    /// <summary>The runner's kernels.</summary>
    public const string KernelsPath = "Data/SolarSystem";

    /// <summary>Lot 1 (design of #458): orbital_elements, time_conversion and frame_transformation.</summary>
    public static readonly SuiteFixture Lot1 = new("Fixture/Lot1",
        new[] { "orbital_elements", "time_conversion", "frame_transformation" }, new[] { KernelsPath });

    /// <summary>
    /// Lot 2 (design of #500): the propagator cases 006 to 018, whose field files the runner reads from <c>data/</c>; the
    /// suite's Earth orientation file is the one of lot 1's fixture.
    /// </summary>
    public static readonly SuiteFixture Lot2 = new("Fixture/Lot2",
        new[] { "propagator" }, new[] { KernelsPath, "Fixture/Lot1/data" });

    private static readonly string[] Schemas = { "case.schema.json", "expected.schema.json", "runner-report.schema.json" };
    private static readonly string[] CaseFiles = { "inputs.yaml", "expected-result.json" };

    private readonly IReadOnlyList<string> _referenceData;

    private SuiteFixture(string root, IReadOnlyList<string> categories, IReadOnlyList<string> referenceData)
    {
        Root = root;
        Categories = categories;
        _referenceData = referenceData;
    }

    /// <summary>The root of the fixture.</summary>
    public string Root { get; }

    /// <summary>The categories of the lot.</summary>
    public IReadOnlyList<string> Categories { get; }

    /// <summary>The case directories of the fixture, relative to its root: <c>cases/&lt;category&gt;/&lt;NNN&gt;</c>.</summary>
    public IReadOnlyList<string> CaseDirectories()
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
    /// by default) and, in <c>data/</c>, the files they read.
    /// </summary>
    /// <exception cref="InvalidOperationException">A file is not in <c>data.sha256</c>, or no copy of it has its
    /// SHA-256.</exception>
    public void WriteSuite(string directory, params string[] cases)
    {
        cases = cases.Length == 0 ? CaseDirectories().ToArray() : cases;
        Directory.CreateDirectory(Path.Combine(directory, "schemas", "v1"));
        foreach (var schema in Schemas)
        {
            CopyFile(Path.Combine(Root, "schemas", "v1", schema), Path.Combine(directory, "schemas", "v1", schema));
        }

        CopyFile(Path.Combine(Root, "tolerances.yaml"), Path.Combine(directory, "tolerances.yaml"));

        var files = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var caseDirectory in cases)
        {
            Directory.CreateDirectory(Path.Combine(directory, caseDirectory));
            foreach (var file in CaseFiles)
            {
                CopyFile(Path.Combine(Root, caseDirectory, file), Path.Combine(directory, caseDirectory, file));
            }

            files.UnionWith(DataFilesOf(ReadCase(Root, caseDirectory)));
        }

        var hashes = DataHashes();
        foreach (var file in files)
        {
            if (!hashes.TryGetValue(file, out var expected))
            {
                throw new InvalidOperationException($"{file} is not in {Root}/data.sha256: sync the fixture again.");
            }

            var sources = new[] { Path.Combine(Root, "data") }.Concat(_referenceData).Select(d => Path.Combine(d, file))
                .Where(File.Exists).ToList();
            var source = sources.FirstOrDefault(path => ContentSha256(path) == expected) ?? throw new InvalidOperationException(
                $"No copy of {file} ({string.Join(", ", sources)}) has the SHA-256 of the suite's file, {expected}: sync the fixture again (tools/conformance/sync_fixture.py).");
            Directory.CreateDirectory(Path.Combine(directory, "data"));
            CopyFile(source, Path.Combine(directory, "data", file));
        }
    }

    /// <summary>The SHA-256 of the files of <c>data.sha256</c>, by name.</summary>
    public IReadOnlyDictionary<string, string> DataHashes()
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

    /// <summary>The tolerances of a suite.</summary>
    public static ToleranceConfig ReadTolerances(string suite)
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

    // The files of data/ a case reads: its kernels and its field file, which the runner reads from data/
    private static IEnumerable<string> DataFilesOf(CaseInput caseInput)
    {
        var kernels = caseInput.Inputs.TryGetValue("kernels", out var list) && list is List<object> names
            ? names.Select(kernel => kernel.ToString())
            : Enumerable.Empty<string>();
        return caseInput.Inputs.TryGetValue("geopotential_model", out var field) && field?.ToString() is { } model && model != "none"
            ? kernels.Append(model)
            : kernels;
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
