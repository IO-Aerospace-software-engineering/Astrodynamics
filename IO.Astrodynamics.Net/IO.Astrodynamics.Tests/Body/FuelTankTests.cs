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
            FuelTank fuelTank = new FuelTank("ft", 4000.0, 2000.0, "model", "sn1");
            Assert.Equal("ft", fuelTank.Name);
            Assert.Equal("model", fuelTank.Model);
            Assert.Equal(4000.0, fuelTank.Capacity);
            Assert.Equal(2000.0, fuelTank.InitialQuantity);
            Assert.Equal("sn1", fuelTank.SerialNumber);
        }

        [Fact]
        public void CreateInvalid()
        {
            Assert.Throws<ArgumentException>(() => new FuelTank("", 4000.0, 2000.0, "model", "sn1"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FuelTank("ft", 0.0, 2000.0, "model", "sn1"));
            // The model and the serial number are informational since 11.0.0 (decision 76 of #668): any value, null included
            Assert.Equal(("", ""), (new FuelTank("ft", 4000.0, 2000.0, "", "").Model, new FuelTank("ft", 4000.0, 2000.0, "", "").SerialNumber));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FuelTank("ft", 4000.0, -10.0, "model", "sn1"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FuelTank("ft", -4000.0, 10.0, "model", "sn1"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FuelTank("ft", 4000.0, 5000.0, "model", "sn1"));
        }

        // Reference equality since 11.0.0 (decision 76 of #668): a twin of the same values is another tank
        [Fact]
        public void Equality()
        {
            FuelTank fuelTank = new FuelTank("ft", 4000.0, 2000.0, "model", "sn1");
            FuelTank fuelTank2 = new FuelTank("ft", 4000.0, 2000.0, "model", "sn1");
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