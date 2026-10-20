using System.Linq;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Atmosphere;

/// <summary>
/// The density of NRLMSISE-00 is discontinuous at the altitudes above which the model drops the mixing contribution of
/// a species (<c>altl</c> of the model, with flag 15, as in the reference implementation of Picone et al.): the
/// behaviour of the model that the drag partials must handle (#413).
/// </summary>
public class Nrlmsise00CutoffTests
{
    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0);
    private static readonly (double Latitude, double Longitude)[] Places = { (0.25, 0.1), (-0.8, 2.0), (1.2, -2.5), (0.0, 3.0) };

    private readonly ITestOutputHelper _output;

    public Nrlmsise00CutoffTests(ITestOutputHelper output)
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        _output = output;
    }

    // The relative jump of the density from 1 mm below to 1 mm above an altitude, the worst of four places, and the
    // change of a smooth density over the same 2 mm (from its change over 1 km)
    private static (double Jump, double Smooth) Jump(Nrlmsise00Model model, double altitude)
    {
        double jump = 0.0, smooth = 0.0;
        foreach (var (latitude, longitude) in Places)
        {
            double below = model.GetDensity(AtmosphericContext.FromPlanetodetic(altitude - 1e-3, latitude, longitude, Epoch));
            double above = model.GetDensity(AtmosphericContext.FromPlanetodetic(altitude + 1e-3, latitude, longitude, Epoch));
            double far = model.GetDensity(AtmosphericContext.FromPlanetodetic(altitude + 1e3, latitude, longitude, Epoch));
            jump = System.Math.Max(jump, System.Math.Abs(above / below - 1.0));
            smooth = System.Math.Max(smooth, System.Math.Abs(far / below - 1.0) * 2e-6);
        }

        return (jump, smooth);
    }

    [Fact]
    public void Density_JumpsAtTheCutoffsOfTheMixing()
    {
        // Arrange
        var model = new Nrlmsise00Model(SpaceWeather.Nominal);

        // Act and assert: measured 2026-10-20, 1.8e-4 (160 km), 1.6e-6 (250 km), 9.0e-5 (300 km), 3.0e-5 (450 km),
        // against about 4e-8 for a smooth density; at 200, 240 and 320 km the jump is at the level of the smooth change
        foreach (double kilometres in new[] { 160.0, 250.0, 300.0, 450.0 })
        {
            var (jump, smooth) = Jump(model, kilometres * 1000.0);
            _output.WriteLine($"{kilometres} km: jump {jump:E2}, smooth change {smooth:E2}");
            Assert.True(jump > 30.0 * smooth, $"{kilometres} km: {jump:E2} against {smooth:E2}");
        }

        // Away from the cutoffs, the density is continuous: the change over 2 mm is the smooth one
        foreach (double kilometres in new[] { 350.0, 400.0, 451.0, 600.0 })
        {
            var (jump, smooth) = Jump(model, kilometres * 1000.0);
            _output.WriteLine($"{kilometres} km: change {jump:E2}, smooth change {smooth:E2}");
            Assert.True(jump < 2.0 * smooth, $"{kilometres} km: {jump:E2} against {smooth:E2}");
        }
    }

    [Fact]
    public void WithoutTheMixingSwitch_TheDensityIsContinuous()
    {
        // Arrange: flag 15 off, the model keeps the mixing contribution at every altitude
        var flags = NrlmsiseFlags.CreateStandard();
        flags.Switches[15] = 0;
        var model = new Nrlmsise00Model(flags, SpaceWeather.Nominal);

        // Act and assert
        foreach (double kilometres in new[] { 160.0, 250.0, 300.0, 450.0 })
        {
            var (jump, smooth) = Jump(model, kilometres * 1000.0);
            _output.WriteLine($"flag 15 off, {kilometres} km: change {jump:E2}, smooth change {smooth:E2}");
            Assert.True(jump < 2.0 * smooth, $"{kilometres} km: {jump:E2} against {smooth:E2}");
        }
    }

    [Fact]
    public void Discontinuities_AreTheCutoffsOfTheMixing_WithTheComparisonsOfTheModel()
    {
        // Arrange
        IDensityDiscontinuities model = new Nrlmsise00Model(SpaceWeather.Nominal);

        // Act and assert: the distinct values of altl, in m
        Assert.Equal(new[] { 160000.0, 200000.0, 240000.0, 250000.0, 300000.0, 320000.0, 450000.0 },
            model.DensityDiscontinuities.ToArray());

        // The intervals: the mixing of He holds for z < 200 km, the others for z <= altl, so 200 km exactly is above
        // its cutoff and 450 km exactly below its own
        Assert.Equal(0, model.DensityInterval(100000.0));
        Assert.Equal(0, model.DensityInterval(160000.0));
        Assert.Equal(1, model.DensityInterval(160000.001));
        Assert.Equal(1, model.DensityInterval(199999.999));
        Assert.Equal(2, model.DensityInterval(200000.0));
        Assert.Equal(6, model.DensityInterval(450000.0));
        Assert.Equal(7, model.DensityInterval(450000.001));
        Assert.Equal(7, model.DensityInterval(800000.0));

        // The jumps are where the intervals change: the density is continuous within an interval (the test above)
        foreach (double cutoff in model.DensityDiscontinuities)
        {
            Assert.NotEqual(model.DensityInterval(cutoff - 1e-3), model.DensityInterval(cutoff + 1e-3));
        }
    }

    [Fact]
    public void WithoutTheMixingSwitch_NoDiscontinuity()
    {
        // Arrange
        var flags = NrlmsiseFlags.CreateStandard();
        flags.Switches[15] = 0;
        IDensityDiscontinuities model = new Nrlmsise00Model(flags, SpaceWeather.Nominal);

        // Act and assert
        Assert.True(model.DensityDiscontinuities.IsEmpty);
        Assert.Equal(0, model.DensityInterval(160000.001));
        Assert.Equal(0, model.DensityInterval(800000.0));
    }
}
