// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.CCSDS.OPM;
using Xunit;

// Use aliases to avoid namespace conflicts with IO.Astrodynamics.Tests.* namespaces
using AstroFrame = IO.Astrodynamics.Frames.Frame;
using AstroTime = IO.Astrodynamics.TimeSystem.Time;
using AstroTimeFrame = IO.Astrodynamics.TimeSystem.TimeFrame;
using AstroCelestialBody = IO.Astrodynamics.Body.CelestialBody;
using AstroVector3 = IO.Astrodynamics.Math.Vector3;
using AstroStateVector = IO.Astrodynamics.OrbitalParameters.StateVector;

namespace IO.Astrodynamics.Tests.CCSDS.OPM
{
    public class OpmStateVectorTests
    {
        private readonly DateTime _testEpoch = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        private readonly AstroCelestialBody _earth;

        public OpmStateVectorTests()
        {
            TestKernels.EnsureSolarSystem();
            _earth = new AstroCelestialBody(399, AstroFrame.ICRF, new AstroTime(_testEpoch, AstroTimeFrame.UTCFrame));
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ValidParameters_Succeeds()
        {
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 100.0, 50.0, 1.0, 7.5, 0.5);

            Assert.Equal(_testEpoch, stateVector.Epoch);
            Assert.Equal(6778.0, stateVector.X);
            Assert.Equal(100.0, stateVector.Y);
            Assert.Equal(50.0, stateVector.Z);
            Assert.Equal(1.0, stateVector.XDot);
            Assert.Equal(7.5, stateVector.YDot);
            Assert.Equal(0.5, stateVector.ZDot);
        }

        [Fact]
        public void Constructor_WithComments_StoresComments()
        {
            var comments = new[] { "Comment 1", "Comment 2" };
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0, comments);

            Assert.Equal(2, stateVector.Comments.Count);
            Assert.Equal("Comment 1", stateVector.Comments[0]);
            Assert.Equal("Comment 2", stateVector.Comments[1]);
        }

        [Fact]
        public void Constructor_NullComments_UsesEmptyArray()
        {
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0, null);

