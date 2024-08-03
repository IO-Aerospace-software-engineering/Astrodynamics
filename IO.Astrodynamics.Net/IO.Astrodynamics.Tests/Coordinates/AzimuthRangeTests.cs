using IO.Astrodynamics.Coordinates;
using Xunit;

namespace IO.Astrodynamics.Tests.Coordinates
{
    public class AzimuthRangeTests
    {
        [Fact]
        public void Create()
        {
            AzimuthRange az = new AzimuthRange(1.0, 3.0);
            Assert.Equal(1.0, az.Start);
            Assert.Equal(3.0, az.End);
            Assert.Equal(2.0, az.Span);
        }

        [Fact]
        public void IsInRange()
        {
            AzimuthRange az = new AzimuthRange(1.0, 3.0);
            Assert.True(az.IsInRange(1.0));
            Assert.True(az.IsInRange(2.0));
            Assert.True(az.IsInRange(3.0));
            Assert.False(az.IsInRange(-1.0));
            Assert.False(az.IsInRange(4.0));

            AzimuthRange az2 = new AzimuthRange(6.0, 1.0);
            Assert.False(az2.IsInRange(3.0));
            Assert.False(az2.IsInRange(5.0));
            Assert.True(az2.IsInRange(0.1));

            AzimuthRange az3 = new AzimuthRange(1.0, 6.0);
            Assert.True(az3.IsInRange(3.0));
            Assert.True(az3.IsInRange(5.0));
            Assert.False(az3.IsInRange(0.1));

        }

        [Fact]
        public void IsIntersected()
        {
            AzimuthRange az = new AzimuthRange(1.0, 3.0);
            AzimuthRange az2 = new AzimuthRange(2.0, 4.0);
            Assert.True(az.IsIntersected(az2));

            AzimuthRange az3 = new AzimuthRange(4.0,0.5);
            Assert.False(az.IsIntersected(az3));

            AzimuthRange az4 = new AzimuthRange(-1.0, 1.5);
            Assert.True(az4.IsIntersected(az));
            Assert.True(az4.IsIntersected(az3));
            Assert.False(az4.IsIntersected(new AzimuthRange(-3.0,-2.0)));

        }
    }
}
