using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Atmosphere;

/// <summary>
/// NRLMSISE-00 takes universal time (#409): an epoch in any time scale gives the density of the same instant in UTC,
/// and the model receives the day of year and the seconds of the day of the UTC calendar.
/// </summary>
public class Nrlmsise00TimeScaleTests
{
    // The epoch of the reference cases of phase 2 (TDB), and a geodetic point at 400 km
    private static readonly TimeSystem.Time TdbEpoch = new(2021, 3, 20, 12, 0, 0);
    private const double Altitude = 400e3;
    private const double Latitude = 0.7;
    private const double Longitude = -1.2;

    private readonly ITestOutputHelper _output;

    public Nrlmsise00TimeScaleTests(ITestOutputHelper output)
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        _output = output;
    }

    [Fact]
    public void TdbEpoch_GivesTheDensityOfTheSameInstantInUtc_BitForBit()
    {
        // Arrange
        var model = new Nrlmsise00Model(SpaceWeather.Nominal);
        var utcEpoch = TdbEpoch.ToUTC();

        // Act
        double tdb = model.GetDensity(AtmosphericContext.FromPlanetodetic(Altitude, Latitude, Longitude, TdbEpoch));
        double utc = model.GetDensity(AtmosphericContext.FromPlanetodetic(Altitude, Latitude, Longitude, utcEpoch));

        // Assert
        Assert.Equal(utc, tdb);
    }

    [Fact]
    public void UtcEpoch_TheModelReceivesTheUtcCalendar()
    {
        // Arrange: the low-level model (SI inputs) with the UTC calendar of the instant, written out (UT, and the local
        // solar time sec/3600 + longitude/15° of the notes on the input variables)
        var utc = TdbEpoch.ToUTC().DateTime;
        double sec = utc.TimeOfDay.TotalSeconds;
        double longitudeDeg = Longitude * IO.Astrodynamics.Constants.Rad2Deg;
        double lst = sec / 3600.0 + longitudeDeg / 15.0;
        if (lst < 0.0) lst += 24.0;
        var input = new NrlmsiseInput
        {
            Year = utc.Year, Doy = utc.DayOfYear, Sec = sec, Alt = Altitude, GLat = Latitude, GLong = Longitude, Lst = lst,
            F107A = SpaceWeather.Nominal.F107A, F107 = SpaceWeather.Nominal.F107, Ap = SpaceWeather.Nominal.Ap
        };
        // Total mass density, in kg/m³ with the standard flags of the adapter
        double expected = new NRLMSISE00().Calculate(input, NrlmsiseFlags.CreateStandard()).D[5];

        // Act
        double density = new Nrlmsise00Model(SpaceWeather.Nominal)
            .GetDensity(AtmosphericContext.FromPlanetodetic(Altitude, Latitude, Longitude, TdbEpoch));

        // Assert
        Assert.Equal(expected, density, expected * 1e-14);
    }

    [Fact]
    public void TdbCalendarTakenAsUt_DepartureMeasured()
    {
        // Arrange: what the model gave before #409, the TDB calendar taken as UT, 69.2 s later than the instant
        var model = new Nrlmsise00Model(SpaceWeather.Nominal);
        var tdbCalendarAsUtc = new TimeSystem.Time(TdbEpoch.DateTime, TimeSystem.TimeFrame.UTCFrame);
        double worst = 0.0;

        // Act: over the latitudes and local times of a LEO orbit
        for (int i = 0; i < 24; i++)
        {
            double latitude = -0.9 + 1.8 * (i % 6) / 5.0;
            double longitude = -3.0 + 0.25 * i;
            double now = model.GetDensity(AtmosphericContext.FromPlanetodetic(Altitude, latitude, longitude, TdbEpoch));
            double before = model.GetDensity(
                AtmosphericContext.FromPlanetodetic(Altitude, latitude, longitude, tdbCalendarAsUtc));
            worst = System.Math.Max(worst, System.Math.Abs(before / now - 1.0));
        }

        // Assert: the departure of #409 at 400 km, measured 2.8e-3 at most on this grid (2026-10-19; 1.5e-3 at most along
        // the drag case G6 of #408)
        _output.WriteLine($"TDB calendar taken as UT against UTC, 400 km: {worst:E2} at most");
        Assert.InRange(worst, 1e-5, 5e-3);
    }
}
