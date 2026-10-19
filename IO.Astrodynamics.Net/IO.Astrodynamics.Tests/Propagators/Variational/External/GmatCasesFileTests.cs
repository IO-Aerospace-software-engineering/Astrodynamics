// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators.Variational.External;

/// <summary>
/// The file that hands the cases G1 to G6 to GMAT, <c>Data/GmatReference/cases.json</c>, is the reference cases as the
/// library holds them: the GMAT scripts are generated from it, so the outputs compared by <c>GmatReferenceTests</c>
/// start from exactly the states and models of the library.
/// </summary>
/// <remarks>
/// With <c>IO_ASTRODYNAMICS_GMAT_CASES_CAPTURE</c> set to a directory, the test writes the file there and fails, as
/// <c>PropagationGoldenTests</c> does for its goldens; <c>tools/gmat_reference/README.md</c> gives the procedure.
/// </remarks>
public class GmatCasesFileTests : IClassFixture<ReferenceCases>
{
    private const string CaptureVariable = "IO_ASTRODYNAMICS_GMAT_CASES_CAPTURE";

    /// <summary>The directory of the GMAT inputs and outputs in the output directory of the tests.</summary>
    internal const string DataDirectory = "Data/GmatReference";

    private readonly ReferenceCases _cases;

    public GmatCasesFileTests(ReferenceCases cases)
    {
        _cases = cases;
    }

    [Fact]
    public void CasesFile_IsTheReferenceCasesAsTheLibraryHoldsThem()
    {
        // Act
        var actual = GmatCases.ToJson(_cases);

        // Assert
        var captureDirectory = Environment.GetEnvironmentVariable(CaptureVariable);
        if (!string.IsNullOrEmpty(captureDirectory))
        {
            File.WriteAllText(Path.Combine(captureDirectory, "cases.json"), actual);
            Assert.Fail($"cases.json captured in {captureDirectory}, not compared.");
        }

        Assert.Equal(File.ReadAllText(Path.Combine(DataDirectory, "cases.json")).ReplaceLineEndings("\n"), actual);
    }
}
