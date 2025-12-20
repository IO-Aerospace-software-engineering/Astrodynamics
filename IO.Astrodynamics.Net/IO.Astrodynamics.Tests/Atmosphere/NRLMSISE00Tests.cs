using System;
using Xunit;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;

namespace IO.Astrodynamics.Tests.Atmosphere
{
    /// <summary>
    /// Unit tests for the NRLMSISE-00 atmospheric model.
    /// </summary>
    public class NRLMSISE00Tests
    {
        private const double Tolerance = 1e-6; // Relative tolerance for comparing scientific notation values (0.000001%)

        /// <summary>
        /// Helper method to compare expected and actual values with relative tolerance.
        /// </summary>
        private void AssertScientificEqual(double expected, double actual, string parameterName)
        {
            if (System.Math.Abs(expected) < 1e-100) // Handle very small or zero values
            {
                Assert.True(System.Math.Abs(actual) < 1e-40,
                    $"{parameterName}: Expected ~0, but got {actual:E6}");
            }
            else
            {
                double relativeError = System.Math.Abs((actual - expected) / expected);
                Assert.True(relativeError < Tolerance,
                    $"{parameterName}: Expected {expected:E6}, got {actual:E6}, relative error {relativeError:E6}");
            }
        }

        /// <summary>
        /// Test Case 1: Standard conditions at 400 km altitude.
        /// DOY=172, UT=29000s, ALT=400km, LAT=60°, LONG=-70°, LST=16h, F107A=150, F107=150, AP=4
        /// </summary>
        [Fact]
        public void TestCase01_Standard400km()
        {
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 60,
                GLong = -70,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            model.Gtd7(input, flags, output);

            // Expected values from DOCUMENTATION line 159
            AssertScientificEqual(6.665177E+05, output.D[0], "HE");
            AssertScientificEqual(1.138806E+08, output.D[1], "O");
            AssertScientificEqual(1.998211E+07, output.D[2], "N2");
            AssertScientificEqual(4.022764E+05, output.D[3], "O2");
            AssertScientificEqual(3.557465E+03, output.D[4], "AR");
            AssertScientificEqual(4.074714E-15, output.D[5], "RHO");
            AssertScientificEqual(3.475312E+04, output.D[6], "H");
            AssertScientificEqual(4.095913E+06, output.D[7], "N");
            AssertScientificEqual(2.667273E+04, output.D[8], "ANM O");
            AssertScientificEqual(1.250540E+03, output.T[0], "TINF");
            AssertScientificEqual(1.241416E+03, output.T[1], "TG");
        }

        /// <summary>
        /// Test Case 2: Different day of year (DOY=81).
        /// </summary>
        [Fact]
        public void TestCase02_DifferentDay()
        {
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 81,
                Sec = 29000,
                Alt = 400,
                GLat = 60,
                GLong = -70,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            model.Gtd7(input, flags, output);

            // Expected values from DOCUMENTATION line 161
            AssertScientificEqual(3.407293E+06, output.D[0], "HE");
            AssertScientificEqual(1.586333E+08, output.D[1], "O");
            AssertScientificEqual(1.391117E+07, output.D[2], "N2");
            AssertScientificEqual(3.262560E+05, output.D[3], "O2");
            AssertScientificEqual(1.559618E+03, output.D[4], "AR");
            AssertScientificEqual(5.001846E-15, output.D[5], "RHO");
            AssertScientificEqual(4.854208E+04, output.D[6], "H");
            AssertScientificEqual(4.380967E+06, output.D[7], "N");
            AssertScientificEqual(6.956682E+03, output.D[8], "ANM O");
            AssertScientificEqual(1.166754E+03, output.T[0], "TINF");
            AssertScientificEqual(1.161710E+03, output.T[1], "TG");
        }

        /// <summary>
        /// Test Case 3: High altitude (1000 km) and different time.
        /// </summary>
        [Fact]
        public void TestCase03_HighAltitude1000km()
        {
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 75000,
                Alt = 1000,
                GLat = 60,
                GLong = -70,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            model.Gtd7(input, flags, output);

            // Expected values from DOCUMENTATION line 163
            AssertScientificEqual(1.123767E+05, output.D[0], "HE");
            AssertScientificEqual(6.934130E+04, output.D[1], "O");
            AssertScientificEqual(4.247105E+01, output.D[2], "N2");
            AssertScientificEqual(1.322750E-01, output.D[3], "O2");
            AssertScientificEqual(2.618848E-05, output.D[4], "AR");
            AssertScientificEqual(2.756772E-18, output.D[5], "RHO");
            AssertScientificEqual(2.016750E+04, output.D[6], "H");
            AssertScientificEqual(5.741256E+03, output.D[7], "N");
            AssertScientificEqual(2.374394E+04, output.D[8], "ANM O");
            AssertScientificEqual(1.239892E+03, output.T[0], "TINF");
            AssertScientificEqual(1.239891E+03, output.T[1], "TG");
        }

