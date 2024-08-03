using IO.Astrodynamics.Math;
using Xunit;

namespace IO.Astrodynamics.Tests.Math
{
    public class LegendreTests
    {
        public LegendreTests()
        {
            API.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        }

        [Fact]
        public void AssociatedLegendrePolynomials()
        {
            var expectedP00 = 1.0;
            var expectedP10 = 0.5;
            var expectedP22 = 3 * (1 - 0.5 * 0.5);
            var expectedP44 = 105.0 * ((1.0 - 0.5 * 0.5) * (1.0 - 0.5 * 0.5));

            Assert.Equal(expectedP00, LegendreFunctions.AssociatedLegendre(0, 0, 0.5));
            Assert.Equal(expectedP10, LegendreFunctions.AssociatedLegendre(1, 0, 0.5));
            Assert.Equal(expectedP22, LegendreFunctions.AssociatedLegendre(2, 2, 0.5));
            Assert.Equal(expectedP44, LegendreFunctions.AssociatedLegendre(4, 4, 0.5),12);
        }

        [Fact]
        public void NormalizedAssociatedLegendrePolynomials()
        {
            var expectedP00 = 0.5 * System.Math.Sqrt(1.0 / System.Math.PI);
            var expectedP10 = 0.5 * (System.Math.Sqrt(3.0 / System.Math.PI)*System.Math.Cos(0.5));
            var expectedP22 = 0.25 * System.Math.Sqrt(15.0 / (2 * System.Math.PI)) * System.Math.Pow(System.Math.Sin(0.5), 2.0);

            Assert.Equal(expectedP00, LegendreFunctions.NormalizedAssociatedLegendre(0, 0, 0.5));
            Assert.Equal(expectedP10, LegendreFunctions.NormalizedAssociatedLegendre(1, 0, 0.5));
            Assert.Equal(expectedP22, LegendreFunctions.NormalizedAssociatedLegendre(2, 2, 0.5),12);
        }
    }
}