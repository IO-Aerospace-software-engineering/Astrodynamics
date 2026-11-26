// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.CCSDS.OPM;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.OPM
{
    public class OpmKeplerianElementsTests
    {
        public OpmKeplerianElementsTests()
        {
            TestKernels.EnsureSolarSystem();
        }

        #region CreateWithTrueAnomaly Tests

        [Fact]
        public void CreateWithTrueAnomaly_ValidParameters_Succeeds()
        {
            var kep = OpmKeplerianElements.CreateWithTrueAnomaly(
                semiMajorAxis: 6778.137,
                eccentricity: 0.0001,
                inclination: 51.6,
                raan: 120.0,
                aop: 90.0,
                trueAnomaly: 45.0,
                gm: 398600.4418);

            Assert.Equal(6778.137, kep.SemiMajorAxis, 3);
            Assert.Equal(0.0001, kep.Eccentricity, 6);
            Assert.Equal(51.6, kep.Inclination, 4);
            Assert.Equal(120.0, kep.RightAscensionOfAscendingNode, 4);
            Assert.Equal(90.0, kep.ArgumentOfPericenter, 4);
            Assert.Equal(45.0, kep.TrueAnomaly.Value, 4);
            Assert.Null(kep.MeanAnomaly);
            Assert.Equal(398600.4418, kep.GravitationalParameter, 4);
        }

        [Fact]
        public void CreateWithTrueAnomaly_UsesTrueAnomaly_ReturnsTrue()
        {
            var kep = OpmKeplerianElements.CreateWithTrueAnomaly(
                6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, 398600.4418);

            Assert.True(kep.UsesTrueAnomaly);
            Assert.False(kep.UsesMeanAnomaly);
        }

        #endregion

        #region CreateWithMeanAnomaly Tests

        [Fact]
        public void CreateWithMeanAnomaly_ValidParameters_Succeeds()
        {
            var kep = OpmKeplerianElements.CreateWithMeanAnomaly(
                semiMajorAxis: 6778.137,
                eccentricity: 0.0001,
                inclination: 51.6,
                raan: 120.0,
                aop: 90.0,
                meanAnomaly: 45.0,
                gm: 398600.4418);

            Assert.Equal(6778.137, kep.SemiMajorAxis, 3);
            Assert.Equal(45.0, kep.MeanAnomaly.Value, 4);
            Assert.Null(kep.TrueAnomaly);
        }

        [Fact]
        public void CreateWithMeanAnomaly_UsesMeanAnomaly_ReturnsTrue()
        {
            var kep = OpmKeplerianElements.CreateWithMeanAnomaly(
                6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, 398600.4418);

            Assert.True(kep.UsesMeanAnomaly);
            Assert.False(kep.UsesTrueAnomaly);
        }

        #endregion

        #region Validation Tests

        [Fact]
        public void CreateWithTrueAnomaly_NegativeSemiMajorAxis_EllipticalOrbit_ThrowsArgumentOutOfRangeException()
        {
            // For elliptical orbits (e < 1), semi-major axis must be positive
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OpmKeplerianElements.CreateWithTrueAnomaly(-6778.137, 0.5, 51.6, 120.0, 90.0, 45.0, 398600.4418));
        }

        [Fact]
        public void CreateWithTrueAnomaly_ZeroSemiMajorAxis_EllipticalOrbit_ThrowsArgumentOutOfRangeException()
        {
            // For elliptical orbits (e < 1), semi-major axis must be positive
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OpmKeplerianElements.CreateWithTrueAnomaly(0.0, 0.5, 51.6, 120.0, 90.0, 45.0, 398600.4418));
        }

        [Fact]
        public void CreateWithTrueAnomaly_HyperbolicOrbit_NegativeSemiMajorAxis_Succeeds()
        {
            // For hyperbolic orbits (e > 1), semi-major axis must be negative
            var kep = OpmKeplerianElements.CreateWithTrueAnomaly(
                semiMajorAxis: -25000.0,  // Negative for hyperbolic
                eccentricity: 1.5,         // e > 1 for hyperbolic
                inclination: 30.0,
                raan: 45.0,
                aop: 60.0,
                trueAnomaly: 90.0,
                gm: 398600.4418);

            Assert.Equal(-25000.0, kep.SemiMajorAxis, 3);
            Assert.Equal(1.5, kep.Eccentricity, 6);
        }

        [Fact]
        public void CreateWithTrueAnomaly_HyperbolicOrbit_PositiveSemiMajorAxis_ThrowsArgumentOutOfRangeException()
        {
            // For hyperbolic orbits (e > 1), semi-major axis must be negative
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OpmKeplerianElements.CreateWithTrueAnomaly(25000.0, 1.5, 51.6, 120.0, 90.0, 45.0, 398600.4418));
        }

        [Fact]
        public void CreateWithTrueAnomaly_HyperbolicOrbit_ZeroSemiMajorAxis_ThrowsArgumentOutOfRangeException()
        {
            // For hyperbolic orbits (e > 1), semi-major axis must be negative (not zero)
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OpmKeplerianElements.CreateWithTrueAnomaly(0.0, 1.5, 51.6, 120.0, 90.0, 45.0, 398600.4418));
        }

        [Fact]
        public void CreateWithMeanAnomaly_HyperbolicOrbit_NegativeSemiMajorAxis_Succeeds()
        {
            // For hyperbolic orbits (e > 1), semi-major axis must be negative
            var kep = OpmKeplerianElements.CreateWithMeanAnomaly(
                semiMajorAxis: -50000.0,
                eccentricity: 2.0,
                inclination: 45.0,
                raan: 90.0,
                aop: 180.0,
                meanAnomaly: 30.0,
                gm: 398600.4418);

            Assert.Equal(-50000.0, kep.SemiMajorAxis, 3);
            Assert.Equal(2.0, kep.Eccentricity, 6);
            Assert.True(kep.UsesMeanAnomaly);
        }

        [Fact]
        public void CreateWithTrueAnomaly_NegativeEccentricity_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OpmKeplerianElements.CreateWithTrueAnomaly(6778.137, -0.0001, 51.6, 120.0, 90.0, 45.0, 398600.4418));
        }

        [Fact]
        public void CreateWithTrueAnomaly_InclinationNegative_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OpmKeplerianElements.CreateWithTrueAnomaly(6778.137, 0.0001, -1.0, 120.0, 90.0, 45.0, 398600.4418));
        }

        [Fact]
        public void CreateWithTrueAnomaly_InclinationOver180_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OpmKeplerianElements.CreateWithTrueAnomaly(6778.137, 0.0001, 181.0, 120.0, 90.0, 45.0, 398600.4418));
        }

        [Fact]
        public void CreateWithTrueAnomaly_NegativeGM_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OpmKeplerianElements.CreateWithTrueAnomaly(6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, -398600.4418));
        }

        [Fact]
        public void CreateWithTrueAnomaly_ZeroGM_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                OpmKeplerianElements.CreateWithTrueAnomaly(6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, 0.0));
        }

        #endregion

        #region AnomalyDegrees Tests

        [Fact]
        public void AnomalyDegrees_WithTrueAnomaly_ReturnsTrueAnomaly()
        {
            var kep = OpmKeplerianElements.CreateWithTrueAnomaly(
                6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, 398600.4418);

            Assert.Equal(45.0, kep.AnomalyDegrees, 4);
        }

        [Fact]
        public void AnomalyDegrees_WithMeanAnomaly_ReturnsMeanAnomaly()
        {
            var kep = OpmKeplerianElements.CreateWithMeanAnomaly(
                6778.137, 0.0001, 51.6, 120.0, 90.0, 60.0, 398600.4418);

            Assert.Equal(60.0, kep.AnomalyDegrees, 4);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ContainsRelevantInfo()
        {
            var kep = OpmKeplerianElements.CreateWithTrueAnomaly(
                6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, 398600.4418);

            var result = kep.ToString();

            Assert.Contains("OpmKeplerianElements", result);
            Assert.Contains("6778", result);
            Assert.Contains("TA", result);
        }

        [Fact]
        public void ToString_WithMeanAnomaly_ContainsMA()
        {
            var kep = OpmKeplerianElements.CreateWithMeanAnomaly(
                6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, 398600.4418);

            var result = kep.ToString();

            Assert.Contains("MA", result);
        }

        #endregion

        #region Comments Tests

        [Fact]
        public void CreateWithTrueAnomaly_WithComments_StoresComments()
        {
            var comments = new[] { "Comment 1", "Comment 2" };
            var kep = OpmKeplerianElements.CreateWithTrueAnomaly(
                6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, 398600.4418, comments);

            Assert.Equal(2, kep.Comments.Count);
            Assert.Equal("Comment 1", kep.Comments[0]);
        }

        [Fact]
        public void CreateWithTrueAnomaly_NullComments_UsesEmptyArray()
        {
            var kep = OpmKeplerianElements.CreateWithTrueAnomaly(
                6778.137, 0.0001, 51.6, 120.0, 90.0, 45.0, 398600.4418, null);

            Assert.NotNull(kep.Comments);
            Assert.Empty(kep.Comments);
        }

        #endregion

        #region Non-finite values (#708)

        public static TheoryData<string, double> NonFiniteRangedParameters()
        {
            var data = new TheoryData<string, double>();
            foreach (var parameter in new[] { "semiMajorAxis", "eccentricity", "inclination", "gm" })
            {
                data.Add(parameter, double.NaN);
                data.Add(parameter, double.PositiveInfinity);
                data.Add(parameter, double.NegativeInfinity);
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(NonFiniteRangedParameters))]
        public void Create_NonFiniteRangedParameter_ThrowsArgumentOutOfRangeException(string parameter, double value)
        {
            // e >= 0, i in [0, 180], GM > 0 and a finite a of the sign of 1 - e (nonNegativeDouble, inclinationRange and
            // positiveDouble of ndmxml-4.0.0-common-4.0.xsd): NaN, and +Infinity for e, GM and a, passed the checks before
            double V(string name, double nominal) => name == parameter ? value : nominal;
            foreach (var create in new Func<OpmKeplerianElements>[]
                     {
                         () => OpmKeplerianElements.CreateWithTrueAnomaly(V("semiMajorAxis", 6778.137), V("eccentricity", 0.0001),
                             V("inclination", 51.6), 120.0, 90.0, 45.0, V("gm", 398600.4418)),
                         () => OpmKeplerianElements.CreateWithMeanAnomaly(V("semiMajorAxis", 6778.137), V("eccentricity", 0.0001),
                             V("inclination", 51.6), 120.0, 90.0, 45.0, V("gm", 398600.4418))
                     })
            {
                var ex = Assert.Throws<ArgumentOutOfRangeException>(() => create());
                Assert.Equal(parameter, ex.ParamName);
            }
        }

        [Fact]
        public void Create_HyperbolicWithNonFiniteSemiMajorAxis_Throws()
        {
            // e > 1: a negative and finite; -Infinity and NaN are not
            foreach (var a in new[] { double.NaN, double.NegativeInfinity })
            {
                var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                    OpmKeplerianElements.CreateWithTrueAnomaly(a, 1.5, 51.6, 120.0, 90.0, 10.0, 398600.4418));
                Assert.Equal("semiMajorAxis", ex.ParamName);
            }

            Assert.Equal(-20000.0, OpmKeplerianElements.CreateWithTrueAnomaly(-20000.0, 1.5, 51.6, 120.0, 90.0, 10.0, 398600.4418).SemiMajorAxis);
        }

        #endregion

        #region Angles (#708)

        /// <summary>
        /// The four angles of an OPM, RA_OF_ASC_NODE, ARG_OF_PERICENTER, TRUE_ANOMALY and MEAN_ANOMALY, are ndm:angleType
        /// in ndmxml-4.0.0-opm-3.0.xsd, whose angleRange (ndmxml-4.0.0-common-4.0.xsd) is minInclusive -360.0,
        /// maxExclusive 360.0: 360, an angle below -360, NaN (in no range) and ±Infinity are outside it, for each angle.
        /// </summary>
        public static TheoryData<string, double> AnglesOutOfRange()
        {
            var data = new TheoryData<string, double>();
            foreach (var parameter in new[] { "raan", "aop", "trueAnomaly", "meanAnomaly" })
            {
                foreach (var value in new[] { 360.0, 1000.0, -360.5, -720.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                {
                    data.Add(parameter, value);
                }
            }

            return data;
        }

        private static OpmKeplerianElements CreateWithAngle(string parameter, double value)
        {
            double V(string name, double nominal) => name == parameter ? value : nominal;
            return parameter == "meanAnomaly"
                ? OpmKeplerianElements.CreateWithMeanAnomaly(6778.137, 0.001, 51.6, V("raan", 120.0), V("aop", 90.0), value, 398600.4418)
                : OpmKeplerianElements.CreateWithTrueAnomaly(6778.137, 0.001, 51.6, V("raan", 120.0), V("aop", 90.0), V("trueAnomaly", 45.0), 398600.4418);
        }

        [Theory]
        [MemberData(nameof(AnglesOutOfRange))]
        public void Create_AngleOutOfSchemaRange_ThrowsArgumentOutOfRangeException(string parameter, double value)
        {
            // Before #708: built, no angle being checked, as MeanElements checks those of an OMM
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => CreateWithAngle(parameter, value));
            Assert.Equal(parameter, ex.ParamName);
            Assert.Contains("[-360, 360)", ex.Message);
        }

        [Theory]
        [InlineData("raan")]
        [InlineData("aop")]
        [InlineData("trueAnomaly")]
        [InlineData("meanAnomaly")]
        public void Create_AngleAtSchemaBounds_StoresTheValueGiven(string parameter)
        {
            // -360 (minInclusive), -0, the largest double below 360, and a negative angle: inside angleRange, stored bit for bit
            foreach (var value in new[] { -360.0, -0.0, -10.0, 359.99999999999994 })
            {
                var elements = CreateWithAngle(parameter, value);
                double stored = parameter switch
                {
                    "raan" => elements.RightAscensionOfAscendingNode,
                    "aop" => elements.ArgumentOfPericenter,
                    "trueAnomaly" => elements.TrueAnomaly!.Value,
                    _ => elements.MeanAnomaly!.Value
                };
                Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(stored));
            }

            // The literal above is the largest double below 360 (IEEE 754: 360 - 2^-44)
            Assert.Equal(System.Math.BitDecrement(360.0), 359.99999999999994);
            Assert.Equal(360.0 - System.Math.Pow(2, -44), 359.99999999999994);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void FromKeplerianElements_AngleAtTwoPi_WritesItsValueInZeroTo360(bool useTrueAnomaly)
        {
            // KeplerianElements normalizes its angles to [0, 2π) by x % 2π + 2π for x < 0, which for -1e-17 rad gives 2π
            // exactly (1e-17 is below half the ulp of 2π, 4.4e-16), that is 360° (2π × 180/π rounds to 360): an angle
            // outside angleRange, which the elements' range check (#708) refuses. FromKeplerianElements writes each angle as
            // its value in [0, 360): 0 for 2π. Before #708 it wrote 360, which the schema rejects
            var earth = new IO.Astrodynamics.Body.CelestialBody(IO.Astrodynamics.SolarSystemObjects.PlanetsAndMoons.Earth);
            var epoch = IO.Astrodynamics.TimeSystem.Time.J2000Tdb;
            var kep = new IO.Astrodynamics.OrbitalParameters.KeplerianElements(6778137.0, 0.001, 0.9, -1e-17, -1e-17, -1e-17,
                earth, epoch, IO.Astrodynamics.Frames.Frame.ICRF);
            Assert.Equal(IO.Astrodynamics.Constants.TwoPi, kep.AscendingNode());
            Assert.Equal(360.0, kep.AscendingNode() * IO.Astrodynamics.Constants.Rad2Deg);

            var elements = OpmKeplerianElements.FromKeplerianElements(kep, useTrueAnomaly);
            Assert.Equal(0.0, elements.RightAscensionOfAscendingNode);
            Assert.Equal(0.0, elements.ArgumentOfPericenter);
            Assert.InRange(elements.AnomalyDegrees, 0.0, System.Math.BitDecrement(360.0));
            Assert.True(elements.AnomalyDegrees < 1e-9 || elements.AnomalyDegrees > 360.0 - 1e-9, $"anomaly {elements.AnomalyDegrees}");

            // An angle inside [0, 360) is written as before, bit for bit: x × 180/π
            var inside = OpmKeplerianElements.FromKeplerianElements(new IO.Astrodynamics.OrbitalParameters.KeplerianElements(
                6778137.0, 0.001, 0.9, 1.0, 2.0, 3.0, earth, epoch, IO.Astrodynamics.Frames.Frame.ICRF), useTrueAnomaly);
            Assert.Equal(1.0 * IO.Astrodynamics.Constants.Rad2Deg, inside.RightAscensionOfAscendingNode);
            Assert.Equal(2.0 * IO.Astrodynamics.Constants.Rad2Deg, inside.ArgumentOfPericenter);
            Assert.Equal(0.9 * IO.Astrodynamics.Constants.Rad2Deg, inside.Inclination);
        }

        #endregion
    }
}
