// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Exceptions;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Tests.Body;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The error SPICE signals in a call of the library (R03 of #668, structuring choice 10, decided by S. Guillet, 2026-11-19):
/// <see cref="SpiceException"/>, an <see cref="InvalidOperationException"/>, with SPICE's short and long messages, which the
/// native layer now reports beside its message. The references are SPICE's own messages: the short ones those CSPICE N0067
/// signals for these errors (SPKINSUFFDATA of spkezr_c past the coverage of the SPK, FRAMEDATANOTFOUND of sxform_c past
/// that of the Earth's binary PCK, UNKNOWNFRAME for a name it does not know, BADVARNAME for a variable name of more than 32
/// characters, Kernel Required Reading), and, in the Validation category, the messages CSPICE gives for the same calls in
/// this process.
/// </summary>
public class SpiceExceptionTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("spice-exception-");

    // Past the coverage of de440s.bsp (1849 to 2150) and of earth_latest_high_prec.bpc (from 2000)
    private static readonly TimeSystem.Time FarFuture = new(3000, 1, 1, TimeScale.TDB);
    private static readonly TimeSystem.Time BeforeTheEarthPck = new(1900, 1, 1, TimeScale.TDB);

    public SpiceExceptionTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    public void Dispose()
    {
        if (_root.Exists)
        {
            _root.Delete(true);
        }
    }

    [Fact]
    public void AnEphemerisReadPastTheCoverage_ThrowsSpiceException_WithSpicesMessages()
    {
        var error = Assert.Throws<SpiceException>(() =>
            SpiceApi.Instance.ReadEphemeris(FarFuture, 399, 301, IO.Astrodynamics.Frames.Frame.ICRF, Aberration.None));
        Assert.Equal("SPICE(SPKINSUFFDATA)", error.ShortMessage);
        Assert.NotEmpty(error.LongMessage);
        Assert.Contains(error.LongMessage, error.Message);
        Assert.IsAssignableFrom<InvalidOperationException>(error);
    }

    [Fact]
    public void ATransformPastTheFrameData_ThrowsSpiceException_WithSpicesMessages()
    {
        var error = Assert.Throws<SpiceException>(() =>
            SpiceApi.Instance.TransformFrame(BeforeTheEarthPck, IO.Astrodynamics.Frames.Frame.ICRF, IO.Astrodynamics.Frames.Frame.ITRF93));
        Assert.Equal("SPICE(FRAMEDATANOTFOUND)", error.ShortMessage);
        Assert.StartsWith("SPICE gives no transformation from J2000 to ITRF93 at ", error.Message);
        Assert.EndsWith(": " + error.LongMessage, error.Message);
    }

    // R25's ArgumentException keeps the SPICE error as its inner exception, now a SpiceException
    [Fact]
    public void AFrameNameSpiceDoesNotKnow_ThrowsArgumentException_WithTheSpiceExceptionInside()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            SpiceApi.Instance.TransformFrame(new TimeSystem.Time(2021, 1, 1, TimeScale.TDB), IO.Astrodynamics.Frames.Frame.ICRF, new IO.Astrodynamics.Frames.Frame("IOA_R03_NO_FRAME")));
        Assert.Equal("toFrame", error.ParamName);
        var spice = Assert.IsType<SpiceException>(error.InnerException);
        Assert.Equal("SPICE(UNKNOWNFRAME)", spice.ShortMessage);
        Assert.Contains("IOA_R03_NO_FRAME", spice.LongMessage);
    }

    [Fact]
    public void AKernelSpiceCannotRead_ThrowsSpiceException_NamingTheFile()
    {
        var kernel = new FileInfo(Path.Combine(_root.FullName, "bad.tf"));
        File.WriteAllText(kernel.FullName, "KPL/FK\n\\begindata\nIOA_R03_A_NAME_LONGER_THAN_32_CHARACTERS = 1\n\\begintext\n");

        var error = Assert.Throws<SpiceException>(() => SpiceApi.Instance.LoadKernels(kernel));

        Assert.Equal("SPICE(BADVARNAME)", error.ShortMessage);
        Assert.Equal($"Kernel {kernel.FullName} can't be loaded: {error.LongMessage}", error.Message);
        Assert.DoesNotContain(SpiceApi.Instance.GetLoadedKernels(), k => k.FullName == kernel.FullName);
    }

    // prop2b_c signals SPICE(ZEROPOSITION) with no long message (#714): the short message is the message, the long one empty
    [Fact]
    public void ATwoBodyPropagationSpiceRejects_ThrowsSpiceException_WithAnEmptyLongMessage()
    {
        var state = new IO.Astrodynamics.OrbitalParameters.StateVector(IO.Astrodynamics.Math.Vector3.Zero, new IO.Astrodynamics.Math.Vector3(0.0, 7500.0, 0.0),
            IO.Astrodynamics.SolarSystemObjects.PlanetsAndMoons.EARTH_BODY, new TimeSystem.Time(2021, 1, 1, TimeScale.TDB), IO.Astrodynamics.Frames.Frame.ICRF);
        var error = Assert.Throws<SpiceException>(() => SpiceApi.Instance.Propagate2Bodies(state, 3.986004418e14, TimeSpan.FromSeconds(60.0)));
        Assert.Equal("SPICE(ZEROPOSITION)", error.ShortMessage);
        Assert.Equal("", error.LongMessage);
        Assert.Equal("The state cannot be propagated by two-body motion: SPICE(ZEROPOSITION)", error.Message);
    }

    [Fact]
    public void TheStandardConstructors()
    {
        Assert.Equal("", new SpiceException().ShortMessage);
        var plain = new SpiceException("m");
        Assert.Equal("m", plain.Message);
        Assert.Equal("", plain.ShortMessage);
        Assert.Equal("", plain.LongMessage);
        var inner = new InvalidOperationException();
        Assert.Same(inner, new SpiceException("m", inner).InnerException);
        var full = new SpiceException("m", "SPICE(X)", "long", inner);
        Assert.Equal(("m", "SPICE(X)", "long"), (full.Message, full.ShortMessage, full.LongMessage));
        Assert.Same(inner, full.InnerException);
        var nulls = new SpiceException("m", null, null);
        Assert.Equal(("", ""), (nulls.ShortMessage, nulls.LongMessage));
    }

    // The messages SPICE gives for the same calls, read from CSPICE in this process (Linux: the Windows DLL exports no CSPICE
    // routine)
    [Fact]
    [Trait("Category", "Validation")]
    public void TheMessagesAreSpices()
    {
        var read = Assert.Throws<SpiceException>(() =>
            SpiceApi.Instance.ReadEphemeris(FarFuture, 399, 301, IO.Astrodynamics.Frames.Frame.ICRF, Aberration.None));
        var spkezr = CspiceDirect.SpkezrMessages("301", FarFuture.TimeSpanFromJ2000().TotalSeconds, "J2000", "NONE", "399");
        Assert.Equal((spkezr.Short, spkezr.Long), (read.ShortMessage, read.LongMessage));

        var transform = Assert.Throws<SpiceException>(() =>
            SpiceApi.Instance.TransformFrame(BeforeTheEarthPck, IO.Astrodynamics.Frames.Frame.ICRF, IO.Astrodynamics.Frames.Frame.ITRF93));
        var sxform = CspiceDirect.SxformMessages("J2000", "ITRF93", BeforeTheEarthPck.TimeSpanFromJ2000().TotalSeconds);
        Assert.Equal((sxform.Short, sxform.Long), (transform.ShortMessage, transform.LongMessage));

        var unknown = Assert.Throws<ArgumentException>(() =>
            SpiceApi.Instance.TransformFrame(new TimeSystem.Time(2021, 1, 1, TimeScale.TDB), IO.Astrodynamics.Frames.Frame.ICRF, new IO.Astrodynamics.Frames.Frame("IOA_R03_NO_FRAME")));
        var unknownSxform = CspiceDirect.SxformMessages("J2000", "IOA_R03_NO_FRAME", new TimeSystem.Time(2021, 1, 1, TimeScale.TDB).ToTdb().TimeSpanFromJ2000().TotalSeconds);
        var inner = Assert.IsType<SpiceException>(unknown.InnerException);
        Assert.Equal((unknownSxform.Short, unknownSxform.Long), (inner.ShortMessage, inner.LongMessage));
        Assert.False(CspiceDirect.ErrorPending());
    }
}