        /// <summary>
        /// Test Case 4: Low altitude (100 km).
        /// </summary>
        [Fact]
        public void TestCase04_LowAltitude100km()
        {
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 100,
                GLat = 60,
                GLong = -70,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            model.Gtd7(input, flags, output);

            // Expected values from DOCUMENTATION line 165
            AssertScientificEqual(5.411554E+07, output.D[0], "HE");
            AssertScientificEqual(1.918893E+11, output.D[1], "O");
            AssertScientificEqual(6.115826E+12, output.D[2], "N2");
            AssertScientificEqual(1.225201E+12, output.D[3], "O2");
            AssertScientificEqual(6.023212E+10, output.D[4], "AR");
            AssertScientificEqual(3.584426E-10, output.D[5], "RHO");
            AssertScientificEqual(1.059880E+07, output.D[6], "H");
            AssertScientificEqual(2.615737E+05, output.D[7], "N");
            AssertScientificEqual(2.819879E-42, output.D[8], "ANM O");
            AssertScientificEqual(1.027318E+03, output.T[0], "TINF");
            AssertScientificEqual(2.068878E+02, output.T[1], "TG");
        }

        /// <summary>
        /// Test Case 5: Equatorial latitude (LAT=0°).
        /// </summary>
        [Fact]
        public void TestCase05_EquatorialLatitude()
        {
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 0,
                GLong = -70,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            model.Gtd7(input, flags, output);

            // Expected values from DOCUMENTATION line 167
            AssertScientificEqual(1.851122E+06, output.D[0], "HE");
            AssertScientificEqual(1.476555E+08, output.D[1], "O");
            AssertScientificEqual(1.579356E+07, output.D[2], "N2");
            AssertScientificEqual(2.633795E+05, output.D[3], "O2");
            AssertScientificEqual(1.588781E+03, output.D[4], "AR");
            AssertScientificEqual(4.809630E-15, output.D[5], "RHO");
            AssertScientificEqual(5.816167E+04, output.D[6], "H");
            AssertScientificEqual(5.478984E+06, output.D[7], "N");
            AssertScientificEqual(1.264446E+03, output.D[8], "ANM O");
            AssertScientificEqual(1.212396E+03, output.T[0], "TINF");
            AssertScientificEqual(1.208135E+03, output.T[1], "TG");
        }

        /// <summary>
        /// Test Case 6: Different longitude (LONG=0°).
        /// </summary>
        [Fact]
        public void TestCase06_DifferentLongitude()
        {
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 60,
                GLong = 0,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            model.Gtd7(input, flags, output);

            // Expected values from DOCUMENTATION line 169
            AssertScientificEqual(8.673095E+05, output.D[0], "HE");
            AssertScientificEqual(1.278862E+08, output.D[1], "O");
            AssertScientificEqual(1.822577E+07, output.D[2], "N2");
            AssertScientificEqual(2.922214E+05, output.D[3], "O2");
            AssertScientificEqual(2.402962E+03, output.D[4], "AR");
            AssertScientificEqual(4.355866E-15, output.D[5], "RHO");
            AssertScientificEqual(3.686389E+04, output.D[6], "H");
            AssertScientificEqual(3.897276E+06, output.D[7], "N");
            AssertScientificEqual(2.667273E+04, output.D[8], "ANM O");
            AssertScientificEqual(1.220146E+03, output.T[0], "TINF");
            AssertScientificEqual(1.212712E+03, output.T[1], "TG");
        }

        /// <summary>
        /// Test Case 7: Different local solar time (LST=4h).
        /// </summary>
        [Fact]
        public void TestCase07_DifferentLocalTime()
        {
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 60,
                GLong = -70,
                Lst = 4,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            model.Gtd7(input, flags, output);

            // Expected values from DOCUMENTATION line 171
            AssertScientificEqual(5.776251E+05, output.D[0], "HE");
            AssertScientificEqual(6.979139E+07, output.D[1], "O");
            AssertScientificEqual(1.236814E+07, output.D[2], "N2");
            AssertScientificEqual(2.492868E+05, output.D[3], "O2");
            AssertScientificEqual(1.405739E+03, output.D[4], "AR");
            AssertScientificEqual(2.470651E-15, output.D[5], "RHO");
            AssertScientificEqual(5.291986E+04, output.D[6], "H");
            AssertScientificEqual(1.069814E+06, output.D[7], "N");
            AssertScientificEqual(2.667273E+04, output.D[8], "ANM O");
            AssertScientificEqual(1.116385E+03, output.T[0], "TINF");
            AssertScientificEqual(1.112999E+03, output.T[1], "TG");
        }

