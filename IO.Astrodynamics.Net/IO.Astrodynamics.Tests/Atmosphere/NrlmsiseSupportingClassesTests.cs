using Xunit;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;

namespace IO.Astrodynamics.Tests.Atmosphere
{
    /// <summary>
    /// Comprehensive unit tests for NRLMSISE-00 supporting classes:
    /// NrlmsiseInput, NrlmsiseOutput, ApArray, and NrlmsiseFlags.
    /// Note: These classes use SI units (meters, radians, m^-3, kg/m^3).
    /// </summary>
    public class NrlmsiseSupportingClassesTests
    {
        #region NrlmsiseInput Tests

        /// <summary>
        /// Tests that NrlmsiseInput can be instantiated with default values.
        /// </summary>
        [Fact]
        public void NrlmsiseInput_DefaultInstantiation_CreatesObjectWithDefaultValues()
        {
            // Arrange & Act
            var input = new NrlmsiseInput();

            // Assert
            Assert.Equal(0, input.Year);
            Assert.Equal(0, input.Doy);
            Assert.Equal(0.0, input.Sec);
            Assert.Equal(0.0, input.Alt);
            Assert.Equal(0.0, input.GLat);
            Assert.Equal(0.0, input.GLong);
            Assert.Equal(0.0, input.Lst);
            Assert.Equal(0.0, input.F107A);
            Assert.Equal(0.0, input.F107);
            Assert.Equal(0.0, input.Ap);
            Assert.Null(input.ApA);
        }

        /// <summary>
        /// Tests that all properties of NrlmsiseInput can be set and retrieved correctly.
        /// Init-only properties must be set using object initializer syntax.
        /// Mutable properties (Alt, GLat, GLong) can be set after construction.
        /// </summary>
        [Fact]
        public void NrlmsiseInput_SetAndGetAllProperties_WorksCorrectly()
        {
            // Arrange
            var apArray = new ApArray();

            // Act - Init-only properties set during construction
            var input = new NrlmsiseInput
            {
                Year = 2025,
                Doy = 172,
                Sec = 43200.0,
                Alt = 300.0,  // Initial value
                GLat = 40.0,  // Initial value
                GLong = -70.0,  // Initial value
                Lst = 12.5,
                F107A = 150.0,
                F107 = 155.0,
                Ap = 4.0,
                ApA = apArray
            };

            // Mutable properties can still be modified after construction
            input.Alt = 400.0;
            input.GLat = 45.5;
            input.GLong = -75.3;

            // Assert
            Assert.Equal(2025, input.Year);
            Assert.Equal(172, input.Doy);
            Assert.Equal(43200.0, input.Sec);
            Assert.Equal(400.0, input.Alt);
            Assert.Equal(45.5, input.GLat);
            Assert.Equal(-75.3, input.GLong);
            Assert.Equal(12.5, input.Lst);
            Assert.Equal(150.0, input.F107A);
            Assert.Equal(155.0, input.F107);
            Assert.Equal(4.0, input.Ap);
            Assert.Same(apArray, input.ApA);
        }

        /// <summary>
        /// Tests that NrlmsiseInput can be created using object initializer syntax.
        /// </summary>
        [Fact]
        public void NrlmsiseInput_ObjectInitializerSyntax_CreatesObjectCorrectly()
        {
            // Arrange & Act
            var input = new NrlmsiseInput
            {
                Year = 2025,
                Doy = 100,
                Sec = 86400.0,
                Alt = 500.0,
                GLat = 30.0,
                GLong = 120.0,
                Lst = 18.0,
                F107A = 140.0,
                F107 = 145.0,
                Ap = 5.0,
                ApA = new ApArray()
            };

            // Assert
            Assert.Equal(2025, input.Year);
            Assert.Equal(100, input.Doy);
            Assert.Equal(86400.0, input.Sec);
            Assert.Equal(500.0, input.Alt);
            Assert.Equal(30.0, input.GLat);
            Assert.Equal(120.0, input.GLong);
            Assert.Equal(18.0, input.Lst);
            Assert.Equal(140.0, input.F107A);
            Assert.Equal(145.0, input.F107);
            Assert.Equal(5.0, input.Ap);
            Assert.NotNull(input.ApA);
        }

