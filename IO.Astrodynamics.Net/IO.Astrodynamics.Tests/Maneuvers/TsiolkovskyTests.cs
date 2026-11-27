using System;
using IO.Astrodynamics.Physics;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers
{
    public class TsiolkovskyTests
    {
        [Fact]
        public void DeltaM()
        {
            Assert.Equal(1000.0, Tsiolkovsky.DeltaM(300.0, 3000.0, 1192.876320728679), 9);
        }

        [Fact]
        public void DeltaT()
        {
            Assert.Equal(TimeSpan.FromSeconds(10.0), Tsiolkovsky.DeltaT(300.0, 3000.0, 100.0, 1192.876320728679));
        }

        [Fact]
        public void DeltaV()
        {
            double deltaV = Tsiolkovsky.DeltaV(300.0, 3000.0, 2000.0);

            Assert.Equal(1192.876320728679, deltaV);
        }
    }
}
