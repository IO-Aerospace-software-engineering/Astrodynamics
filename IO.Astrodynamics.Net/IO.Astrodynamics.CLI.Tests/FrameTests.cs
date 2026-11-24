namespace IO.Astrodynamics.CLI.Tests;

public class FrameTests
{
    private const string AtJ2000Utc =
        "Orientation : Quaternion { W = 0.7686303144036278, VectorPart = X : -1.861440644864163E-05 Y : 8.939414975839561E-07 Z: 0.6396932385389773 } Angular velocity : X : -1.963756068266585E-09 Y : -2.038938971497743E-09 Z: 7.292115064256362E-05 Frame : J2000";

    [Fact]
    public void Convert()
    {
        using (Configuration.Lock())
        {
            // The TDB label of the UTC epoch takes the nearest tick of TDB − TT since #625 (.1839274 when it was truncated,
            // tools/seconds_to_ticks/time_conversion_reference.py): the quaternion is spiceypy's m2q of sxform(J2000, ITRF93)
            // at 64.1839273 s, digit for digit, the angular velocity its xf2rav within 2.3e-13 relative. Written in TDB, the
            // label before 11.0.0, with --time-scale tdb; in UTC, the scale of the input, by default (R29 of #668)
            Assert.Equal($"Epoch : 2000-01-01T12:01:04.1839273 TDB {AtJ2000Utc}{Environment.NewLine}",
                CliRun.Ok("frame-converter", "ICRF", "ITRF93", "-K", "Data", "--epoch", "2000-01-01T12:00:00Z", "--time-scale", "tdb"));
            Assert.Equal($"Epoch : 2000-01-01T12:00:00.0000000Z {AtJ2000Utc}{Environment.NewLine}",
                CliRun.Ok("frame-converter", "ICRF", "ITRF93", "-K", "Data", "--epoch", "2000-01-01T12:00:00Z"));

            var json = System.Text.Json.JsonDocument.Parse(CliRun.Ok("frame-converter", "ICRF", "ITRF93", "-K", "Data", "--epoch", "2000-01-01T12:00:00Z",
                "--format", "json")).RootElement;
            Assert.Equal("2000-01-01T12:00:00.0000000Z", json.GetProperty("epoch").GetString());
            Assert.Equal("J2000", json.GetProperty("frame").GetString());
            Assert.Equal(0.7686303144036278, json.GetProperty("qw").GetDouble());
            Assert.Equal(7.292115064256362E-05, json.GetProperty("wz_rad_s").GetDouble());
        }
    }

    [Theory]
    [InlineData("GCRF")]
    [InlineData("gcrf")]
    public void ConvertFromGcrf(string frame)
    {
        // GCRF has the axes of J2000 (#543): the conversion from a frame built from the name GCRF is that from ICRF, SPICE's
        // sxform from J2000 (Convert), labelled with the frame given
        using (Configuration.Lock())
        {
            string Run(string from) => CliRun.Ok("frame-converter", from, "ITRF93", "-K", "Data", "--epoch", "2000-01-01T12:00:00Z");

            var icrf = Run("ICRF");
            Assert.Contains("W = 0.7686303144036278", icrf);
            Assert.Equal(icrf.Replace("Frame : J2000", $"Frame : {frame}"), Run(frame));
        }
    }

    [Theory]
    [InlineData("CIRS", "ICRF")]
    [InlineData("cirs", "ITRF93")]
    [InlineData("TIRS", "ICRF")]
    [InlineData("Tirs", "cirs")]
    public void ConvertFromCirsOrTirs_IsTheManagedFrame(string from, string to)
    {
        // #656: a frame built from the name CIRS or TIRS, in any case, is Frame.CIRS or Frame.TIRS: the conversion of the
        // static frame, labelled with the frame given (the library's ManagedFrameByNameTests check the static frames
        // against SOFA), in TDB, the label of the library
        using (Configuration.Lock())
        {
            var res = CliRun.Ok("frame-converter", from, to, "-K", "Data", "--epoch", "2000-01-01T12:00:00Z", "--time-scale", "tdb");

            Frames.Frame StaticFrame(string name) => name.ToUpperInvariant() switch
            {
                "CIRS" => Frames.Frame.CIRS,
                "TIRS" => Frames.Frame.TIRS,
                "ICRF" => Frames.Frame.ICRF,
                _ => new Frames.Frame(name)
            };

            var expected = StaticFrame(from).ToFrame(StaticFrame(to), TimeSystem.Time.Parse("2000-01-01T12:00:00Z"));
            Assert.Equal(
                expected.ToString().Replace($"Frame : {StaticFrame(from).Name}", $"Frame : {from}") + Environment.NewLine,
                res);
        }
    }

    [Theory]
    [InlineData("NOT_A_FRAME", "ITRF93", "from")]
    [InlineData("ICRF", "NOT_A_FRAME", "to")]
    public void AnUnknownFrame_IsAnInputError(string from, string to, string argument)
    {
        using (Configuration.Lock())
        {
            var error = CliRun.Fails(2, "frame-converter", from, to, "-K", "Data", "--epoch", "0");
            Assert.Equal($"Error: {argument}: 'NOT_A_FRAME' is not a frame of the library or of the loaded kernels.{Environment.NewLine}", error);
        }
    }
}
