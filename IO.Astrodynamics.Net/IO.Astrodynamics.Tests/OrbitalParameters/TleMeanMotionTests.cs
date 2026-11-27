// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// The mean motion of a TLE (decision 111 of #668, decided by S. Guillet, 2026-11-26): <see cref="Tle.MeanMeanMotion"/> is
/// the field of line 2 (columns 53-63, rev/day) in rad/s, read without kernels, and <see cref="Tle.ToOmm"/> writes the
/// field's own value. The expected values are the field itself and the TLE format's closed form, n = field · 2π / 86400.
/// The sets are those of <c>Data/Sgp4Reference</c> (Vallado's verification set and the instants of
/// <c>tools/sgp4_reference</c>).
/// </summary>
public class TleMeanMotionTests
{
    private readonly ITestOutputHelper _output;

    public TleMeanMotionTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private static readonly Lazy<(string Line1, string Line2)[]> Sets = new(() =>
    {
        var root = JsonDocument.Parse(File.ReadAllText("Data/Sgp4Reference/sgp4_reference.json")).RootElement;
        return root.GetProperty("verification").EnumerateArray().Concat(root.GetProperty("instants").EnumerateArray())
            .Select(x => (x.GetProperty("line1").GetString(), x.GetProperty("line2").GetString())).ToArray();
    });

    private static string Field(string line2) => line2.Substring(52, 11).Trim();

    [Fact]
    public void MeanMeanMotion_IsTheFieldInRadiansPerSecond()
    {
        Assert.True(Sets.Value.Length >= 30);
        foreach (var (line1, line2) in Sets.Value)
        {
            var tle = new Tle("SET", line1, line2);
            double expected = double.Parse(Field(line2), CultureInfo.InvariantCulture) * IO.Astrodynamics.Constants.TwoPi / 86400.0;
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(tle.MeanMeanMotion));
        }
    }

    [Fact]
    public void ToOmm_WritesTheFieldsMeanMotion()
    {
        // Before, MEAN_MOTION was √(GM/a³)·86400/2π, a = ∛(GM/n²) of the kernel GM: 1 ulp off the field on about half the
        // sets, e.g. 15.957445309999999 for 15.95744531
        int moved = 0;
        long worstUlps = 0, worstRadUlps = 0;
        foreach (var (line1, line2) in Sets.Value)
        {
            var tle = new Tle("SET", line1, line2);
            var omm = tle.ToOmm();
            double field = double.Parse(Field(line2), CultureInfo.InvariantCulture);
            Assert.Equal(BitConverter.DoubleToInt64Bits(field), BitConverter.DoubleToInt64Bits(omm.Data.MeanElements.MeanMotion!.Value));
            var text = XDocument.Parse(omm.ToXml()).Descendants("MEAN_MOTION").Single().Value;
            Assert.Equal(field, double.Parse(text, CultureInfo.InvariantCulture));

            double former = System.Math.Sqrt(tle.Observer.GM / (tle.MeanSemiMajorAxis * tle.MeanSemiMajorAxis * tle.MeanSemiMajorAxis)) * 86400.0 / IO.Astrodynamics.Constants.TwoPi;
            if (former != field) moved++;
            worstUlps = System.Math.Max(worstUlps, System.Math.Abs(BitConverter.DoubleToInt64Bits(former) - BitConverter.DoubleToInt64Bits(field)));
            // The former MeanMotion() (√(GM/a³), TLE.cs of 10.x) against MeanMeanMotion, in rad/s
            double formerRad = System.Math.Sqrt(tle.Observer.GM / (tle.MeanSemiMajorAxis * tle.MeanSemiMajorAxis * tle.MeanSemiMajorAxis));
            worstRadUlps = System.Math.Max(worstRadUlps,
                System.Math.Abs(BitConverter.DoubleToInt64Bits(formerRad) - BitConverter.DoubleToInt64Bits(tle.MeanMeanMotion)));
        }

        _output.WriteLine($"MEASURE MEAN_MOTION moved on {moved} of {Sets.Value.Length} sets, by {worstUlps} ulp at most; " +
                          $"the former MeanMotion() is {worstRadUlps} ulp at most from MeanMeanMotion");
        Assert.True(moved > 0, "no set whose former MEAN_MOTION differed from its field");
    }

    [Fact]
    public void MeanSemiMajorAxis_IsTheCubeRootOfTheKernelGmOverNSquared()
    {
        // The former value, bit for bit: a = ∛(GM/n²), GM the kernel GM of the Earth (BODY399_GM), n the field in rad/s
        var earth = new CelestialBody(399);
        foreach (var (line1, line2) in Sets.Value)
        {
            var tle = new Tle("SET", line1, line2);
            double n = double.Parse(Field(line2), CultureInfo.InvariantCulture) * IO.Astrodynamics.Constants.TwoPi / 86400.0;
            Assert.Equal(BitConverter.DoubleToInt64Bits(System.Math.Cbrt(earth.GM / (n * n))), BitConverter.DoubleToInt64Bits(tle.MeanSemiMajorAxis));
        }
    }

    [Fact]
    public void MeanMotionMethod_IsRemoved()
    {
        // Decision 111 of #668 and the amendment of 2026-11-19 (no forwarders): tle.MeanMotion() now binds to the osculating
        // OrbitalParameters.MeanMotion(double), as for any orbit
        var declared = typeof(Tle).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == "MeanMotion");
        Assert.Empty(declared);
    }
}
