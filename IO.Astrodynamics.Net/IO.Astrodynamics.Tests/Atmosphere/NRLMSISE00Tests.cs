using System;
using Xunit;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;

namespace IO.Astrodynamics.Tests.Atmosphere
{
    /// <summary>
    /// Unit tests for the NRLMSISE-00 atmospheric model.
    /// All inputs and expected outputs use SI units (meters, radians, m^-3, kg/m^3, Kelvin).
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
        /// DOY=172, UT=29000s, ALT=400000m, LAT=1.047198rad, LONG=-1.221730rad, LST=16h, F107A=150, F107=150, AP=4
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
                Alt = 400000,
                GLat = 60 * System.Math.PI / 180.0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            var output = model.Calculate(input, flags);

            // Expected values from DOCUMENTATION line 159 (converted to SI units)
            AssertScientificEqual(6.665177E+05 * 1E6, output.D[0], "HE");
            AssertScientificEqual(1.138806E+08 * 1E6, output.D[1], "O");
            AssertScientificEqual(1.998211E+07 * 1E6, output.D[2], "N2");
            AssertScientificEqual(4.022764E+05 * 1E6, output.D[3], "O2");
            AssertScientificEqual(3.557465E+03 * 1E6, output.D[4], "AR");
            AssertScientificEqual(4.074714E-15 * 1000, output.D[5], "RHO");
            AssertScientificEqual(3.475312E+04 * 1E6, output.D[6], "H");
            AssertScientificEqual(4.095913E+06 * 1E6, output.D[7], "N");
            AssertScientificEqual(2.667273E+04 * 1E6, output.D[8], "ANM O");
            AssertScientificEqual(1.250540E+03, output.T[0], "TINF");
            AssertScientificEqual(1.241416E+03, output.T[1], "TG");

            var stringOutput = output.ToString();
            var expected = @"NRLMSISE-00 Atmospheric Model Output:
Densities:
  He (Helium):          6.665E+011 m^-3
  O (Oxygen):           1.139E+014 m^-3
  N2 (Nitrogen):        1.998E+013 m^-3
  O2 (Dioxygen):        4.023E+011 m^-3
  Ar (Argon):           3.557E+009 m^-3
  Total Mass Density:   4.075E-012 kg/m^3
  H (Hydrogen):         3.475E+010 m^-3
  N (Nitrogen):         4.096E+012 m^-3
  Anomalous O:          2.667E+010 m^-3

Temperatures:
  Exospheric:           1250.54 K
  At Altitude:          1241.42 K";
            Assert.Equal(TestHelpers.NormalizeWhitespace(expected), TestHelpers.NormalizeWhitespace(stringOutput));
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
                Alt = 400000,
                GLat = 60 * System.Math.PI / 180.0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            var output = model.Calculate(input, flags);

            // Expected values from DOCUMENTATION line 161 (converted to SI units)
            AssertScientificEqual(3.407293E+06 * 1E6, output.D[0], "HE");
            AssertScientificEqual(1.586333E+08 * 1E6, output.D[1], "O");
            AssertScientificEqual(1.391117E+07 * 1E6, output.D[2], "N2");
            AssertScientificEqual(3.262560E+05 * 1E6, output.D[3], "O2");
            AssertScientificEqual(1.559618E+03 * 1E6, output.D[4], "AR");
            AssertScientificEqual(5.001846E-15 * 1000, output.D[5], "RHO");
            AssertScientificEqual(4.854208E+04 * 1E6, output.D[6], "H");
            AssertScientificEqual(4.380967E+06 * 1E6, output.D[7], "N");
            AssertScientificEqual(6.956682E+03 * 1E6, output.D[8], "ANM O");
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
                Alt = 1000000,
                GLat = 60 * System.Math.PI / 180.0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            var output = model.Calculate(input, flags);

            // Expected values from DOCUMENTATION line 163 (converted to SI units)
            AssertScientificEqual(1.123767E+05 * 1E6, output.D[0], "HE");
            AssertScientificEqual(6.934130E+04 * 1E6, output.D[1], "O");
            AssertScientificEqual(4.247105E+01 * 1E6, output.D[2], "N2");
            AssertScientificEqual(1.322750E-01 * 1E6, output.D[3], "O2");
            AssertScientificEqual(2.618848E-05 * 1E6, output.D[4], "AR");
            AssertScientificEqual(2.756772E-18 * 1000, output.D[5], "RHO");
            AssertScientificEqual(2.016750E+04 * 1E6, output.D[6], "H");
            AssertScientificEqual(5.741256E+03 * 1E6, output.D[7], "N");
            AssertScientificEqual(2.374394E+04 * 1E6, output.D[8], "ANM O");
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
                Alt = 100000,
                GLat = 60 * System.Math.PI / 180.0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            var output = model.Calculate(input, flags);

