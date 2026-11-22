// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Globalization;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using Xunit.Abstractions;
using StateOrientation = IO.Astrodynamics.OrbitalParameters.StateOrientation;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;

namespace IO.Astrodynamics.Tests.DataProvider;

/// <summary>
/// The frame and the aberration of <see cref="MemoryDataProvider.GetEphemeris"/> (#559): a read is the difference of the
/// stored states rotated into the frame asked, by the provider's orientation of it, and an aberration other than
/// <see cref="Aberration.None"/> throws <see cref="NotSupportedException"/> (decided by S. Guillet, 2026-11-17). Before, the
/// stored ICRF difference came back labelled with the frame asked, and an aberrated read was the geometric state.
/// </summary>
/// <remarks>
/// The references are SPICE's <c>spkezr_c</c> on the states the test stores, read from SPICE in J2000 at the same epochs
/// (<see cref="SpiceAPI.ReadEphemeris(TimeSystem.Time, int, int, Frames.Frame, Aberration)"/>), and the closed form of
/// ECLIPJ2000, SPICE's rotation of the J2000 obliquity, 84381.448″ about x (CSPICE <c>chgirf.c</c>). The providers are
/// called directly: the global provider is never swapped (#631).
/// </remarks>
public class MemoryDataProviderFrameTests
{
    private static readonly TimeSystem.Time Start = new(2024, 3, 1, 0, 0, 0, TimeSystem.TimeScale.TDB);

    // Stored states every 10 minutes over 80 minutes; the reads at stored epochs, so that no interpolation enters
    private static readonly TimeSystem.Time[] Epochs = Enumerable.Range(0, 9).Select(k => Start.AddSeconds(600.0 * k)).ToArray();

    // Twice the worst over Linux x64, Windows x64 and macOS arm64, rounded up at the second significant digit: the rounding
    // of the subtraction of two states of 1.5e11 m about the barycentre (an ulp of 3.1e-5 m) and of the rotation, against
    // spkezr's own rounding of the same states. Measured on the three platforms alike (CI run 44225490669): |dr| 7.831e-5 m (ECLIPJ2000 states read in ICRF; 4.78e-5 m for the others), |dv| 8.778e-12 m/s in the
    // inertial frames, 3.481e-9 m/s in ITRF93 (1.2e-13 of the 2.8e4 m/s of the Moon in it, the rounding of ω × r, ω read
    // by sxform_c and xf2rav_c), the closed form 2.152e-16 relative, sxform 1.110e-16 rad
    private const double PositionTolerance = 1.6e-4;
    private const double VelocityTolerance = 1.8e-11;
    private const double RotatingVelocityTolerance = 7.0e-9;
    private const double ClosedFormTolerance = 4.4e-16;
    private const double SxformTolerance = 2.3e-16;

    private readonly ITestOutputHelper _output;

    public MemoryDataProviderFrameTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    // The Moon and the Earth about the solar system barycentre, read from SPICE in J2000 at the epochs, stored in frame
    private static MemoryDataProvider StatesProvider(Frames.Frame storedIn)
    {
        var provider = new MemoryDataProvider();
        foreach (var epoch in Epochs)
        {
            foreach (var id in new[] { 301, 399 })
            {
                var state = SpiceAPI.Instance.ReadEphemeris(epoch, 0, id, storedIn, Aberration.None);
                provider.AddStateVector(id, epoch, new StateVector(state.Position, state.Velocity, TestHelpers.SsbAtJ2000, epoch, storedIn));
            }
        }

        return provider;
    }

    private static StateVector Spkezr(TimeSystem.Time epoch, Frames.Frame frame) =>
        SpiceAPI.Instance.ReadEphemeris(epoch, 399, 301, frame, Aberration.None);

    private void Measure(string label, MemoryDataProvider provider, Frames.Frame frame, double velocityTolerance = VelocityTolerance)
    {
        double worstPosition = 0.0, worstVelocity = 0.0;
        foreach (var epoch in Epochs)
        {
            var read = provider.GetEphemeris(epoch, PlanetsAndMoons.MOON_BODY, PlanetsAndMoons.EARTH_BODY, frame, Aberration.None);
            var reference = Spkezr(epoch, frame);
            Assert.Equal(frame, read.Frame);
            Assert.Same(frame, read.Frame);
            LabelAssert.Equal(epoch, read.Epoch);
            worstPosition = System.Math.Max(worstPosition, (read.Position - reference.Position).Magnitude());
            worstVelocity = System.Math.Max(worstVelocity, (read.Velocity - reference.Velocity).Magnitude());
        }

        _output.WriteLine(
            $"MEASURE #559 {label}: |dr| {worstPosition.ToString("E3", CultureInfo.InvariantCulture)} m, |dv| {worstVelocity.ToString("E3", CultureInfo.InvariantCulture)} m/s");
        Assert.InRange(worstPosition, 0.0, PositionTolerance);
        Assert.InRange(worstVelocity, 0.0, velocityTolerance);
    }