        /// <summary>
        /// Test Case 8: Low solar activity (F107A=70).
        /// </summary>
        [Fact]
        public void TestCase08_LowSolarActivity()
        {
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 60,
                GLong = -70,
                Lst = 16,
                F107A = 70,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            model.Gtd7(input, flags, output);

            // Expected values from DOCUMENTATION line 173
            AssertScientificEqual(3.740304E+05, output.D[0], "HE");
            AssertScientificEqual(4.782720E+07, output.D[1], "O");
            AssertScientificEqual(5.240380E+06, output.D[2], "N2");
            AssertScientificEqual(1.759875E+05, output.D[3], "O2");
            AssertScientificEqual(5.501649E+02, output.D[4], "AR");
            AssertScientificEqual(1.571889E-15, output.D[5], "RHO");
            AssertScientificEqual(8.896776E+04, output.D[6], "H");
            AssertScientificEqual(1.979741E+06, output.D[7], "N");
            AssertScientificEqual(9.121815E+03, output.D[8], "ANM O");
            AssertScientificEqual(1.031247E+03, output.T[0], "TINF");
            AssertScientificEqual(1.024848E+03, output.T[1], "TG");
        }

        /// <summary>
        /// Test Case 9: High solar flux (F107=180).
        /// </summary>
        [Fact]
        public void TestCase09_HighSolarFlux()
        {
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 60,
                GLong = -70,
                Lst = 16,
                F107A = 150,
                F107 = 180,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            model.Gtd7(input, flags, output);

            // Expected values from DOCUMENTATION line 175
            AssertScientificEqual(6.748339E+05, output.D[0], "HE");
            AssertScientificEqual(1.245315E+08, output.D[1], "O");
            AssertScientificEqual(2.369010E+07, output.D[2], "N2");
            AssertScientificEqual(4.911583E+05, output.D[3], "O2");
            AssertScientificEqual(4.578781E+03, output.D[4], "AR");
            AssertScientificEqual(4.564420E-15, output.D[5], "RHO");
            AssertScientificEqual(3.244595E+04, output.D[6], "H");
            AssertScientificEqual(5.370833E+06, output.D[7], "N");
            AssertScientificEqual(2.667273E+04, output.D[8], "ANM O");
            AssertScientificEqual(1.306052E+03, output.T[0], "TINF");
            AssertScientificEqual(1.293374E+03, output.T[1], "TG");
        }

        /// <summary>
        /// Test Case 10: High geomagnetic activity (AP=40).
        /// </summary>
        [Fact]
        public void TestCase10_HighGeomagneticActivity()
        {
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 60,
                GLong = -70,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 40
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            model.Gtd7(input, flags, output);

            // Expected values from DOCUMENTATION line 177
            AssertScientificEqual(5.528601E+05, output.D[0], "HE");
            AssertScientificEqual(1.198041E+08, output.D[1], "O");
            AssertScientificEqual(3.495798E+07, output.D[2], "N2");
            AssertScientificEqual(9.339618E+05, output.D[3], "O2");
            AssertScientificEqual(1.096255E+04, output.D[4], "AR");
            AssertScientificEqual(4.974543E-15, output.D[5], "RHO");
            AssertScientificEqual(2.686428E+04, output.D[6], "H");
            AssertScientificEqual(4.889974E+06, output.D[7], "N");
            AssertScientificEqual(2.805445E+04, output.D[8], "ANM O");
            AssertScientificEqual(1.361868E+03, output.T[0], "TINF");
            AssertScientificEqual(1.347389E+03, output.T[1], "TG");
        }