        /// <summary>
        /// Tests that NrlmsiseInput record equality works correctly for instances with same values.
        /// </summary>
        [Fact]
        public void NrlmsiseInput_RecordEquality_TwoInstancesWithSameValuesAreEqual()
        {
            // Arrange
            var apArray = new ApArray();
            var input1 = new NrlmsiseInput
            {
                Year = 2025,
                Doy = 50,
                Sec = 3600.0,
                Alt = 200.0,
                GLat = 40.0,
                GLong = -80.0,
                Lst = 10.0,
                F107A = 130.0,
                F107 = 135.0,
                Ap = 3.0,
                ApA = apArray
            };

            var input2 = new NrlmsiseInput
            {
                Year = 2025,
                Doy = 50,
                Sec = 3600.0,
                Alt = 200.0,
                GLat = 40.0,
                GLong = -80.0,
                Lst = 10.0,
                F107A = 130.0,
                F107 = 135.0,
                Ap = 3.0,
                ApA = apArray
            };

            // Act & Assert
            Assert.Equal(input1, input2);
            Assert.NotSame(input1, input2);
        }

        /// <summary>
        /// Tests that NrlmsiseInput with-expressions work for creating modified copies.
        /// </summary>
        [Fact]
        public void NrlmsiseInput_WithExpression_CreatesModifiedCopy()
        {
            // Arrange
            var original = new NrlmsiseInput
            {
                Year = 2025,
                Doy = 100,
                Alt = 300.0,
                GLat = 50.0
            };

            // Act
            var modified = original with { Alt = 400.0, GLat = 60.0 };

            // Assert
            Assert.Equal(2025, modified.Year);
            Assert.Equal(100, modified.Doy);
            Assert.Equal(400.0, modified.Alt);
            Assert.Equal(60.0, modified.GLat);
            Assert.Equal(300.0, original.Alt);
            Assert.Equal(50.0, original.GLat);
        }

        /// <summary>
        /// Tests that NrlmsiseInput handles null ApA property correctly.
        /// </summary>
        [Fact]
        public void NrlmsiseInput_NullApAProperty_HandledCorrectly()
        {
            // Arrange & Act
            var input = new NrlmsiseInput
            {
                Year = 2025,
                Doy = 150,
                ApA = null
            };

            // Assert
            Assert.Null(input.ApA);
        }

        /// <summary>
        /// Tests NrlmsiseInput with negative values for various properties.
        /// </summary>
        [Fact]
        public void NrlmsiseInput_NegativeValues_AcceptedForRelevantProperties()
        {
            // Arrange & Act
            var input = new NrlmsiseInput
            {
                GLat = -45.0,
                GLong = -120.0,
                Alt = -10.0  // Below sea level
            };

            // Assert
            Assert.Equal(-45.0, input.GLat);
            Assert.Equal(-120.0, input.GLong);
            Assert.Equal(-10.0, input.Alt);
        }

        /// <summary>
        /// Tests NrlmsiseInput with zero values for all numeric properties.
        /// </summary>
        [Fact]
        public void NrlmsiseInput_ZeroValues_AcceptedForAllProperties()
        {
            // Arrange & Act
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 0,
                Sec = 0.0,
                Alt = 0.0,
                GLat = 0.0,
                GLong = 0.0,
                Lst = 0.0,
                F107A = 0.0,
                F107 = 0.0,
                Ap = 0.0
            };

            // Assert
            Assert.Equal(0, input.Year);
            Assert.Equal(0, input.Doy);
            Assert.Equal(0.0, input.Sec);
            Assert.Equal(0.0, input.Alt);
            Assert.Equal(0.0, input.GLat);
            Assert.Equal(0.0, input.GLong);
            Assert.Equal(0.0, input.Lst);
            Assert.Equal(0.0, input.F107A);
            Assert.Equal(0.0, input.F107);
            Assert.Equal(0.0, input.Ap);
        }