    [Fact]
    public void Read_InEclipJ2000_WithoutStoredOrientation_IsSpkezr()
    {
        // ECLIPJ2000, the default frame of the bodies, is known by its definition: no orientation stored
        Measure("ICRF states read in ECLIPJ2000", StatesProvider(Frames.Frame.ICRF), Frames.Frame.ECLIPTIC_J2000);
    }

    [Fact]
    public void Read_InItrf93_WithStoredOrientations_IsSpkezr()
    {
        // A rotating frame: the orientations SPICE gives at the epochs, stored; the velocity takes ω × r
        var provider = StatesProvider(Frames.Frame.ICRF);
        var itrf93 = new Frames.Frame("ITRF93");
        foreach (var epoch in Epochs)
        {
            provider.AddStateOrientationToICRF(itrf93, epoch, SpiceAPI.Instance.TransformFrame(epoch, itrf93, Frames.Frame.ICRF));
        }

        Measure("ICRF states read in ITRF93", provider, itrf93, RotatingVelocityTolerance);
    }

    [Fact]
    public void Read_InIcrf_OfStatesStoredInEclipJ2000_IsSpkezr()
    {
        // Stored states in another frame are taken in ICRF first
        Measure("ECLIPJ2000 states read in ICRF", StatesProvider(Frames.Frame.ECLIPTIC_J2000), Frames.Frame.ICRF);
    }

    [Fact]
    public void Read_InTemeByName_IsTheIcrfReadRotatedByTeme()
    {
        // A managed frame (#451, #656) needs no stored orientation either
        var provider = StatesProvider(Frames.Frame.ICRF);
        var teme = new Frames.Frame("teme");
        double worst = 0.0;
        foreach (var epoch in Epochs)
        {
            var read = provider.GetEphemeris(epoch, PlanetsAndMoons.MOON_BODY, PlanetsAndMoons.EARTH_BODY, teme, Aberration.None);
            var reference = Spkezr(epoch, Frames.Frame.TEME);
            worst = System.Math.Max(worst, (read.Position - reference.Position).Magnitude());
            Assert.InRange((read.Velocity - reference.Velocity).Magnitude(), 0.0, VelocityTolerance);
        }

        _output.WriteLine($"MEASURE #559 ICRF states read in TEME by name: |dr| {worst.ToString("E3", CultureInfo.InvariantCulture)} m");
        Assert.InRange(worst, 0.0, PositionTolerance);
    }

    [Fact]
    public void Read_InIcrfAndGcrf_IsTheStoredDifference_BitForBit()
    {
        // ICRF's axes need no rotation: the stored numbers subtracted, as before #559
        var provider = StatesProvider(Frames.Frame.ICRF);
        foreach (var frame in new[] { Frames.Frame.ICRF, Frames.Frame.GCRF, new Frames.Frame("gcrf") })
        {
            foreach (var epoch in Epochs)
            {
                var read = provider.GetEphemeris(epoch, PlanetsAndMoons.MOON_BODY, PlanetsAndMoons.EARTH_BODY, frame, Aberration.None);
                var moon = SpiceAPI.Instance.ReadEphemeris(epoch, 0, 301, Frames.Frame.ICRF, Aberration.None);
                var earth = SpiceAPI.Instance.ReadEphemeris(epoch, 0, 399, Frames.Frame.ICRF, Aberration.None);
                Assert.Equal(moon.Position - earth.Position, read.Position);
                Assert.Equal(moon.Velocity - earth.Velocity, read.Velocity);
                Assert.Same(frame, read.Frame);
            }
        }
    }