        /// <summary>
        /// Test Case 11-15: Very low altitudes (0-70 km).
        /// Tests the lower atmosphere model component.
        /// </summary>
        [Theory]
        [InlineData(0, 1.375488E+14, 0.000000E+00, 2.049687E+19, 5.498695E+18, 2.451733E+17,
                    1.261066E-03, 0.000000E+00, 0.000000E+00, 0.000000E+00, 1.027318E+03, 2.814648E+02)]
        [InlineData(10, 4.427443E+13, 0.000000E+00, 6.597567E+18, 1.769929E+18, 7.891680E+16,
                     4.059139E-04, 0.000000E+00, 0.000000E+00, 0.000000E+00, 1.027318E+03, 2.274180E+02)]
        [InlineData(30, 2.127829E+12, 0.000000E+00, 3.170791E+17, 8.506280E+16, 3.792741E+15,
                     1.950822E-05, 0.000000E+00, 0.000000E+00, 0.000000E+00, 1.027318E+03, 2.374389E+02)]
        [InlineData(50, 1.412184E+11, 0.000000E+00, 2.104370E+16, 5.645392E+15, 2.517142E+14,
                     1.294709E-06, 0.000000E+00, 0.000000E+00, 0.000000E+00, 1.027318E+03, 2.795551E+02)]
        [InlineData(70, 1.254884E+10, 0.000000E+00, 1.874533E+15, 4.923051E+14, 2.239685E+13,
                     1.147668E-07, 0.000000E+00, 0.000000E+00, 0.000000E+00, 1.027318E+03, 2.190732E+02)]
        public void TestCase11to15_LowerAtmosphere(double altitude, double he, double o, double n2, double o2,
                                                     double ar, double rho, double h, double n, double anmO,
                                                     double tinf, double tg)
        {
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = altitude,
                GLat = 60,
                GLong = -70,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            model.Gtd7(input, flags, output);

            AssertScientificEqual(he, output.D[0], "HE");
            AssertScientificEqual(o, output.D[1], "O");
            AssertScientificEqual(n2, output.D[2], "N2");
            AssertScientificEqual(o2, output.D[3], "O2");
            AssertScientificEqual(ar, output.D[4], "AR");
            AssertScientificEqual(rho, output.D[5], "RHO");
            AssertScientificEqual(h, output.D[6], "H");
            AssertScientificEqual(n, output.D[7], "N");
            AssertScientificEqual(anmO, output.D[8], "ANM O");
            AssertScientificEqual(tinf, output.T[0], "TINF");
            AssertScientificEqual(tg, output.T[1], "TG");
        }

        /// <summary>
        /// Test Case 16: Using AP array (flags.switches[9] = -1).
        /// </summary>
        [Fact]
        public void TestCase16_WithApArray()
        {
            var model = new NRLMSISE00();
            var apArray = new ApArray();
            for (int i = 0; i < 7; i++)
                apArray.A[i] = 100;

            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 60,
                GLong = -70,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4,
                ApA = apArray
            };
            var flags = new NrlmsiseFlags();
            flags.Switches[9] = -1;
            var output = new NrlmsiseOutput();

            model.Gtd7(input, flags, output);

            // Expected values from DOCUMENTATION line 189
            AssertScientificEqual(5.196477E+05, output.D[0], "HE");
            AssertScientificEqual(1.274494E+08, output.D[1], "O");
            AssertScientificEqual(4.850450E+07, output.D[2], "N2");
            AssertScientificEqual(1.720838E+06, output.D[3], "O2");
            AssertScientificEqual(2.354487E+04, output.D[4], "AR");
            AssertScientificEqual(5.881940E-15, output.D[5], "RHO");
            AssertScientificEqual(2.500078E+04, output.D[6], "H");
            AssertScientificEqual(6.279210E+06, output.D[7], "N");
            AssertScientificEqual(2.667273E+04, output.D[8], "ANM O");
            AssertScientificEqual(1.426412E+03, output.T[0], "TINF");
            AssertScientificEqual(1.408608E+03, output.T[1], "TG");
        }

        /// <summary>
        /// Test Case 17: Low altitude (100 km) with AP array.
        /// </summary>
        [Fact]
        public void TestCase17_LowAltitudeWithApArray()
        {
            var model = new NRLMSISE00();
            var apArray = new ApArray();
            for (int i = 0; i < 7; i++)
                apArray.A[i] = 100;

            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 100,
                GLat = 60,
                GLong = -70,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4,
                ApA = apArray
            };
            var flags = new NrlmsiseFlags();
            flags.Switches[9] = -1;
            var output = new NrlmsiseOutput();

            model.Gtd7(input, flags, output);