        /// <summary>
        /// Tests NrlmsiseInput with extreme boundary values.
        /// </summary>
        [Fact]
        public void NrlmsiseInput_ExtremeValues_AcceptedCorrectly()
        {
            // Arrange & Act
            var input = new NrlmsiseInput
            {
                Doy = 366,           // Leap year max
                Sec = 86400.0,       // Seconds in a day
                Alt = 10000.0,       // Arbitrary test altitude (meters in SI)
                GLat = 90.0,         // Arbitrary test latitude (radians in SI)
                GLong = 180.0,       // Arbitrary test longitude (radians in SI)
                Lst = 24.0,          // End of day
                F107A = 500.0,       // Very high solar activity
                F107 = 500.0,
                Ap = 400.0           // Very high geomagnetic activity
            };

            // Assert
            Assert.Equal(366, input.Doy);
            Assert.Equal(86400.0, input.Sec);
            Assert.Equal(10000.0, input.Alt);
            Assert.Equal(90.0, input.GLat);
            Assert.Equal(180.0, input.GLong);
            Assert.Equal(24.0, input.Lst);
            Assert.Equal(500.0, input.F107A);
            Assert.Equal(500.0, input.F107);
            Assert.Equal(400.0, input.Ap);
        }

        /// <summary>
        /// Tests that NrlmsiseInput record inequality works when values differ.
        /// </summary>
        [Fact]
        public void NrlmsiseInput_RecordInequality_DifferentValuesAreNotEqual()
        {
            // Arrange
            var input1 = new NrlmsiseInput { Year = 2025, Doy = 100 };
            var input2 = new NrlmsiseInput { Year = 2025, Doy = 101 };

            // Act & Assert
            Assert.NotEqual(input1, input2);
        }

        #endregion

        #region NrlmsiseOutput Tests

        /// <summary>
        /// Tests that NrlmsiseOutput default instantiation creates arrays properly.
        /// </summary>
        [Fact]
        public void NrlmsiseOutput_DefaultInstantiation_CreatesArraysProperly()
        {
            // Arrange & Act
            var output = new NrlmsiseOutput();

            // Assert
            Assert.NotNull(output.D);
            Assert.NotNull(output.T);
        }

        /// <summary>
        /// Tests that NrlmsiseOutput arrays are initialized to correct sizes.
        /// </summary>
        [Fact]
        public void NrlmsiseOutput_ArraySizes_InitializedCorrectly()
        {
            // Arrange & Act
            var output = new NrlmsiseOutput();

            // Assert
            Assert.Equal(9, output.D.Length);
            Assert.Equal(2, output.T.Length);
        }

        /// <summary>
        /// Tests that NrlmsiseOutput array elements can be set and retrieved.
        /// </summary>
        [Fact]
        public void NrlmsiseOutput_SetAndGetArrayElements_WorksCorrectly()
        {
            // Arrange
            var output = new NrlmsiseOutput();

            // Act
            for (int i = 0; i < 9; i++)
            {
                output.D[i] = i * 1.5;
            }
            output.T[0] = 1000.0;
            output.T[1] = 500.0;

            // Assert
            for (int i = 0; i < 9; i++)
            {
                Assert.Equal(i * 1.5, output.D[i]);
            }
            Assert.Equal(1000.0, output.T[0]);
            Assert.Equal(500.0, output.T[1]);
        }

        /// <summary>
        /// Tests that NrlmsiseOutput arrays are initialized to zeros.
        /// </summary>
        [Fact]
        public void NrlmsiseOutput_ArrayInitialization_AllElementsAreZero()
        {
            // Arrange & Act
            var output = new NrlmsiseOutput();

            // Assert
            for (int i = 0; i < 9; i++)
            {
                Assert.Equal(0.0, output.D[i]);
            }
            Assert.Equal(0.0, output.T[0]);
            Assert.Equal(0.0, output.T[1]);
        }

