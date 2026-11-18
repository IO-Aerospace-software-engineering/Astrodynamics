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

        // The 21 terms in the order of the constructor, the parameter name of each and its (row, column) in the 6x6 matrix
        private static readonly (string Name, int Row, int Column)[] Terms =
        {
            ("cxX", 0, 0),
            ("cyX", 1, 0), ("cyY", 1, 1),
            ("czX", 2, 0), ("czY", 2, 1), ("czZ", 2, 2),
            ("cxDotX", 3, 0), ("cxDotY", 3, 1), ("cxDotZ", 3, 2), ("cxDotXDot", 3, 3),
            ("cyDotX", 4, 0), ("cyDotY", 4, 1), ("cyDotZ", 4, 2), ("cyDotXDot", 4, 3), ("cyDotYDot", 4, 4),
            ("czDotX", 5, 0), ("czDotY", 5, 1), ("czDotZ", 5, 2), ("czDotXDot", 5, 3), ("czDotYDot", 5, 4), ("czDotZDot", 5, 5)
        };

        public static TheoryData<int, double> NonFiniteTerms()
        {
            var data = new TheoryData<int, double>();
            for (int i = 0; i < 21; i++)
            foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                data.Add(i, value);
            return data;
        }

        private static double[,] SampleFullMatrix(int term, double value)
        {
            var matrix = CreateSampleCovariance().ToFullMatrix();
            var (_, row, column) = Terms[term];
            matrix[row, column] = value;
            matrix[column, row] = value;
            return matrix;
        }

        [Theory]
        [MemberData(nameof(NonFiniteTerms))]
        public void Constructor_NonFiniteTerm_ThrowsArgumentOutOfRangeException(int term, double value)
        {
            // Each of the 21 terms is an xsd:double the library reads as a real number (#708, #717); before, a NaN or
            // infinite term built, a covariance (CY_X ...) passed Validate() and SaveToFile wrote NaN, Infinity or -Infinity
            var t = CreateSampleCovariance().ToLowerTriangularArray();
            t[term] = value;
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new CovarianceMatrix(
                t[0], t[1], t[2], t[3], t[4], t[5], t[6], t[7], t[8], t[9], t[10],
                t[11], t[12], t[13], t[14], t[15], t[16], t[17], t[18], t[19], t[20], "RTN"));
            Assert.Equal(Terms[term].Name, ex.ParamName);
        }

        [Theory]
        [MemberData(nameof(NonFiniteTerms))]
        public void FromFullMatrix_NonFiniteTerm_ThrowsArgumentOutOfRangeException(int term, double value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => CovarianceMatrix.FromFullMatrix(SampleFullMatrix(term, value)));
            Assert.Equal(Terms[term].Name, ex.ParamName);
        }

        [Theory]
        [MemberData(nameof(NonFiniteTerms))]
        public void FromMatrix_NonFiniteTerm_ThrowsArgumentOutOfRangeException(int term, double value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                CovarianceMatrix.FromMatrix(new IO.Astrodynamics.Math.Matrix(SampleFullMatrix(term, value))));
            Assert.Equal(Terms[term].Name, ex.ParamName);
        }

        [Theory]
        [MemberData(nameof(NonFiniteTerms))]
        public void FromMatrixWithUnitConversion_NonFiniteTerm_ThrowsArgumentOutOfRangeException(int term, double value)
        {
            // A StateVector's covariance (m², m²/s, m²/s²) holds any value; Opm.CreateFromStateVector converts it here
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                CovarianceMatrix.FromMatrixWithUnitConversion(new IO.Astrodynamics.Math.Matrix(SampleFullMatrix(term, value))));
            Assert.Equal(Terms[term].Name, ex.ParamName);
        }

        // The largest double whose product by 1e6 is finite (IEEE 754 binary64, round to nearest: 1.7976931348623154e302,
        // computed with Python's math.nextafter on 2026-11-17); the next double gives +Infinity in m², m²/s or m²/s²
        private static readonly double LargestKm2 = BitConverter.Int64BitsToDouble(0x7eb0c6f7a0b5ed8cL);

        [Fact]
        public void Constructor_LargestTermsFiniteInSiUnits_AreStoredAsGiven()
        {
            // The largest magnitude whose value in SI units is a real number and the smallest subnormal are stored as
            // given, and a negative covariance builds (the validators report a negative variance) (#729: double.MaxValue
            // km² built, and ToMatrixWithUnitConversion gave +Infinity m²)
            var covariance = new CovarianceMatrix(
                LargestKm2, -LargestKm2, double.Epsilon, -double.Epsilon, 0, -1.0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, LargestKm2);
            Assert.Equal(LargestKm2, covariance.CxX);
            Assert.Equal(-LargestKm2, covariance.CyX);
            Assert.Equal(double.Epsilon, covariance.CyY);
            Assert.Equal(-double.Epsilon, covariance.CzX);
            Assert.Equal(-1.0, covariance.CzZ);

            var matrix = covariance.ToMatrixWithUnitConversion();
            Assert.Equal(LargestKm2 * 1e6, matrix.Get(0, 0));
            Assert.Equal(-LargestKm2 * 1e6, matrix.Get(1, 0));
            Assert.Equal(LargestKm2 * 1e6, matrix.Get(5, 5));
            Assert.True(double.IsFinite(matrix.Get(0, 0)));
        }

        public static TheoryData<int, double> TermsOverflowingInSiUnits()
        {
            var data = new TheoryData<int, double>();
            var next = System.Math.BitIncrement(LargestKm2);
            for (int i = 0; i < 21; i++)
            foreach (var value in new[] { next, -next, double.MaxValue })
                data.Add(i, value);
            return data;
        }

        [Theory]
        [MemberData(nameof(TermsOverflowingInSiUnits))]
        public void Constructor_TermOverflowingInSiUnits_ThrowsArgumentOutOfRangeException(int term, double value)
        {
            // #729: a finite term whose product by 1e6 is beyond the range of a double built, and
            // ToMatrixWithUnitConversion (Opm.ToStateVector's covariance) gave ±Infinity
            Assert.False(double.IsFinite(value * 1e6));
            var t = CreateSampleCovariance().ToLowerTriangularArray();
            t[term] = value;
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new CovarianceMatrix(
                t[0], t[1], t[2], t[3], t[4], t[5], t[6], t[7], t[8], t[9], t[10],
                t[11], t[12], t[13], t[14], t[15], t[16], t[17], t[18], t[19], t[20], "RTN"));
            Assert.Equal(Terms[term].Name, ex.ParamName);
            Assert.Contains("SI units", ex.Message);

            ex = Assert.Throws<ArgumentOutOfRangeException>(() => CovarianceMatrix.FromFullMatrix(SampleFullMatrix(term, value)));
            Assert.Equal(Terms[term].Name, ex.ParamName);
        }

        [Fact]
        public void FromMatrixWithUnitConversion_LargestDouble_StaysConvertible()
        {
            // A StateVector's covariance of double.MaxValue m² is MaxValue × 1e-6 km², whose product by 1e6 is finite: a
            // state the library holds converts to an OPM covariance and back
            var matrix = new IO.Astrodynamics.Math.Matrix(6, 6);
            for (int i = 0; i < 6; i++)
                matrix.Set(i, i, double.MaxValue);
            var covariance = CovarianceMatrix.FromMatrixWithUnitConversion(matrix);
            Assert.True(double.IsFinite(covariance.ToMatrixWithUnitConversion().Get(0, 0)));
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