    [Fact]
    public void Read_TheIssuesBarycentreInEclipJ2000_IsTheClosedForm()
    {
        // The probe of #559: one state of the Earth-Moon barycentre at J2000 TDB in ICRF, read in ECLIPJ2000. The closed form:
        // J2000 -> ECLIPJ2000 is SPICE's ROTATE(84381.448", 1), y' = c y + s z, z' = -s y + c z
        var provider = new MemoryDataProvider();
        var epoch = TimeSystem.Time.J2000TDB;
        var r = new Vector3(-2.6e10, 1.33e11, 5.76e10);
        var v = new Vector3(-29800.0, -5000.0, -2200.0);
        provider.AddStateVector(3, epoch, new StateVector(r, v, TestHelpers.SsbAtJ2000, epoch, Frames.Frame.ICRF));
        var barycentre = new Barycenter(3);

        var read = provider.GetEphemeris(epoch, barycentre, TestHelpers.SsbAtJ2000, Frames.Frame.ECLIPTIC_J2000, Aberration.None);

        double eps = 84381.448 / 3600.0 * System.Math.PI / 180.0;
        double c = System.Math.Cos(eps), s = System.Math.Sin(eps);
        var expectedR = new Vector3(r.X, c * r.Y + s * r.Z, -s * r.Y + c * r.Z);
        var expectedV = new Vector3(v.X, c * v.Y + s * v.Z, -s * v.Y + c * v.Z);
        double relativeR = (read.Position - expectedR).Magnitude() / r.Magnitude();
        double relativeV = (read.Velocity - expectedV).Magnitude() / v.Magnitude();
        _output.WriteLine(
            $"MEASURE #559 barycentre in ECLIPJ2000 against the closed form: {relativeR.ToString("E3", CultureInfo.InvariantCulture)}, {relativeV.ToString("E3", CultureInfo.InvariantCulture)} relative");
        Assert.InRange(relativeR, 0.0, ClosedFormTolerance);
        Assert.InRange(relativeV, 0.0, ClosedFormTolerance);
        // The issue's figures, by hand: z = -5.739496e7 m where the read gave 5.76e10 m
        Assert.InRange(read.Position.Z, -5.73950e7, -5.73949e7);
    }

    [Fact]
    public void FrameTransformation_EclipJ2000_IsSpicesWithoutStoredOrientation()
    {
        var provider = new MemoryDataProvider();
        var orientation = provider.FrameTransformationToICRF(Start, Frames.Frame.ECLIPTIC_J2000);
        var spice = SpiceAPI.Instance.TransformFrame(Start, Frames.Frame.ECLIPTIC_J2000, Frames.Frame.ICRF);
        var delta = orientation.Rotation * spice.Rotation.Conjugate();
        double angle = 2.0 * System.Math.Atan2(delta.VectorPart.Magnitude(), System.Math.Abs(delta.W));
        _output.WriteLine($"MEASURE #559 ECLIPJ2000 against sxform: {angle.ToString("E3", CultureInfo.InvariantCulture)} rad");
        Assert.InRange(angle, 0.0, SxformTolerance);
        Assert.Equal(Vector3.Zero, orientation.AngularVelocity);
        Assert.Same(Frames.Frame.ECLIPTIC_J2000, orientation.ReferenceFrame);
    }

    [Fact]
    public void FrameTransformation_StoredOrientationsComeFirst()
    {
        // A frame whose orientations were added is read from them, as before (ConfigurationTest); ICRF and GCRF take none (#744)
        var provider = new MemoryDataProvider();
        var rotation = new Quaternion(Vector3.VectorZ, 0.3);
        provider.AddStateOrientationToICRF(Frames.Frame.ECLIPTIC_J2000, Start,
            new StateOrientation(rotation, Vector3.Zero, Start, Frames.Frame.ECLIPTIC_J2000));
        Assert.Equal(rotation, provider.FrameTransformationToICRF(Start, Frames.Frame.ECLIPTIC_J2000).Rotation);
        Assert.Throws<InvalidOperationException>(() => provider.FrameTransformationToICRF(Start.AddSeconds(10.0), Frames.Frame.ECLIPTIC_J2000));
    }

    [Theory]
    [InlineData("B1950")]
    [InlineData("IAU_EARTH")]
    [InlineData("ITRF93")]
    public void Read_InAFrameNeitherStoredNorKnown_Throws(string name)
    {
        // A frame the provider neither stores nor defines, a body-fixed frame such as the Earth's included: no rotation into
        // it exists, so the read throws (#559; before, the ICRF difference came back labelled with the frame). Migration:
        // store the frame's orientations (AddStateOrientationToICRF)
        var provider = StatesProvider(Frames.Frame.ICRF);
        var ex = Assert.Throws<ArgumentException>(() =>
            provider.GetEphemeris(Start, PlanetsAndMoons.MOON_BODY, PlanetsAndMoons.EARTH_BODY, new Frames.Frame(name), Aberration.None));
        Assert.Contains(name, ex.Message);
    }