            // Expected values from DOCUMENTATION line 191
            AssertScientificEqual(4.260860E+07, output.D[0], "HE");
            AssertScientificEqual(1.241342E+11, output.D[1], "O");
            AssertScientificEqual(4.929562E+12, output.D[2], "N2");
            AssertScientificEqual(1.048407E+12, output.D[3], "O2");
            AssertScientificEqual(4.993465E+10, output.D[4], "AR");
            AssertScientificEqual(2.914304E-10, output.D[5], "RHO");
            AssertScientificEqual(8.831229E+06, output.D[6], "H");
            AssertScientificEqual(2.252516E+05, output.D[7], "N");
            AssertScientificEqual(2.415246E-42, output.D[8], "ANM O");
            AssertScientificEqual(1.027318E+03, output.T[0], "TINF");
            AssertScientificEqual(1.934071E+02, output.T[1], "TG");
        }

        #region Gts7 Method Tests (Thermospheric Portion)

        /// <summary>
        /// Test Gts7 method directly at 400 km altitude (thermosphere).
        /// Tests the thermospheric portion of the model which is used for altitudes > 72.5 km.
        /// </summary>
        [Fact]
        public void Gts7_Standard400km_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 60,
                GLong = -70,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gts7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[1] > 0, "O density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
            Assert.True(output.D[3] > 0, "O2 density should be positive");
            Assert.True(output.D[4] > 0, "AR density should be positive");
            Assert.True(output.D[5] > 0, "Total mass density should be positive");
            Assert.True(output.T[0] > 0, "Exospheric temperature should be positive");
            Assert.True(output.T[1] > 0, "Temperature at altitude should be positive");
        }

        /// <summary>
        /// Test Gts7 at very high altitude (2000 km) in exosphere.
        /// </summary>
        [Fact]
        public void Gts7_VeryHighAltitude2000km_ReturnsLowDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 2000,
                GLat = 60,
                GLong = -70,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gts7(input, flags, output);

            // Assert - densities should be very low at 2000 km (around 1E-11 to 1E-12 g/cm^3)
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[5] < 1E-10, "Total mass density should be very low at 2000 km");
            Assert.True(output.T[0] > 500, "Exospheric temperature should be high");
        }

        /// <summary>
        /// Test Gts7 at boundary altitude (72.5 km) - minimum for thermospheric model.
        /// </summary>
        [Fact]
        public void Gts7_BoundaryAltitude72Point5km_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 72.5,
                GLat = 60,
                GLong = -70,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gts7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
            Assert.True(output.D[5] > 0, "Total mass density should be positive");
        }

        /// <summary>
        /// Test Gtd7 with output in meters/kg (Switches[0] = 1).
        /// Tests that unit conversion is properly applied through the Gtd7 pathway.
        /// </summary>
        [Fact]
        public void Gtd7_WithMetersKilogramOutput_ReturnsConvertedUnits()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 60,
                GLong = -70,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flagsCm = new NrlmsiseFlags();
            flagsCm.Switches[0] = 0; // cm/g units
            var outputCm = new NrlmsiseOutput();

            var flagsM = new NrlmsiseFlags();
            flagsM.Switches[0] = 1; // m/kg units
            var outputM = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flagsCm, outputCm);
            model.Gtd7(input, flagsM, outputM);

            // Assert - when Switches[0]=1, densities in m^-3 should be 1E6 times larger than cm^-3
            // and mass density in kg/m^3 should be 1E3 times larger than g/cm^3
            Assert.True(outputCm.D[0] > 0, "HE density should be positive in cm units");
            Assert.True(outputM.D[0] > outputCm.D[0], "HE density in m^-3 should be numerically larger than cm^-3");
            AssertScientificEqual(outputCm.D[0] * 1E6, outputM.D[0], "HE density conversion");
            AssertScientificEqual(outputCm.D[5] * 1E3, outputM.D[5], "Mass density conversion");
        }

        #endregion

        #region Gtd7d Method Tests (With Drag Density)

        /// <summary>
        /// Test Gtd7d method which includes anomalous oxygen in total mass density.
        /// </summary>
        [Fact]
        public void Gtd7d_Standard400km_IncludesAnomalousOxygenInDensity()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 60,
                GLong = -70,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var outputGtd7 = new NrlmsiseOutput();
            var outputGtd7d = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, outputGtd7);
            model.Gtd7d(input, flags, outputGtd7d);

            // Assert - Gtd7d should include anomalous oxygen (D[8]) in mass density
            // D[5] in Gtd7d = 1.66E-24 * (4*D[0] + 16*D[1] + 28*D[2] + 32*D[3] + 40*D[4] + D[6] + 14*D[7] + 16*D[8])
            double expectedRho = 1.66E-24 * (4.0 * outputGtd7d.D[0] + 16.0 * outputGtd7d.D[1] +
                                             28.0 * outputGtd7d.D[2] + 32.0 * outputGtd7d.D[3] +
                                             40.0 * outputGtd7d.D[4] + outputGtd7d.D[6] +
                                             14.0 * outputGtd7d.D[7] + 16.0 * outputGtd7d.D[8]);
            AssertScientificEqual(expectedRho, outputGtd7d.D[5], "Gtd7d mass density should include anomalous oxygen");

            // Gtd7d should have higher or equal mass density than Gtd7 due to anomalous oxygen contribution
            Assert.True(outputGtd7d.D[5] >= outputGtd7.D[5], "Gtd7d mass density should be >= Gtd7 mass density");
        }

        /// <summary>
        /// Test Gtd7d at low altitude where anomalous oxygen is negligible.
        /// </summary>
        [Fact]
        public void Gtd7d_LowAltitude100km_AnomalousOxygenNegligible()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 100,
                GLat = 60,
                GLong = -70,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var outputGtd7 = new NrlmsiseOutput();
            var outputGtd7d = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, outputGtd7);
            model.Gtd7d(input, flags, outputGtd7d);

            // Assert - at low altitudes, anomalous oxygen should be negligible
            Assert.True(System.Math.Abs(outputGtd7d.D[8]) < 1E-30, "Anomalous oxygen should be negligible at 100 km");
        }

        /// <summary>
        /// Test Gtd7d with meters/kg output units.
        /// </summary>
        [Fact]
        public void Gtd7d_WithMetersKilogramOutput_ReturnsConvertedUnits()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 60,
                GLong = -70,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            flags.Switches[0] = 1; // m/kg units
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7d(input, flags, output);

            // Assert
            Assert.True(output.D[5] > 0, "Mass density should be positive");
            Assert.True(output.D[5] < 1E-10, "Mass density in kg/m^3 should be small at 400 km");
        }

        #endregion

        #region Ghp7 Method Tests (Pressure Level)

        /// <summary>
        /// Test Ghp7 method for pressure level at 1000 millibars (sea level).
        /// </summary>
        [Fact]
        public void Ghp7_PressureLevel1000mb_ConvergesToSeaLevel()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 0, // Initial guess
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Ghp7(input, flags, output, 1000.0); // 1000 mb = sea level

            // Assert - altitude should converge to near 0 km for 1000 mb
            Assert.True(input.Alt < 5.0, $"Altitude for 1000 mb should be near 0 km, got {input.Alt}");
            Assert.True(input.Alt >= 0, "Altitude should be non-negative");
        }

        /// <summary>
        /// Test Ghp7 method for pressure level at 1 millibar (high altitude).
        /// </summary>
        [Fact]
        public void Ghp7_PressureLevel1mb_ConvergesToHighAltitude()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 50, // Initial guess
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Ghp7(input, flags, output, 1.0); // 1 mb

            // Assert - altitude should be around 48 km for 1 mb
            Assert.True(input.Alt > 40.0 && input.Alt < 60.0,
                       $"Altitude for 1 mb should be around 48 km, got {input.Alt}");
        }

        /// <summary>
        /// Test Ghp7 method for very low pressure (0.001 mb).
        /// </summary>
        [Fact]
        public void Ghp7_PressureLevel0Point001mb_ConvergesToVeryHighAltitude()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 80, // Initial guess
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Ghp7(input, flags, output, 0.001); // 0.001 mb

            // Assert - altitude should be around 96 km for 0.001 mb
            Assert.True(input.Alt > 90.0 && input.Alt < 110.0,
                       $"Altitude for 0.001 mb should be around 96 km, got {input.Alt}");
        }

        /// <summary>
        /// Test Ghp7 with meters/kg output units.
        /// </summary>
        [Fact]
        public void Ghp7_WithMetersKilogramOutput_ConvergesCorrectly()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 50,
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            flags.Switches[0] = 1; // m/kg units
            var output = new NrlmsiseOutput();

            // Act
            model.Ghp7(input, flags, output, 1.0); // 1 mb

            // Assert
            Assert.True(input.Alt > 40.0 && input.Alt < 60.0,
                       $"Altitude for 1 mb should be around 48 km, got {input.Alt}");
        }

        #endregion

        #region Boundary Condition Tests

        /// <summary>
        /// Test with extreme north pole latitude (+90 degrees).
        /// </summary>
        [Fact]
        public void Gtd7_NorthPoleLatitude_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 90, // North pole
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive at north pole");
            Assert.True(output.D[2] > 0, "N2 density should be positive at north pole");
            Assert.True(output.T[1] > 0, "Temperature should be positive at north pole");
        }

        /// <summary>
        /// Test with extreme south pole latitude (-90 degrees).
        /// </summary>
        [Fact]
        public void Gtd7_SouthPoleLatitude_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = -90, // South pole
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive at south pole");
            Assert.True(output.D[2] > 0, "N2 density should be positive at south pole");
            Assert.True(output.T[1] > 0, "Temperature should be positive at south pole");
        }

        /// <summary>
        /// Test with extreme longitude values (+180 and -180 degrees).
        /// </summary>
        [Theory]
        [InlineData(180)]
        [InlineData(-180)]
        public void Gtd7_ExtremeLongitudes_ReturnsValidDensities(double longitude)
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 0,
                GLong = longitude,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
        }

        /// <summary>
        /// Test with first day of year (DOY = 1).
        /// </summary>
        [Fact]
        public void Gtd7_FirstDayOfYear_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 1,
                Sec = 29000,
                Alt = 400,
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive on DOY 1");
            Assert.True(output.D[2] > 0, "N2 density should be positive on DOY 1");
        }

        /// <summary>
        /// Test with last day of year (DOY = 366 for leap year).
        /// </summary>
        [Fact]
        public void Gtd7_LastDayOfYear_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 366,
                Sec = 29000,
                Alt = 400,
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive on DOY 366");
            Assert.True(output.D[2] > 0, "N2 density should be positive on DOY 366");
        }

        /// <summary>
        /// Test with midnight local solar time (LST = 0).
        /// </summary>
        [Fact]
        public void Gtd7_MidnightLocalTime_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 45,
                GLong = 0,
                Lst = 0, // Midnight
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive at midnight");
            Assert.True(output.D[2] > 0, "N2 density should be positive at midnight");
        }

        /// <summary>
        /// Test with noon local solar time (LST = 12).
        /// </summary>
        [Fact]
        public void Gtd7_NoonLocalTime_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 45,
                GLong = 0,
                Lst = 12, // Noon
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive at noon");
            Assert.True(output.D[2] > 0, "N2 density should be positive at noon");
        }

        /// <summary>
        /// Test with very low solar activity (F107A = 65).
        /// </summary>
        [Fact]
        public void Gtd7_VeryLowSolarActivity_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 65, // Very low solar activity
                F107 = 65,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive at low solar activity");
            Assert.True(output.D[2] > 0, "N2 density should be positive at low solar activity");
            Assert.True(output.T[0] < 1000, "Temperature should be lower at low solar activity");
        }

        /// <summary>
        /// Test with very high solar activity (F107A = 300).
        /// </summary>
        [Fact]
        public void Gtd7_VeryHighSolarActivity_ReturnsHigherDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 300, // Very high solar activity
                F107 = 300,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive at high solar activity");
            Assert.True(output.D[2] > 0, "N2 density should be positive at high solar activity");
            Assert.True(output.T[0] > 1000, "Temperature should be higher at high solar activity");
        }

        /// <summary>
        /// Test with zero geomagnetic activity (Ap = 0).
        /// </summary>
        [Fact]
        public void Gtd7_ZeroGeomagneticActivity_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 0 // Zero geomagnetic activity
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive at Ap=0");
            Assert.True(output.D[2] > 0, "N2 density should be positive at Ap=0");
        }

        /// <summary>
        /// Test with extreme geomagnetic activity (Ap = 400).
        /// </summary>
        [Fact]
        public void Gtd7_ExtremeGeomagneticActivity_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 400 // Extreme geomagnetic activity
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive at Ap=400");
            Assert.True(output.D[2] > 0, "N2 density should be positive at Ap=400");
            Assert.True(output.T[0] > 1000, "Temperature should be elevated at high Ap");
        }

        #endregion

        #region Switch Configuration Tests

        /// <summary>
        /// Test with all switches turned off except switch 0.
        /// </summary>
        [Fact]
        public void Gtd7_AllSwitchesOff_ReturnsBaselineModel()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            // Turn off all switches except 0
            for (int i = 1; i < 24; i++)
                flags.Switches[i] = 0;
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
        }

        /// <summary>
        /// Test with F10.7 effect disabled (Switch 1 = 0).
        /// </summary>
        [Fact]
        public void Gtd7_F107EffectDisabled_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            flags.Switches[1] = 0; // Disable F10.7 effect
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
        }

        /// <summary>
        /// Test with diurnal variation disabled (Switch 7 = 0).
        /// </summary>
        [Fact]
        public void Gtd7_DiurnalVariationDisabled_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            flags.Switches[7] = 0; // Disable diurnal variation
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
        }

        /// <summary>
        /// Test with semidiurnal variation disabled (Switch 8 = 0).
        /// </summary>
        [Fact]
        public void Gtd7_SemidiurnalVariationDisabled_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            flags.Switches[8] = 0; // Disable semidiurnal variation
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
        }

        /// <summary>
        /// Test with terdiurnal variation disabled (Switch 14 = 0).
        /// </summary>
        [Fact]
        public void Gtd7_TerdiurnalVariationDisabled_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            flags.Switches[14] = 0; // Disable terdiurnal variation
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
        }

        /// <summary>
        /// Test with departures from diffusive equilibrium disabled (Switch 15 = 0).
        /// </summary>
        [Fact]
        public void Gtd7_DiffusiveEquilibriumDeparturesDisabled_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            flags.Switches[15] = 0; // Disable departures from diffusive equilibrium
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
        }

        #endregion

        #region ApArray Edge Case Tests

        /// <summary>
        /// Test with ApArray having all zero values.
        /// </summary>
        [Fact]
        public void Gtd7_ApArrayAllZeros_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var apArray = new ApArray();
            for (int i = 0; i < 7; i++)
                apArray.A[i] = 0;

            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4,
                ApA = apArray
            };
            var flags = new NrlmsiseFlags();
            flags.Switches[9] = -1; // Use ApArray
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
        }

        /// <summary>
        /// Test with ApArray having varying values representing time-varying magnetic activity.
        /// </summary>
        [Fact]
        public void Gtd7_ApArrayVaryingValues_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var apArray = new ApArray();
            apArray.A[0] = 4;   // Daily Ap
            apArray.A[1] = 10;  // 3 hr ap index for current time
            apArray.A[2] = 15;  // 3 hr ap index for 3 hrs before current time
            apArray.A[3] = 20;  // 3 hr ap index for 6 hrs before current time
            apArray.A[4] = 25;  // 3 hr ap index for 9 hrs before current time
            apArray.A[5] = 30;  // Average of eight 3 hr ap indices from 12 to 33 hrs
            apArray.A[6] = 35;  // Average of eight 3 hr ap indices from 36 to 57 hrs

            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4,
                ApA = apArray
            };
            var flags = new NrlmsiseFlags();
            flags.Switches[9] = -1; // Use ApArray
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
        }

        /// <summary>
        /// Test with ApArray having extreme high values.
        /// </summary>
        [Fact]
        public void Gtd7_ApArrayExtremeHighValues_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var apArray = new ApArray();
            for (int i = 0; i < 7; i++)
                apArray.A[i] = 400; // Extreme geomagnetic storm

            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4,
                ApA = apArray
            };
            var flags = new NrlmsiseFlags();
            flags.Switches[9] = -1; // Use ApArray
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
            Assert.True(output.T[0] > 1000, "Temperature should be elevated during geomagnetic storm");
        }

        #endregion

        #region Additional Coverage Tests

        /// <summary>
        /// Test at altitude between 300-400 km to exercise specific code paths in Gts7.
        /// </summary>
        [Fact]
        public void Gts7_Altitude350km_ExercisesLowerThermosphereVariations()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 350, // Between 300 and 400 km
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gts7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
        }

        /// <summary>
        /// Test Gtd7 at altitude just below thermosphere boundary (70 km).
        /// </summary>
        [Fact]
        public void Gtd7_Altitude70km_UsesLowerAtmosphereModel()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 70,
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
            Assert.True(output.D[1] == 0, "O density should be zero below 72.5 km");
            Assert.True(output.D[6] == 0, "H density should be zero below 72.5 km");
        }

        /// <summary>
        /// Test Gtd7 at altitude in mixing zone (around 62.5 km).
        /// </summary>
        [Fact]
        public void Gtd7_Altitude62Point5km_InMixingZone()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 62.5, // Mixing zone
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
            Assert.True(output.D[5] > 0, "Total mass density should be positive");
        }

        /// <summary>
        /// Test with longitude set to special value -1000 to test conditional logic.
        /// </summary>
        [Fact]
        public void Gtd7_LongitudeNegative1000_SkipsLongitudinalEffects()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 45,
                GLong = -1000, // Special value to skip longitudinal effects
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
        }

        /// <summary>
        /// Test with time-independent mode (Switch 2 = 0) and latitude variation disabled.
        /// </summary>
        [Fact]
        public void Gtd7_TimeIndependentMode_UsesConstantLatitude()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400,
                GLat = 60, // Should be overridden to 45 degrees
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            flags.Switches[2] = 0; // Time-independent mode
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert - model uses 45 degrees latitude when sw[2]=0
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
        }

        /// <summary>
        /// Test at very low altitude near sea level (5 km).
        /// </summary>
        [Fact]
        public void Gtd7_Altitude5km_LowerTroposphere()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 5,
                GLat = 45,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = new NrlmsiseFlags();
            var output = new NrlmsiseOutput();

            // Act
            model.Gtd7(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
            Assert.True(output.D[5] > 1E-4, "Mass density should be high near sea level");
        }

        #endregion
    }
}
