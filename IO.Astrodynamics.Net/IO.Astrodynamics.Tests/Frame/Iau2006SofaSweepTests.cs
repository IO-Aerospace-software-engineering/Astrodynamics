using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using TimeSystem_Time = IO.Astrodynamics.TimeSystem.Time;

namespace IO.Astrodynamics.Tests.Frame;

/// <summary>
/// Compares the IAU 2006 / 2000B frame chain with SOFA on 300 epochs from 1990 to 2040.
/// Reference: Data/SofaReference/iau2006_sweep.json, produced by <c>tools/sofa_reference/sofa_ref.c --sweep</c>
/// (SOFA iauXy06 and iauS06, IAU 2006/2000A; iauEra00; iauC2tcio without polar motion; UT1 = UTC).
/// Each test reports the largest deviation it measured, so that the published accuracy figures can be
/// traced to a test run.
/// </summary>
public class Iau2006SofaSweepTests
{
    private const string SweepPath = "Data/SofaReference/iau2006_sweep.json";

    private readonly ITestOutputHelper _output;

    public Iau2006SofaSweepTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private sealed record SweepEpoch(DateTime Utc, double T, double X, double Y, double S, double Era, double[] GcrsToTirs);

    private static readonly Lazy<IReadOnlyList<SweepEpoch>> Sweep = new(LoadSweep);

