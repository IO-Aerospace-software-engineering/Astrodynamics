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
/// The text kernel writers and the Cosmographia export in a named culture (#821, #798), in the child process
/// <c>IO.Astrodynamics.Tests.Culture</c>, which runs outside the invariant globalization of the repository
/// (<c>Directory.Build.props</c>): this process cannot hold a named culture, and an application that does not set
/// invariant globalization runs its writers in the culture of its thread. The child writes the clock, the spacecraft frame
/// kernel, the frame and instrument kernels of a circular, a rectangular and an elliptical instrument and the site frame
/// kernel of <see cref="CultureKernelSet"/>, loads each and reports what SPICE knows, or exports a scenario of the same
/// objects; this process writes the same set, which is the reference: the bytes a writer gives in the invariant culture.
/// </summary>
/// <remarks>
/// <para>
/// Before #821 (measured on 21d0e648, Linux x64, ICU 74): the negative ids were substituted in the current culture,
/// U+2212 before the digits under sv-SE and fi-FI, U+200E then '-' under he-IL, so the spacecraft and instrument frame
/// kernels failed to load (<c>SpiceException</c>), the export with them, and the instrument kernels defined
/// <c>INS−…</c>, which <c>getfov_c</c> does not find. The site frame kernel (positive ids) and the clock (the absolute
/// value of the id) were not affected, and are compared too.
/// </para>
/// <para>
/// Before #798 (the same machine): the names were upper-cased in the current culture, "i" giving "İ" (U+0130) under
/// tr-TR, so the kernels and the catalog of an export named <c>İSS799_İMAGER</c> what any other culture names
/// <c>ISS799_IMAGER</c>, a site's frame <c>SİTE798_TOPO</c>, and the catalog named the body of a site on Jupiter
/// <c>Jupıter</c> (U+0131), in <c>jupıter-features.json</c>.
/// </para>
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
    public void TextKernels_InACultureOfAnotherMinusSign_AreTheInvariantBytes_AndDefineTheirIds(string culture)
    {
        const int spacecraftId = -1821;
        const int siteId = 821;
        var root = Directory.CreateTempSubdirectory("culture821_");
        try
        {
            // The reference: the set written in this process, in the invariant culture
            var reference = root.CreateSubdirectory("invariant");
            var (spacecraft, site) = CultureKernelSet.Build(spacecraftId, "SAT821", ["CAM821", "RECT821", "ELL821"], siteId, "SITE821");
            CultureKernelSet.Write(spacecraft, site, reference);

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
    public void CosmographiaExport_InACultureOfAnotherMinusSignAndDecimalSeparator_WritesTheInvariantKernelsAndNumbers(string culture)
    {
        // Before #821 the export failed under sv-SE and he-IL: it loads the spacecraft frame kernel it writes in its scratch
        // directory, which held the id in the culture (SpiceException); its trajectory plot wrote "1,5 d" under sv-SE
        var root = Directory.CreateTempSubdirectory("culture821_");
        try
        {
            var reference = root.CreateSubdirectory("invariant");
            var (spacecraft, site) = CultureKernelSet.Build(-1822, "SAT822", ["CAM822", "RECT822", "ELL822"], 822, "SITE822");
            CultureKernelSet.Write(spacecraft, site, reference);

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

    [Fact]
    public void TextKernels_InTurkish_AreTheInvariantBytes_AndDefineTheInvariantNames()
    {
        // Before #798 the writers upper-cased with the current culture: under tr-TR "iss798" became "İSS798" (U+0130), a
        // name no other culture writes for the same object; the site's frame, named at construction, too
        var root = Directory.CreateTempSubdirectory("culture798_");
        try
        {
            var reference = root.CreateSubdirectory("invariant");
            var (spacecraft, site) = CultureKernelSet.Build(-1798, "iss798", ["imager", "irspec", "lidar"], 798, "site798");
            CultureKernelSet.Write(spacecraft, site, reference);
            Assert.Equal("SITE798_TOPO", site.Frame.Name);

            var written = Path.Combine(root.FullName, "tr-TR");
            var lines = RunChild("tr-TR", "text-kernels", written, "-1798", "iss798", "imager", "irspec", "lidar", "798", "site798");

            // The child runs in Turkish, where "i" upper-cases to U+0130: the test measures what it should
            Assert.Equal("U+0130", lines["CULTURE"].Split(' ')[4]);

            foreach (var file in CultureKernelSet.Files)
            {
                Assert.True(File.ReadAllBytes(Path.Combine(reference.FullName, file)).SequenceEqual(File.ReadAllBytes(Path.Combine(written, file))),
                    $"{file} under tr-TR differs from the invariant culture:{Environment.NewLine}{File.ReadAllText(Path.Combine(written, file))}");
                Assert.Equal("OK", lines[$"LOAD {file}"]);
            }

            Assert.Equal("ISS798_FRAME", lines["FRAME -1798000"]);
            Assert.Equal("ISS798_IMAGER", lines["FRAME -1798001"]);
            Assert.Equal("ISS798_IRSPEC", lines["FRAME -1798002"]);
            Assert.Equal("ISS798_LIDAR", lines["FRAME -1798003"]);
            Assert.Equal("SITE798_TOPO", lines["FRAME 1399798"]);
            Assert.Equal("SITE798_TOPO", lines["SITE-FRAME-NAME"]);
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal("CIRCLE", lines["FOV -1798001"]);
                Assert.Equal("RECTANGLE", lines["FOV -1798002"]);
                Assert.Equal("ELLIPSE", lines["FOV -1798003"]);
            }
        }
        finally
        {
            root.Delete(true);
        }
    }

    [Fact]
    public void CosmographiaExport_InTurkish_NamesTheObjectsOfItsKernels_AndTheBodiesAsCosmographia()
    {
        // Before #798, under tr-TR, the kernels and the catalog both wrote "İSS799_İMAGER", and the catalog named the
        // centre of a site on Jupiter "Jupıter" (U+0131), in "jupıter-features.json"
        var root = Directory.CreateTempSubdirectory("culture798_");
        try
        {
            var reference = root.CreateSubdirectory("invariant");
            var (spacecraft, site) = CultureKernelSet.Build(-1799, "iss799", ["imager", "irspec", "lidar"], 799, "site799");
            CultureKernelSet.Write(spacecraft, site, reference);

            var written = Path.Combine(root.FullName, "tr-TR");
            var lines = RunChild("tr-TR", "export", written, "-1799", "iss799", "imager", "irspec", "lidar", "799", "site799");
            Assert.Equal("U+0130", lines["CULTURE"].Split(' ')[4]);
            Assert.Equal("OK", lines["EXPORT"]);
            var export = Path.Combine(written, CultureKernelSet.ExportName);

            // The kernels of the export are the bytes of the invariant culture
            var kernels = new Dictionary<string, string>
            {
                ["spacecraft.tf"] = Path.Combine("Spacecrafts", "iss799", "Frames", "iss799.tf"),
                ["circular.tf"] = Path.Combine("Spacecrafts", "iss799", "Instruments", "imager.tf"),
                ["circular.ti"] = Path.Combine("Spacecrafts", "iss799", "Instruments", "imager.ti"),
                ["rectangular.tf"] = Path.Combine("Spacecrafts", "iss799", "Instruments", "irspec.tf"),
                ["rectangular.ti"] = Path.Combine("Spacecrafts", "iss799", "Instruments", "irspec.ti"),
                ["elliptical.tf"] = Path.Combine("Spacecrafts", "iss799", "Instruments", "lidar.tf"),
                ["elliptical.ti"] = Path.Combine("Spacecrafts", "iss799", "Instruments", "lidar.ti"),
                ["site.tf"] = Path.Combine("Sites", "site799", "site799.tf")
            };
            foreach (var (file, exported) in kernels)
            {
                Assert.True(File.ReadAllBytes(Path.Combine(reference.FullName, file)).SequenceEqual(File.ReadAllBytes(Path.Combine(export, exported))),
                    $"{exported} under tr-TR differs from the invariant culture:{Environment.NewLine}{File.ReadAllText(Path.Combine(export, exported))}");
            }

            // The catalog names the bodies and frames its kernels define
            string frameKernel = File.ReadAllText(Path.Combine(export, kernels["spacecraft.tf"]));
            using var spacecrafts = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(export, "spacecrafts.json")));
            var item = spacecrafts.RootElement.GetProperty("items")[0];
            Assert.Equal("ISS799", item.GetProperty("name").GetString());
            Assert.Equal("ISS799", item.GetProperty("trajectory").GetProperty("target").GetString());
            Assert.Equal("ISS799_FRAME", item.GetProperty("bodyFrame").GetProperty("name").GetString());
            Assert.Equal("Earth", item.GetProperty("center").GetString());
            Assert.Contains("NAIF_BODY_NAME              += 'ISS799'", frameKernel);
            Assert.Contains("FRAME_-1799000_NAME      = 'ISS799_FRAME'", frameKernel);

            using var sensors = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(export, "sensors.json")));
            var sensorItems = sensors.RootElement.GetProperty("items");
            string[] instruments = ["imager", "irspec", "lidar"];
            for (int i = 0; i < instruments.Length; i++)
            {
                string expected = "ISS799_" + instruments[i].ToUpperInvariant();
                Assert.Equal(expected, sensorItems[i].GetProperty("name").GetString());
                Assert.Equal(expected, sensorItems[i].GetProperty("geometry").GetProperty("instrName").GetString());
                Assert.Equal("ISS799", sensorItems[i].GetProperty("center").GetString());
                Assert.Equal("ISS799", sensorItems[i].GetProperty("parent").GetString());
                Assert.Equal("Earth", sensorItems[i].GetProperty("geometry").GetProperty("target").GetString());
                Assert.Contains($"NAIF_BODY_NAME              += '{expected}'",
                    File.ReadAllText(Path.Combine(export, "Spacecrafts", "iss799", "Instruments", instruments[i] + ".tf")));
            }

            // The site on Jupiter: Cosmographia's "Jupiter", not "Jupıter"
            Assert.True(File.Exists(Path.Combine(export, "jupiter-features.json")),
                $"The export holds {string.Join(", ", Directory.GetFiles(export, "*-features.json").Select(Path.GetFileName))}");
            using var features = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(export, "jupiter-features.json")));
            Assert.Equal("Jupiter", features.RootElement.GetProperty("items")[0].GetProperty("body").GetString());
            Assert.Equal("Jupiter surface sites", features.RootElement.GetProperty("items")[0].GetProperty("name").GetString());
            using var loader = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(export, CultureKernelSet.ExportName + ".json")));
            Assert.Contains("jupiter-features.json", loader.RootElement.GetProperty("require").EnumerateArray().Select(x => x.GetString()));
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
