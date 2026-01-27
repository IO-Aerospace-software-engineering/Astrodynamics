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
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
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
    }
}