    private static IReadOnlyList<SweepEpoch> LoadSweep()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(SweepPath));
        var epochs = new List<SweepEpoch>();
        foreach (var e in document.RootElement.EnumerateArray())
        {
            var utc = DateTime.ParseExact(e.GetProperty("utc").GetString()!, "yyyy-MM-dd'T'HH:mm:ss.fffffff",
                CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            var matrix = new double[9];
            int i = 0;
            foreach (var value in e.GetProperty("gcrs_to_tirs").EnumerateArray())
            {
                matrix[i++] = value.GetDouble();
            }

            epochs.Add(new SweepEpoch(utc, e.GetProperty("t_tt").GetDouble(), e.GetProperty("cip_x").GetDouble(),
                e.GetProperty("cip_y").GetDouble(), e.GetProperty("cio_s").GetDouble(), e.GetProperty("era").GetDouble(), matrix));
        }

        return epochs;
    }

    private static double AngleDifference(double a, double b)
    {
        double d = (a - b) % (2.0 * System.Math.PI);
        if (d > System.Math.PI) d -= 2.0 * System.Math.PI;
        if (d < -System.Math.PI) d += 2.0 * System.Math.PI;
        return System.Math.Abs(d);
    }

    [Fact]
    public void SweepCoversNineteenNinetyToTwentyForty()
    {
        Assert.Equal(300, Sweep.Value.Count);
        Assert.Equal(1990, Sweep.Value[0].Utc.Year);
        Assert.Equal(2039, Sweep.Value[^1].Utc.Year);
    }

    [Fact]
    public void CipXyMatchesSofa()
    {
        double worstX = 0.0, worstY = 0.0;
        foreach (var epoch in Sweep.Value)
        {
            var (x, y) = Iau2006Model.CipXY(epoch.T);
            worstX = System.Math.Max(worstX, System.Math.Abs(x - epoch.X));
            worstY = System.Math.Max(worstY, System.Math.Abs(y - epoch.Y));
        }

        _output.WriteLine($"CIP X: max |IO - SOFA| = {worstX:E3} rad; CIP Y: {worstY:E3} rad");
        Assert.True(worstX < CipTolerance, $"CIP X deviates by {worstX:E3} rad");
        Assert.True(worstY < CipTolerance, $"CIP Y deviates by {worstY:E3} rad");
    }

    [Fact]
    public void CioLocatorMatchesSofa()
    {
        // Given SOFA's X, Y, the CIO locator must reproduce iauS06: same series, same arguments.
        double worst = 0.0;
        foreach (var epoch in Sweep.Value)
        {
            double s = Iau2006Model.CioLocator(epoch.T, epoch.X, epoch.Y);
            worst = System.Math.Max(worst, System.Math.Abs(s - epoch.S));
        }

        _output.WriteLine($"CIO locator s: max |IO - SOFA| = {worst:E3} rad");
        Assert.True(worst < CioLocatorTolerance, $"CIO locator deviates by {worst:E3} rad");
    }

    [Fact]
    public void EarthRotationAngleMatchesSofa()
    {
        double worst = 0.0;
        foreach (var epoch in Sweep.Value)
        {
            // Two-part Julian date, as TirsFrame does: a single double would only resolve ~40 us.
            var utc = new TimeSystem_Time(epoch.Utc, TimeFrame.UTCFrame);
            double era = Iau2006Model.EarthRotationAngle(TimeSystem_Time.JULIAN_J2000, utc.DaysFromJ2000());
            worst = System.Math.Max(worst, AngleDifference(era, epoch.Era));
        }

        _output.WriteLine($"ERA: max |IO - SOFA| = {worst:E3} rad");
        Assert.True(worst < EraTolerance, $"ERA deviates by {worst:E3} rad");
    }

    [Fact]
    public void GcrsToTirsMatchesSofa()
    {
        double worstElement = 0.0, worstAngle = 0.0;
        var tirs = new TirsFrame();
        foreach (var epoch in Sweep.Value)
        {
            var utc = new TimeSystem_Time(epoch.Utc, TimeFrame.UTCFrame);
            var tirsToIcrf = Matrix.FromQuaternion(tirs.GetStateOrientationToICRF(utc).Rotation);

            // SOFA gives GCRS -> TIRS, which is the transpose of TIRS -> ICRF (pivot = GCRS axes).
            var sofa = new Matrix(3, 3);
            for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                sofa.Set(i, j, epoch.GcrsToTirs[3 * i + j]);

            var gcrsToTirs = tirsToIcrf.Transpose();
            for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                worstElement = System.Math.Max(worstElement, System.Math.Abs(gcrsToTirs.Get(i, j) - sofa.Get(i, j)));

            // Angle of the residual rotation between the two matrices, from its skew-symmetric part
            // (sin) and its trace (cos), which keeps full precision for small angles.
            var residual = gcrsToTirs.Multiply(sofa.Transpose());
            double trace = residual.Get(0, 0) + residual.Get(1, 1) + residual.Get(2, 2);
            var axis = new Vector3(residual.Get(2, 1) - residual.Get(1, 2), residual.Get(0, 2) - residual.Get(2, 0),
                residual.Get(1, 0) - residual.Get(0, 1));
            double angle = System.Math.Atan2(axis.Magnitude() / 2.0, (trace - 1.0) / 2.0);
            worstAngle = System.Math.Max(worstAngle, angle);
        }

        _output.WriteLine($"GCRS->TIRS: max element deviation = {worstElement:E3}, max residual rotation = {worstAngle:E3} rad");
        Assert.True(worstAngle < TirsTolerance, $"GCRS->TIRS deviates by {worstAngle:E3} rad");
    }

    // Tolerances, with the maximum deviation measured on 2026-10-04 over the 300 epochs:
    // - CIP X, Y: IAU 2000B nutation is specified at 1 mas (4.8e-9 rad) against IAU 2000A; measured 3.55e-9.
    private const double CipTolerance = 5e-9;

    // - CIO locator s, given SOFA's X, Y: same series and arguments as iauS06; measured 9.3e-23.
    private const double CioLocatorTolerance = 1e-11;

    // - ERA: double resolution of the two-part date (about 2e-12 day, i.e. 1.1e-11 rad); measured 1.3e-11.
    private const double EraTolerance = 2e-11;

    // - GCRS to TIRS: inherits the CIP deviation; measured 4.23e-9 rad.
    private const double TirsTolerance = 5e-9;
}
