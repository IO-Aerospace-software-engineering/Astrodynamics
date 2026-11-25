// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The text kernel writers in a named culture (#821), in the child process <c>IO.Astrodynamics.Tests.Culture</c>, which
/// runs outside the invariant globalization of the repository (<c>Directory.Build.props</c>): this process cannot hold a
/// named culture, and an application that does not set invariant globalization runs its writers in the culture of its
/// thread. The child writes the clock, the spacecraft frame kernel, the frame and instrument kernels of a circular, a
/// rectangular and an elliptical instrument and the site frame kernel of <see cref="CultureKernelSet"/>, loads each and
/// reports what SPICE knows; this process writes the same set, which is the reference: the bytes a writer gives in the
/// invariant culture.
/// </summary>
/// <remarks>
/// Before #821 (measured on 21d0e648, Linux x64, ICU 74): the negative ids were substituted in the current culture,
/// U+2212 before the digits under sv-SE and fi-FI, U+200E then '-' under he-IL, so the spacecraft and instrument frame
/// kernels failed to load (<c>SpiceException</c>) and the instrument kernels defined <c>INS−…</c>, which
/// <c>getfov_c</c> does not find. The site frame kernel (positive ids) and the clock (the absolute value of the id) were
/// not affected, and are compared too.
/// </remarks>
[Collection(nameof(ColdStartCollection))]
public class KernelWriterCultureTests
{
    // A child writes and loads a few text kernels: well under 1 s; the limit is for a slow CI machine
    private static readonly TimeSpan ChildTimeout = TimeSpan.FromSeconds(120);
    private readonly ITestOutputHelper _output;

