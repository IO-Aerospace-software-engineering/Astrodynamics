// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.CCSDS.Common.Enums;
using IO.Astrodynamics.CCSDS.OPM;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.OPM
{
    public class OpmMetadataTests
    {
        #region Constructor Tests

        [Fact]
        public void Constructor_ValidParameters_Succeeds()
        {
            var metadata = new OpmMetadata(
                objectName: "ISS (ZARYA)",
                objectId: "1998-067A",
                centerName: "EARTH",
                referenceFrame: "ICRF",
                timeSystem: "UTC");

            Assert.Equal("ISS (ZARYA)", metadata.ObjectName);
            Assert.Equal("1998-067A", metadata.ObjectId);
            Assert.Equal("EARTH", metadata.CenterName);
            Assert.Equal("ICRF", metadata.ReferenceFrame);
            Assert.Equal("UTC", metadata.TimeSystem);
        }

        [Fact]
        public void Constructor_NullObjectName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new OpmMetadata(null, "1998-067A", "EARTH", "ICRF", "UTC"));
        }

        [Fact]
        public void Constructor_EmptyObjectName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new OpmMetadata("", "1998-067A", "EARTH", "ICRF", "UTC"));
        }

        [Fact]
        public void Constructor_NullObjectId_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new OpmMetadata("ISS", null, "EARTH", "ICRF", "UTC"));
        }

        [Fact]
        public void Constructor_NullCenterName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new OpmMetadata("ISS", "1998-067A", null, "ICRF", "UTC"));
        }

        [Fact]
        public void Constructor_NullReferenceFrame_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new OpmMetadata("ISS", "1998-067A", "EARTH", null, "UTC"));
        }

        [Fact]
        public void Constructor_NullTimeSystem_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", null));
        }

        #endregion

        #region Reference Frame Enum Tests

        [Fact]
        public void ReferenceFrameEnum_ICRF_ReturnsEnum()
        {
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");

            Assert.True(metadata.ReferenceFrameEnum.HasValue);
            Assert.Equal(CcsdsReferenceFrame.ICRF, metadata.ReferenceFrameEnum.Value);
        }

        [Fact]
        public void ReferenceFrameEnum_TEME_ReturnsEnum()
        {
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "TEME", "UTC");

            Assert.True(metadata.ReferenceFrameEnum.HasValue);
            Assert.Equal(CcsdsReferenceFrame.TEME, metadata.ReferenceFrameEnum.Value);
        }

        [Fact]
        public void ReferenceFrameEnum_CustomFrame_ReturnsNull()
        {
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "CUSTOM_FRAME", "UTC");

            Assert.False(metadata.ReferenceFrameEnum.HasValue);
        }

        #endregion

        #region Time System Enum Tests

        [Fact]
        public void TimeSystemEnum_UTC_ReturnsEnum()
        {
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");

            Assert.True(metadata.TimeSystemEnum.HasValue);
            Assert.Equal(CcsdsTimeSystem.UTC, metadata.TimeSystemEnum.Value);
        }

        [Fact]
        public void TimeSystemEnum_TDB_ReturnsEnum()
        {
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "TDB");

            Assert.True(metadata.TimeSystemEnum.HasValue);
            Assert.Equal(CcsdsTimeSystem.TDB, metadata.TimeSystemEnum.Value);
        }

        #endregion

        #region Optional Fields Tests

        [Fact]
        public void ReferenceFrameEpoch_NotSet_ReturnsNull()
        {
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");

            Assert.Null(metadata.ReferenceFrameEpoch);
        }

        [Fact]
        public void ReferenceFrameEpoch_Set_ReturnsValue()
        {
            var epoch = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC",
                referenceFrameEpoch: epoch);

            Assert.True(metadata.ReferenceFrameEpoch.HasValue);
            Assert.Equal(epoch, metadata.ReferenceFrameEpoch.Value);
        }

        [Fact]
        public void Comments_NotSet_ReturnsEmptyCollection()
        {
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC");

            Assert.NotNull(metadata.Comments);
            Assert.Empty(metadata.Comments);
        }

        [Fact]
        public void Comments_Set_ReturnsComments()
        {
            var comments = new[] { "Comment 1", "Comment 2" };
            var metadata = new OpmMetadata("ISS", "1998-067A", "EARTH", "ICRF", "UTC",
                comments: comments);

            Assert.Equal(2, metadata.Comments.Count);
            Assert.Equal("Comment 1", metadata.Comments[0]);
        }

        #endregion

        #region ToString Tests

        [Fact]
        public void ToString_ContainsRelevantInfo()
        {
            var metadata = new OpmMetadata("ISS (ZARYA)", "1998-067A", "EARTH", "ICRF", "UTC");

            var result = metadata.ToString();

            Assert.Contains("ISS", result);
            Assert.Contains("1998-067A", result);
        }

        #endregion
    }
}
