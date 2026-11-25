// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// The instruments of a spacecraft, lot L5b of #668 (R10, decisions 72 to 74, decided by S. Guillet, 2026-11-23): one
/// <c>Add*Instrument</c> per shape, which returns the instrument it adds; the NAIF id optional, by default the smallest
/// free NaifId × 1000 − n, n in [1, 999], and required on a kernel-backed spacecraft; the model optional; an instrument
/// name unique on its invariant upper-cased form; the checks run before the instrument is built; the constructor of
/// <see cref="Instrument"/> <c>private protected</c>, so that <c>Add*Instrument</c> is the only way to build one.
/// </summary>
/// <remarks>
/// The references are the decided rules (NAIF IDs Required Reading, "Instruments": NaifId × 1000 − n) and the explicit
/// forms, which the defaults equal byte for byte in the kernels written.
/// </remarks>
public class InstrumentAdditionTests
{
    public InstrumentAdditionTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static readonly Time S = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);
    private static int _nextId = -182000;
    private static int NextId() => Interlocked.Decrement(ref _nextId);

    private static Spacecraft NewSpacecraft(int naifId) => new(naifId, "Sat" + -naifId, 1000.0,
        new KeplerianElements(7000e3, 0.01, 0.5, 0.0, 0.0, 0.0, new CelestialBody(PlanetsAndMoons.EARTH), S, Frames.Frame.ICRF));

    private static Spacecraft NewSpacecraft() => NewSpacecraft(NextId());

    private static Instrument Add(Spacecraft spacecraft, string shape, string name, int? naifId = null, string model = null) =>
        shape switch
        {
            "Circular" => spacecraft.AddCircularInstrument(name, 0.1, Vector3.VectorZ, Vector3.VectorX, naifId: naifId, model: model),
            "Rectangular" => spacecraft.AddRectangularInstrument(name, 0.1, 0.05, Vector3.VectorZ, Vector3.VectorX, naifId: naifId, model: model),
            _ => spacecraft.AddEllipticalInstrument(name, 0.1, 0.05, Vector3.VectorZ, Vector3.VectorX, naifId: naifId, model: model)
        };

    public static TheoryData<string> Shapes() => new() { "Circular", "Rectangular", "Elliptical" };

    // ---------------------------------------------------------------------------------------------------------------------
    // One Add*Instrument per shape, returning its instrument (decision 72)
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void AddInstrument_ReturnsTheInstrumentOfItsShape()
    {
        var spacecraft = NewSpacecraft();

        CircularInstrument circle = spacecraft.AddCircularInstrument("Cam", 0.1, Vector3.VectorZ, Vector3.VectorX);
        RectangularInstrument rectangle = spacecraft.AddRectangularInstrument("Imager", 0.1, 0.05, Vector3.VectorZ, Vector3.VectorX);
        EllipticalInstrument ellipse = spacecraft.AddEllipticalInstrument("Radar", 0.1, 0.05, Vector3.VectorZ, Vector3.VectorX,
            new Vector3(0.1, 0.0, 0.0));

        Assert.Equal(new Instrument[] { circle, rectangle, ellipse }.OrderBy(i => i.Name), spacecraft.Instruments.OrderBy(i => i.Name));
        Assert.All(spacecraft.Instruments, i => Assert.Same(spacecraft, i.Spacecraft));
        Assert.Equal(Vector3.Zero, circle.Orientation);
        Assert.Equal(new Vector3(0.1, 0.0, 0.0), ellipse.Orientation);
        Assert.Equal(0.05, rectangle.CrossAngle);
        Assert.Null(circle.Model);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Model_IsOptional(string shape)
    {
        var spacecraft = NewSpacecraft();

        Assert.Null(Add(spacecraft, shape, "A").Model);
        Assert.Equal("Imager", Add(spacecraft, shape, "B", model: "Imager").Model);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The default NAIF id (decision 72)
    // ---------------------------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Shapes))]
    public void DefaultId_IsTheSmallestFreeNOfNaifIdTimes1000MinusN(string shape)
    {
        int id = NextId();
        var spacecraft = NewSpacecraft(id);

        Assert.Equal(id * 1000 - 1, Add(spacecraft, shape, "A").NaifId);
        Assert.Equal(id * 1000 - 3, Add(spacecraft, shape, "B", id * 1000 - 3).NaifId);
        Assert.Equal(id * 1000 - 2, Add(spacecraft, shape, "C").NaifId);
        Assert.Equal(id * 1000 - 4, Add(spacecraft, shape, "D").NaifId);
        // An explicit id outside the convention is accepted (mission kernels use them)
        Assert.Equal(-77, Add(spacecraft, shape, "E", -77).NaifId);
        Assert.Equal(id * 1000 - 5, Add(spacecraft, shape, "F").NaifId);
    }

    [Fact]
    public void DefaultId_ThrowsWhenNoneIsLeft()
    {
        int id = NextId();
        var spacecraft = NewSpacecraft(id);
        for (int n = 1; n <= 999; n++)
        {
            spacecraft.AddCircularInstrument("I" + n, 0.1, Vector3.VectorZ, Vector3.VectorX);
        }

        Assert.Equal(Enumerable.Range(1, 999).Select(n => id * 1000 - n).Order(), spacecraft.Instruments.Select(i => i.NaifId).Order());
        var exception = Assert.Throws<InvalidOperationException>(() =>
            spacecraft.AddCircularInstrument("I1000", 0.1, Vector3.VectorZ, Vector3.VectorX));
        Assert.Contains("naifId", exception.Message);
        Assert.Equal(999, spacecraft.Instruments.Count);

        // An explicit id still goes in
        Assert.Equal(-77, spacecraft.AddCircularInstrument("I1000", 0.1, Vector3.VectorZ, Vector3.VectorX, naifId: -77).NaifId);
    }

    /// <summary>
    /// At the smallest spacecraft id, -2147483, NaifId × 1000 − n is an int down to n = 648 only.
    /// </summary>
    [Fact]
    public void DefaultId_StaysAnInt()
    {
        var spacecraft = NewSpacecraft(Spacecraft.MinimumNaifId);
        for (int n = 1; n <= 648; n++)
        {
            spacecraft.AddCircularInstrument("I" + n, 0.1, Vector3.VectorZ, Vector3.VectorX);
        }

        Assert.Equal(int.MinValue, spacecraft.Instruments.Min(i => i.NaifId));
        Assert.Throws<InvalidOperationException>(() => spacecraft.AddCircularInstrument("I649", 0.1, Vector3.VectorZ, Vector3.VectorX));
        Assert.Equal(648, spacecraft.Instruments.Count);
    }

    /// <summary>
    /// The default id writes the kernels of the explicit id NaifId × 1000 − 1, byte for byte.
    /// </summary>
    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task DefaultId_WritesTheKernelsOfTheExplicitId(string shape)
    {
        int id = NextId();
        var byDefault = Add(NewSpacecraft(id), shape, "Cam");
        var explicitId = Add(NewSpacecraft(id), shape, "Cam", id * 1000 - 1);

        var directory = Directory.CreateTempSubdirectory("l5b-instrument-");
        try
        {
            string Path(string name) => System.IO.Path.Combine(directory.FullName, name);
            await byDefault.WriteFrameAsync(new FileInfo(Path("default.tf")));
            await explicitId.WriteFrameAsync(new FileInfo(Path("explicit.tf")));
            await byDefault.WriteKernelAsync(new FileInfo(Path("default.ti")));
            await explicitId.WriteKernelAsync(new FileInfo(Path("explicit.ti")));

            Assert.Equal(File.ReadAllBytes(Path("explicit.tf")), File.ReadAllBytes(Path("default.tf")));
            Assert.Equal(File.ReadAllBytes(Path("explicit.ti")), File.ReadAllBytes(Path("default.ti")));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The checks before the instrument is built (decisions 72 to 74)
    // ---------------------------------------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Shapes))]
    public void DuplicateId_ThrowsNamingNaifId(string shape)
    {
        int id = NextId();
        var spacecraft = NewSpacecraft(id);
        Add(spacecraft, shape, "A");

        var exception = Assert.Throws<ArgumentException>(() => Add(spacecraft, shape, "B", id * 1000 - 1));

        Assert.Equal("naifId", exception.ParamName);
        Assert.Single(spacecraft.Instruments);
    }

    /// <summary>
    /// Two names of one invariant upper-cased form write the same kernel variable FRAME_&lt;SC&gt;_&lt;INSTRUMENT&gt; and the
    /// same file on a case-insensitive file system (decision 74).
    /// </summary>
    [Theory]
    [MemberData(nameof(Shapes))]
    public void NameOfTheSameUpperCasedForm_ThrowsNamingName(string shape)
    {
        var spacecraft = NewSpacecraft();
        Add(spacecraft, shape, "Cam");

        var exception = Assert.Throws<ArgumentException>(() => Add(spacecraft, shape, "CAM"));

        Assert.Equal("name", exception.ParamName);
        Assert.Contains("FRAME_", exception.Message);
        Assert.Single(spacecraft.Instruments);
        // A name of another form goes in
        Add(spacecraft, shape, "Cam2");
        Assert.Equal(2, spacecraft.Instruments.Count);
    }

    /// <summary>
    /// A kernel-backed spacecraft requires the id, under which <c>gftfov</c> reads the instrument kernel of the mission
    /// (decision 73): a default id could read another instrument of that kernel.
    /// </summary>
    [Theory]
    [MemberData(nameof(Shapes))]
    public void KernelBackedSpacecraft_RequiresTheNaifId(string shape)
    {
        TestKernels.EnsureLro();
        var lro = new Spacecraft(-85, "LRO", new Time(2023, 12, 15, 0, 5, 0, TimeScale.UTC));

        var exception = Assert.Throws<ArgumentNullException>(() => Add(lro, shape, "Cam"));

        Assert.Equal("naifId", exception.ParamName);
        Assert.Empty(lro.Instruments);
        Assert.Equal(-85001, Add(lro, shape, "Cam", -85001).NaifId);
    }

    /// <summary>
    /// A rejected call adds nothing, and an instrument value the constructor rejects throws as before (#707, #715).
    /// </summary>
    [Fact]
    public void RejectedCall_AddsNothing()
    {
        var spacecraft = NewSpacecraft();

        Assert.Throws<ArgumentException>(() => spacecraft.AddCircularInstrument("Cam", double.NaN, Vector3.VectorZ, Vector3.VectorX));
        Assert.Throws<ArgumentException>(() => spacecraft.AddCircularInstrument("", 0.1, Vector3.VectorZ, Vector3.VectorX));
        Assert.Throws<ArgumentOutOfRangeException>(() => spacecraft.AddRectangularInstrument("Cam", 0.1, 0.0, Vector3.VectorZ, Vector3.VectorX));
        Assert.Throws<ArgumentOutOfRangeException>(() => spacecraft.AddCircularInstrument("Cam", 0.1, Vector3.VectorZ, Vector3.VectorX,
            naifId: spacecraft.Frame.Id));

        Assert.Empty(spacecraft.Instruments);
        Assert.Equal(spacecraft.NaifId * 1000 - 1, spacecraft.AddCircularInstrument("Cam", 0.1, Vector3.VectorZ, Vector3.VectorX).NaifId);
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The hierarchy is closed (decision 72, the critic's gap 1 of L5-32)
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void InstrumentConstructor_IsPrivateProtected()
    {
        var constructors = typeof(Instrument).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotEmpty(constructors);
        Assert.All(constructors, c => Assert.True(c.IsFamilyAndAssembly, c.ToString()));
        foreach (var type in new[] { typeof(CircularInstrument), typeof(RectangularInstrument), typeof(EllipticalInstrument) })
        {
            Assert.All(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                c => Assert.False(c.IsPublic || c.IsFamily || c.IsFamilyOrAssembly, c.ToString()));
        }
    }
}
