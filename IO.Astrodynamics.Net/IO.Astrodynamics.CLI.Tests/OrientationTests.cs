using System.Globalization;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.CLI.Tests;

public class OrientationsTests
{
    private const string Expected =
        "Epoch : 2023-01-01T01:00:00.0000000 TDB Orientation : Quaternion { W = 0.5384132956449944, VectorPart = X : -0.0009258429802486701 Y : -0.0006066138778422119 Z: -0.842680187204538 } Angular velocity : X : 1.6147412630749346E-07 Y : 1.9022200070736152E-09 Z: 7.292097246566707E-05 Frame : J2000\n" +
        "Epoch : 2023-01-01T01:01:00.0000000 TDB Orientation : Quaternion { W = 0.5365685324801104, VectorPart = X : -0.0009271700009545769 Y : -0.0006045864227802883 Z: -0.8438560213586128 } Angular velocity : X : 1.6147414096478393E-07 Y : 1.9022120345758394E-09 Z: 7.29209724657282E-05 Frame : J2000\n";

    private static string[] Orientation(params string[] options) =>
    [
        "orientation", "399", "-K", "Data", "--begin", new Time(2023, 01, 01, 1, 0, 0, TimeScale.TDB).ToString(),
        "--end", new Time(2023, 01, 01, 1, 1, 0, TimeScale.TDB).ToString(), "--step", "1min", .. options
    ];

    [Fact]
    public void CallWithAllOptions()
    {
        using (Configuration.Lock())
        {
            OutputAssert.EqualApprox(Expected.Replace("\n", Environment.NewLine), CliRun.Ok(Orientation("--frame", "ICRF")));
        }
    }

    [Fact]
    public void CallWithoutOptions()
    {
        using (Configuration.Lock())
        {
            OutputAssert.EqualApprox(Expected.Replace("\n", Environment.NewLine), CliRun.Ok(Orientation()));
            Assert.Equal(CliRun.Ok(Orientation()), CliRun.Ok("orientation", "EARTH", "-K", "Data", "--begin", "2023-01-01T01:00:00 TDB",
                "--end", "2023-01-01T01:01:00 TDB", "--step", "00:01:00"));
        }
    }

    /// <summary>
    /// The epochs of a UTC window are read on the UTC grid and printed in UTC, the scale of the input (R29 of #668); before,
    /// in TDB: <c>--time-scale tdb</c> prints those labels, bit for bit the states the TDB labels of the same instants give.
    /// </summary>
    [Fact]
    public void AUtcWindow_IsPrintedInUtc_OrInTimeScale()
    {
        using (Configuration.Lock())
        {
            string[] Utc(params string[] options) =>
                ["orientation", "399", "-K", "Data", "--begin", "2023-01-01T01:00:00Z", "--end", "2023-01-01T01:01:00Z", "--step", "60s", .. options];

            var utc = CliRun.Ok(Utc()).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, utc.Length);
            Assert.StartsWith("Epoch : 2023-01-01T01:00:00.0000000Z ", utc[0]);
            Assert.StartsWith("Epoch : 2023-01-01T01:01:00.0000000Z ", utc[1]);

            var tdb = CliRun.Ok(Utc("--time-scale", "tdb")).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            var begin = Time.Parse("2023-01-01T01:00:00Z");
            var earth = new Body.CelestialBody(399);
            Assert.Equal(earth.GetOrientation(Frames.Frame.ICRF, begin).ToString(), tdb[0]);
            Assert.Equal(utc[0][utc[0].IndexOf(" Orientation", StringComparison.Ordinal)..], tdb[0][tdb[0].IndexOf(" Orientation", StringComparison.Ordinal)..]);
        }
    }

    /// <summary>A step of zero or less is an input error (#815: before, the command never ended).</summary>
    [Theory]
    [InlineData("0s")]
    [InlineData("00:00:00")]
    [InlineData("-1min")]
    [InlineData("-00:01:00")]
    public void AStepOfZeroOrLess_IsAnInputError(string step)
    {
        using (Configuration.Lock())
        {
            var error = CliRun.Fails(2, "orientation", "399", "-K", "Data", "--begin", "0", "--end", "60", "--step", step);
            Assert.StartsWith("Error: --", error);
        }
    }

    [Fact]
    public void Csv_ReadsBackBitForBit()
    {
        using (Configuration.Lock())
        {
            var csv = CliRun.Ok(Orientation("--format", "csv")).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("epoch,frame,qw,qx,qy,qz,wx_rad_s,wy_rad_s,wz_rad_s", csv[0]);
            var row = csv[1].Split(',');
            Assert.Equal("2023-01-01T01:00:00.0000000 TDB", row[0]);
            var earth = new Body.CelestialBody(399);
            var expected = earth.GetOrientation(Frames.Frame.ICRF, new Time(2023, 01, 01, 1, 0, 0, TimeScale.TDB));
            Assert.Equal(expected.Rotation.W, double.Parse(row[2], CultureInfo.InvariantCulture));
            Assert.Equal(expected.Rotation.VectorPart.Z, double.Parse(row[5], CultureInfo.InvariantCulture));
            Assert.Equal(expected.AngularVelocity.Z, double.Parse(row[8], CultureInfo.InvariantCulture));
        }
    }
}