    [Theory]
    [InlineData(Aberration.LT)]
    [InlineData(Aberration.LTS)]
    [InlineData(Aberration.CN)]
    [InlineData(Aberration.CNS)]
    [InlineData(Aberration.XLT)]
    [InlineData(Aberration.XLTS)]
    [InlineData(Aberration.XCN)]
    [InlineData(Aberration.XCNS)]
    public void Read_WithAnAberration_ThrowsNotSupported(Aberration aberration)
    {
        // The provider holds geometric states only (#559, decided by S. Guillet, 2026-11-17): it refuses an aberrated read,
        // whatever the frame, the target and the observer
        var provider = StatesProvider(Frames.Frame.ICRF);
        foreach (var (target, frame) in new (ILocalizable, Frames.Frame)[]
                 {
                     (PlanetsAndMoons.MOON_BODY, Frames.Frame.ICRF), (PlanetsAndMoons.MOON_BODY, Frames.Frame.ECLIPTIC_J2000),
                     (PlanetsAndMoons.EARTH_BODY, Frames.Frame.ICRF)
                 })
        {
            var ex = Assert.Throws<NotSupportedException>(() => provider.GetEphemeris(Start, target, PlanetsAndMoons.EARTH_BODY, frame, aberration));
            Assert.Contains(aberration.ToString(), ex.Message);
        }
    }

    /// <summary>
    /// The frames the provider defines and never reads from stored entries, ICRF and GCRF (the identity: its states are
    /// ICRF, #559, and GCRF shares its axes, #543) and the managed TEME, CIRS and TIRS (#656), by name in any case: storing
    /// an orientation under one of them throws <see cref="ArgumentException"/> naming <c>frame</c> and stores nothing
    /// (#744, decided by S. Guillet, 2026-11-18). Before, an orientation stored under ICRF or GCRF was returned by
    /// <see cref="MemoryDataProvider.FrameTransformationToICRF"/> while <see cref="MemoryDataProvider.GetEphemeris"/> took
    /// the identity: the probe of the issue, 90° about z under ICRF, gave 1.5708 rad from the identity and the stored state
    /// (1e11, 0, 0) m unrotated. The references are the definitions: the identity, and the managed frame's own orientation.
    /// </summary>
    [Theory]
    [InlineData("J2000")]
    [InlineData("j2000")]
    [InlineData("GCRF")]
    [InlineData("gcrf")]
    [InlineData("TEME")]
    [InlineData("teme")]
    [InlineData("CIRS")]
    [InlineData("Cirs")]
    [InlineData("TIRS")]
    [InlineData("tirs")]
    public void AddStateOrientation_UnderAFrameTheProviderDefines_Throws(string name)
    {
        var provider = new MemoryDataProvider();
        var frame = new Frames.Frame(name);
        var rotation = new Quaternion(Vector3.VectorZ, Astrodynamics.Constants.HalfPi);

        var ex = Assert.Throws<ArgumentException>(() => provider.AddStateOrientationToICRF(frame, Start, new StateOrientation(rotation, Vector3.Zero, Start, frame)));
        Assert.Equal("frame", ex.ParamName);
        Assert.Contains(name, ex.Message);

        // Nothing stored: the read is the definition
        var read = provider.FrameTransformationToICRF(Start, frame);
        var expected = name.Equals("J2000", StringComparison.OrdinalIgnoreCase) || name.Equals("GCRF", StringComparison.OrdinalIgnoreCase)
            ? Quaternion.Identity
            : (name.ToUpperInvariant() switch { "TEME" => Frames.Frame.TEME, "CIRS" => Frames.Frame.CIRS, _ => Frames.Frame.TIRS })
                .GetStateOrientationToICRF(Start).Rotation;
        var delta = read.Rotation * expected.Conjugate();
        Assert.Equal(0.0, 2.0 * System.Math.Atan2(delta.VectorPart.Magnitude(), System.Math.Abs(delta.W)));
    }

    [Fact]
    public void AddStateOrientation_UnderTheLibrarysDefinedFrames_Throws()
    {
        // The library's own instances, the probe of #744 under ICRF among them
        var provider = new MemoryDataProvider();
        foreach (var frame in new[] { Frames.Frame.ICRF, Frames.Frame.GCRF, Frames.Frame.TEME, Frames.Frame.CIRS, Frames.Frame.TIRS })
        {
            var orientation = new StateOrientation(new Quaternion(Vector3.VectorZ, Astrodynamics.Constants.HalfPi), Vector3.Zero, Start, frame);
            Assert.Equal("frame", Assert.Throws<ArgumentException>(() => provider.AddStateOrientationToICRF(frame, Start, orientation)).ParamName);
        }

        // The provider's two answers for ICRF agree: the identity, and the stored state unrotated
        Assert.Equal(Quaternion.Identity, provider.FrameTransformationToICRF(Start, Frames.Frame.ICRF).Rotation);
        provider.AddStateVector(399, Start, new StateVector(new Vector3(1e11, 0.0, 0.0), Vector3.Zero, TestHelpers.SsbAtJ2000, Start, Frames.Frame.ICRF));
        var state = provider.GetEphemeris(Start, PlanetsAndMoons.EARTH_BODY, new Barycenter(0), Frames.Frame.ICRF, Aberration.None);
        Assert.Equal(new Vector3(1e11, 0.0, 0.0), state.ToStateVector().Position);
    }
}
