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
            FuelTank ft = new FuelTank( "ft", 1000.0, 100.0, "ftmodel", "sn0");
            Engine engine = new Engine("eng", 350.0, 50.0, ft, "model", "sn1");
            Assert.Equal("eng", engine.Name);
            Assert.Equal("model", engine.Model);
            Assert.Equal(350.0, engine.Isp);
            Assert.Equal(50.0, engine.FuelFlow);
            Assert.Equal(ft, engine.FuelTank);
            Assert.Equal("model", engine.Model);
            Assert.Equal("sn1", engine.SerialNumber);
        }

        [Fact]
        public void CreateInvalid()
        {
            FuelTank ft = new FuelTank("ft", 1000.0, 100.0, "ftmodel", "sn0");
            Assert.Throws<ArgumentException>(() => new Engine("", 350.0, 50.0, ft, "model", "sn1"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Engine("eng", 0.0, 50.0, ft, "model", "sn1"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Engine("eng", 350.0, 0.0, ft, "model", "sn1"));
            // The model and the serial number are informational since 11.0.0 (decision 76 of #668): any value, null included
            Assert.Null(new Engine("eng", 350.0, 50.0, ft).Model);
            Assert.Throws<ArgumentNullException>(() => new Engine("eng", 350.0, 50.0, null, "model", "sn1"));
        }

        [Fact]
        public void Equality()
        {
            FuelTank ft = new FuelTank("ft", 1000.0, 100.0, "ftmodel", "sn0");
            Engine engine = new Engine("eng", 350.0, 50.0, ft, "model", "sn1");
            
            // Reference equality since 11.0.0 (decision 76 of #668): a twin of the same values is another engine
            Engine engine2 = new Engine("eng", 350.0, 50.0, ft, "model", "sn1");
            
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