using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The constructors of <see cref="Spacecraft"/> (both), <see cref="FuelTank"/>, <see cref="Engine"/> and
/// <see cref="Payload"/> take finite values in their ranges, and throw <see cref="ArgumentOutOfRangeException"/> naming the
/// parameter otherwise (#800, decision 75 of #668, decided by S. Guillet, 2026-11-23): NaN, of either sign, and the
/// infinities passed the comparisons, the kernel-backed constructor checked no range, and some rejections named no
/// parameter.
/// </summary>
public class InputRangeTests
{
    // A NaN whose sign bit is clear: ThrowIfNegative and ThrowIfNegativeOrZero rejected double.NaN only through its sign bit
    private static readonly double PositiveNaN = BitConverter.Int64BitsToDouble(0x7FF8000000000000L);

    public InputRangeTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    public static TheoryData<string, double> SpacecraftValues()
    {
        var data = new TheoryData<string, double>();
        foreach (var bad in new[] { double.NaN, PositiveNaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            data.Add("mass", bad);
            data.Add("maxOperatingMass", bad);
            data.Add("sectionalArea", bad);
            data.Add("dragCoefficient", bad);
            data.Add("solarRadiationCoefficient", bad);
        }

        data.Add("mass", -1.0);
        data.Add("maxOperatingMass", 99.0);
        data.Add("sectionalArea", 0.0);
        data.Add("sectionalArea", -1.0);
        data.Add("dragCoefficient", -1.0);
        data.Add("solarRadiationCoefficient", -1.0);
        return data;
    }

    private static StateVector Leo() => new(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.0, 0.0),
        new CelestialBody(PlanetsAndMoons.EARTH), TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);

    // mass 100 and a maximum of 1000 unless the value under test replaces one of them
    private static (double mass, double max, double area, double cd, double cr) Arguments(string parameter, double value) =>
    (
        parameter == "mass" ? value : 100.0,
        parameter == "maxOperatingMass" ? value : 1000.0,
        parameter == "sectionalArea" ? value : 1.0,
        parameter == "dragCoefficient" ? value : 2.2,
        parameter == "solarRadiationCoefficient" ? value : 1.0
    );

    [Theory]
    [MemberData(nameof(SpacecraftValues))]
    public void PropagatedSpacecraft_RejectsAValueNotFiniteOrOutOfRange(string parameter, double value)
    {
        var (mass, max, area, cd, cr) = Arguments(parameter, value);

        var exception = Record.Exception(() => new Spacecraft(-1800, "SC1800", mass, Leo(), max, new Clock("CLK1800", 65536),
            sectionalArea: area, dragCoefficient: cd, solarRadiationCoefficient: cr));

        var rejected = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal(parameter, rejected.ParamName);
    }

    /// <summary>
    /// The kernel-backed constructor runs the same checks, before it reads the kernels: -1801 has no SPK, and its read threw
    /// <see cref="SpiceException"/> before any argument was looked at.
    /// </summary>
    [Theory]
    [MemberData(nameof(SpacecraftValues))]
    public void KernelBackedSpacecraft_RejectsAValueNotFiniteOrOutOfRange_BeforeReadingTheKernels(string parameter, double value)
    {
        var (mass, max, area, cd, cr) = Arguments(parameter, value);

        var exception = Record.Exception(() => new Spacecraft(-1801, "SC1801", TimeSystem.Time.J2000TDB, mass, max,
            sectionalArea: area, dragCoefficient: cd, solarRadiationCoefficient: cr));

        var rejected = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal(parameter, rejected.ParamName);
    }

    [Fact]
    public void KernelBackedSpacecraft_RejectsAHardBodyRadius_BeforeReadingTheKernels()
    {
        var exception = Record.Exception(() => new Spacecraft(-1802, "SC1802", TimeSystem.Time.J2000TDB, hardBodyRadius: double.NaN));

        var rejected = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal("hardBodyRadius", rejected.ParamName);
    }