        /// <summary>
        /// Tests that NrlmsiseOutput record equality works correctly.
        /// </summary>
        [Fact]
        public void NrlmsiseOutput_RecordEquality_WorksWithArrays()
        {
            // Arrange
            var densities = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            var temps = new double[] { 1000, 500 };

            var output1 = new NrlmsiseOutput { D = densities, T = temps };
            var output2 = new NrlmsiseOutput { D = densities, T = temps };

            // Act & Assert
            Assert.Equal(output1, output2);
            Assert.NotSame(output1, output2);
        }

        /// <summary>
        /// Tests that modifying one NrlmsiseOutput instance doesn't affect another when arrays are shared.
        /// </summary>
        [Fact]
        public void NrlmsiseOutput_IndependentArrayModifications_WhenArraysAreShared()
        {
            // Arrange
            var sharedD = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            var sharedT = new double[] { 1000, 500 };

            var output1 = new NrlmsiseOutput { D = sharedD, T = sharedT };
            var output2 = new NrlmsiseOutput { D = sharedD, T = sharedT };

            // Act
            output1.D[0] = 999.0;
            output1.T[0] = 2000.0;

            // Assert - both instances reference the same arrays
            Assert.Equal(999.0, output2.D[0]);
            Assert.Equal(2000.0, output2.T[0]);
        }

        /// <summary>
        /// Tests that separate NrlmsiseOutput instances have independent default arrays.
        /// </summary>
        [Fact]
        public void NrlmsiseOutput_DefaultArraysAreIndependent_BetweenInstances()
        {
            // Arrange
            var output1 = new NrlmsiseOutput();
            var output2 = new NrlmsiseOutput();

            // Act
            output1.D[0] = 123.0;
            output1.T[0] = 456.0;

            // Assert
            Assert.Equal(123.0, output1.D[0]);
            Assert.Equal(0.0, output2.D[0]);
            Assert.Equal(456.0, output1.T[0]);
            Assert.Equal(0.0, output2.T[0]);
        }

        /// <summary>
        /// Tests that NrlmsiseOutput can be modified using with-expression.
        /// </summary>
        [Fact]
        public void NrlmsiseOutput_WithExpression_CreatesModifiedCopy()
        {
            // Arrange
            var original = new NrlmsiseOutput();
            original.D[0] = 100.0;

            var newDensities = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            // Act
            var modified = original with { D = newDensities };

            // Assert
            Assert.Equal(100.0, original.D[0]);
            Assert.Equal(1.0, modified.D[0]);
            Assert.NotSame(original.D, modified.D);
        }

        /// <summary>
        /// Tests that NrlmsiseOutput arrays can hold negative values.
        /// </summary>
        [Fact]
        public void NrlmsiseOutput_NegativeValues_AcceptedInArrays()
        {
            // Arrange
            var output = new NrlmsiseOutput();

            // Act
            output.D[0] = -123.456;
            output.T[0] = -273.15;

            // Assert
            Assert.Equal(-123.456, output.D[0]);
            Assert.Equal(-273.15, output.T[0]);
        }

        /// <summary>
        /// Tests that NrlmsiseOutput arrays can hold very large values.
        /// </summary>
        [Fact]
        public void NrlmsiseOutput_ExtremeValues_AcceptedInArrays()
        {
            // Arrange
            var output = new NrlmsiseOutput();

            // Act
            output.D[0] = 1.0e20;
            output.T[0] = 1.0e6;

            // Assert
            Assert.Equal(1.0e20, output.D[0]);
            Assert.Equal(1.0e6, output.T[0]);
        }

        #endregion

        #region ApArray Tests

        /// <summary>
        /// Tests that ApArray default instantiation creates array properly.
        /// </summary>
        [Fact]
        public void ApArray_DefaultInstantiation_CreatesArrayProperly()
        {
            // Arrange & Act
            var apArray = new ApArray();

            // Assert
            Assert.NotNull(apArray.A);
        }

        /// <summary>
        /// Tests that ApArray is initialized to correct size.
        /// </summary>
        [Fact]
        public void ApArray_ArraySize_InitializedToSevenElements()
        {
            // Arrange & Act
            var apArray = new ApArray();

            // Assert
            Assert.Equal(7, apArray.A.Length);
        }

