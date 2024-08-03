using System;
using System.IO;
using System.Threading.Tasks;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using Xunit;

namespace IO.Astrodynamics.Tests.Body
{
    public class ClockTests
    {
        public ClockTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }
        [Fact]
        public void CreateClock()
        {
            Clock clock = new Clock("clk", 256);
            Assert.Equal("clk", clock.Name);
            Assert.Equal((uint)256, clock.Resolution);
        }

        [Fact]
        public async Task WriteClock()
        {
            Clock clock = new Clock("clk", 256);
            Spacecraft spc = new Spacecraft(-1001, "MySpacecraft", 1000.0, 10000.0, clock,
                new StateVector(new Vector3(1.0, 2.0, 3.0), new Vector3(1.0, 2.0, 3.0), TestHelpers.EarthAtJ2000,
                    DateTime.MinValue, Frames.Frame.ICRF));
            await clock.WriteAsync(new FileInfo("clock.tsc"));
            TextReader tr = new StreamReader("clock.tsc");
            var res = await tr.ReadToEndAsync();
            Assert.Equal($"KPL/SCLK{Environment.NewLine}\\begindata{Environment.NewLine}SCLK_KERNEL_ID           = ( @1957-01-01/00:00:00.0 ){Environment.NewLine}SCLK_DATA_TYPE_1001        = ( 1 ){Environment.NewLine}SCLK01_TIME_SYSTEM_1001    = ( 1 ){Environment.NewLine}SCLK01_N_FIELDS_1001       = ( 2 ){Environment.NewLine}SCLK01_MODULI_1001         = ( 4294967296 256 ){Environment.NewLine}SCLK01_OFFSETS_1001        = ( 0 0 ){Environment.NewLine}SCLK01_OUTPUT_DELIM_1001   = ( 2 ){Environment.NewLine}SCLK_PARTITION_START_1001  = ( 0.0000000000000E+00 ){Environment.NewLine}SCLK_PARTITION_END_1001    = ( 2.8147497671065E+14 ){Environment.NewLine}SCLK01_COEFFICIENTS_1001   = ( 0.0000000000000E+00     -1.3569552000000E+09     1.0000000000000E+00 ){Environment.NewLine}\\begintext{Environment.NewLine}", res);
        }

        [Fact]
        public void CreateInvalidClock()
        {
            Assert.Throws<ArgumentException>(() => new Clock("", 256));
            Assert.Throws<ArgumentException>(() => new Clock("clk", 0));
        }

        [Fact]
        public void Equality()
        {
            Clock clock = new Clock("clk", 256);
            Clock clock2 = new Clock("clk", 128);
            Assert.True(clock != clock2);
            Assert.False(clock == clock2);
            Assert.False(clock.Equals(clock2));
            Assert.False(clock.Equals((object)clock2));
            Assert.False(clock.Equals(null));
            Assert.False(clock.Equals((object)null));
        }
    }
}