            Assert.NotNull(stateVector.Comments);
            Assert.Empty(stateVector.Comments);
        }

        #endregion

        #region ToStateVector Tests

        [Fact]
        public void ToStateVector_ConvertsKmToMeters()
        {
            // OPM uses km, km/s
            var opmSv = new OpmStateVector(_testEpoch, 6778.137, 100.0, 50.0, 1.0, 7.5, 0.5);

            var stateVector = opmSv.ToStateVector(_earth, AstroFrame.ICRF);

            // Should convert to meters
            Assert.Equal(6778137.0, stateVector.Position.X, 0);
            Assert.Equal(100000.0, stateVector.Position.Y, 0);
            Assert.Equal(50000.0, stateVector.Position.Z, 0);
            Assert.Equal(1000.0, stateVector.Velocity.X, 0);
            Assert.Equal(7500.0, stateVector.Velocity.Y, 0);
            Assert.Equal(500.0, stateVector.Velocity.Z, 0);
        }

        [Fact]
        public void ToStateVector_SetsCorrectObserver()
        {
            var opmSv = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0);

            var stateVector = opmSv.ToStateVector(_earth, AstroFrame.ICRF);

            Assert.Equal(_earth.NaifId, ((AstroCelestialBody)stateVector.Observer).NaifId);
        }

        [Fact]
        public void ToStateVector_SetsCorrectFrame()
        {
            var opmSv = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0);

            var stateVector = opmSv.ToStateVector(_earth, AstroFrame.ICRF);

            Assert.Equal("J2000", stateVector.Frame.Name);
        }

        [Fact]
        public void ToStateVector_SetsCorrectEpoch()
        {
            var opmSv = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 0.0, 7.5, 0.0);

            var stateVector = opmSv.ToStateVector(_earth, AstroFrame.ICRF);

            Assert.Equal(_testEpoch, stateVector.Epoch.DateTime);
        }

        #endregion

        #region FromStateVector Tests

        [Fact]
        public void FromStateVector_ConvertsMetersToKm()
        {
            // Framework uses meters
            var position = new AstroVector3(6778137.0, 100000.0, 50000.0);
            var velocity = new AstroVector3(1000.0, 7500.0, 500.0);
            var epoch = new AstroTime(_testEpoch, AstroTimeFrame.UTCFrame);
            var stateVector = new AstroStateVector(position, velocity, _earth, epoch, AstroFrame.ICRF);

            var opmSv = OpmStateVector.FromStateVector(stateVector);

            // Should convert to km
            Assert.Equal(6778.137, opmSv.X, 3);
            Assert.Equal(100.0, opmSv.Y, 3);
            Assert.Equal(50.0, opmSv.Z, 3);
            Assert.Equal(1.0, opmSv.XDot, 3);
            Assert.Equal(7.5, opmSv.YDot, 3);
            Assert.Equal(0.5, opmSv.ZDot, 3);
        }

        [Fact]
        public void FromStateVector_PreservesEpoch()
        {
            var position = new AstroVector3(6778137.0, 0.0, 0.0);
            var velocity = new AstroVector3(0.0, 7500.0, 0.0);
            var epoch = new AstroTime(_testEpoch, AstroTimeFrame.UTCFrame);
            var stateVector = new AstroStateVector(position, velocity, _earth, epoch, AstroFrame.ICRF);

            var opmSv = OpmStateVector.FromStateVector(stateVector);

            Assert.Equal(_testEpoch, opmSv.Epoch);
        }

        [Fact]
        public void FromStateVector_NullStateVector_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => OpmStateVector.FromStateVector(null));
        }

        [Fact]
        public void FromStateVector_WithComments_StoresComments()
        {
            var position = new AstroVector3(6778137.0, 0.0, 0.0);
            var velocity = new AstroVector3(0.0, 7500.0, 0.0);
            var epoch = new AstroTime(_testEpoch, AstroTimeFrame.UTCFrame);
            var stateVector = new AstroStateVector(position, velocity, _earth, epoch, AstroFrame.ICRF);
            var comments = new[] { "Test comment" };

            var opmSv = OpmStateVector.FromStateVector(stateVector, comments);

            Assert.Single(opmSv.Comments);
            Assert.Equal("Test comment", opmSv.Comments[0]);
        }

        #endregion

        #region RoundTrip Tests

        [Fact]
        public void RoundTrip_FromStateVectorToStateVector_PreservesData()
        {
            var position = new AstroVector3(6778137.0, 100000.0, 50000.0);
            var velocity = new AstroVector3(1000.0, 7500.0, 500.0);
            var epoch = new AstroTime(_testEpoch, AstroTimeFrame.UTCFrame);
            var original = new AstroStateVector(position, velocity, _earth, epoch, AstroFrame.ICRF);

            var opmSv = OpmStateVector.FromStateVector(original);
            var roundTripped = opmSv.ToStateVector(_earth, AstroFrame.ICRF);

            Assert.Equal(original.Position.X, roundTripped.Position.X, 0);
            Assert.Equal(original.Position.Y, roundTripped.Position.Y, 0);
            Assert.Equal(original.Position.Z, roundTripped.Position.Z, 0);
            Assert.Equal(original.Velocity.X, roundTripped.Velocity.X, 0);
            Assert.Equal(original.Velocity.Y, roundTripped.Velocity.Y, 0);
            Assert.Equal(original.Velocity.Z, roundTripped.Velocity.Z, 0);
        }

        #endregion

        #region Property Tests

        [Fact]
        public void PositionKm_ReturnsCorrectVector()
        {
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 100.0, 50.0, 0.0, 7.5, 0.0);

            var posKm = stateVector.PositionKm;

            Assert.Equal(6778.0, posKm.X);
            Assert.Equal(100.0, posKm.Y);
            Assert.Equal(50.0, posKm.Z);
        }

        [Fact]
        public void VelocityKmPerSec_ReturnsCorrectVector()
        {
            var stateVector = new OpmStateVector(_testEpoch, 6778.0, 0.0, 0.0, 1.0, 7.5, 0.5);

            var velKmS = stateVector.VelocityKmPerSec;

            Assert.Equal(1.0, velKmS.X);
            Assert.Equal(7.5, velKmS.Y);
            Assert.Equal(0.5, velKmS.Z);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ContainsRelevantInfo()
        {
            var stateVector = new OpmStateVector(_testEpoch, 6778.137, 100.0, 50.0, 1.0, 7.5, 0.5);

            var result = stateVector.ToString();

            Assert.Contains("OpmStateVector", result);
            Assert.Contains("6778", result);
            Assert.Contains("7.5", result);
        }

        #endregion

        #region Non-finite values (#717)

        public static TheoryData<string, double> NonFiniteComponents()
        {
            var data = new TheoryData<string, double>();
            foreach (var parameter in new[] { "x", "y", "z", "xDot", "yDot", "zDot" })
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                data.Add(parameter, value);
            return data;
        }

        private static double[] Components(string parameter, double value)
        {
            var names = new[] { "x", "y", "z", "xDot", "yDot", "zDot" };
            var components = new[] { 6778.137, 100.0, -50.0, 0.1, 7.5, 0.2 };
            components[Array.IndexOf(names, parameter)] = value;
            return components;
        }

        [Theory]
        [MemberData(nameof(NonFiniteComponents))]
        public void Constructor_NonFiniteComponent_ThrowsArgumentOutOfRangeException(string parameter, double value)
        {
            // Every component is an xsd:double the library reads as a real number (#708); before, NaN and ±Infinity built
            // a state that Validate() passed, that SaveToFile wrote as NaN, Infinity or -Infinity and that the reader refused
            var c = Components(parameter, value);
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new OpmStateVector(_testEpoch, c[0], c[1], c[2], c[3], c[4], c[5]));
            Assert.Equal(parameter, ex.ParamName);
        }

        [Theory]
        [MemberData(nameof(NonFiniteComponents))]
        public void FromStateVector_NonFiniteComponent_ThrowsArgumentOutOfRangeException(string parameter, double value)
        {
            // A StateVector holds any value; its OPM state refuses a non-finite component (Opm.FromStateVector and
            // Spacecraft.ToOpm build it), where it was written as NaN or ±Infinity
            var c = Components(parameter, value);
            var stateVector = new AstroStateVector(new AstroVector3(c[0] * 1000.0, c[1] * 1000.0, c[2] * 1000.0),
                new AstroVector3(c[3] * 1000.0, c[4] * 1000.0, c[5] * 1000.0), _earth,
                new AstroTime(_testEpoch, AstroTimeFrame.UTCFrame), AstroFrame.ICRF);
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => OpmStateVector.FromStateVector(stateVector));
            Assert.Equal(parameter, ex.ParamName);
        }

        [Fact]
        public void Constructor_LargestFiniteComponents_AreStoredAsGiven()
        {
            // The bounds of the finite doubles, the largest magnitude and the smallest subnormal, are real numbers
            var sv = new OpmStateVector(_testEpoch, double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 0.0, -0.0);
            Assert.Equal(double.MaxValue, sv.X);
            Assert.Equal(-double.MaxValue, sv.Y);
            Assert.Equal(double.Epsilon, sv.Z);
            Assert.Equal(-double.Epsilon, sv.XDot);
            Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0), BitConverter.DoubleToInt64Bits(sv.ZDot));
        }

        #endregion
    }
}
