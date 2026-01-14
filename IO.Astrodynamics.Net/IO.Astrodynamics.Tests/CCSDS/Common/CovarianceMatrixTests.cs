// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.CCSDS.Common;
using Xunit;

namespace IO.Astrodynamics.Tests.CCSDS.Common
{
    public class CovarianceMatrixTests
    {
        [Fact]
        public void Constructor_WithAllElements_Succeeds()
        {
            var covariance = CreateSampleCovariance();

            Assert.Equal(1.0, covariance.CxX);
            Assert.Equal(0.1, covariance.CyX);
            Assert.Equal(2.0, covariance.CyY);
            Assert.Equal(0.2, covariance.CzX);
            Assert.Equal(0.3, covariance.CzY);
            Assert.Equal(3.0, covariance.CzZ);
            Assert.Equal("RTN", covariance.ReferenceFrame);
        }

        [Fact]
        public void Constructor_WithNullReferenceFrame_Succeeds()
        {
            var covariance = new CovarianceMatrix(
                cxX: 1.0,
                cyX: 0.0, cyY: 1.0,
                czX: 0.0, czY: 0.0, czZ: 1.0,
                cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 0.001,
                cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 0.001,
                czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 0.001);

            Assert.Null(covariance.ReferenceFrame);
        }

        [Fact]
        public void PositionSigma_ReturnsCorrectValues()
        {
            var covariance = CreateDiagonalCovariance(1.0, 4.0, 9.0, 0.01, 0.04, 0.09);

            var posSigma = covariance.PositionSigma;

            Assert.Equal(1.0, posSigma.SigmaX, 10);
            Assert.Equal(2.0, posSigma.SigmaY, 10);
            Assert.Equal(3.0, posSigma.SigmaZ, 10);
        }

        [Fact]
        public void VelocitySigma_ReturnsCorrectValues()
        {
            var covariance = CreateDiagonalCovariance(1.0, 1.0, 1.0, 0.01, 0.04, 0.09);

            var velSigma = covariance.VelocitySigma;

            Assert.Equal(0.1, velSigma.SigmaXDot, 10);
            Assert.Equal(0.2, velSigma.SigmaYDot, 10);
            Assert.Equal(0.3, velSigma.SigmaZDot, 10);
        }

        [Fact]
        public void ToFullMatrix_ReturnsSymmetric6x6Matrix()
        {
            var covariance = CreateSampleCovariance();

            var matrix = covariance.ToFullMatrix();

            // Check dimensions
            Assert.Equal(6, matrix.GetLength(0));
            Assert.Equal(6, matrix.GetLength(1));

            // Check diagonal elements
            Assert.Equal(1.0, matrix[0, 0]);
            Assert.Equal(2.0, matrix[1, 1]);
            Assert.Equal(3.0, matrix[2, 2]);

            // Check symmetry
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    Assert.Equal(matrix[i, j], matrix[j, i]);
                }
            }
        }

        [Fact]
        public void ToLowerTriangularArray_Returns21Elements()
        {
            var covariance = CreateSampleCovariance();

            var array = covariance.ToLowerTriangularArray();

            Assert.Equal(21, array.Length);
            Assert.Equal(covariance.CxX, array[0]);
            Assert.Equal(covariance.CyX, array[1]);
            Assert.Equal(covariance.CyY, array[2]);
        }

        [Fact]
        public void FromFullMatrix_WithValid6x6Matrix_Succeeds()
        {
            var matrix = new double[6, 6];
            matrix[0, 0] = 1.0;  // CxX
            matrix[1, 0] = matrix[0, 1] = 0.1;  // CyX
            matrix[1, 1] = 2.0;  // CyY
            matrix[2, 0] = matrix[0, 2] = 0.2;  // CzX
            matrix[2, 1] = matrix[1, 2] = 0.3;  // CzY
            matrix[2, 2] = 3.0;  // CzZ
            matrix[3, 3] = 0.001;  // CxDotXDot
            matrix[4, 4] = 0.002;  // CyDotYDot
            matrix[5, 5] = 0.003;  // CzDotZDot

            var covariance = CovarianceMatrix.FromFullMatrix(matrix, "ICRF");

            Assert.Equal(1.0, covariance.CxX);
            Assert.Equal(0.1, covariance.CyX);
            Assert.Equal(2.0, covariance.CyY);
            Assert.Equal("ICRF", covariance.ReferenceFrame);
        }

        [Fact]
        public void FromFullMatrix_WithNullMatrix_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => CovarianceMatrix.FromFullMatrix(null));
        }

        [Fact]
        public void FromFullMatrix_WithWrongSize_ThrowsArgumentException()
        {
            var matrix = new double[3, 3];
            Assert.Throws<ArgumentException>(() => CovarianceMatrix.FromFullMatrix(matrix));
        }

        [Fact]
        public void RoundTrip_FromFullMatrix_ToFullMatrix_PreservesValues()
        {
            var original = new double[6, 6];
            // Fill with test values
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    original[i, j] = i * 10 + j;
                    original[j, i] = original[i, j];  // Symmetric
                }
            }

            var covariance = CovarianceMatrix.FromFullMatrix(original, "TNW");
            var result = covariance.ToFullMatrix();

            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    Assert.Equal(original[i, j], result[i, j], 10);
                }
            }
        }

        [Fact]
        public void ToString_ContainsRelevantInfo()
        {
            var covariance = CreateSampleCovariance();

            var result = covariance.ToString();

            Assert.Contains("Covariance", result);
            Assert.Contains("RTN", result);
        }

        [Fact]
        public void Comments_DefaultsToEmptyList()
        {
            var covariance = CreateDiagonalCovariance(1.0, 1.0, 1.0, 0.001, 0.001, 0.001);

            Assert.NotNull(covariance.Comments);
            Assert.Empty(covariance.Comments);
        }

        [Fact]
        public void Comments_CanBeProvided()
        {
            var comments = new List<string> { "Covariance from OD solution" };
            var covariance = new CovarianceMatrix(
                cxX: 1.0,
                cyX: 0.0, cyY: 1.0,
                czX: 0.0, czY: 0.0, czZ: 1.0,
                cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 0.001,
                cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 0.001,
                czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 0.001,
                comments: comments);

            Assert.Single(covariance.Comments);
            Assert.Equal("Covariance from OD solution", covariance.Comments[0]);
        }

        private static CovarianceMatrix CreateSampleCovariance()
        {
            return new CovarianceMatrix(
                cxX: 1.0,
                cyX: 0.1, cyY: 2.0,
                czX: 0.2, czY: 0.3, czZ: 3.0,
                cxDotX: 0.01, cxDotY: 0.02, cxDotZ: 0.03, cxDotXDot: 0.001,
                cyDotX: 0.04, cyDotY: 0.05, cyDotZ: 0.06, cyDotXDot: 0.002, cyDotYDot: 0.003,
                czDotX: 0.07, czDotY: 0.08, czDotZ: 0.09, czDotXDot: 0.004, czDotYDot: 0.005, czDotZDot: 0.006,
                referenceFrame: "RTN");
        }

        private static CovarianceMatrix CreateDiagonalCovariance(
            double cxX, double cyY, double czZ,
            double cxDotXDot, double cyDotYDot, double czDotZDot)
        {
            return new CovarianceMatrix(
                cxX: cxX,
                cyX: 0.0, cyY: cyY,
                czX: 0.0, czY: 0.0, czZ: czZ,
                cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: cxDotXDot,
                cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: cyDotYDot,
                czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: czDotZDot);
        }
    }
}