    public KernelWriterCultureTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    [Theory]
    [InlineData("sv-SE")]
    [InlineData("fi-FI")]
    [InlineData("he-IL")]
    public async Task TextKernels_InACultureOfAnotherMinusSign_AreTheInvariantBytes_AndDefineTheirIds(string culture)
    {
        const int spacecraftId = -1821;
        const int siteId = 821;
        var root = Directory.CreateTempSubdirectory("culture821_");
        try
        {
            // The reference: the set written in this process, in the invariant culture
            var reference = root.CreateSubdirectory("invariant");
            var (spacecraft, site) = CultureKernelSet.Build(spacecraftId, "SAT821", ["CAM821", "RECT821", "ELL821"], siteId, "SITE821");
            await CultureKernelSet.WriteAsync(spacecraft, site, reference);

            var written = Path.Combine(root.FullName, culture);
            var lines = RunChild(culture, "text-kernels", written, "-1821", "SAT821", "CAM821", "RECT821", "ELL821", "821", "SITE821");

            // The child runs in the culture, whose minus sign is not '-': the test measures what it should
            Assert.NotEqual("U+002D+U+0031", lines["CULTURE"].Split(' ')[2]);

            // Each kernel holds the bytes of the invariant culture
            foreach (var file in CultureKernelSet.Files)
            {
                Assert.True(File.ReadAllBytes(Path.Combine(reference.FullName, file)).SequenceEqual(File.ReadAllBytes(Path.Combine(written, file))),
                    $"{file} under {culture} differs from the invariant culture:{Environment.NewLine}{File.ReadAllText(Path.Combine(written, file))}");
            }

            // Each loads, and defines its frame and its field of view
            foreach (var file in CultureKernelSet.Files)
            {
                Assert.Equal("OK", lines[$"LOAD {file}"]);
            }

            Assert.Equal("SAT821_FRAME", lines["FRAME -1821000"]);
            Assert.Equal("SAT821_CAM821", lines["FRAME -1821001"]);
            Assert.Equal("SAT821_RECT821", lines["FRAME -1821002"]);
            Assert.Equal("SAT821_ELL821", lines["FRAME -1821003"]);
            Assert.Equal("SITE821_TOPO", lines["FRAME 1399821"]);
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal("CIRCLE", lines["FOV -1821001"]);
                Assert.Equal("RECTANGLE", lines["FOV -1821002"]);
                Assert.Equal("ELLIPSE", lines["FOV -1821003"]);
            }
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Theory]
    [InlineData("sv-SE")]
    [InlineData("he-IL")]
    public async Task CosmographiaExport_InACultureOfAnotherMinusSignAndDecimalSeparator_WritesTheInvariantKernelsAndNumbers(string culture)
    {
        // Before #821 the export failed under sv-SE and he-IL: it loads the spacecraft frame kernel it writes in its scratch
        // directory, which held the id in the culture (SpiceException); its trajectory plot wrote "1,5 d" under sv-SE
        var root = Directory.CreateTempSubdirectory("culture821_");
        try
        {
            var reference = root.CreateSubdirectory("invariant");
            var (spacecraft, site) = CultureKernelSet.Build(-1822, "SAT822", ["CAM822", "RECT822", "ELL822"], 822, "SITE822");
            await CultureKernelSet.WriteAsync(spacecraft, site, reference);

            var written = Path.Combine(root.FullName, culture);
            var lines = RunChild(culture, "export", written, "-1822", "SAT822", "CAM822", "RECT822", "ELL822", "822", "SITE822");
            Assert.Equal("OK", lines["EXPORT"]);
            var export = Path.Combine(written, CultureKernelSet.ExportName);

            // The text kernels of the export are the bytes of the invariant culture
            var kernels = new Dictionary<string, string>
            {
                ["clock.tsc"] = Path.Combine("Spacecrafts", "SAT822", "Clocks", "SAT822.tsc"),
                ["spacecraft.tf"] = Path.Combine("Spacecrafts", "SAT822", "Frames", "SAT822.tf"),
                ["circular.tf"] = Path.Combine("Spacecrafts", "SAT822", "Instruments", "CAM822.tf"),
                ["circular.ti"] = Path.Combine("Spacecrafts", "SAT822", "Instruments", "CAM822.ti"),
                ["rectangular.tf"] = Path.Combine("Spacecrafts", "SAT822", "Instruments", "RECT822.tf"),
                ["rectangular.ti"] = Path.Combine("Spacecrafts", "SAT822", "Instruments", "RECT822.ti"),
                ["elliptical.tf"] = Path.Combine("Spacecrafts", "SAT822", "Instruments", "ELL822.tf"),
                ["elliptical.ti"] = Path.Combine("Spacecrafts", "SAT822", "Instruments", "ELL822.ti"),
                ["site.tf"] = Path.Combine("Sites", "SITE822", "SITE822.tf")
            };
            foreach (var (file, exported) in kernels)
            {
                Assert.True(File.ReadAllBytes(Path.Combine(reference.FullName, file)).SequenceEqual(File.ReadAllBytes(Path.Combine(export, exported))),
                    $"{exported} under {culture} differs from the invariant culture");
            }

            // The trajectory plot: the period in days, and a tenth of it, in the invariant culture
            using var catalog = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(export, "spacecrafts.json")));
            var plot = catalog.RootElement.GetProperty("items")[0].GetProperty("trajectoryPlot");
            double days = spacecraft.InitialOrbitalParameters.Period().TotalDays;
            Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"{days} d"), plot.GetProperty("duration").GetString());
            Assert.Equal(string.Create(CultureInfo.InvariantCulture, $"{days * 0.1} d"), plot.GetProperty("lead").GetString());
        }
        finally
        {
            root.Delete(true);
        }
    }

    /// <summary>
    /// Runs the child with the mode, the culture and the output directory, requires it to exit 0 within
    /// <see cref="ChildTimeout"/>, and returns its lines keyed by their words before the last (<c>CULTURE</c> by its
    /// first word).
    /// </summary>
    private Dictionary<string, string> RunChild(string culture, string mode, string output, params string[] arguments)
    {
        string child = ChildProcess.AssemblyPath("Culture", "IO.Astrodynamics.Tests.Culture.dll");
        Assert.True(File.Exists(child), $"The child process {child} is missing: the build of the test project copies it.");
        var result = ChildProcess.Run(child, ChildTimeout,
            new[] { Path.Combine(AppContext.BaseDirectory, "Data", "SolarSystem"), mode, culture, output }.Concat(arguments).ToArray());
        Assert.True(result.HasValue, $"The child ({mode}, {culture}) did not end within {ChildTimeout.TotalSeconds} s.");
        var (exitCode, standardOutput, error) = result.Value;
        _output.WriteLine(standardOutput);
        Assert.True(exitCode == 0, $"The child ({mode}, {culture}) exited {exitCode}. Output: {standardOutput} Error: {error}");
        var lines = new Dictionary<string, string>();
        foreach (var line in standardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("CULTURE ", StringComparison.Ordinal))
            {
                lines["CULTURE"] = line["CULTURE ".Length..];
                continue;
            }

            int last = line.LastIndexOf(' ');
            lines[line[..last]] = line[(last + 1)..];
        }

        return lines;
    }
}
