// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Text;
using IO.Astrodynamics.CLI.Commands;
using IO.Astrodynamics.CLI.Commands.Parameters;
using IO.Astrodynamics.Tests.OrbitalParameters;
using Xunit.Abstractions;

namespace IO.Astrodynamics.CLI.Tests;

public class PropagateTests
{
    private readonly ITestOutputHelper _output;

    public PropagateTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task PropagateWithPerturbations()
    {
        var command = new PropagateCommand();
        StringBuilder sb = new StringBuilder();
        StringWriter sw = new StringWriter(sb);
        string res;

        using (await Configuration.LockAsync())
        {
            Console.SetOut(sw);

            await command.Propagate("Data", "MyBody",
                new Commands.Parameters.OrbitalParameters
                {
                    CenterOfMotionId = 399, OrbitalParametersEpoch = "0.0", Frame = "ICRF", OrbitalParametersValues = "6800000.0 0.0 0.0 0.0 8000.0 0.0",
                    FromStateVector = true
                }, new WindowParameters { Begin = "0.0", End = "3600.0" }, "PropagatorExport", true,
                true, 20, celestialBodies: [10, 301]);

            res = sb.ToString();
        }

        Assert.Contains("Propagation completed", res);
    }

    [Theory]
    [InlineData("circular, i 30, node 40 (suite 155)", "P452C")]
    [InlineData("equatorial, e 0.05, lop 120 (suite 156)", "P452E")]
    public async Task PropagateFromEquinoctial_StartsAtTheReference(string name, string outputDirectory)
    {
        // Arrange: --from-equinoctial with the elements of the conformance cases 155 (circular) and 156 (equatorial),
        // at J2000 TDB, the start of the window. Before #452, the start of 155 was 4.79e6 m off, that of 156 1.32e7 m.
        // The output directory is short: CSPICE loads a kernel path of at most 255 characters
        var reference = EquinoctialReference.Load();
        var c = reference.Cli.Single(x => x.Name == name);
        var command = new PropagateCommand();
        StringBuilder sb = new StringBuilder();
        StringWriter sw = new StringWriter(sb);
        OrbitalParameters.StateVector start;
        double gm;

        using (await Configuration.LockAsync())
        {
            Console.SetOut(sw);

            // Act: propagate, then read the exported ephemeris of the spacecraft (-1000) at the start of the window
            await command.Propagate("Data", "P452",
                new Commands.Parameters.OrbitalParameters
                {
                    CenterOfMotionId = 399, OrbitalParametersEpoch = "0.0", Frame = "ICRF",
                    OrbitalParametersValues = c.CliValues, FromEquinoctial = true
                }, new WindowParameters { Begin = "0.0", End = "60.0" }, outputDirectory, false, false);
            var ephemeris = new FileInfo(Directory.GetFiles(outputDirectory, "P452.spk", SearchOption.AllDirectories)
                .Single());
            try
            {
                SpiceAPI.Instance.LoadKernels(ephemeris);
                start = SpiceAPI.Instance.ReadEphemeris(TimeSystem.Time.J2000TDB, 399, -1000, Frames.Frame.ICRF,
                    Aberration.None).ToStateVector();
                gm = new Body.CelestialBody(399).GM;
            }
            finally
            {
                SpiceAPI.Instance.UnloadKernels(ephemeris);
            }
        }

        // Assert against the 60-digit state, with the GM of the kernels of these tests (gm_de431). Provisional, Linux
        // only until the three-platform CI, and measured again after #453: twice 1.60e-16 (suite 156) and 2.80e-16
        // (suite 155; Linux, .NET 10, 2026-10-25)
        Assert.Contains("Propagation completed", sb.ToString());
        Assert.Equal(reference.Gm["gm_de431"], gm);
        var (position, velocity) = c.At(0.0).RelativeError(start);
        _output.WriteLine($"MEASURE #452 PropagateFromEquinoctial_StartsAtTheReference, {name}: position " +
                          $"{position:E2}, velocity {velocity:E2} (relative)");
        Assert.True(position < 3.3e-16, $"position {position:E2}");
        Assert.True(velocity < 5.7e-16, $"velocity {velocity:E2}");
    }
}
