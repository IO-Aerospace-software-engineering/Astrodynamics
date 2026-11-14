using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.OrbitalParameters.TLE;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.Coordinates
{
    public class EquatorialTests
    {
        private readonly ITestOutputHelper _output;

        public EquatorialTests(ITestOutputHelper output)
        {
            _output = output;
            TestKernels.EnsureSolarSystem();
        }

        [Fact]
        public void Create()
        {
            Equatorial eq = new Equatorial(1.0, 2.0, 3.0, TimeSystem.Time.J2000TDB);
            Assert.Equal(1.0, eq.Declination);
            Assert.Equal(2.0, eq.RightAscension);
            Assert.Equal(3.0, eq.Distance);
        }

        [Fact]
        public void CreateFromStateVector()
        {
            Astrodynamics.Mission.Mission mission = new Astrodynamics.Mission.Mission("mission1");
            new Scenario("scn1", mission, new Window(new TimeSystem.Time(new DateTime(2001, 1, 1), TimeFrame.TDBFrame), new TimeSystem.Time(new DateTime(2001, 2, 1), TimeFrame.TDBFrame)));
            var epoch = new TimeSystem.Time(DateTime.MinValue, TimeFrame.TDBFrame);
            CelestialBody earth = PlanetsAndMoons.EARTH_BODY;
            Equatorial eq = new Equatorial(new StateVector(
                new Vector3(-291608.38463344, -266716.83339423, -76102.48709990), new Vector3(), earth, epoch,
                Frames.Frame.ICRF));
            Assert.Equal(new Equatorial(-0.19024413568211371, 3.8824377884371972, 402448.63988732797, epoch), eq);
        }

        [Fact]
        public void CreateFromStateVector2()
        {
            var epoch = new TimeSystem.Time(new DateTime(2021, 1, 1), TimeFrame.TDBFrame);

            var moon = TestHelpers.Moon;
            var earth = moon.InitialOrbitalParameters.Observer;
            var eq = new Equatorial(new StateVector(
                new Vector3(-202831.34150844064, 284319.70678317308, 150458.88140126597),
                new Vector3(-0.48702480142667454, -0.26438331399030518, -0.17175837261637006), earth, epoch,
                Frames.Frame.ICRF));
            Assert.Equal(new Equatorial(0.406773808779999, 2.1904536325374035, 380284.26703704614, epoch), eq);
        }

        [Fact]
        public void ToCartesian()
        {
            var moon = TestHelpers.MoonAtJ2000;
            var earth = TestHelpers.EarthAtJ2000;
            var eq = moon.GetEphemeris(TimeSystem.Time.J2000TDB, earth, Frames.Frame.ICRF, Aberration.None).ToEquatorial();

            Assert.Equal(new Equatorial(-0.19024413568211912, 3.8824377884372114, 402448639.8873273, TimeSystem.Time.J2000TDB), eq);
            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(new Vector3(-291608384.63343555, -266716833.3942334, -76102487.09990202), eq.ToCartesian(),TestHelpers.VectorComparer);
            }
            else
            {
                Assert.Equal(new Vector3(-291608384.63343555, -266716833.3942334, -76102487.09990202), eq.ToCartesian(),TestHelpers.VectorComparer);
            }
        }
            // #680. The reference: the closed form of the right ascension and declination of a position in ICRF, RA = atan2(y, x)
        // in [0, 2π), Dec = asin(z / |r|), written here, applied to a position SPICE gives in J2000 (spkezr_c)
        private static (double Ra, double Dec) RaDec(in Vector3 position)
        {
            double ra = System.Math.Atan2(position.Y, position.X);
            if (ra < 0.0) ra += IO.Astrodynamics.Constants._2PI;
            return (ra, System.Math.Asin(position.Z / position.Magnitude()));
        }

        // The difference of two right ascensions, modulo 2π
        private static double RaDifference(double a, double b) => System.Math.Abs(System.Math.IEEERemainder(a - b, IO.Astrodynamics.Constants._2PI));

        // Measured on Linux x64 (2026-11-14), the worst over the 24 reads of the Moon and the frame changes below: 4.441e-16 rad
        // in right ascension (one ulp of the angles in [2, 4)) and 1.110e-16 rad in declination; the bounds are twice the worst,
        // provisional until the three platforms are measured. Before the fix, up to 166.4 deg in RA (MOON_ME, 2024)
        private const double RaTolerance = 8.9e-16;
        private const double DecTolerance = 2.3e-16;

        private static readonly double[] MoonDays = { 0.0, 1234.5, 8000.25, 9000.75 };

        private static Frames.Frame[] MoonFrames() => new[]
        {
            Frames.Frame.ICRF, Frames.Frame.ECLIPTIC_J2000, new Frames.Frame("ITRF93"), new Frames.Frame("IAU_MOON"),
            new Frames.Frame("MOON_ME"), Frames.Frame.TEME
        };

        [Fact]
        public void ToEquatorial_OfAStateInAnyFrame_IsTheIcrfRaDecOfSpice()
        {
            var earth = new CelestialBody(PlanetsAndMoons.EARTH);
            var moon = new CelestialBody(PlanetsAndMoons.MOON);
            double worstRa = 0.0;
            double worstDec = 0.0;
            foreach (var day in MoonDays)
            {
                var epoch = TimeSystem.Time.J2000TDB.AddSeconds(day * 86400.0);
                var reference = SpiceAPI.Instance.ReadEphemeris(epoch, PlanetsAndMoons.EARTH.NaifId, PlanetsAndMoons.MOON.NaifId,
                    Frames.Frame.ICRF, Aberration.None).ToStateVector().Position;
                var (ra, dec) = RaDec(reference);
                foreach (var frame in MoonFrames())
                {
                    var state = moon.GetEphemeris(epoch, earth, frame, Aberration.None).ToStateVector();

                    var fromMethod = state.ToEquatorial();
                    var fromConstructor = new Equatorial(state);

                    Assert.Equal(fromMethod, fromConstructor);
                    double dRa = RaDifference(fromMethod.RightAscension, ra);
                    double dDec = System.Math.Abs(fromMethod.Declination - dec);
                    worstRa = System.Math.Max(worstRa, dRa);
                    worstDec = System.Math.Max(worstDec, dDec);
                    Assert.True(dRa <= RaTolerance, $"{epoch} {frame.Name}: RA off by {dRa:E3} rad");
                    Assert.True(dDec <= DecTolerance, $"{epoch} {frame.Name}: Dec off by {dDec:E3} rad");
                    Assert.Equal(state.Position.Magnitude(), fromMethod.Distance);
                    Assert.Equal(epoch, fromMethod.Epoch);
                }
            }

            _output.WriteLine($"MEASURE #680 ToEquatorial against SPICE RA: {worstRa:E3} rad");
            _output.WriteLine($"MEASURE #680 ToEquatorial against SPICE Dec: {worstDec:E3} rad");
        }

        [Fact]
        public void ToEquatorial_IsInvariantUnderAChangeOfFrame()
        {
            var epoch = new TimeSystem.Time(2024, 6, 21, 12, 0, 0, frame: TimeFrame.TDBFrame);
            var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, epoch);
            var icrf = new StateVector(new Vector3(6678137.0, 987654.0, 489312.0), new Vector3(-1032.0, 7154.0, 2531.0), earth, epoch,
                Frames.Frame.ICRF);

            // An ICRF state: the closed form, bit for bit
            var (ra, dec) = RaDec(icrf.Position);
            Assert.Equal(ra, icrf.ToEquatorial().RightAscension);
            Assert.Equal(dec, icrf.ToEquatorial().Declination);

            double worstRa = 0.0;
            double worstDec = 0.0;
            foreach (var frame in new[] { Frames.Frame.ECLIPTIC_J2000, new Frames.Frame("ITRF93"), Frames.Frame.TEME, Frames.Frame.GCRF })
            {
                var equatorial = icrf.ToFrame(frame).ToEquatorial();
                worstRa = System.Math.Max(worstRa, RaDifference(equatorial.RightAscension, ra));
                worstDec = System.Math.Max(worstDec, System.Math.Abs(equatorial.Declination - dec));
            }

            // Keplerian elements defined in ECLIPJ2000 and a TLE (TEME), against the closed form of their ICRF state
            var keplerian = new KeplerianElements(7.0e6, 0.01, 0.9, 1.1, 0.4, 2.3, earth, epoch, Frames.Frame.ECLIPTIC_J2000);
            var tle = new TLE("ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
                "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
            foreach (Astrodynamics.OrbitalParameters.OrbitalParameters elements in new Astrodynamics.OrbitalParameters.OrbitalParameters[] { keplerian, tle })
            {
                var (elementsRa, elementsDec) = RaDec(elements.ToFrame(Frames.Frame.ICRF).ToStateVector().Position);
                var equatorial = elements.ToEquatorial();
                worstRa = System.Math.Max(worstRa, RaDifference(equatorial.RightAscension, elementsRa));
                worstDec = System.Math.Max(worstDec, System.Math.Abs(equatorial.Declination - elementsDec));
            }

            _output.WriteLine($"MEASURE #680 ToEquatorial frame invariance RA: {worstRa:E3} rad");
            _output.WriteLine($"MEASURE #680 ToEquatorial frame invariance Dec: {worstDec:E3} rad");
            Assert.True(worstRa <= RaTolerance, $"RA off by {worstRa:E3} rad");
            Assert.True(worstDec <= DecTolerance, $"Dec off by {worstDec:E3} rad");
        }

        [Fact]
        public void ToEquatorial_OnThePolarAxisAndTheEquator()
        {
            var epoch = new TimeSystem.Time(2024, 6, 21, 12, 0, 0, frame: TimeFrame.TDBFrame);
            var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, epoch);

            // The ICRF pole, expressed in ECLIPJ2000: Dec π/2 (RA is undefined there and not asserted)
            var pole = new StateVector(new Vector3(0.0, 0.0, 7.0e6), Vector3.Zero, earth, epoch, Frames.Frame.ICRF)
                .ToFrame(Frames.Frame.ECLIPTIC_J2000).ToEquatorial();
            double poleError = System.Math.Abs(pole.Declination - IO.Astrodynamics.Constants.PI2);
            _output.WriteLine($"MEASURE #680 ToEquatorial ICRF pole from ECLIPJ2000, Dec: {poleError:E3} rad");
            Assert.True(poleError <= PoleTolerance, $"{poleError:E3} rad");

            // The ICRF equator, expressed in ECLIPJ2000: Dec 0 and its ICRF right ascension
            double worstRa = 0.0;
            double worstDec = 0.0;
            for (int i = 0; i < 12; i++)
            {
                double ra = i * IO.Astrodynamics.Constants._2PI / 12.0;
                var equatorial = new StateVector(new Vector3(7.0e6 * System.Math.Cos(ra), 7.0e6 * System.Math.Sin(ra), 0.0), Vector3.Zero, earth,
                    epoch, Frames.Frame.ICRF).ToFrame(Frames.Frame.ECLIPTIC_J2000).ToEquatorial();
                worstRa = System.Math.Max(worstRa, RaDifference(equatorial.RightAscension, ra));
                worstDec = System.Math.Max(worstDec, System.Math.Abs(equatorial.Declination));
            }

            _output.WriteLine($"MEASURE #680 ToEquatorial ICRF equator from ECLIPJ2000 RA: {worstRa:E3} rad");
            _output.WriteLine($"MEASURE #680 ToEquatorial ICRF equator from ECLIPJ2000 Dec: {worstDec:E3} rad");
            Assert.True(worstRa <= RaTolerance, $"RA off by {worstRa:E3} rad");
            Assert.True(worstDec <= DecTolerance, $"Dec off by {worstDec:E3} rad");
        }

        // Measured 0 on Linux x64 (2026-11-14): z / |r| rounds to 1. The bound is the resolution of asin(z / |r|) at the pole,
        // where one ulp below 1 is π/2 - 1.49e-8 rad, not twice the measurement, which would be 0
        private const double PoleTolerance = 1.5e-8;
    }
}
