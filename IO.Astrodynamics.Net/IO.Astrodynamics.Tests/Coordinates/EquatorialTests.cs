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

        // A difference of angles in units of the spacing of the doubles at the reference angle, that spacing taken at 1 rad at
        // least: an angle in [4, 2π) is held to 8.9e-16 rad, one in [2, 4) to 4.4e-16 rad, so a bound in radians would not
        // scale with the angle, while the rotation's own rounding (about 1e-16 rad whatever the angle) is the floor below 1 rad
        private static double Ulps(double difference, double reference)
        {
            double magnitude = System.Math.Max(System.Math.Abs(reference), 1.0);
            return difference / (System.Math.BitIncrement(magnitude) - magnitude);
        }

        // The worst over the 24 reads of the Moon, the frame changes and the equator below, in ulps of the reference angle
        // (Ulps), the same on Linux x64, Windows x64 and macOS arm64 (CI run 43595573884, 2026-11-14): 1 in right ascension
        // (4.441e-16 rad, an RA in [2, 4)) and 0.5 in declination (1.110e-16 rad); the bounds are twice the worst. Before the
        // fix, up to 166.4 deg in RA (MOON_ME, 2024)
        private const double RaToleranceUlps = 2.0;
        private const double DecToleranceUlps = 1.0;

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
                    double dRa = Ulps(RaDifference(fromMethod.RightAscension, ra), ra);
                    double dDec = Ulps(System.Math.Abs(fromMethod.Declination - dec), dec);
                    worstRa = System.Math.Max(worstRa, dRa);
                    worstDec = System.Math.Max(worstDec, dDec);
                    Assert.True(dRa <= RaToleranceUlps, $"{epoch} {frame.Name}: RA off by {dRa} ulps");
                    Assert.True(dDec <= DecToleranceUlps, $"{epoch} {frame.Name}: Dec off by {dDec} ulps");
                    Assert.Equal(state.Position.Magnitude(), fromMethod.Distance);
                    Assert.Equal(epoch, fromMethod.Epoch);
                }
            }

            _output.WriteLine($"MEASURE #680 ToEquatorial against SPICE RA: {worstRa} ulps");
            _output.WriteLine($"MEASURE #680 ToEquatorial against SPICE Dec: {worstDec} ulps");
        }

        // ECLIPJ2000 → J2000 in closed form: R1(-ε), ε = 84381.448″, the obliquity of the ecliptic at J2000 of the Explanatory
        // Supplement (1992) eq. 3.222-1, which defines ECLIPJ2000 (NAIF Frames Required Reading, frame 17), as
        // FrameTransformationSolverTests
        private static Vector3 EclipticToIcrf(in Vector3 ecliptic)
        {
            double epsilon = 84381.448 / 3600.0 * System.Math.PI / 180.0;
            double c = System.Math.Cos(epsilon), s = System.Math.Sin(epsilon);
            return new Vector3(ecliptic.X, c * ecliptic.Y - s * ecliptic.Z, s * ecliptic.Y + c * ecliptic.Z);
        }

        // The TLE against the ICRF state of tools/teme_reference (Vallado's SGP4 rotated by ERFA, IAU 2006/2000B): the residual
        // is that of the SGP4 state and of the TEME rotation (9.03e-4 m in LEO, TemeFrameTests), not the rounding of the angles.
        // The worst, the same on Linux x64, Windows x64 and macOS arm64 (CI run 43595573884, 2026-11-14): 1.101e-13 rad in RA
        // and 5.870e-11 rad in Dec; twice the worst, rounded up at the second significant digit
        private const double TleRaTolerance = 2.3e-13;
        private const double TleDecTolerance = 1.2e-10;

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

            // The same state expressed in other frames by the library: a structural check (ToEquatorial rotates back with the frame
            // rotation ToFrame applied), the rotations themselves checked against SPICE, ERFA and GMAT by their own tests
            double worstRa = 0.0;
            double worstDec = 0.0;
            foreach (var frame in new[] { Frames.Frame.ECLIPTIC_J2000, new Frames.Frame("ITRF93"), Frames.Frame.TEME, Frames.Frame.GCRF })
            {
                var equatorial = icrf.ToFrame(frame).ToEquatorial();
                worstRa = System.Math.Max(worstRa, Ulps(RaDifference(equatorial.RightAscension, ra), ra));
                worstDec = System.Math.Max(worstDec, Ulps(System.Math.Abs(equatorial.Declination - dec), dec));
            }

            // Keplerian elements defined in ECLIPJ2000, against the closed form of the obliquity rotation applied to their
            // ECLIPJ2000 state: independent of the library's frame rotation
            var keplerian = new KeplerianElements(7.0e6, 0.01, 0.9, 1.1, 0.4, 2.3, earth, epoch, Frames.Frame.ECLIPTIC_J2000);
            var (keplerianRa, keplerianDec) = RaDec(EclipticToIcrf(keplerian.ToStateVector().Position));
            var fromKeplerian = keplerian.ToEquatorial();
            worstRa = System.Math.Max(worstRa, Ulps(RaDifference(fromKeplerian.RightAscension, keplerianRa), keplerianRa));
            worstDec = System.Math.Max(worstDec, Ulps(System.Math.Abs(fromKeplerian.Declination - keplerianDec), keplerianDec));

            _output.WriteLine($"MEASURE #680 ToEquatorial frame invariance RA: {worstRa} ulps");
            _output.WriteLine($"MEASURE #680 ToEquatorial frame invariance Dec: {worstDec} ulps");
            Assert.True(worstRa <= RaToleranceUlps, $"RA off by {worstRa} ulps");
            Assert.True(worstDec <= DecToleranceUlps, $"Dec off by {worstDec} ulps");

            // A TLE (TEME) at its epoch, against the closed form on the ICRF state of tools/teme_reference
            var reference = Frame.TemeFrameTests.Reference("ISS 2021");
            var tle = new TLE(reference.Name, reference.Line1, reference.Line2);
            var (tleRa, tleDec) = RaDec(Frame.TemeFrameTests.Position(reference.CipB));
            var fromTle = tle.ToEquatorial();
            double tleRaError = RaDifference(fromTle.RightAscension, tleRa);
            double tleDecError = System.Math.Abs(fromTle.Declination - tleDec);
            _output.WriteLine($"MEASURE #680 ToEquatorial TLE against tools/teme_reference RA: {tleRaError:E3} rad");
            _output.WriteLine($"MEASURE #680 ToEquatorial TLE against tools/teme_reference Dec: {tleDecError:E3} rad");
            Assert.True(tleRaError <= TleRaTolerance, $"TLE RA off by {tleRaError:E3} rad");
            Assert.True(tleDecError <= TleDecTolerance, $"TLE Dec off by {tleDecError:E3} rad");
        }

        [Fact]
        public void ToEquatorial_OnThePolarAxisAndTheEquator()
        {
            var epoch = new TimeSystem.Time(2024, 6, 21, 12, 0, 0, frame: TimeFrame.TDBFrame);
            var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, epoch);

            // The ICRF pole, expressed in ECLIPJ2000: Dec π/2 exactly (RA is undefined there and not asserted). asin(z / |r|) cannot
            // resolve a direction within 1.49e-8 rad of the pole (one double below 1), and no quantity derived from the angles can;
            // what the pole checks is the singular coordinate (no NaN, π/2 itself, measured exactly on Linux x64, 2026-11-14), the
            // rotation's accuracy being resolved by the equator below: a rotation error about the polar axis shifts its right
            // ascensions, one about an equatorial axis its declinations
            var pole = new StateVector(new Vector3(0.0, 0.0, 7.0e6), Vector3.Zero, earth, epoch, Frames.Frame.ICRF)
                .ToFrame(Frames.Frame.ECLIPTIC_J2000).ToEquatorial();
            _output.WriteLine($"MEASURE #680 ToEquatorial ICRF pole from ECLIPJ2000, Dec: {System.Math.Abs(pole.Declination - System.Math.PI / 2.0):E3} rad");
            Assert.Equal(System.Math.PI / 2.0, pole.Declination);

            // The ICRF equator, expressed in ECLIPJ2000: Dec 0 and its ICRF right ascension
            double worstRa = 0.0;
            double worstDec = 0.0;
            for (int i = 0; i < 12; i++)
            {
                double ra = i * IO.Astrodynamics.Constants._2PI / 12.0;
                var equatorial = new StateVector(new Vector3(7.0e6 * System.Math.Cos(ra), 7.0e6 * System.Math.Sin(ra), 0.0), Vector3.Zero, earth,
                    epoch, Frames.Frame.ICRF).ToFrame(Frames.Frame.ECLIPTIC_J2000).ToEquatorial();
                worstRa = System.Math.Max(worstRa, Ulps(RaDifference(equatorial.RightAscension, ra), ra));
                worstDec = System.Math.Max(worstDec, Ulps(System.Math.Abs(equatorial.Declination), 0.0));
            }

            _output.WriteLine($"MEASURE #680 ToEquatorial ICRF equator from ECLIPJ2000 RA: {worstRa} ulps");
            _output.WriteLine($"MEASURE #680 ToEquatorial ICRF equator from ECLIPJ2000 Dec: {worstDec} ulps");
            Assert.True(worstRa <= RaToleranceUlps, $"RA off by {worstRa} ulps");
            Assert.True(worstDec <= DecToleranceUlps, $"Dec off by {worstDec} ulps");
        }
    }
}
