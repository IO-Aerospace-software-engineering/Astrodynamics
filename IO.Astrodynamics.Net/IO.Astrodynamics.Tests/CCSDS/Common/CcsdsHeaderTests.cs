// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.CCSDS.Common;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.Common
{
    public class CcsdsHeaderTests
    {
        [Fact]
        public void Constructor_WithRequiredFields_Succeeds()
        {
            var creationDate = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
            var header = new CcsdsHeader(creationDate, "NASA");

            Assert.Equal(creationDate, header.CreationDate);
            Assert.Equal("NASA", header.Originator);
            Assert.Empty(header.Comments);
            Assert.Null(header.Classification);
            Assert.Null(header.MessageId);
        }

        [Fact]
        public void Constructor_WithAllFields_Succeeds()
        {
            var creationDate = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
            var comments = new List<string> { "Test comment 1", "Test comment 2" };
            var header = new CcsdsHeader(creationDate, "ESA", comments)
            {
                Classification = "UNCLASSIFIED",
                MessageId = "MSG-001"
            };

            Assert.Equal(creationDate, header.CreationDate);
            Assert.Equal("ESA", header.Originator);
            Assert.Equal(2, header.Comments.Count);
            Assert.Equal("Test comment 1", header.Comments[0]);
            Assert.Equal("UNCLASSIFIED", header.Classification);
            Assert.Equal("MSG-001", header.MessageId);
        }

        [Fact]
        public void Constructor_WithNullOriginator_ThrowsArgumentNullException()
        {
            var creationDate = DateTime.UtcNow;
            Assert.Throws<ArgumentNullException>(() => new CcsdsHeader(creationDate, null));
        }

        [Fact]
        public void Constructor_WithEmptyOriginator_ThrowsArgumentException()
        {
            var creationDate = DateTime.UtcNow;
            Assert.Throws<ArgumentException>(() => new CcsdsHeader(creationDate, ""));
        }

        [Fact]
        public void Constructor_WithWhitespaceOriginator_ThrowsArgumentException()
        {
            var creationDate = DateTime.UtcNow;
            Assert.Throws<ArgumentException>(() => new CcsdsHeader(creationDate, "   "));
        }

        [Fact]
        public void Constructor_WithNullComments_ThrowsArgumentNullException()
        {
            var creationDate = DateTime.UtcNow;
            Assert.Throws<ArgumentNullException>(() => new CcsdsHeader(creationDate, "NASA", null));
        }

        [Fact]
        public void CreateDefault_ReturnsValidHeader()
        {
            var header = CcsdsHeader.CreateDefault();

            Assert.Equal("IO.Astrodynamics", header.Originator);
            Assert.True((DateTime.UtcNow - header.CreationDate).TotalSeconds < 5);
            Assert.Empty(header.Comments);
        }

        [Fact]
        public void Create_WithOriginator_ReturnsValidHeader()
        {
            var header = CcsdsHeader.Create("JAXA");

            Assert.Equal("JAXA", header.Originator);
            Assert.True((DateTime.UtcNow - header.CreationDate).TotalSeconds < 5);
        }

        [Fact]
        public void ToString_ReturnsFormattedString()
        {
            var creationDate = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
            var header = new CcsdsHeader(creationDate, "NASA");

            var result = header.ToString();

            Assert.Contains("CreationDate=", result);
            Assert.Contains("Originator=NASA", result);
        }
    }
}