            // Expected values from DOCUMENTATION line 165 (converted to SI units)
            AssertScientificEqual(5.411554E+07 * 1E6, output.D[0], "HE");
            AssertScientificEqual(1.918893E+11 * 1E6, output.D[1], "O");
            AssertScientificEqual(6.115826E+12 * 1E6, output.D[2], "N2");
            AssertScientificEqual(1.225201E+12 * 1E6, output.D[3], "O2");
            AssertScientificEqual(6.023212E+10 * 1E6, output.D[4], "AR");
            AssertScientificEqual(3.584426E-10 * 1000, output.D[5], "RHO");
            AssertScientificEqual(1.059880E+07 * 1E6, output.D[6], "H");
            AssertScientificEqual(2.615737E+05 * 1E6, output.D[7], "N");
            AssertScientificEqual(2.819879E-42 * 1E6, output.D[8], "ANM O");
            AssertScientificEqual(1.027318E+03, output.T[0], "TINF");
            AssertScientificEqual(2.068878E+02, output.T[1], "TG");
        }

        /// <summary>
        /// Test Case 5: Equatorial latitude (LAT=0 rad).
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
                Alt = 400000,
                GLat = 0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            var output = model.Calculate(input, flags);

            // Expected values from DOCUMENTATION line 167 (converted to SI units)
            AssertScientificEqual(1.851122E+06 * 1E6, output.D[0], "HE");
            AssertScientificEqual(1.476555E+08 * 1E6, output.D[1], "O");
            AssertScientificEqual(1.579356E+07 * 1E6, output.D[2], "N2");
            AssertScientificEqual(2.633795E+05 * 1E6, output.D[3], "O2");
            AssertScientificEqual(1.588781E+03 * 1E6, output.D[4], "AR");
            AssertScientificEqual(4.809630E-15 * 1000, output.D[5], "RHO");
            AssertScientificEqual(5.816167E+04 * 1E6, output.D[6], "H");
            AssertScientificEqual(5.478984E+06 * 1E6, output.D[7], "N");
            AssertScientificEqual(1.264446E+03 * 1E6, output.D[8], "ANM O");
            AssertScientificEqual(1.212396E+03, output.T[0], "TINF");
            AssertScientificEqual(1.208135E+03, output.T[1], "TG");
        }

        /// <summary>
        /// Test Case 6: Different longitude (LONG=0 rad).
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
                Alt = 400000,
                GLat = 60 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            var output = model.Calculate(input, flags);

            // Expected values from DOCUMENTATION line 169 (converted to SI units)
            AssertScientificEqual(8.673095E+05 * 1E6, output.D[0], "HE");
            AssertScientificEqual(1.278862E+08 * 1E6, output.D[1], "O");
            AssertScientificEqual(1.822577E+07 * 1E6, output.D[2], "N2");
            AssertScientificEqual(2.922214E+05 * 1E6, output.D[3], "O2");
            AssertScientificEqual(2.402962E+03 * 1E6, output.D[4], "AR");
            AssertScientificEqual(4.355866E-15 * 1000, output.D[5], "RHO");
            AssertScientificEqual(3.686389E+04 * 1E6, output.D[6], "H");
            AssertScientificEqual(3.897276E+06 * 1E6, output.D[7], "N");
            AssertScientificEqual(2.667273E+04 * 1E6, output.D[8], "ANM O");
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
                Alt = 400000,
                GLat = 60 * System.Math.PI / 180.0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 4,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            var output = model.Calculate(input, flags);

            // Expected values from DOCUMENTATION line 171 (converted to SI units)
            AssertScientificEqual(5.776251E+05 * 1E6, output.D[0], "HE");
            AssertScientificEqual(6.979139E+07 * 1E6, output.D[1], "O");
            AssertScientificEqual(1.236814E+07 * 1E6, output.D[2], "N2");
            AssertScientificEqual(2.492868E+05 * 1E6, output.D[3], "O2");
            AssertScientificEqual(1.405739E+03 * 1E6, output.D[4], "AR");
            AssertScientificEqual(2.470651E-15 * 1000, output.D[5], "RHO");
            AssertScientificEqual(5.291986E+04 * 1E6, output.D[6], "H");
            AssertScientificEqual(1.069814E+06 * 1E6, output.D[7], "N");
            AssertScientificEqual(2.667273E+04 * 1E6, output.D[8], "ANM O");
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
                Alt = 400000,
                GLat = 60 * System.Math.PI / 180.0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 16,
                F107A = 70,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            var output = model.Calculate(input, flags);

            // Expected values from DOCUMENTATION line 173 (converted to SI units)
            AssertScientificEqual(3.740304E+05 * 1E6, output.D[0], "HE");
            AssertScientificEqual(4.782720E+07 * 1E6, output.D[1], "O");
            AssertScientificEqual(5.240380E+06 * 1E6, output.D[2], "N2");
            AssertScientificEqual(1.759875E+05 * 1E6, output.D[3], "O2");
            AssertScientificEqual(5.501649E+02 * 1E6, output.D[4], "AR");
            AssertScientificEqual(1.571889E-15 * 1000, output.D[5], "RHO");
            AssertScientificEqual(8.896776E+04 * 1E6, output.D[6], "H");
            AssertScientificEqual(1.979741E+06 * 1E6, output.D[7], "N");
            AssertScientificEqual(9.121815E+03 * 1E6, output.D[8], "ANM O");
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
                Alt = 400000,
                GLat = 60 * System.Math.PI / 180.0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 16,
                F107A = 150,
                F107 = 180,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            var output = model.Calculate(input, flags);

            // Expected values from DOCUMENTATION line 175 (converted to SI units)
            AssertScientificEqual(6.748339E+05 * 1E6, output.D[0], "HE");
            AssertScientificEqual(1.245315E+08 * 1E6, output.D[1], "O");
            AssertScientificEqual(2.369010E+07 * 1E6, output.D[2], "N2");
            AssertScientificEqual(4.911583E+05 * 1E6, output.D[3], "O2");
            AssertScientificEqual(4.578781E+03 * 1E6, output.D[4], "AR");
            AssertScientificEqual(4.564420E-15 * 1000, output.D[5], "RHO");
            AssertScientificEqual(3.244595E+04 * 1E6, output.D[6], "H");
            AssertScientificEqual(5.370833E+06 * 1E6, output.D[7], "N");
            AssertScientificEqual(2.667273E+04 * 1E6, output.D[8], "ANM O");
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
                Alt = 400000,
                GLat = 60 * System.Math.PI / 180.0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 40
            };
            var flags = NrlmsiseFlags.CreateStandard();

            var output = model.Calculate(input, flags);

            // Expected values from DOCUMENTATION line 177 (converted to SI units)
            AssertScientificEqual(5.528601E+05 * 1E6, output.D[0], "HE");
            AssertScientificEqual(1.198041E+08 * 1E6, output.D[1], "O");
            AssertScientificEqual(3.495798E+07 * 1E6, output.D[2], "N2");
            AssertScientificEqual(9.339618E+05 * 1E6, output.D[3], "O2");
            AssertScientificEqual(1.096255E+04 * 1E6, output.D[4], "AR");
            AssertScientificEqual(4.974543E-15 * 1000, output.D[5], "RHO");
            AssertScientificEqual(2.686428E+04 * 1E6, output.D[6], "H");
            AssertScientificEqual(4.889974E+06 * 1E6, output.D[7], "N");
            AssertScientificEqual(2.805445E+04 * 1E6, output.D[8], "ANM O");
            AssertScientificEqual(1.361868E+03, output.T[0], "TINF");
            AssertScientificEqual(1.347389E+03, output.T[1], "TG");
        }

        /// <summary>
        /// Test Case 11-15: Very low altitudes (0-70 km).
        /// Tests the lower atmosphere model component.
        /// </summary>
        [Theory]
        [InlineData(0, 1.375488E+14 * 1E6, 0.000000E+00, 2.049687E+19 * 1E6, 5.498695E+18 * 1E6, 2.451733E+17 * 1E6,
            1.261066E-03 * 1000, 0.000000E+00, 0.000000E+00, 0.000000E+00, 1.027318E+03, 2.814648E+02)]
        [InlineData(10000, 4.427443E+13 * 1E6, 0.000000E+00, 6.597567E+18 * 1E6, 1.769929E+18 * 1E6, 7.891680E+16 * 1E6,
            4.059139E-04 * 1000, 0.000000E+00, 0.000000E+00, 0.000000E+00, 1.027318E+03, 2.274180E+02)]
        [InlineData(30000, 2.127829E+12 * 1E6, 0.000000E+00, 3.170791E+17 * 1E6, 8.506280E+16 * 1E6, 3.792741E+15 * 1E6,
            1.950822E-05 * 1000, 0.000000E+00, 0.000000E+00, 0.000000E+00, 1.027318E+03, 2.374389E+02)]
        [InlineData(50000, 1.412184E+11 * 1E6, 0.000000E+00, 2.104370E+16 * 1E6, 5.645392E+15 * 1E6, 2.517142E+14 * 1E6,
            1.294709E-06 * 1000, 0.000000E+00, 0.000000E+00, 0.000000E+00, 1.027318E+03, 2.795551E+02)]
        [InlineData(70000, 1.254884E+10 * 1E6, 0.000000E+00, 1.874533E+15 * 1E6, 4.923051E+14 * 1E6, 2.239685E+13 * 1E6,
            1.147668E-07 * 1000, 0.000000E+00, 0.000000E+00, 0.000000E+00, 1.027318E+03, 2.190732E+02)]
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
                GLat = 60 * System.Math.PI / 180.0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            var output = model.Calculate(input, flags);

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
                Alt = 400000,
                GLat = 60 * System.Math.PI / 180.0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4,
                ApA = apArray
            };
            var flags = NrlmsiseFlags.CreateStandard();
            flags.Switches[9] = -1;

            var output = model.Calculate(input, flags);

            // Expected values from DOCUMENTATION line 189 (converted to SI units)
            AssertScientificEqual(5.196477E+05 * 1E6, output.D[0], "HE");
            AssertScientificEqual(1.274494E+08 * 1E6, output.D[1], "O");
            AssertScientificEqual(4.850450E+07 * 1E6, output.D[2], "N2");
            AssertScientificEqual(1.720838E+06 * 1E6, output.D[3], "O2");
            AssertScientificEqual(2.354487E+04 * 1E6, output.D[4], "AR");
            AssertScientificEqual(5.881940E-15 * 1000, output.D[5], "RHO");
            AssertScientificEqual(2.500078E+04 * 1E6, output.D[6], "H");
            AssertScientificEqual(6.279210E+06 * 1E6, output.D[7], "N");
            AssertScientificEqual(2.667273E+04 * 1E6, output.D[8], "ANM O");
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
                Alt = 100000,
                GLat = 60 * System.Math.PI / 180.0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4,
                ApA = apArray
            };
            var flags = NrlmsiseFlags.CreateStandard();
            flags.Switches[9] = -1;

            var output = model.Calculate(input, flags);

            // Expected values from DOCUMENTATION line 191 (converted to SI units)
            AssertScientificEqual(4.260860E+07 * 1E6, output.D[0], "HE");
            AssertScientificEqual(1.241342E+11 * 1E6, output.D[1], "O");
            AssertScientificEqual(4.929562E+12 * 1E6, output.D[2], "N2");
            AssertScientificEqual(1.048407E+12 * 1E6, output.D[3], "O2");
            AssertScientificEqual(4.993465E+10 * 1E6, output.D[4], "AR");
            AssertScientificEqual(2.914304E-10 * 1000, output.D[5], "RHO");
            AssertScientificEqual(8.831229E+06 * 1E6, output.D[6], "H");
            AssertScientificEqual(2.252516E+05 * 1E6, output.D[7], "N");
            AssertScientificEqual(2.415246E-42 * 1E6, output.D[8], "ANM O");
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
                Alt = 400000,
                GLat = 60 * System.Math.PI / 180.0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();
            var output = new NrlmsiseOutput();

            // Act
            model.CalculateThermosphere(input, flags, output);

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
        /// At this altitude, temperature approaches the exospheric temperature.
        /// </summary>
        [Fact]
        public void Gts7_VeryHighAltitude2000km_ReturnsValidDensities()
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 2000000, // 2000 km
                GLat = 60 * System.Math.PI / 180.0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();
            var output = new NrlmsiseOutput();

            // Act
            model.CalculateThermosphere(input, flags, output);

            // Assert - densities should be positive
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[5] > 0, "Total mass density should be positive");

            // Temperature at 2000 km should approach exospheric temperature
            Assert.True(output.T[1] > 0, "Temperature at altitude should be positive");
            Assert.True(output.T[0] > 0, "Exospheric temperature should be positive");

            // Temperature at altitude should be within ~10% of TINF at 2000 km
            double relDiff = System.Math.Abs(output.T[0] - output.T[1]) / output.T[0];
            Assert.True(relDiff < 0.10,
                $"At 2000 km, temperature ({output.T[1]:F1} K) should be within 10% of TINF ({output.T[0]:F1} K)");
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
                Alt = 72500,
                GLat = 60 * System.Math.PI / 180.0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();
            var output = new NrlmsiseOutput();

            // Act
            model.CalculateThermosphere(input, flags, output);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
            Assert.True(output.D[5] > 0, "Total mass density should be positive");
        }

        /// <summary>
        /// Test that Gtd7 always returns values in SI units (m^-3, kg/m^3).
        /// Note: Switches[0] is now ignored - the API always uses SI units.
        /// Uses same conditions as TestCase01 and verifies exact expected values.
        /// </summary>
        [Fact]
        public void Gtd7_AlwaysReturnsSIUnits()
        {
            // Arrange - same conditions as TestCase01
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400000,
                GLat = 60 * System.Math.PI / 180.0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var output = model.Calculate(input, flags);

            // Assert - verify exact expected SI unit values (from TestCase01)
            // Number densities in m^-3 (should match DOCUMENTATION values * 1E6)
            AssertScientificEqual(6.665177E+05 * 1E6, output.D[0], "HE density (m^-3)");
            AssertScientificEqual(1.138806E+08 * 1E6, output.D[1], "O density (m^-3)");
            // Mass density in kg/m^3 (should match DOCUMENTATION value * 1000)
            AssertScientificEqual(4.074714E-15 * 1000, output.D[5], "Mass density (kg/m^3)");
            // Temperature in Kelvin
            AssertScientificEqual(1.250540E+03, output.T[0], "Exospheric temperature (K)");
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
                Alt = 400000,
                GLat = 60 * System.Math.PI / 180.0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var outputGtd7 = model.Calculate(input, flags);
            var outputGtd7d = model.CalculateWithDrag(input, flags);

            // Assert - Gtd7d should include anomalous oxygen (D[8]) in mass density
            // D[5] in Gtd7d (kg/m^3) = 1.66E-27 * (4*D[0] + 16*D[1] + 28*D[2] + 32*D[3] + 40*D[4] + D[6] + 14*D[7] + 16*D[8])
            // where D[0-8] are in m^-3 (SI units)
            double expectedRho = 1.66E-27 * (4.0 * outputGtd7d.D[0] + 16.0 * outputGtd7d.D[1] +
                                             28.0 * outputGtd7d.D[2] + 32.0 * outputGtd7d.D[3] +
                                             40.0 * outputGtd7d.D[4] + outputGtd7d.D[6] +
                                             14.0 * outputGtd7d.D[7] + 16.0 * outputGtd7d.D[8]);
            AssertScientificEqual(expectedRho, outputGtd7d.D[5], "Gtd7d mass density should include anomalous oxygen");

            // Gtd7d should have higher or equal mass density than Gtd7 due to anomalous oxygen contribution
            Assert.True(outputGtd7d.D[5] >= outputGtd7.D[5], "Gtd7d mass density should be >= Gtd7 mass density");
        }

        /// <summary>
        /// Test Gtd7d at low altitude where anomalous oxygen is negligible.
        /// At 100 km, anomalous oxygen should be essentially zero, so Calculate and
        /// CalculateWithDrag should return the same mass density.
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
                Alt = 100000,
                GLat = 60 * System.Math.PI / 180.0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var outputStandard = model.Calculate(input, flags);
            var outputWithDrag = model.CalculateWithDrag(input, flags);

            // Assert - at low altitudes, anomalous oxygen should be negligible
            Assert.True(System.Math.Abs(outputWithDrag.D[8]) < 1E-30,
                $"Anomalous oxygen should be negligible at 100 km, got {outputWithDrag.D[8]:E3}");

            // Since anomalous oxygen is negligible, both methods should give same mass density
            double relDiff = System.Math.Abs(outputWithDrag.D[5] - outputStandard.D[5]) / outputStandard.D[5];
            Assert.True(relDiff < 1E-10,
                $"Mass densities should be equal when anomalous O is negligible. Standard: {outputStandard.D[5]:E6}, WithDrag: {outputWithDrag.D[5]:E6}");
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
                Alt = 400000,
                GLat = 60 * System.Math.PI / 180.0,
                GLong = -70 * System.Math.PI / 180.0,
                Lst = 16,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();
            flags.Switches[0] = 1; // m/kg units

            // Act
            var output = model.CalculateWithDrag(input, flags);

            // Assert
            Assert.True(output.D[5] > 0, "Mass density should be positive");
            Assert.True(output.D[5] < 1E-10, "Mass density in kg/m^3 should be small at 400 km");
        }

        #endregion

        #region Ghp7 Method Tests (Pressure Level)

        /// <summary>
        /// Test Ghp7 method for pressure level at 1000 millibars (sea level).
        /// Standard atmosphere: 1000 mb should correspond to ~110m altitude.
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
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var result = model.FindAltitudeAtPressure(input, flags, 1000.0); // 1000 mb = sea level

            // Assert - 1000 mb should be within ~500m of sea level
            Assert.True(result.altitude < 1000, $"Altitude for 1000 mb should be near 0 m, got {result.altitude}");
            Assert.True(result.altitude >= -500, "Altitude should be near sea level");
            Assert.NotNull(result.atmosphere);
            Assert.True(result.atmosphere.D[5] > 1E-3, "Mass density at sea level should be > 1 g/m^3");
        }

        /// <summary>
        /// Test Ghp7 method for pressure level at 1 millibar (high altitude).
        /// Standard atmosphere: 1 mb corresponds to ~48 km altitude.
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
                Alt = 50000, // Initial guess
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var result = model.FindAltitudeAtPressure(input, flags, 1.0); // 1 mb

            // Assert - altitude should be around 48 km (48000 m) for 1 mb, ±5%
            double expectedAlt = 48000.0;
            double tolerance = 0.05; // 5% tolerance
            Assert.True(result.altitude > expectedAlt * (1 - tolerance) && result.altitude < expectedAlt * (1 + tolerance),
                $"Altitude for 1 mb should be around {expectedAlt} m (±5%), got {result.altitude}");
            Assert.True(result.atmosphere.D[5] > 0, "Mass density should be positive");
        }

        /// <summary>
        /// Test Ghp7 method for very low pressure (0.001 mb).
        /// Standard atmosphere: 0.001 mb corresponds to ~96 km altitude.
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
                Alt = 80000, // Initial guess
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var result = model.FindAltitudeAtPressure(input, flags, 0.001); // 0.001 mb

            // Assert - altitude should be around 96 km (96000 m) for 0.001 mb, ±5%
            double expectedAlt = 96000.0;
            double tolerance = 0.05; // 5% tolerance
            Assert.True(result.altitude > expectedAlt * (1 - tolerance) && result.altitude < expectedAlt * (1 + tolerance),
                $"Altitude for 0.001 mb should be around {expectedAlt} m (±5%), got {result.altitude}");
            Assert.True(result.atmosphere.T[1] > 0, "Temperature should be positive");
        }

        /// <summary>
        /// Test Ghp7 with meters/kg output units - should produce same result as without.
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
                Alt = 50000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();
            flags.Switches[0] = 1; // m/kg units

            // Act
            var result = model.FindAltitudeAtPressure(input, flags, 1.0); // 1 mb

            // Assert - should be same as without switch (48km ±5%)
            double expectedAlt = 48000.0;
            double tolerance = 0.05; // 5% tolerance
            Assert.True(result.altitude > expectedAlt * (1 - tolerance) && result.altitude < expectedAlt * (1 + tolerance),
                $"Altitude for 1 mb should be around {expectedAlt} m (±5%), got {result.altitude}");
        }

        #endregion

        #region Boundary Condition Tests

        /// <summary>
        /// Test with extreme north pole latitude (+90 degrees = π/2 rad).
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
                Alt = 400000,
                GLat = 90 * System.Math.PI / 180.0, // North pole
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var output = model.Calculate(input, flags);
            Assert.True(output.D[0] > 0, "HE density should be positive at north pole");
            Assert.True(output.D[2] > 0, "N2 density should be positive at north pole");
            Assert.True(output.T[1] > 0, "Temperature should be positive at north pole");
        }

        /// <summary>
        /// Test with extreme south pole latitude (-90 degrees = -π/2 rad).
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
                Alt = 400000,
                GLat = -90 * System.Math.PI / 180.0, // South pole
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var output = model.Calculate(input, flags);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive at south pole");
            Assert.True(output.D[2] > 0, "N2 density should be positive at south pole");
            Assert.True(output.T[1] > 0, "Temperature should be positive at south pole");
        }

        /// <summary>
        /// Test with extreme longitude values (+180 and -180 degrees = ±π rad).
        /// </summary>
        [Theory]
        [InlineData(180)]
        [InlineData(-180)]
        public void Gtd7_ExtremeLongitudes_ReturnsValidDensities(double longitudeDeg)
        {
            // Arrange
            var model = new NRLMSISE00();
            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400000,
                GLat = 0,
                GLong = longitudeDeg * System.Math.PI / 180.0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var output = model.Calculate(input, flags);
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
                Alt = 400000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            
            // Act
            var output=model.Calculate(input, flags);

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
                Alt = 400000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var output=model.Calculate(input, flags);

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
                Alt = 400000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 0, // Midnight
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var output = model.Calculate(input, flags);

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
                Alt = 400000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12, // Noon
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var output = model.Calculate(input, flags);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive at noon");
            Assert.True(output.D[2] > 0, "N2 density should be positive at noon");
        }

        /// <summary>
        /// Test with very low solar activity (F107A = 65).
        /// Higher solar activity leads to higher thermospheric temperatures and densities.
        /// </summary>
        [Fact]
        public void Gtd7_VeryLowSolarActivity_ReturnsLowerTemperatureAndDensity()
        {
            // Arrange
            var model = new NRLMSISE00();
            var baseInput = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150, // Moderate solar activity (baseline)
                F107 = 150,
                Ap = 4
            };
            var lowInput = baseInput with { F107A = 65, F107 = 65 }; // Very low solar activity
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var outputBaseline = model.Calculate(baseInput, flags);
            var outputLow = model.Calculate(lowInput, flags);

            // Assert - low solar activity should give lower temperature and density
            Assert.True(outputLow.T[0] < outputBaseline.T[0],
                $"Exospheric temp at low F10.7 ({outputLow.T[0]:F1} K) should be < baseline ({outputBaseline.T[0]:F1} K)");
            Assert.True(outputLow.D[5] < outputBaseline.D[5],
                $"Mass density at low F10.7 ({outputLow.D[5]:E3}) should be < baseline ({outputBaseline.D[5]:E3})");
            Assert.True(outputLow.T[0] < 1000, "Temperature should be below 1000 K at low solar activity");
        }

        /// <summary>
        /// Test with very high solar activity (F107A = 300).
        /// Higher solar activity leads to higher thermospheric temperatures and densities.
        /// </summary>
        [Fact]
        public void Gtd7_VeryHighSolarActivity_ReturnsHigherTemperatureAndDensity()
        {
            // Arrange
            var model = new NRLMSISE00();
            var baseInput = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150, // Moderate solar activity (baseline)
                F107 = 150,
                Ap = 4
            };
            var highInput = baseInput with { F107A = 300, F107 = 300 }; // Very high solar activity
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var outputBaseline = model.Calculate(baseInput, flags);
            var outputHigh = model.Calculate(highInput, flags);

            // Assert - high solar activity should give higher temperature and density
            Assert.True(outputHigh.T[0] > outputBaseline.T[0],
                $"Exospheric temp at high F10.7 ({outputHigh.T[0]:F1} K) should be > baseline ({outputBaseline.T[0]:F1} K)");
            Assert.True(outputHigh.D[5] > outputBaseline.D[5],
                $"Mass density at high F10.7 ({outputHigh.D[5]:E3}) should be > baseline ({outputBaseline.D[5]:E3})");
            Assert.True(outputHigh.T[0] > 1000, "Temperature should be above 1000 K at high solar activity");
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
                Alt = 400000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 0 // Zero geomagnetic activity
            };
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var output = model.Calculate(input, flags);

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
                Alt = 400000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 400 // Extreme geomagnetic activity
            };
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var output = model.Calculate(input, flags);

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
                Alt = 400000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();
            // Turn off all switches except 0
            for (int i = 1; i < 24; i++)
                flags.Switches[i] = 0;

            // Act
            var output = model.Calculate(input, flags);

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
                Alt = 400000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();
            flags.Switches[1] = 0; // Disable F10.7 effect

            // Act
            var output = model.Calculate(input, flags);

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
                Alt = 400000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();
            flags.Switches[7] = 0; // Disable diurnal variation

            // Act
            var output = model.Calculate(input, flags);

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
                Alt = 400000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();
            flags.Switches[8] = 0; // Disable semidiurnal variation

            // Act
            var output = model.Calculate(input, flags);

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
                Alt = 400000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();
            flags.Switches[14] = 0; // Disable terdiurnal variation

            // Act
            var output = model.Calculate(input, flags);

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
                Alt = 400000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();
            flags.Switches[15] = 0; // Disable departures from diffusive equilibrium

            // Act
            var output = model.Calculate(input, flags);

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
                Alt = 400000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4,
                ApA = apArray
            };
            var flags = NrlmsiseFlags.CreateStandard();
            flags.Switches[9] = -1; // Use ApArray

            // Act
            var output = model.Calculate(input, flags);

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
            apArray.A[0] = 4; // Daily Ap
            apArray.A[1] = 10; // 3 hr ap index for current time
            apArray.A[2] = 15; // 3 hr ap index for 3 hrs before current time
            apArray.A[3] = 20; // 3 hr ap index for 6 hrs before current time
            apArray.A[4] = 25; // 3 hr ap index for 9 hrs before current time
            apArray.A[5] = 30; // Average of eight 3 hr ap indices from 12 to 33 hrs
            apArray.A[6] = 35; // Average of eight 3 hr ap indices from 36 to 57 hrs

            var input = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4,
                ApA = apArray
            };
            var flags = NrlmsiseFlags.CreateStandard();
            flags.Switches[9] = -1; // Use ApArray

            // Act
            var output = model.Calculate(input, flags);

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
                Alt = 400000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4,
                ApA = apArray
            };
            var flags = NrlmsiseFlags.CreateStandard();
            flags.Switches[9] = -1; // Use ApArray

            // Act
            var output = model.Calculate(input, flags);

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
                Alt = 350000, // Between 300 and 400 km
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();
            var output = new NrlmsiseOutput();

            // Act
            model.CalculateThermosphere(input, flags, output);

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
                Alt = 70000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var output = model.Calculate(input, flags);

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
                Alt = 62500, // Mixing zone
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var output = model.Calculate(input, flags);

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
                Alt = 400000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = -1000, // Special value to skip longitudinal effects
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var output = model.Calculate(input, flags);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
        }

        /// <summary>
        /// Test with time-independent mode (Switch 2 = 0) and latitude variation disabled.
        /// When sw[2]=0, the model uses a constant latitude of 45 degrees for gravity calculations.
        /// </summary>
        [Fact]
        public void Gtd7_TimeIndependentMode_UsesConstantLatitude()
        {
            // Arrange
            var model = new NRLMSISE00();
            var baseInput = new NrlmsiseInput
            {
                Year = 0,
                Doy = 172,
                Sec = 29000,
                Alt = 400000,
                GLat = 60 * System.Math.PI / 180.0, // 60 degrees, will be overridden
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };

            var flagsTimeIndependent = NrlmsiseFlags.CreateStandard();
            flagsTimeIndependent.Switches[2] = 0; // Time-independent mode (uses 45° lat)

            var flagsNormal = NrlmsiseFlags.CreateStandard();

            // Act - calculate with switch[2]=0 at 60° latitude
            var outputTimeIndep60 = model.Calculate(baseInput, flagsTimeIndependent);
            // Calculate with normal mode at 60° latitude (for comparison)
            var outputNormal60 = model.Calculate(baseInput, flagsNormal);

            // Assert - time-independent mode at 60° should behave like 45° for gravity
            // The temperature should be affected since gravity affects scale height
            Assert.True(outputTimeIndep60.D[0] > 0, "HE density should be positive");
            Assert.True(outputTimeIndep60.D[5] > 0, "Mass density should be positive");

            // The 60° input with sw[2]=0 should differ from normal 60° (verifies the switch has effect)
            Assert.NotEqual(outputTimeIndep60.D[5], outputNormal60.D[5]);
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
                Alt = 5000,
                GLat = 45 * System.Math.PI / 180.0,
                GLong = 0,
                Lst = 12,
                F107A = 150,
                F107 = 150,
                Ap = 4
            };
            var flags = NrlmsiseFlags.CreateStandard();

            // Act
            var output = model.Calculate(input, flags);

            // Assert
            Assert.True(output.D[0] > 0, "HE density should be positive");
            Assert.True(output.D[2] > 0, "N2 density should be positive");
            Assert.True(output.D[5] > 1E-4, "Mass density should be high near sea level");
        }

        #endregion
    }
}