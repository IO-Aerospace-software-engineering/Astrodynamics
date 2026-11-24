using System;
using IO.Astrodynamics.Body.Spacecraft;
using Xunit;

namespace IO.Astrodynamics.Tests.Body
{
    public class PayloadTests
    {
        [Fact]
        public void Create()
        {
            Payload payload = new Payload("pl", 1000.0, "sn");
            Assert.Equal("pl", payload.Name);
            Assert.Equal(1000.0, payload.Mass);
            Assert.Equal("sn", payload.SerialNumber);
        }

        [Fact]
        public void CreateInvalid()
        {
            Assert.Throws<ArgumentException>(() => new Payload("", 1000.0, "sn"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Payload("pl", 0.0, "sn"));
            // The serial number is informational and optional since 11.0.0 (decision 76 of #668)
            Assert.Null(new Payload("pl", 1000.0).SerialNumber);
        }

        // Reference equality since 11.0.0 (decision 76 of #668): two payloads of the same values were equal before
        [Fact]
        public void PayloadEquality()
        {
            Payload payload = new Payload("pl", 1000.0, "sn");
            Payload payload2 = new Payload("pl", 1000.0, "sn");
            Assert.NotEqual(payload, payload2);
            Assert.False(payload == null);
            Assert.True(payload != payload2);
            Assert.False(payload.Equals(payload2));
            Assert.False(payload.Equals(null));
            Assert.True(payload.Equals((object)payload));
        }
    }
}