using System;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Time;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

public class StarTests
{
    public StarTests()
    {
        API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void Create()
    {
        var star = new Star(1, "star1", 1E+30, "spec", 2, 0.3792, new Equatorial(10, 20, DateTimeExtension.J2000), 4, 5, 6, 7, 8, 9, DateTimeExtension.J2000);
        Assert.Equal(1, star.CatalogNumber);
        Assert.Equal(1000000001, star.NaifId);
        Assert.Equal("star1", star.Name);
        Assert.Equal(1E+30, star.Mass);
        Assert.Equal(6.674299999999999E+19, star.GM);
        Assert.Equal("spec", star.SpectralType);
        Assert.Equal(2, star.VisualMagnitude);
        Assert.Equal(DateTimeExtension.J2000, star.Epoch);
        Assert.Equal(new Equatorial(10, 20, DateTimeExtension.J2000), star.EquatorialCoordinatesAtEpoch);
        Assert.Equal(4, star.DeclinationProperMotion);
        Assert.Equal(5, star.RightAscensionProperMotion);
        Assert.Equal(6, star.DeclinationSigma);
        Assert.Equal(7, star.RightAscensionSigma);
        Assert.Equal(8, star.DeclinationSigmaProperMotion);
        Assert.Equal(9, star.RightAscensionSigmaProperMotion);
        Assert.Equal(0.3792, star.Parallax);
        Assert.Equal(8.1373353929324900E+16, star.Distance);
        Assert.Null(star.InitialOrbitalParameters);
        Assert.Equal(0.0, star.Flattening);
    }

    [Fact]
    public void GetEquatorialCoordinates()
    {
        var star = new Star(1, "star1", 1E+30, "spec", 2, 0.3792, new Equatorial(10, 20, DateTimeExtension.J2000), 4, 5, 6, 7, 8, 9, DateTimeExtension.J2000);
        var res = star.GetEquatorialCoordinates(new DateTime(2001, 1, 1, 12, 0, 0));
        Assert.Equal(new Equatorial(1.4418429380022246, 6.160711018912988, 8.1373353929324900E+16, new DateTime(2001, 1, 1, 12, 0, 0)), res);
    }

    [Fact]
    public void GetEquatorialCoordinates2()
    {
        var star = new Star(1, "star1", 1E+30, "spec", 2, 0.3792,
            new Equatorial(10 - (Astrodynamics.Constants.PI2 * 10), 20 - (Astrodynamics.Constants._2PI * 10), DateTimeExtension.J2000), 4, 5, 6, 7, 8,
            9, DateTimeExtension.J2000);
        var res = star.GetEquatorialCoordinates(new DateTime(2001, 1, 1, 12, 0, 0));
        Assert.Equal(new Equatorial(-0.12895338879267282, 6.160711018912984, 81373353929324896, new DateTime(2001, 1, 1, 12, 0, 0)), res);
    }

    [Fact]
    public void GetDeclinationSigma()
    {
        var star = new Star(1, "star1", 1E+30, "spec", 2, 0.3792, new Equatorial(10, 20, DateTimeExtension.J2000), 4, 5, 6, 7, 8, 9, DateTimeExtension.J2000);
        var res = star.GetDeclinationSigma(new DateTime(2001, 1, 1, 12, 0, 0));
        Assert.Equal(0.5883685739286051, res);
    }


    [Fact]
    public void GetRightAscensionSigma()
    {
        var star = new Star(1, "star1", 1E+30, "spec", 2, 0.3792, new Equatorial(10, 20, DateTimeExtension.J2000), 4, 5, 6, 7, 8, 9, DateTimeExtension.J2000);
        var res = star.GetRightAscensionSigma(new DateTime(2001, 1, 1, 12, 0, 0));
        Assert.Equal(5.1331621997619905, res);
    }

    [Fact]
    public async Task Propagate()
    {
        var epoch = new DateTime(2001, 1, 1);
        var observer = new Barycenter(0);
        var star = new Star(1, "star1", 1E+30, "spec", 2, 0.3792, new Equatorial(1, 1, epoch), 0.1, 0.1, 0, 0, 0, 0, epoch);
        await star.PropagateAsync(new Window(epoch, epoch + TimeSpan.FromDays(365 * 4)), TimeSpan.FromDays(365), Constants.OutputPath);
        var eph0 = star.GetEphemeris(epoch, observer, Frames.Frame.ICRF, Aberration.None);
        var eph1 = star.GetEphemeris(epoch.Add(TimeSpan.FromDays(365)), observer, Frames.Frame.ICRF, Aberration.None);
        var eph2 = star.GetEphemeris(epoch.Add(TimeSpan.FromDays(365 + 365)), observer, Frames.Frame.ICRF, Aberration.None);
        Assert.Equal(1.0, eph0.ToEquatorial().RightAscension, 12);
        Assert.Equal(1.0, eph0.ToEquatorial().Declination, 12);
        Assert.Equal(8.1373353929324900E+16, eph0.ToEquatorial().Distance);

        Assert.Equal(1.1, eph1.ToEquatorial().RightAscension, 3);
        Assert.Equal(1.1, eph1.ToEquatorial().Declination, 3);
        Assert.Equal(8.1373353929324910E+16, eph1.ToEquatorial().Distance);

        Assert.Equal(1.2, eph2.ToEquatorial().RightAscension, 3);
        Assert.Equal(1.2, eph2.ToEquatorial().Declination, 3);
        Assert.Equal(8.1373353929324910E+16, eph2.ToEquatorial().Distance);
    }
}