    [Fact]
    public void Spacecraft_TakesTheBoundsOfTheRanges()
    {
        var spacecraft = new Spacecraft(-1803, "SC1803", 0.0, Leo(), 0.0, new Clock("CLK1803", 65536), sectionalArea: double.Epsilon,
            dragCoefficient: 0.0, solarRadiationCoefficient: 0.0);

        Assert.Equal(0.0, spacecraft.Mass);
        Assert.Equal(0.0, spacecraft.MaxOperatingMass);
        Assert.Equal(double.Epsilon, spacecraft.SectionalArea);
        Assert.Equal(0.0, spacecraft.DragCoefficient);
        Assert.Equal(0.0, spacecraft.SolarRadiationCoeff);
    }

    [Theory]
    [InlineData(double.NaN, 1.0, "capacity")]
    [InlineData(0x7FF8000000000000L, 1.0, "capacity")]
    [InlineData(double.PositiveInfinity, 1.0, "capacity")]
    [InlineData(double.NegativeInfinity, 1.0, "capacity")]
    [InlineData(0.0, 1.0, "capacity")]
    [InlineData(-1.0, 1.0, "capacity")]
    [InlineData(double.NaN, double.NaN, "capacity")]
    [InlineData(100.0, double.NaN, "initialQuantity")]
    [InlineData(100.0, 0x7FF8000000000000L, "initialQuantity")]
    [InlineData(100.0, double.PositiveInfinity, "initialQuantity")]
    [InlineData(100.0, double.NegativeInfinity, "initialQuantity")]
    [InlineData(100.0, -1e-300, "initialQuantity")]
    [InlineData(100.0, 100.5, "initialQuantity")]
    public void FuelTank_RejectsAValueNotFiniteOrOutOfRange(object capacity, object initialQuantity, string parameter)
    {
        var exception = Record.Exception(() => new FuelTank("T", AsDouble(capacity), AsDouble(initialQuantity), "M", "S"));

        var rejected = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal(parameter, rejected.ParamName);
    }

    [Theory]
    [InlineData(double.NaN, 1.0, "isp")]
    [InlineData(0x7FF8000000000000L, 1.0, "isp")]
    [InlineData(double.PositiveInfinity, 1.0, "isp")]
    [InlineData(double.NegativeInfinity, 1.0, "isp")]
    [InlineData(0.0, 1.0, "isp")]
    [InlineData(-300.0, 1.0, "isp")]
    [InlineData(300.0, double.NaN, "fuelFlow")]
    [InlineData(300.0, 0x7FF8000000000000L, "fuelFlow")]
    [InlineData(300.0, double.PositiveInfinity, "fuelFlow")]
    [InlineData(300.0, double.NegativeInfinity, "fuelFlow")]
    [InlineData(300.0, 0.0, "fuelFlow")]
    [InlineData(300.0, -1.0, "fuelFlow")]
    public void Engine_RejectsAValueNotFiniteOrOutOfRange(object isp, object fuelFlow, string parameter)
    {
        var tank = new FuelTank("T", 100.0, 10.0, "M", "S");

        var exception = Record.Exception(() => new Engine("E", AsDouble(isp), AsDouble(fuelFlow), tank, "M", "S"));

        var rejected = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal(parameter, rejected.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(0x7FF8000000000000L)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void Payload_RejectsAMassNotFiniteOrNotPositive(object mass)
    {
        var exception = Record.Exception(() => new Payload("P", AsDouble(mass), "S"));

        var rejected = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal("mass", rejected.ParamName);
    }

    /// <summary>
    /// The range of instrument ids the message offers at the bottom of the NAIF id range: the frame id of -2147483 is
    /// -2147483000, and the bound NaifId × 1000 − 999 is below int.MinValue. Computed in int, the message read
    /// "[2147483297, -2147483001]".
    /// </summary>
    [Fact]
    public void InstrumentAtTheFrameId_OffersARangeWithoutOverflow()
    {
        var spacecraft = new Spacecraft(-2147483, "SC2147483", 100.0, Leo(), 1000.0, new Clock("CLK2147483", 65536));

        var exception = Record.Exception(() => spacecraft.AddCircularInstrument("CAM", 0.1, Vector3.VectorZ,
            Vector3.VectorX, Vector3.Zero, naifId: -2147483000, model: "M"));

        var rejected = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal("naifId", rejected.ParamName);
        Assert.Contains("Take an id in [-2147483648, -2147483001].", rejected.Message);
    }

    // xUnit gives a long for a hexadecimal literal: the bits of a double
    private static double AsDouble(object value) => value is long bits ? BitConverter.Int64BitsToDouble(bits) : (double)value;
}