        /// <summary>
        /// Tests that all ApArray elements can be set and retrieved.
        /// </summary>
        [Fact]
        public void ApArray_SetAndGetAllElements_WorksCorrectly()
        {
            // Arrange
            var apArray = new ApArray();
            var expectedValues = new double[] { 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0 };

            // Act
            for (int i = 0; i < 7; i++)
            {
                apArray.A[i] = expectedValues[i];
            }

            // Assert
            for (int i = 0; i < 7; i++)
            {
                Assert.Equal(expectedValues[i], apArray.A[i]);
            }
        }

        /// <summary>
        /// Tests that ApArray is initialized to all zeros.
        /// </summary>
        [Fact]
        public void ApArray_ArrayInitialization_AllElementsAreZero()
        {
            // Arrange & Act
            var apArray = new ApArray();

            // Assert
            for (int i = 0; i < 7; i++)
            {
                Assert.Equal(0.0, apArray.A[i]);
            }
        }

        /// <summary>
        /// Tests that separate ApArray instances have independent arrays.
        /// </summary>
        [Fact]
        public void ApArray_IndependentInstances_HaveIndependentArrays()
        {
            // Arrange
            var apArray1 = new ApArray();
            var apArray2 = new ApArray();

            // Act
            apArray1.A[0] = 100.0;
            apArray1.A[3] = 200.0;

            // Assert
            Assert.Equal(100.0, apArray1.A[0]);
            Assert.Equal(0.0, apArray2.A[0]);
            Assert.Equal(200.0, apArray1.A[3]);
            Assert.Equal(0.0, apArray2.A[3]);
        }

        /// <summary>
        /// Tests that ApArray can be created with a custom array using object initializer.
        /// </summary>
        [Fact]
        public void ApArray_WithCustomArray_WorksCorrectly()
        {
            // Arrange
            var newArray = new double[] { 10, 20, 30, 40, 50, 60, 70 };

            // Act - use object initializer syntax for init-only property
            var apArray = new ApArray { A = newArray };

            // Assert
            Assert.Same(newArray, apArray.A);
            Assert.Equal(10.0, apArray.A[0]);
            Assert.Equal(70.0, apArray.A[6]);
        }

        /// <summary>
        /// Tests that ApArray accepts negative values.
        /// </summary>
        [Fact]
        public void ApArray_NegativeValues_AcceptedCorrectly()
        {
            // Arrange
            var apArray = new ApArray();

            // Act
            apArray.A[0] = -5.0;
            apArray.A[6] = -10.0;

            // Assert
            Assert.Equal(-5.0, apArray.A[0]);
            Assert.Equal(-10.0, apArray.A[6]);
        }

        /// <summary>
        /// Tests that ApArray accepts very large values.
        /// </summary>
        [Fact]
        public void ApArray_ExtremeValues_AcceptedCorrectly()
        {
            // Arrange
            var apArray = new ApArray();

            // Act
            apArray.A[0] = 400.0;  // Maximum realistic AP index
            apArray.A[6] = 1.0e10; // Unrealistically large value

            // Assert
            Assert.Equal(400.0, apArray.A[0]);
            Assert.Equal(1.0e10, apArray.A[6]);
        }

        /// <summary>
        /// Tests that ApArray with typical realistic values.
        /// </summary>
        [Fact]
        public void ApArray_TypicalRealisticValues_SetCorrectly()
        {
            // Arrange
            var apArray = new ApArray();

            // Act - Set typical AP values as described in documentation
            apArray.A[0] = 15.0;   // daily AP
            apArray.A[1] = 12.0;   // 3 hr AP index for current time
            apArray.A[2] = 18.0;   // 3 hr AP index for 3 hrs before
            apArray.A[3] = 20.0;   // 3 hr AP index for 6 hrs before
            apArray.A[4] = 16.0;   // 3 hr AP index for 9 hrs before
            apArray.A[5] = 14.0;   // Average 12-33 hrs prior
            apArray.A[6] = 13.0;   // Average 36-57 hrs prior

            // Assert
            Assert.Equal(15.0, apArray.A[0]);
            Assert.Equal(12.0, apArray.A[1]);
            Assert.Equal(18.0, apArray.A[2]);
            Assert.Equal(20.0, apArray.A[3]);
            Assert.Equal(16.0, apArray.A[4]);
            Assert.Equal(14.0, apArray.A[5]);
            Assert.Equal(13.0, apArray.A[6]);
        }

