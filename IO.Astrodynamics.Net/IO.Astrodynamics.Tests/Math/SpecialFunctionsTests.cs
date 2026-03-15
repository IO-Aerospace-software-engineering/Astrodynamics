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
        
        [Fact]
        public void NormalizeAngle_PositiveAngleWithinRange_ReturnsSameAngle()
        {
            double angle = System.Math.PI;
            double result = SpecialFunctions.NormalizeAngle(angle);
            Assert.Equal(System.Math.PI, result, 6);
        }
        
        [Fact]
        public void NormalizeAngle_PositiveAngleExceeding2PI_WrapsToRange()
        {
            double angle = 3 * System.Math.PI;
            double result = SpecialFunctions.NormalizeAngle(angle);
            Assert.Equal(System.Math.PI, result, 6);
        }
        
        [Fact]
        public void NormalizeAngle_NegativeAngle_WrapsToPositiveRange()
        {
            double angle = -System.Math.PI;
            double result = SpecialFunctions.NormalizeAngle(angle);
            Assert.Equal(System.Math.PI, result, 6);
        }
        
        [Fact]
        public void NormalizeAngle_ZeroAngle_ReturnsZero()
        {
            double angle = 0;
            double result = SpecialFunctions.NormalizeAngle(angle);
            Assert.Equal(0, result, 6);
        }
        
        [Fact]
        public void NormalizeAngle_NegativeMultipleOf2PI_ReturnsZero()
        {
            double angle = -2 * System.Math.PI;
            double result = SpecialFunctions.NormalizeAngle(angle);
            Assert.Equal(0, result, 6);
        }

        [Fact]
        public void ErrorFunction_Zero_ReturnsZero()
        {
            Assert.Equal(0.0, SpecialFunctions.ErrorFunction(0.0), 7);
        }

        [Theory]
        [InlineData(1.0, 0.8427007929)]
        [InlineData(2.0, 0.9953222650)]
        [InlineData(0.5, 0.5204998778)]
        [InlineData(3.0, 0.9999779095)]
        public void ErrorFunction_KnownValues(double x, double expected)
        {
            Assert.Equal(expected, SpecialFunctions.ErrorFunction(x), 6);
        }

        [Fact]
        public void ErrorFunction_IsOddFunction()
        {
            double x = 1.5;
            Assert.Equal(-SpecialFunctions.ErrorFunction(x), SpecialFunctions.ErrorFunction(-x), 10);
        }

        [Fact]
        public void ErrorFunction_LargePositive_ApproachesOne()
        {
            Assert.True(SpecialFunctions.ErrorFunction(6.0) > 0.999999);
        }

        [Fact]
        public void ErrorFunction_LargeNegative_ApproachesMinusOne()
        {
            Assert.True(SpecialFunctions.ErrorFunction(-6.0) < -0.999999);
        }
    }
}
