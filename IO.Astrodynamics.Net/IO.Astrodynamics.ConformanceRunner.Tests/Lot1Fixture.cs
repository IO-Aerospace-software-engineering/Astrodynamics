// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IO.Astrodynamics.ConformanceRunner.Models;

namespace IO.Astrodynamics.ConformanceRunner.Tests;

/// <summary>
/// The lot-1 fixture, <c>Fixture/Lot1</c> (<see cref="SuiteFixture.Lot1"/>), written by
/// <c>tools/conformance/sync_fixture.py --lot 1</c> from the conformance suite: its schemas, tolerances and the cases of
/// orbital_elements, time_conversion and frame_transformation; <c>data.sha256</c>, the SHA-256 of every kernel the cases
/// name; and in <c>data/</c> only the kernels that differ from the runner's.
/// </summary>
internal static class Lot1Fixture
{
    public const string Root = "Fixture/Lot1";
    public const string KernelsPath = SuiteFixture.KernelsPath;

    /// <summary>The categories of lot 1.</summary>
    public static readonly IReadOnlyList<string> Categories = SuiteFixture.Lot1.Categories;

    /// <summary>The three example cases of the design, which the suite keeps among its cases.</summary>
    public const string ElementsCase = "cases/orbital_elements/001";
    public const string TimeCase = "cases/time_conversion/001";
    public const string FrameCase = "cases/frame_transformation/003";

    /// <inheritdoc cref="SuiteFixture.CaseDirectories"/>
    public static IReadOnlyList<string> CaseDirectories() => SuiteFixture.Lot1.CaseDirectories();

    /// <inheritdoc cref="SuiteFixture.WriteSuite"/>
    public static void WriteSuite(string directory, params string[] cases) => SuiteFixture.Lot1.WriteSuite(directory, cases);

    /// <inheritdoc cref="SuiteFixture.DataHashes"/>
    public static IReadOnlyDictionary<string, string> DataHashes() => SuiteFixture.Lot1.DataHashes();

    /// <inheritdoc cref="SuiteFixture.ContentSha256"/>
    public static string ContentSha256(string path) => SuiteFixture.ContentSha256(path);

    /// <inheritdoc cref="SuiteFixture.ReadCase"/>
    public static CaseInput ReadCase(string suite, string caseDirectory) => SuiteFixture.ReadCase(suite, caseDirectory);

    /// <inheritdoc cref="SuiteFixture.ReadTolerances"/>
    public static ToleranceConfig ReadTolerances(string suite = Root) => SuiteFixture.ReadTolerances(suite);

    /// <inheritdoc cref="SuiteFixture.EditGolden"/>
    public static void EditGolden(string suite, string caseDirectory, Action<JsonObject> edit) =>
        SuiteFixture.EditGolden(suite, caseDirectory, edit);
}