        #endregion

        #region NrlmsiseFlags Tests

        /// <summary>
        /// Tests that NrlmsiseFlags default constructor initializes switches correctly.
        /// </summary>
        [Fact]
        public void NrlmsiseFlags_DefaultConstructor_InitializesSwitchesCorrectly()
        {
            // Arrange & Act
            var flags = NrlmsiseFlags.CreateStandard();

            // Assert
            Assert.Equal(0, flags.Switches[0]);
            for (int i = 1; i < 24; i++)
            {
                Assert.Equal(1, flags.Switches[i]);
            }
        }

        /// <summary>
        /// Tests that all NrlmsiseFlags arrays are initialized to correct size.
        /// </summary>
        [Fact]
        public void NrlmsiseFlags_ArraySizes_AllInitializedToTwentyFourElements()
        {
            // Arrange & Act
            var flags = NrlmsiseFlags.CreateStandard();

            // Assert
            Assert.Equal(24, flags.Switches.Length);
            Assert.Equal(24, flags.Sw.Length);
            Assert.Equal(24, flags.Swc.Length);
        }

        /// <summary>
        /// Tests that NrlmsiseFlags switch values can be modified.
        /// </summary>
        [Fact]
        public void NrlmsiseFlags_ModifySwitchValues_WorksCorrectly()
        {
            // Arrange
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            flags.Switches[0] = 1;  // Change from 0 to 1
            flags.Switches[9] = -1; // Set to -1 for AP array mode
            flags.Switches[10] = 0; // Turn off a switch

            // Assert
            Assert.Equal(1, flags.Switches[0]);
            Assert.Equal(-1, flags.Switches[9]);
            Assert.Equal(0, flags.Switches[10]);
        }

        /// <summary>
        /// Tests that separate NrlmsiseFlags instances have independent arrays.
        /// </summary>
        [Fact]
        public void NrlmsiseFlags_IndependentInstances_HaveIndependentArrays()
        {
            // Arrange
            var flags1 = NrlmsiseFlags.CreateStandard();
            var flags2 = NrlmsiseFlags.CreateStandard();

            // Act
            flags1.Switches[5] = 2;
            flags1.Sw[10] = 3.5;
            flags1.Swc[15] = 4.2;

            // Assert
            Assert.Equal(2, flags1.Switches[5]);
            Assert.Equal(1, flags2.Switches[5]);
            Assert.Equal(3.5, flags1.Sw[10]);
            Assert.Equal(0.0, flags2.Sw[10]);
            Assert.Equal(4.2, flags1.Swc[15]);
            Assert.Equal(0.0, flags2.Swc[15]);
        }

        /// <summary>
        /// Tests that Sw and Swc arrays start as all zeros.
        /// </summary>
        [Fact]
        public void NrlmsiseFlags_SwAndSwcArrays_InitializedToZeros()
        {
            // Arrange & Act
            var flags = NrlmsiseFlags.CreateStandard();

            // Assert
            for (int i = 0; i < 24; i++)
            {
                Assert.Equal(0.0, flags.Sw[i]);
                Assert.Equal(0.0, flags.Swc[i]);
            }
        }

        /// <summary>
        /// Tests that Sw and Swc array values can be set and retrieved.
        /// </summary>
        [Fact]
        public void NrlmsiseFlags_SetSwAndSwcValues_WorksCorrectly()
        {
            // Arrange
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            flags.Sw[0] = 1.5;
            flags.Sw[23] = 2.5;
            flags.Swc[0] = 3.5;
            flags.Swc[23] = 4.5;

            // Assert
            Assert.Equal(1.5, flags.Sw[0]);
            Assert.Equal(2.5, flags.Sw[23]);
            Assert.Equal(3.5, flags.Swc[0]);
            Assert.Equal(4.5, flags.Swc[23]);
        }

