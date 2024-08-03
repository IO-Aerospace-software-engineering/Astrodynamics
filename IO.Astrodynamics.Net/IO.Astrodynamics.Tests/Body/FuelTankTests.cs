using System;
using IO.Astrodynamics.Body.Spacecraft;
using Xunit;

namespace IO.Astrodynamics.Tests.Body
{
    public class FuelTankTests
    {
        [Fact]
        public void Create()
        {
            FuelTank fuelTank = new FuelTank("ft", "model", "sn1", 4000.0, 2000.0);
            Assert.Equal("ft", fuelTank.Name);
            Assert.Equal("model", fuelTank.Model);
            Assert.Equal(4000.0, fuelTank.Capacity);
            Assert.Equal(2000.0, fuelTank.InitialQuantity);
            Assert.Equal("sn1", fuelTank.SerialNumber);
        }

        [Fact]
        public void CreateInvalid()
        {
            Assert.Throws<ArgumentException>(() => new FuelTank("", "model", "sn1", 4000.0, 2000.0));
            Assert.Throws<ArgumentException>(() => new FuelTank("ft", "", "sn1", 4000.0, 2000.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FuelTank("ft", "model", "sn1", 0.0, 2000.0));
            Assert.Throws<ArgumentException>(() => new FuelTank("ft", "model", "", 4000.0, 2000.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FuelTank("ft", "model", "sn1", 4000.0, -10.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FuelTank("ft", "model", "sn1", -4000.0, 10.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FuelTank("ft", "model", "sn1", 4000.0, 5000.0));
        }

        [Fact]
        public void Equality()
        {
            FuelTank fuelTank = new FuelTank("ft", "model", "sn1", 4000.0, 2000.0);
            FuelTank fuelTank2 = new FuelTank("ft2", "model2", "sn2", 4000.0, 2000.0);
            Assert.True(fuelTank != fuelTank2);
            Assert.False(fuelTank.Equals(fuelTank2));
            Assert.True(fuelTank.Equals(fuelTank));
            Assert.False(fuelTank.Equals((object)fuelTank2));
            Assert.True(fuelTank.Equals((object)fuelTank));
            Assert.False(fuelTank.Equals((object)null));
            Assert.False(fuelTank.Equals(null));
            Assert.False(fuelTank==fuelTank2);
        }
    }
}