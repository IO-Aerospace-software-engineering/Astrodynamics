using System;
using Xunit;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;

namespace IO.Astrodynamics.Tests.Atmosphere
{
    /// <summary>
    /// Unit tests for the NRLMSISE-00 atmospheric model.
    /// </summary>
    /// <remarks>
    /// These tests validate the C# implementation against the reference C implementation
    /// using expected output values from the DOCUMENTATION file in nrlmsise-00-cmake.
    ///
    /// Test data is from the official NRLMSISE-00 test suite (nrlmsise-00_test.c).
    /// Expected values are from lines 159-192 of the DOCUMENTATION file.
    /// </remarks>
    public class NRLMSISE00Tests
    {
        private const double Tolerance = 1e-5; // Relative tolerance for comparing scientific notation values (0.001%)

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
    }
}
