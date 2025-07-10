using System;
using IO.Astrodynamics.Math;
using Xunit;

namespace IO.Astrodynamics.Tests.Math
{
    public class SpecialFunctionsTests
    {
        [Fact]
        public void Hypergeometric_Returns1ForZeroInput()
        {
            double result = SpecialFunctions.Hypergeometric(0.0, 1e-12);
            Assert.Equal(1.0, result, 12);
        }

        [Theory]
        [InlineData(0.1, 1e-12)]
        [InlineData(-0.1, 1e-12)]
        [InlineData(0.5, 1e-10)]
        [InlineData(-0.5, 1e-10)]
        public void Hypergeometric_ConvergesForSmallZ(double z, double tol)
        {
            double result = SpecialFunctions.Hypergeometric(z, tol);
            Assert.True(double.IsFinite(result));
        }

        [Fact]
        public void Hypergeometric_ThrowsForNonPositiveTolerance()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SpecialFunctions.Hypergeometric(0.1, -1e-10));
        }

        [Fact]
        public void Hypergeometric_ConvergesToExpectedValue()
        {
            // For z = 0.01, the series should be close to 1.013 (empirical, not exact)
            double result = SpecialFunctions.Hypergeometric(0.01, 1e-14);
            Assert.InRange(result, 1.012, 1.014);
        }
    }
}