        /// <summary>
        /// Tests that NrlmsiseFlags can replace entire arrays.
        /// </summary>
        [Fact]
        public void NrlmsiseFlags_ReplaceEntireArrays_WorksCorrectly()
        {
            // Arrange
            var flags = NrlmsiseFlags.CreateStandard();
            var newSwitches = new int[24];
            var newSw = new double[24];
            var newSwc = new double[24];

            for (int i = 0; i < 24; i++)
            {
                newSwitches[i] = 2;
                newSw[i] = i * 1.5;
                newSwc[i] = i * 2.5;
            }

            // Act
            flags.Switches = newSwitches;
            flags.Sw = newSw;
            flags.Swc = newSwc;

            // Assert
            Assert.Same(newSwitches, flags.Switches);
            Assert.Same(newSw, flags.Sw);
            Assert.Same(newSwc, flags.Swc);
            Assert.Equal(2, flags.Switches[0]);
            Assert.Equal(0.0, flags.Sw[0]);
            Assert.Equal(57.5, flags.Swc[23]);
        }

        /// <summary>
        /// Tests that NrlmsiseFlags switch 0 is specifically set to 0 by constructor.
        /// </summary>
        [Fact]
        public void NrlmsiseFlags_Switch0_SpecificallySetToZero()
        {
            // Arrange & Act
            var flags = NrlmsiseFlags.CreateStandard();

            // Assert
            Assert.Equal(0, flags.Switches[0]);
        }

        /// <summary>
        /// Tests that NrlmsiseFlags switches 1-23 are all set to 1 by constructor.
        /// </summary>
        [Fact]
        public void NrlmsiseFlags_Switches1To23_AllSetToOne()
        {
            // Arrange & Act
            var flags = NrlmsiseFlags.CreateStandard();

            // Assert
            for (int i = 1; i < 24; i++)
            {
                Assert.Equal(1, flags.Switches[i]);
            }
        }

        /// <summary>
        /// Tests NrlmsiseFlags with switch mode 2 (main effects off, cross terms on).
        /// </summary>
        [Fact]
        public void NrlmsiseFlags_SwitchMode2_AcceptedCorrectly()
        {
            // Arrange
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            flags.Switches[5] = 2;  // Mode 2: main effects off, cross terms on

            // Assert
            Assert.Equal(2, flags.Switches[5]);
        }

        /// <summary>
        /// Tests NrlmsiseFlags with all switches turned off.
        /// </summary>
        [Fact]
        public void NrlmsiseFlags_AllSwitchesOff_SetCorrectly()
        {
            // Arrange
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            for (int i = 0; i < 24; i++)
            {
                flags.Switches[i] = 0;
            }

            // Assert
            for (int i = 0; i < 24; i++)
            {
                Assert.Equal(0, flags.Switches[i]);
            }
        }

        /// <summary>
        /// Tests NrlmsiseFlags with specific documented switch configurations.
        /// </summary>
        [Fact]
        public void NrlmsiseFlags_DocumentedSwitchConfigurations_SetCorrectly()
        {
            // Arrange
            var flags = NrlmsiseFlags.CreateStandard();

            // Act - Configure some switches as documented
            flags.Switches[0] = 0;   // Reserved (previously unit control, now always SI)
            flags.Switches[1] = 1;   // F10.7 effect on mean (on)
            flags.Switches[7] = 1;   // diurnal (on)
            flags.Switches[8] = 1;   // semidiurnal (on)
            flags.Switches[9] = -1;  // Use AP array mode
            flags.Switches[15] = 1;  // departures from diffusive equilibrium (on)

            // Assert
            Assert.Equal(0, flags.Switches[0]);
            Assert.Equal(1, flags.Switches[1]);
            Assert.Equal(1, flags.Switches[7]);
            Assert.Equal(1, flags.Switches[8]);
            Assert.Equal(-1, flags.Switches[9]);
            Assert.Equal(1, flags.Switches[15]);
        }

        #endregion
    }
}
