using System;
using IO.Astrodynamics.Body.Spacecraft;
using Xunit;

namespace IO.Astrodynamics.Tests.Body
{
    public class EngineTests
    {
        [Fact]
        public void Create()
        {
            FuelTank ft = new FuelTank( "ft", "ftmodel", "sn0", 1000.0, 100.0);
            Engine engine = new Engine("eng", "model", "sn1", 350.0, 50.0, ft);
            Assert.Equal("eng", engine.Name);
            Assert.Equal("model", engine.Model);
            Assert.Equal(350.0, engine.ISP);
            Assert.Equal(50.0, engine.FuelFlow);
            Assert.Equal(ft, engine.FuelTank);
            Assert.Equal("model", engine.Model);
            Assert.Equal("sn1", engine.SerialNumber);
        }

        [Fact]
        public void CreateInvalid()
        {
            FuelTank ft = new FuelTank("ft", "ftmodel", "sn0", 1000.0, 100.0);
            Assert.Throws<ArgumentException>(() => new Engine("", "model", "sn1", 350.0, 50.0, ft));
            Assert.Throws<ArgumentException>(() => new Engine("eng", "", "sn1", 350.0, 50.0, ft));
            Assert.Throws<ArgumentException>(() => new Engine("eng", "model", "sn1", 0.0, 50.0, ft));
            Assert.Throws<ArgumentException>(() => new Engine("eng", "model", "sn1", 350.0, 0.0, ft));
            Assert.Throws<ArgumentException>(() => new Engine("eng", "model", "", 350.0, 50.0, ft));
            Assert.Throws<ArgumentNullException>(() => new Engine("eng", "model", "sn1", 350.0, 50.0, null));
        }

        [Fact]
        public void Equality()
        {
            FuelTank ft = new FuelTank("ft", "ftmodel", "sn0", 1000.0, 100.0);
            Engine engine = new Engine("eng", "model", "sn1", 350.0, 50.0, ft);
            
            FuelTank ft2 = new FuelTank("ft2", "ftmodel2", "sn20", 1000.0, 100.0);
            Engine engine2 = new Engine("eng2", "model2", "sn21", 350.0, 50.0, ft2);
            
            Assert.True(engine!=engine2);
            Assert.True(engine.Equals(engine));
            Assert.True(engine.Equals((object)engine));
            Assert.True(engine!=engine2);
            Assert.False(engine.Equals(engine2));
            Assert.False(engine.Equals((object)engine2));
            Assert.False(engine.Equals((object)null));
            Assert.False(engine.Equals(null));
        }
    }
}