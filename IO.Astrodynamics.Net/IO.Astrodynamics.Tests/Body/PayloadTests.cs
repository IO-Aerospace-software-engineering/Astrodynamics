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
            Assert.Throws<ArgumentException>(() => new Payload("pl", 0.0, "sn"));
            Assert.Throws<ArgumentException>(() => new Payload("pl", 1000.0, ""));
        }

        [Fact]
        public void PayloadEquality()
        {
            Payload payload = new Payload("pl", 1000.0, "sn");
            Payload payload2 = new Payload("pl", 1000.0, "sn");
            Payload payload3 = new Payload("pl1", 1000.0, "sn");
            Assert.Equal(payload, payload2);
            Assert.False(payload == null);
            Assert.False(payload == payload3);
            Assert.True(payload != payload3);
            Assert.True(payload.Equals(payload2));
            Assert.False(payload.Equals(null));
            Assert.False(payload.Equals((object)null));
            Assert.True(payload.Equals((object)payload2));
        }
    }
}