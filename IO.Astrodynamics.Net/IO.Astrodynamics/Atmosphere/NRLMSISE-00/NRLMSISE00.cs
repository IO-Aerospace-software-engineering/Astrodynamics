using System;

namespace IO.Astrodynamics.Atmosphere.NRLMSISE_00
{
    /// <summary>
    /// NRLMSISE-00 Empirical Atmosphere Model.
    /// </summary>
    /// <remarks>
    /// The NRLMSISE-00 model describes the neutral temperature and densities in
    /// Earth's atmosphere from ground to thermospheric heights. Below 72.5 km
    /// the model is primarily based on the MAP Handbook tabulation. Above 72.5 km
    /// NRLMSISE-00 is essentially a revised MSIS-86 model.
    /// </remarks>
    public class NRLMSISE00
    {
        private double _gsurf;
        private double _re;
        private double _dm04, _dm16, _dm28, _dm32, _dm40, _dm01, _dm14;
        private readonly double[] _mesoTn1 = new double[5];
        private readonly double[] _mesoTn2 = new double[4];
        private readonly double[] _mesoTn3 = new double[5];
        private readonly double[] _mesoTgn1 = new double[2];
        private readonly double[] _mesoTgn2 = new double[2];
        private readonly double[] _mesoTgn3 = new double[2];
        private double _dfa;
        private readonly double[,] _plg = new double[4, 9];
        private double _ctloc, _stloc;
        private double _c2tloc, _s2tloc;
        private double _s3tloc, _c3tloc;
        private double _apdf;
        private readonly double[] _apt = new double[4];

        /// <summary>
        /// TSELEC - Set switches based on flags.
        /// </summary>
        private void Tselec(NrlmsiseFlags flags)
        {
            for (int i = 0; i < 24; i++)
            {
                if (i != 9)
                {
                    if (flags.Switches[i] == 1)
                        flags.Sw[i] = 1;
                    else
                        flags.Sw[i] = 0;
                    if (flags.Switches[i] > 0)
                        flags.Swc[i] = 1;
                    else
                        flags.Swc[i] = 0;
                }
                else
                {
                    flags.Sw[i] = flags.Switches[i];
                    flags.Swc[i] = flags.Switches[i];
                }
            }
        }

        /// <summary>
        /// GLATF - Calculate gravity variation and effective radius.
        /// </summary>
        private void Glatf(double lat, out double gv, out double reff)
        {
            double dgtr = 1.74533E-2;
            double c2 = System.Math.Cos(2.0 * dgtr * lat);
            gv = 980.616 * (1.0 - 0.0026373 * c2);
            reff = 2.0 * gv / (3.085462E-6 + 2.27E-9 * c2) * 1.0E-5;
        }

        /// <summary>
        /// CCOR - Chemistry/dissociation correction for MSIS models.
        /// </summary>
        private double Ccor(double alt, double r, double h1, double zh)
        {
            double e = (alt - zh) / h1;
            if (e > 70)
                return System.Math.Exp(0);
            if (e < -70)
                return System.Math.Exp(r);
            double ex = System.Math.Exp(e);
            e = r / (1.0 + ex);
            return System.Math.Exp(e);
        }

        /// <summary>
        /// CCOR2 - Chemistry/dissociation correction with dual scale length.
        /// </summary>
        private double Ccor2(double alt, double r, double h1, double zh, double h2)
        {
            double e1 = (alt - zh) / h1;
            double e2 = (alt - zh) / h2;
            if ((e1 > 70) || (e2 > 70))
                return System.Math.Exp(0);
            if ((e1 < -70) && (e2 < -70))
                return System.Math.Exp(r);
            double ex1 = System.Math.Exp(e1);
            double ex2 = System.Math.Exp(e2);
            double ccor2v = r / (1.0 + 0.5 * (ex1 + ex2));
            return System.Math.Exp(ccor2v);
        }

        /// <summary>
        /// SCALH - Calculate scale height.
        /// </summary>
        private double Scalh(double alt, double xm, double temp)
        {
            double rgas = 831.4;
            double g = _gsurf / System.Math.Pow(1.0 + alt / _re, 2.0);
            g = rgas * temp / (g * xm);
            return g;
        }

        /// <summary>
        /// DNET - Turbopause correction for MSIS models (Root mean density).
        /// </summary>
        private double Dnet(double dd, double dm, double zhm, double xmm, double xm)
        {
            double a = zhm / (xmm - xm);
            if (!((dm > 0) && (dd > 0)))
            {
                if ((dd == 0) && (dm == 0))
                    dd = 1;
                if (dm == 0)
                    return dd;
                if (dd == 0)
                    return dm;
            }

            double ylog = a * System.Math.Log(dm / dd);
            if (ylog < -10)
                return dd;
            if (ylog > 10)
                return dm;
            a = dd * System.Math.Pow(1.0 + System.Math.Exp(ylog), 1.0 / a);
            return a;
        }

        /// <summary>
        /// SPLINI - Integrate cubic spline function from xa[0] to x.
        /// </summary>
        private void Splini(double[] xa, double[] ya, double[] y2a, int n, double x, out double y)
        {
            double yi = 0;
            int klo = 0;
            int khi = 1;
            while ((x > xa[klo]) && (khi < n))
            {
                double xx = x;
                if (khi < (n - 1))
                {
                    if (x < xa[khi])
                        xx = x;
                    else
                        xx = xa[khi];
                }

                double h = xa[khi] - xa[klo];
                double a = (xa[khi] - xx) / h;
                double b = (xx - xa[klo]) / h;
                double a2 = a * a;
                double b2 = b * b;
                yi += ((1.0 - a2) * ya[klo] / 2.0 + b2 * ya[khi] / 2.0 +
                       ((-(1.0 + a2 * a2) / 4.0 + a2 / 2.0) * y2a[klo] +
                        (b2 * b2 / 4.0 - b2 / 2.0) * y2a[khi]) * h * h / 6.0) * h;
                klo++;
                khi++;
            }

            y = yi;
        }

        /// <summary>
        /// SPLINT - Calculate cubic spline interpolation value.
        /// </summary>
        private void Splint(double[] xa, double[] ya, double[] y2a, int n, double x, out double y)
        {
            int klo = 0;
            int khi = n - 1;
            while ((khi - klo) > 1)
            {
                int k = (khi + klo) / 2;
                if (xa[k] > x)
                    khi = k;
                else
                    klo = k;
            }

            double h = xa[khi] - xa[klo];
            if (h == 0.0)
                throw new InvalidOperationException("Bad XA input to splint");
            double a = (xa[khi] - x) / h;
            double b = (x - xa[klo]) / h;
            double yi = a * ya[klo] + b * ya[khi] +
                        ((a * a * a - a) * y2a[klo] + (b * b * b - b) * y2a[khi]) * h * h / 6.0;
            y = yi;
        }

        /// <summary>
        /// SPLINE - Calculate 2nd derivatives of cubic spline interpolation function.
        /// </summary>
        private void Spline(double[] x, double[] y, int n, double yp1, double ypn, double[] y2)
        {
            double[] u = new double[n];
            if (yp1 > 0.99E30)
            {
                y2[0] = 0;
                u[0] = 0;
            }
            else
            {
                y2[0] = -0.5;
                u[0] = (3.0 / (x[1] - x[0])) * ((y[1] - y[0]) / (x[1] - x[0]) - yp1);
            }

            for (int i = 1; i < n - 1; i++)
            {
                double sig = (x[i] - x[i - 1]) / (x[i + 1] - x[i - 1]);
                double p = sig * y2[i - 1] + 2.0;
                y2[i] = (sig - 1.0) / p;
                u[i] = (6.0 * ((y[i + 1] - y[i]) / (x[i + 1] - x[i]) -
                               (y[i] - y[i - 1]) / (x[i] - x[i - 1])) /
                    (x[i + 1] - x[i - 1]) - sig * u[i - 1]) / p;
            }

            double qn, un;
            if (ypn > 0.99E30)
            {
                qn = 0;
                un = 0;
            }
            else
            {
                qn = 0.5;
                un = (3.0 / (x[n - 1] - x[n - 2])) *
                     (ypn - (y[n - 1] - y[n - 2]) / (x[n - 1] - x[n - 2]));
            }

            y2[n - 1] = (un - qn * u[n - 2]) / (qn * y2[n - 2] + 1.0);
            for (int k = n - 2; k >= 0; k--)
                y2[k] = y2[k] * y2[k + 1] + u[k];
        }

        /// <summary>
        /// ZETA - Helper function for densm/densu.
        /// </summary>
        private double Zeta(double zz, double zl)
        {
            return (zz - zl) * (_re + zl) / (_re + zz);
        }

        /// <summary>
        /// DENSM - Calculate temperature and density profiles for lower atmosphere.
        /// </summary>
        private double Densm(double alt, double d0, double xm, ref double tz, int mn3, double[] zn3,
            double[] tn3, double[] tgn3, int mn2, double[] zn2, double[] tn2, double[] tgn2)
        {
            double[] xs = new double[10];
            double[] ys = new double[10];
            double[] y2out = new double[10];
            double rgas = 831.4;
            double densmTmp = d0;

            if (alt > zn2[0])
            {
                if (xm == 0.0)
                    return tz;
                else
                    return d0;
            }

            // STRATOSPHERE/MESOSPHERE TEMPERATURE
            double z;
            if (alt > zn2[mn2 - 1])
                z = alt;
            else
                z = zn2[mn2 - 1];
            int mn = mn2;
            double z1 = zn2[0];
            double z2 = zn2[mn - 1];
            double t1 = tn2[0];
            double t2 = tn2[mn - 1];
            double zg = Zeta(z, z1);
            double zgdif = Zeta(z2, z1);

            // set up spline nodes
            for (int k = 0; k < mn; k++)
            {
                xs[k] = Zeta(zn2[k], z1) / zgdif;
                ys[k] = 1.0 / tn2[k];
            }

            double yd1 = -tgn2[0] / (t1 * t1) * zgdif;
            double yd2 = -tgn2[1] / (t2 * t2) * zgdif * System.Math.Pow((_re + z2) / (_re + z1), 2.0);

            // calculate spline coefficients
            Spline(xs, ys, mn, yd1, yd2, y2out);
            double x = zg / zgdif;
            Splint(xs, ys, y2out, mn, x, out double y);

            // temperature at altitude
            tz = 1.0 / y;
            if (xm != 0.0)
            {
                // calculate stratosphere/mesosphere density
                double glb = _gsurf / System.Math.Pow(1.0 + z1 / _re, 2.0);
                double gamm = xm * glb * zgdif / rgas;

                // Integrate temperature profile
                Splini(xs, ys, y2out, mn, x, out double yi);
                double expl = gamm * yi;
                if (expl > 50.0)
                    expl = 50.0;

                // Density at altitude
                densmTmp = densmTmp * (t1 / tz) * System.Math.Exp(-expl);
            }

            if (alt > zn3[0])
            {
                if (xm == 0.0)
                    return tz;
                else
                    return densmTmp;
            }

            // troposphere/stratosphere temperature
            z = alt;
            mn = mn3;
            z1 = zn3[0];
            z2 = zn3[mn - 1];
            t1 = tn3[0];
            t2 = tn3[mn - 1];
            zg = Zeta(z, z1);
            zgdif = Zeta(z2, z1);

            // set up spline nodes
            for (int k = 0; k < mn; k++)
            {
                xs[k] = Zeta(zn3[k], z1) / zgdif;
                ys[k] = 1.0 / tn3[k];
            }

            yd1 = -tgn3[0] / (t1 * t1) * zgdif;
            yd2 = -tgn3[1] / (t2 * t2) * zgdif * System.Math.Pow((_re + z2) / (_re + z1), 2.0);

            // calculate spline coefficients
            Spline(xs, ys, mn, yd1, yd2, y2out);
            x = zg / zgdif;
            Splint(xs, ys, y2out, mn, x, out y);

            // temperature at altitude
            tz = 1.0 / y;
            if (xm != 0.0)
            {
                // calculate tropospheric/stratosphere density
                double glb = _gsurf / System.Math.Pow(1.0 + z1 / _re, 2.0);
                double gamm = xm * glb * zgdif / rgas;

                // Integrate temperature profile
                Splini(xs, ys, y2out, mn, x, out double yi);
                double expl = gamm * yi;
                if (expl > 50.0)
                    expl = 50.0;

                // Density at altitude
                densmTmp = densmTmp * (t1 / tz) * System.Math.Exp(-expl);
            }

            if (xm == 0.0)
                return tz;

            return densmTmp;
        }

        /// <summary>
        /// DENSU - Calculate temperature and density profiles for MSIS models (new lower thermo polynomial).
        /// </summary>
        private double Densu(double alt, double dlb, double tinf, double tlb, double xm, double alpha,
            ref double tz, double zlb, double s2, int mn1, double[] zn1, double[] tn1, double[] tgn1)
        {
            double rgas = 831.4;
            double densuTemp = 1.0;
            double[] xs = new double[5];
            double[] ys = new double[5];
            double[] y2out = new double[5];

            // joining altitudes of Bates and spline
            double za = zn1[0];
            double z;
            if (alt > za)
                z = alt;
            else
                z = za;

            // geopotential altitude difference from ZLB
            double zg2 = Zeta(z, zlb);

            // Bates temperature
            double tt = tinf - (tinf - tlb) * System.Math.Exp(-s2 * zg2);
            double ta = tt;
            tz = tt;
            densuTemp = tz;

            // Declare variables that need to persist across if blocks (matching C version)
            double z1 = 0, z2 = 0, zg = 0, zgdif = 0;
            double x = 0;
            int mn = 0;

            if (alt < za)
            {
                // calculate temperature below ZA
                // temperature gradient at ZA from Bates profile
                double dta = (tinf - ta) * s2 * System.Math.Pow((_re + zlb) / (_re + za), 2.0);
                tgn1[0] = dta;
                tn1[0] = ta;
                if (alt > zn1[mn1 - 1])
                    z = alt;
                else
                    z = zn1[mn1 - 1];
                mn = mn1;
                z1 = zn1[0];
                z2 = zn1[mn - 1];
                double t1 = tn1[0];
                double t2 = tn1[mn - 1];
                // geopotential difference from z1
                zg = Zeta(z, z1);
                zgdif = Zeta(z2, z1);
                // set up spline nodes
                for (int k = 0; k < mn; k++)
                {
                    xs[k] = Zeta(zn1[k], z1) / zgdif;
                    ys[k] = 1.0 / tn1[k];
                }

                // end node derivatives
                double yd1 = -tgn1[0] / (t1 * t1) * zgdif;
                double yd2 = -tgn1[1] / (t2 * t2) * zgdif * System.Math.Pow((_re + z2) / (_re + z1), 2.0);
                // calculate spline coefficients
                Spline(xs, ys, mn, yd1, yd2, y2out);
                x = zg / zgdif;
                Splint(xs, ys, y2out, mn, x, out double y);
                // temperature at altitude
                tz = 1.0 / y;
                densuTemp = tz;
            }

            if (xm == 0)
                return densuTemp;

            // calculate density above za
            double glb = _gsurf / System.Math.Pow(1.0 + zlb / _re, 2.0);
            double gamma = xm * glb / (s2 * rgas * tinf);
            double expl = System.Math.Exp(-s2 * gamma * zg2);
            if (expl > 50.0)
                expl = 50.0;
            if (tt <= 0)
                expl = 50.0;

            // density at altitude
            double densa = dlb * System.Math.Pow(tlb / tt, 1.0 + alpha + gamma) * expl;
            densuTemp = densa;
            if (alt >= za)
                return densuTemp;

            // calculate density below za
            glb = _gsurf / System.Math.Pow(1.0 + z1 / _re, 2.0);
            double gamm = xm * glb * zgdif / rgas;

            // integrate spline temperatures
            Splini(xs, ys, y2out, mn, x, out double yi);
            expl = gamm * yi;
            if (expl > 50.0)
                expl = 50.0;
            if (tz <= 0)
                expl = 50.0;

            // density at altitude
            densuTemp = densuTemp * System.Math.Pow(tn1[0] / tz, 1.0 + alpha) * System.Math.Exp(-expl);
            return densuTemp;
        }

        /// <summary>
        /// G0 - 3hr Magnetic activity function (Eq. A24d).
        /// </summary>
        private double G0(double a, double[] p)
        {
            double absP24 = System.Math.Abs(p[24]);
            return (a - 4.0 + (p[25] - 1.0) * (a - 4.0 + (System.Math.Exp(-absP24 * (a - 4.0)) - 1.0) / absP24));
        }

        /// <summary>
        /// SUMEX - 3hr Magnetic activity function (Eq. A24c).
        /// </summary>
        private double Sumex(double ex)
        {
            return (1.0 + (1.0 - System.Math.Pow(ex, 19.0)) / (1.0 - ex) * System.Math.Pow(ex, 0.5));
        }

        /// <summary>
        /// SG0 - 3hr Magnetic activity function (Eq. A24a).
        /// </summary>
        private double Sg0(double ex, double[] p, double[] ap)
        {
            return (G0(ap[1], p) + (G0(ap[2], p) * ex + G0(ap[3], p) * ex * ex +
                                    G0(ap[4], p) * System.Math.Pow(ex, 3.0) + (G0(ap[5], p) * System.Math.Pow(ex, 4.0) +
                                                                               G0(ap[6], p) * System.Math.Pow(ex, 12.0)) * (1.0 - System.Math.Pow(ex, 8.0)) / (1.0 - ex))) /
                   Sumex(ex);
        }

        /// <summary>
        /// GLOBE7 - Calculate G(L) function for upper thermosphere parameters.
        /// </summary>
        private double Globe7(double[] p, NrlmsiseInput input, NrlmsiseFlags flags)
        {
            double[] t = new double[15];
            double sr = 7.2722E-5;
            double dgtr = 1.74533E-2;
            double dr = 1.72142E-2;
            double hr = 0.2618;

            double tloc = input.Lst;
            for (int j = 0; j < 14; j++)
                t[j] = 0;

            // calculate legendre polynomials
            double c = System.Math.Sin(input.GLat * dgtr);
            double s = System.Math.Cos(input.GLat * dgtr);
            double c2 = c * c;
            double c4 = c2 * c2;
            double s2 = s * s;

            _plg[0, 1] = c;
            _plg[0, 2] = 0.5 * (3.0 * c2 - 1.0);
            _plg[0, 3] = 0.5 * (5.0 * c * c2 - 3.0 * c);
            _plg[0, 4] = (35.0 * c4 - 30.0 * c2 + 3.0) / 8.0;
            _plg[0, 5] = (63.0 * c2 * c2 * c - 70.0 * c2 * c + 15.0 * c) / 8.0;
            _plg[0, 6] = (11.0 * c * _plg[0, 5] - 5.0 * _plg[0, 4]) / 6.0;

            _plg[1, 1] = s;
            _plg[1, 2] = 3.0 * c * s;
            _plg[1, 3] = 1.5 * (5.0 * c2 - 1.0) * s;
            _plg[1, 4] = 2.5 * (7.0 * c2 * c - 3.0 * c) * s;
            _plg[1, 5] = 1.875 * (21.0 * c4 - 14.0 * c2 + 1.0) * s;
            _plg[1, 6] = (11.0 * c * _plg[1, 5] - 6.0 * _plg[1, 4]) / 5.0;

            _plg[2, 2] = 3.0 * s2;
            _plg[2, 3] = 15.0 * s2 * c;
            _plg[2, 4] = 7.5 * (7.0 * c2 - 1.0) * s2;
            _plg[2, 5] = 3.0 * c * _plg[2, 4] - 2.0 * _plg[2, 3];
            _plg[2, 6] = (11.0 * c * _plg[2, 5] - 7.0 * _plg[2, 4]) / 4.0;
            _plg[2, 7] = (13.0 * c * _plg[2, 6] - 8.0 * _plg[2, 5]) / 5.0;

            _plg[3, 3] = 15.0 * s2 * s;
            _plg[3, 4] = 105.0 * s2 * s * c;
            _plg[3, 5] = (9.0 * c * _plg[3, 4] - 7.0 * _plg[3, 3]) / 2.0;
            _plg[3, 6] = (11.0 * c * _plg[3, 5] - 8.0 * _plg[3, 4]) / 3.0;

            if (!(((flags.Sw[7] == 0) && (flags.Sw[8] == 0)) && (flags.Sw[14] == 0)))
            {
                _stloc = System.Math.Sin(hr * tloc);
                _ctloc = System.Math.Cos(hr * tloc);
                _s2tloc = System.Math.Sin(2.0 * hr * tloc);
                _c2tloc = System.Math.Cos(2.0 * hr * tloc);
                _s3tloc = System.Math.Sin(3.0 * hr * tloc);
                _c3tloc = System.Math.Cos(3.0 * hr * tloc);
            }

            double cd32 = System.Math.Cos(dr * (input.Doy - p[31]));
            double cd18 = System.Math.Cos(2.0 * dr * (input.Doy - p[17]));
            double cd14 = System.Math.Cos(dr * (input.Doy - p[13]));
            double cd39 = System.Math.Cos(2.0 * dr * (input.Doy - p[38]));

            // F10.7 EFFECT
            double df = input.F107 - input.F107A;
            _dfa = input.F107A - 150.0;
            t[0] = p[19] * df * (1.0 + p[59] * _dfa) + p[20] * df * df + p[21] * _dfa + p[29] * System.Math.Pow(_dfa, 2.0);
            double f1 = 1.0 + (p[47] * _dfa + p[19] * df + p[20] * df * df) * flags.Swc[1];
            double f2 = 1.0 + (p[49] * _dfa + p[19] * df + p[20] * df * df) * flags.Swc[1];

            // TIME INDEPENDENT
            t[1] = (p[1] * _plg[0, 2] + p[2] * _plg[0, 4] + p[22] * _plg[0, 6]) +
                   (p[14] * _plg[0, 2]) * _dfa * flags.Swc[1] + p[26] * _plg[0, 1];

            // SYMMETRICAL ANNUAL
            t[2] = p[18] * cd32;

            // SYMMETRICAL SEMIANNUAL
            t[3] = (p[15] + p[16] * _plg[0, 2]) * cd18;

            // ASYMMETRICAL ANNUAL
            t[4] = f1 * (p[9] * _plg[0, 1] + p[10] * _plg[0, 3]) * cd14;

            // ASYMMETRICAL SEMIANNUAL
            t[5] = p[37] * _plg[0, 1] * cd39;

            // DIURNAL
            if (flags.Sw[7] != 0)
            {
                double t71 = (p[11] * _plg[1, 2]) * cd14 * flags.Swc[5];
                double t72 = (p[12] * _plg[1, 2]) * cd14 * flags.Swc[5];
                t[6] = f2 * ((p[3] * _plg[1, 1] + p[4] * _plg[1, 3] + p[27] * _plg[1, 5] + t71) *
                    _ctloc + (p[6] * _plg[1, 1] + p[7] * _plg[1, 3] + p[28] * _plg[1, 5] + t72) * _stloc);
            }

            // SEMIDIURNAL
            if (flags.Sw[8] != 0)
            {
                double t81 = (p[23] * _plg[2, 3] + p[35] * _plg[2, 5]) * cd14 * flags.Swc[5];
                double t82 = (p[33] * _plg[2, 3] + p[36] * _plg[2, 5]) * cd14 * flags.Swc[5];
                t[7] = f2 * ((p[5] * _plg[2, 2] + p[41] * _plg[2, 4] + t81) * _c2tloc +
                             (p[8] * _plg[2, 2] + p[42] * _plg[2, 4] + t82) * _s2tloc);
            }

            // TERDIURNAL
            if (flags.Sw[14] != 0)
            {
                t[13] = f2 * ((p[39] * _plg[3, 3] + (p[93] * _plg[3, 4] + p[46] * _plg[3, 6]) * cd14 * flags.Swc[5]) * _s3tloc +
                              (p[40] * _plg[3, 3] + (p[94] * _plg[3, 4] + p[48] * _plg[3, 6]) * cd14 * flags.Swc[5]) * _c3tloc);
            }

            // magnetic activity based on daily ap
            if (flags.Sw[9] == -1)
            {
                ApArray ap = input.ApA;
                if (p[51] != 0)
                {
                    double exp1 = System.Math.Exp(-10800.0 * System.Math.Sqrt(p[51] * p[51]) / (1.0 + p[138] * (45.0 - System.Math.Sqrt(input.GLat * input.GLat))));
                    if (exp1 > 0.99999)
                        exp1 = 0.99999;
                    if (p[24] < 1.0E-4)
                        p[24] = 1.0E-4;
                    _apt[0] = Sg0(exp1, p, ap.A);

                    if (flags.Sw[9] != 0)
                    {
                        t[8] = _apt[0] * (p[50] + p[96] * _plg[0, 2] + p[54] * _plg[0, 4] +
                                          (p[125] * _plg[0, 1] + p[126] * _plg[0, 3] + p[127] * _plg[0, 5]) * cd14 * flags.Swc[5] +
                                          (p[128] * _plg[1, 1] + p[129] * _plg[1, 3] + p[130] * _plg[1, 5]) * flags.Swc[7] *
                                          System.Math.Cos(hr * (tloc - p[131])));
                    }
                }
            }
            else
            {
                double apd = input.Ap - 4.0;
                double p44 = p[43];
                double p45 = p[44];
                if (p44 < 0)
                    p44 = 1.0E-5;
                _apdf = apd + (p45 - 1.0) * (apd + (System.Math.Exp(-p44 * apd) - 1.0) / p44);
                if (flags.Sw[9] != 0)
                {
                    t[8] = _apdf * (p[32] + p[45] * _plg[0, 2] + p[34] * _plg[0, 4] +
                                    (p[100] * _plg[0, 1] + p[101] * _plg[0, 3] + p[102] * _plg[0, 5]) * cd14 * flags.Swc[5] +
                                    (p[121] * _plg[1, 1] + p[122] * _plg[1, 3] + p[123] * _plg[1, 5]) * flags.Swc[7] *
                                    System.Math.Cos(hr * (tloc - p[124])));
                }
            }

            if ((flags.Sw[10] != 0) && (input.GLong > -1000.0))
            {
                // longitudinal
                if (flags.Sw[11] != 0)
                {
                    t[10] = (1.0 + p[80] * _dfa * flags.Swc[1]) *
                            ((p[64] * _plg[1, 2] + p[65] * _plg[1, 4] + p[66] * _plg[1, 6] +
                              p[103] * _plg[1, 1] + p[104] * _plg[1, 3] + p[105] * _plg[1, 5] +
                              flags.Swc[5] * (p[109] * _plg[1, 1] + p[110] * _plg[1, 3] + p[111] * _plg[1, 5]) * cd14) *
                             System.Math.Cos(dgtr * input.GLong) +
                             (p[90] * _plg[1, 2] + p[91] * _plg[1, 4] + p[92] * _plg[1, 6] +
                              p[106] * _plg[1, 1] + p[107] * _plg[1, 3] + p[108] * _plg[1, 5] +
                              flags.Swc[5] * (p[112] * _plg[1, 1] + p[113] * _plg[1, 3] + p[114] * _plg[1, 5]) * cd14) *
                             System.Math.Sin(dgtr * input.GLong));
                }

                // ut and mixed ut, longitude
                if (flags.Sw[12] != 0)
                {
                    t[11] = (1.0 + p[95] * _plg[0, 1]) * (1.0 + p[81] * _dfa * flags.Swc[1]) *
                            (1.0 + p[119] * _plg[0, 1] * flags.Swc[5] * cd14) *
                            ((p[68] * _plg[0, 1] + p[69] * _plg[0, 3] + p[70] * _plg[0, 5]) *
                             System.Math.Cos(sr * (input.Sec - p[71])));
                    t[11] += flags.Swc[11] *
                             (p[76] * _plg[2, 3] + p[77] * _plg[2, 5] + p[78] * _plg[2, 7]) *
                             System.Math.Cos(sr * (input.Sec - p[79]) + 2.0 * dgtr * input.GLong) * (1.0 + p[137] * _dfa * flags.Swc[1]);
                }

                // ut, longitude magnetic activity
                if (flags.Sw[13] != 0)
                {
                    if (flags.Sw[9] == -1)
                    {
                        if (p[51] != 0)
                        {
                            t[12] = _apt[0] * flags.Swc[11] * (1.0 + p[132] * _plg[0, 1]) *
                                    ((p[52] * _plg[1, 2] + p[98] * _plg[1, 4] + p[67] * _plg[1, 6]) *
                                     System.Math.Cos(dgtr * (input.GLong - p[97]))) +
                                    _apt[0] * flags.Swc[11] * flags.Swc[5] *
                                    (p[133] * _plg[1, 1] + p[134] * _plg[1, 3] + p[135] * _plg[1, 5]) *
                                    cd14 * System.Math.Cos(dgtr * (input.GLong - p[136])) +
                                    _apt[0] * flags.Swc[12] *
                                    (p[55] * _plg[0, 1] + p[56] * _plg[0, 3] + p[57] * _plg[0, 5]) *
                                    System.Math.Cos(sr * (input.Sec - p[58]));
                        }
                    }
                    else
                    {
                        t[12] = _apdf * flags.Swc[11] * (1.0 + p[120] * _plg[0, 1]) *
                                ((p[60] * _plg[1, 2] + p[61] * _plg[1, 4] + p[62] * _plg[1, 6]) *
                                 System.Math.Cos(dgtr * (input.GLong - p[63]))) +
                                _apdf * flags.Swc[11] * flags.Swc[5] *
                                (p[115] * _plg[1, 1] + p[116] * _plg[1, 3] + p[117] * _plg[1, 5]) *
                                cd14 * System.Math.Cos(dgtr * (input.GLong - p[118])) +
                                _apdf * flags.Swc[12] *
                                (p[83] * _plg[0, 1] + p[84] * _plg[0, 3] + p[85] * _plg[0, 5]) *
                                System.Math.Cos(sr * (input.Sec - p[75]));
                    }
                }
            }

            // parms not used: 82, 89, 99, 139-149
            double tinf = p[30];
            for (int i = 0; i < 14; i++)
                tinf = tinf + System.Math.Abs(flags.Sw[i + 1]) * t[i];
            return tinf;
        }

        /// <summary>
        /// GLOB7S - Version of GLOBE for lower atmosphere 10/26/99.
        /// </summary>
        private double Glob7s(double[] p, NrlmsiseInput input, NrlmsiseFlags flags)
        {
            double pset = 2.0;
            double[] t = new double[14];
            double dr = 1.72142E-2;
            double dgtr = 1.74533E-2;

            // confirm parameter set
            if (p[99] == 0)
                p[99] = pset;
            if (p[99] != pset)
            {
                throw new InvalidOperationException("Wrong parameter set for glob7s");
            }

            for (int j = 0; j < 14; j++)
                t[j] = 0.0;

            double cd32 = System.Math.Cos(dr * (input.Doy - p[31]));
            double cd18 = System.Math.Cos(2.0 * dr * (input.Doy - p[17]));
            double cd14 = System.Math.Cos(dr * (input.Doy - p[13]));
            double cd39 = System.Math.Cos(2.0 * dr * (input.Doy - p[38]));

            // F10.7
            t[0] = p[21] * _dfa;

            // time independent
            t[1] = p[1] * _plg[0, 2] + p[2] * _plg[0, 4] + p[22] * _plg[0, 6] + p[26] * _plg[0, 1] + p[14] * _plg[0, 3] + p[59] * _plg[0, 5];

            // SYMMETRICAL ANNUAL
            t[2] = (p[18] + p[47] * _plg[0, 2] + p[29] * _plg[0, 4]) * cd32;

            // SYMMETRICAL SEMIANNUAL
            t[3] = (p[15] + p[16] * _plg[0, 2] + p[30] * _plg[0, 4]) * cd18;

            // ASYMMETRICAL ANNUAL
            t[4] = (p[9] * _plg[0, 1] + p[10] * _plg[0, 3] + p[20] * _plg[0, 5]) * cd14;

            // ASYMMETRICAL SEMIANNUAL
            t[5] = (p[37] * _plg[0, 1]) * cd39;

            // DIURNAL
            if (flags.Sw[7] != 0)
            {
                double t71 = p[11] * _plg[1, 2] * cd14 * flags.Swc[5];
                double t72 = p[12] * _plg[1, 2] * cd14 * flags.Swc[5];
                t[6] = ((p[3] * _plg[1, 1] + p[4] * _plg[1, 3] + t71) * _ctloc + (p[6] * _plg[1, 1] + p[7] * _plg[1, 3] + t72) * _stloc);
            }

            // SEMIDIURNAL
            if (flags.Sw[8] != 0)
            {
                double t81 = (p[23] * _plg[2, 3] + p[35] * _plg[2, 5]) * cd14 * flags.Swc[5];
                double t82 = (p[33] * _plg[2, 3] + p[36] * _plg[2, 5]) * cd14 * flags.Swc[5];
                t[7] = ((p[5] * _plg[2, 2] + p[41] * _plg[2, 4] + t81) * _c2tloc + (p[8] * _plg[2, 2] + p[42] * _plg[2, 4] + t82) * _s2tloc);
            }

            // TERDIURNAL
            if (flags.Sw[14] != 0)
            {
                t[13] = p[39] * _plg[3, 3] * _s3tloc + p[40] * _plg[3, 3] * _c3tloc;
            }

            // MAGNETIC ACTIVITY
            if (flags.Sw[9] != 0)
            {
                if (flags.Sw[9] == 1)
                    t[8] = _apdf * (p[32] + p[45] * _plg[0, 2] * flags.Swc[2]);
                if (flags.Sw[9] == -1)
                    t[8] = (p[50] * _apt[0] + p[96] * _plg[0, 2] * _apt[0] * flags.Swc[2]);
            }

            // LONGITUDINAL
            if (!((flags.Sw[10] == 0) || (flags.Sw[11] == 0) || (input.GLong <= -1000.0)))
            {
                t[10] = (1.0 + _plg[0, 1] * (p[80] * flags.Swc[5] * System.Math.Cos(dr * (input.Doy - p[81])) +
                                             p[85] * flags.Swc[6] * System.Math.Cos(2.0 * dr * (input.Doy - p[86]))) +
                         p[83] * flags.Swc[3] * System.Math.Cos(dr * (input.Doy - p[84])) +
                         p[87] * flags.Swc[4] * System.Math.Cos(2.0 * dr * (input.Doy - p[88]))) *
                        ((p[64] * _plg[1, 2] + p[65] * _plg[1, 4] + p[66] * _plg[1, 6] +
                          p[74] * _plg[1, 1] + p[75] * _plg[1, 3] + p[76] * _plg[1, 5]) *
                         System.Math.Cos(dgtr * input.GLong) +
                         (p[90] * _plg[1, 2] + p[91] * _plg[1, 4] + p[92] * _plg[1, 6] +
                          p[77] * _plg[1, 1] + p[78] * _plg[1, 3] + p[79] * _plg[1, 5]) *
                         System.Math.Sin(dgtr * input.GLong));
            }

            double tt = 0;
            for (int i = 0; i < 14; i++)
                tt += System.Math.Abs(flags.Sw[i + 1]) * t[i];
            return tt;
        }

        /// <summary>
        /// Calculates the thermospheric portion of NRLMSISE-00 (public wrapper).
        /// </summary>
        /// <remarks>
        /// See Calculate for more extensive comments.
        /// Altitude must be > 72500 m (72.5 km internally).
        /// Input uses SI units (meters, radians), output uses SI units (m^-3, kg/m^3, K).
        /// </remarks>
        public void CalculateThermosphere(NrlmsiseInput input, NrlmsiseFlags flags, NrlmsiseOutput output)
        {
            // Convert SI input units to internal units (km, degrees) and create a modified copy
            var internalInput = input with
            {
                Alt = input.Alt / 1000.0, // meters to kilometers
                GLat = input.GLat * Constants.Rad2Deg, // radians to degrees
                GLong = input.GLong * Constants.Rad2Deg // radians to degrees
            };

            // Call internal method that works with internal units (input remains unchanged)
            Gts7Internal(internalInput, flags, output);

            // Convert output from internal units to SI units
            // Number densities: cm^-3 to m^-3 (multiply by 1E6)
            for (int i = 0; i < 9; i++)
            {
                if (i != 5) // Skip mass density
                    output.D[i] = output.D[i] * 1.0E6;
            }

            // Mass density: g/cm^3 to kg/m^3 (multiply by 1000)
            output.D[5] = output.D[5] * 1000.0;
        }

        /// <summary>
        /// Gts7Internal - Thermospheric portion of NRLMSISE-00 (internal implementation).
        /// </summary>
        /// <remarks>
        /// Works with internal units: altitude in km, latitude/longitude in degrees,
        /// number densities in cm^-3, mass density in g/cm^3, temperature in K.
        /// This method performs no unit conversions - it assumes inputs are already in internal units.
        /// </remarks>
        private void Gts7Internal(NrlmsiseInput input, NrlmsiseFlags flags, NrlmsiseOutput output)
        {
            double[] zn1 = { 120.0, 110.0, 100.0, 90.0, 72.5 };
            int mn1 = 5;
            double dgtr = 1.74533E-2;
            double dr = 1.72142E-2;
            double[] alpha = { -0.38, 0.0, 0.0, 0.0, 0.17, 0.0, -0.38, 0.0, 0.0 };
            double[] altl = { 200.0, 300.0, 160.0, 250.0, 240.0, 450.0, 320.0, 450.0 };

            double za = Data.PDL[1][15];
            zn1[0] = za;
            for (int j = 0; j < 9; j++)
                output.D[j] = 0;

            // TINF VARIATIONS NOT IMPORTANT BELOW ZA OR ZN1(1)
            double tinf;
            if (input.Alt > zn1[0])
                tinf = Data.PTM[0] * Data.PT[0] *
                       (1.0 + flags.Sw[16] * Globe7(Data.PT, input, flags));
            else
                tinf = Data.PTM[0] * Data.PT[0];
            output.T[0] = tinf;

            // GRADIENT VARIATIONS NOT IMPORTANT BELOW ZN1(5)
            double g0;
            if (input.Alt > zn1[4])
                g0 = Data.PTM[3] * Data.PS[0] *
                     (1.0 + flags.Sw[19] * Globe7(Data.PS, input, flags));
            else
                g0 = Data.PTM[3] * Data.PS[0];
            double tlb = Data.PTM[1] * (1.0 + flags.Sw[17] * Globe7(Data.PD[3], input, flags)) * Data.PD[3][0];
            double s = g0 / (tinf - tlb);

            // Lower thermosphere temp variations not significant for density above 300 km
            if (input.Alt < 300.0)
            {
                _mesoTn1[1] = Data.PTM[6] * Data.PTL[0][0] / (1.0 - flags.Sw[18] * Glob7s(Data.PTL[0], input, flags));
                _mesoTn1[2] = Data.PTM[2] * Data.PTL[1][0] / (1.0 - flags.Sw[18] * Glob7s(Data.PTL[1], input, flags));
                _mesoTn1[3] = Data.PTM[7] * Data.PTL[2][0] / (1.0 - flags.Sw[18] * Glob7s(Data.PTL[2], input, flags));
                _mesoTn1[4] = Data.PTM[4] * Data.PTL[3][0] / (1.0 - flags.Sw[18] * flags.Sw[20] * Glob7s(Data.PTL[3], input, flags));
                _mesoTgn1[1] = Data.PTM[8] * Data.PMA[8][0] * (1.0 + flags.Sw[18] * flags.Sw[20] * Glob7s(Data.PMA[8], input, flags)) * _mesoTn1[4] * _mesoTn1[4] /
                               (System.Math.Pow(Data.PTM[4] * Data.PTL[3][0], 2.0));
            }
            else
            {
                _mesoTn1[1] = Data.PTM[6] * Data.PTL[0][0];
                _mesoTn1[2] = Data.PTM[2] * Data.PTL[1][0];
                _mesoTn1[3] = Data.PTM[7] * Data.PTL[2][0];
                _mesoTn1[4] = Data.PTM[4] * Data.PTL[3][0];
                _mesoTgn1[1] = Data.PTM[8] * Data.PMA[8][0] * _mesoTn1[4] * _mesoTn1[4] / (System.Math.Pow(Data.PTM[4] * Data.PTL[3][0], 2.0));
            }

            // N2 variation factor at Zlb
            double g28 = flags.Sw[21] * Globe7(Data.PD[2], input, flags);

            // VARIATION OF TURBOPAUSE HEIGHT
            double zhf = Data.PDL[1][24] * (1.0 + flags.Sw[5] * Data.PDL[0][24] * System.Math.Sin(dgtr * input.GLat) * System.Math.Cos(dr * (input.Doy - Data.PT[13])));
            output.T[0] = tinf;
            double xmm = Data.PDM[2][4];
            double z = input.Alt;

            // N2 DENSITY

            // Diffusive density at Zlb
            double db28 = Data.PDM[2][0] * System.Math.Exp(g28) * Data.PD[2][0];
            // Diffusive density at Alt
            output.D[2] = Densu(z, db28, tinf, tlb, 28.0, alpha[2], ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
            double dd = output.D[2];
            // Turbopause
            double zh28 = Data.PDM[2][2] * zhf;
            double zhm28 = Data.PDM[2][3] * Data.PDL[1][5];
            double xmd = 28.0 - xmm;
            // Mixed density at Zlb
            double tz = 0;
            double b28 = Densu(zh28, db28, tinf, tlb, xmd, (alpha[2] - 1.0), ref tz, Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
            if ((flags.Sw[15] != 0) && (z <= altl[2]))
            {
                // Mixed density at Alt
                _dm28 = Densu(z, b28, tinf, tlb, xmm, alpha[2], ref tz, Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
                // Net density at Alt
                output.D[2] = Dnet(output.D[2], _dm28, zhm28, xmm, 28.0);
            }

            // HE DENSITY

            // Density variation factor at Zlb
            double g4 = flags.Sw[21] * Globe7(Data.PD[0], input, flags);
            // Diffusive density at Zlb
            double db04 = Data.PDM[0][0] * System.Math.Exp(g4) * Data.PD[0][0];
            // Diffusive density at Alt
            output.D[0] = Densu(z, db04, tinf, tlb, 4.0, alpha[0], ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
            dd = output.D[0];
            if ((flags.Sw[15] != 0) && (z < altl[0]))
            {
                // Turbopause
                double zh04 = Data.PDM[0][2];
                // Mixed density at Zlb
                double b04 = Densu(zh04, db04, tinf, tlb, 4.0 - xmm, alpha[0] - 1.0, ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
                // Mixed density at Alt
                _dm04 = Densu(z, b04, tinf, tlb, xmm, 0.0, ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
                double zhm04 = zhm28;
                // Net density at Alt
                output.D[0] = Dnet(output.D[0], _dm04, zhm04, xmm, 4.0);
                // Correction to specified mixing ratio at ground
                double rl = System.Math.Log(b28 * Data.PDM[0][1] / b04);
                double zc04 = Data.PDM[0][4] * Data.PDL[1][0];
                double hc04 = Data.PDM[0][5] * Data.PDL[1][1];
                // Net density corrected at Alt
                output.D[0] = output.D[0] * Ccor(z, rl, hc04, zc04);
            }

            // O DENSITY

            // Density variation factor at Zlb
            double g16 = flags.Sw[21] * Globe7(Data.PD[1], input, flags);
            // Diffusive density at Zlb
            double db16 = Data.PDM[1][0] * System.Math.Exp(g16) * Data.PD[1][0];
            // Diffusive density at Alt
            output.D[1] = Densu(z, db16, tinf, tlb, 16.0, alpha[1], ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
            dd = output.D[1];
            if ((flags.Sw[15] != 0) && (z <= altl[1]))
            {
                // Turbopause
                double zh16 = Data.PDM[1][2];
                // Mixed density at Zlb
                double b16 = Densu(zh16, db16, tinf, tlb, 16.0 - xmm, (alpha[1] - 1.0), ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
                // Mixed density at Alt
                _dm16 = Densu(z, b16, tinf, tlb, xmm, 0.0, ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
                double zhm16 = zhm28;
                // Net density at Alt
                output.D[1] = Dnet(output.D[1], _dm16, zhm16, xmm, 16.0);
                double rl = Data.PDM[1][1] * Data.PDL[1][16] * (1.0 + flags.Sw[1] * Data.PDL[0][23] * (input.F107A - 150.0));
                double hc16 = Data.PDM[1][5] * Data.PDL[1][3];
                double zc16 = Data.PDM[1][4] * Data.PDL[1][2];
                double hc216 = Data.PDM[1][5] * Data.PDL[1][4];
                output.D[1] = output.D[1] * Ccor2(z, rl, hc16, zc16, hc216);
                // Chemistry correction
                double hcc16 = Data.PDM[1][7] * Data.PDL[1][13];
                double zcc16 = Data.PDM[1][6] * Data.PDL[1][12];
                double rc16 = Data.PDM[1][3] * Data.PDL[1][14];
                // Net density corrected at Alt
                output.D[1] = output.D[1] * Ccor(z, rc16, hcc16, zcc16);
            }

            // O2 DENSITY

            // Density variation factor at Zlb
            double g32 = flags.Sw[21] * Globe7(Data.PD[4], input, flags);
            // Diffusive density at Zlb
            double db32 = Data.PDM[3][0] * System.Math.Exp(g32) * Data.PD[4][0];
            // Diffusive density at Alt
            output.D[3] = Densu(z, db32, tinf, tlb, 32.0, alpha[3], ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
            dd = output.D[3];
            if (flags.Sw[15] != 0)
            {
                if (z <= altl[3])
                {
                    // Turbopause
                    double zh32 = Data.PDM[3][2];
                    // Mixed density at Zlb
                    double b32 = Densu(zh32, db32, tinf, tlb, 32.0 - xmm, alpha[3] - 1.0, ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
                    // Mixed density at Alt
                    _dm32 = Densu(z, b32, tinf, tlb, xmm, 0.0, ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
                    double zhm32 = zhm28;
                    // Net density at Alt
                    output.D[3] = Dnet(output.D[3], _dm32, zhm32, xmm, 32.0);
                    // Correction to specified mixing ratio at ground
                    double rl = System.Math.Log(b28 * Data.PDM[3][1] / b32);
                    double hc32 = Data.PDM[3][5] * Data.PDL[1][7];
                    double zc32 = Data.PDM[3][4] * Data.PDL[1][6];
                    output.D[3] = output.D[3] * Ccor(z, rl, hc32, zc32);
                }

                // Correction for general departure from diffusive equilibrium above Zlb
                double hcc32 = Data.PDM[3][7] * Data.PDL[1][22];
                double hcc232 = Data.PDM[3][7] * Data.PDL[0][22];
                double zcc32 = Data.PDM[3][6] * Data.PDL[1][21];
                double rc32 = Data.PDM[3][3] * Data.PDL[1][23] * (1.0 + flags.Sw[1] * Data.PDL[0][23] * (input.F107A - 150.0));
                // Net density corrected at Alt
                output.D[3] = output.D[3] * Ccor2(z, rc32, hcc32, zcc32, hcc232);
            }

            // AR DENSITY

            // Density variation factor at Zlb
            double g40 = flags.Sw[21] * Globe7(Data.PD[5], input, flags);
            // Diffusive density at Zlb
            double db40 = Data.PDM[4][0] * System.Math.Exp(g40) * Data.PD[5][0];
            // Diffusive density at Alt
            output.D[4] = Densu(z, db40, tinf, tlb, 40.0, alpha[4], ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
            dd = output.D[4];
            if ((flags.Sw[15] != 0) && (z <= altl[4]))
            {
                // Turbopause
                double zh40 = Data.PDM[4][2];
                // Mixed density at Zlb
                double b40 = Densu(zh40, db40, tinf, tlb, 40.0 - xmm, alpha[4] - 1.0, ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
                // Mixed density at Alt
                _dm40 = Densu(z, b40, tinf, tlb, xmm, 0.0, ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
                double zhm40 = zhm28;
                // Net density at Alt
                output.D[4] = Dnet(output.D[4], _dm40, zhm40, xmm, 40.0);
                // Correction to specified mixing ratio at ground
                double rl = System.Math.Log(b28 * Data.PDM[4][1] / b40);
                double hc40 = Data.PDM[4][5] * Data.PDL[1][9];
                double zc40 = Data.PDM[4][4] * Data.PDL[1][8];
                // Net density corrected at Alt
                output.D[4] = output.D[4] * Ccor(z, rl, hc40, zc40);
            }

            // HYDROGEN DENSITY

            // Density variation factor at Zlb
            double g1 = flags.Sw[21] * Globe7(Data.PD[6], input, flags);
            // Diffusive density at Zlb
            double db01 = Data.PDM[5][0] * System.Math.Exp(g1) * Data.PD[6][0];
            // Diffusive density at Alt
            output.D[6] = Densu(z, db01, tinf, tlb, 1.0, alpha[6], ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
            dd = output.D[6];
            if ((flags.Sw[15] != 0) && (z <= altl[6]))
            {
                // Turbopause
                double zh01 = Data.PDM[5][2];
                // Mixed density at Zlb
                double b01 = Densu(zh01, db01, tinf, tlb, 1.0 - xmm, alpha[6] - 1.0, ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
                // Mixed density at Alt
                _dm01 = Densu(z, b01, tinf, tlb, xmm, 0.0, ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
                double zhm01 = zhm28;
                // Net density at Alt
                output.D[6] = Dnet(output.D[6], _dm01, zhm01, xmm, 1.0);
                // Correction to specified mixing ratio at ground
                double rl = System.Math.Log(b28 * Data.PDM[5][1] * System.Math.Sqrt(Data.PDL[1][17] * Data.PDL[1][17]) / b01);
                double hc01 = Data.PDM[5][5] * Data.PDL[1][11];
                double zc01 = Data.PDM[5][4] * Data.PDL[1][10];
                output.D[6] = output.D[6] * Ccor(z, rl, hc01, zc01);
                // Chemistry correction
                double hcc01 = Data.PDM[5][7] * Data.PDL[1][19];
                double zcc01 = Data.PDM[5][6] * Data.PDL[1][18];
                double rc01 = Data.PDM[5][3] * Data.PDL[1][20];
                // Net density corrected at Alt
                output.D[6] = output.D[6] * Ccor(z, rc01, hcc01, zcc01);
            }

            // ATOMIC NITROGEN DENSITY

            // Density variation factor at Zlb
            double g14 = flags.Sw[21] * Globe7(Data.PD[7], input, flags);
            // Diffusive density at Zlb
            double db14 = Data.PDM[6][0] * System.Math.Exp(g14) * Data.PD[7][0];
            // Diffusive density at Alt
            output.D[7] = Densu(z, db14, tinf, tlb, 14.0, alpha[7], ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
            dd = output.D[7];
            if ((flags.Sw[15] != 0) && (z <= altl[7]))
            {
                // Turbopause
                double zh14 = Data.PDM[6][2];
                // Mixed density at Zlb
                double b14 = Densu(zh14, db14, tinf, tlb, 14.0 - xmm, alpha[7] - 1.0, ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
                // Mixed density at Alt
                _dm14 = Densu(z, b14, tinf, tlb, xmm, 0.0, ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
                double zhm14 = zhm28;
                // Net density at Alt
                output.D[7] = Dnet(output.D[7], _dm14, zhm14, xmm, 14.0);
                // Correction to specified mixing ratio at ground
                double rl = System.Math.Log(b28 * Data.PDM[6][1] * System.Math.Sqrt(Data.PDL[0][2] * Data.PDL[0][2]) / b14);
                double hc14 = Data.PDM[6][5] * Data.PDL[0][1];
                double zc14 = Data.PDM[6][4] * Data.PDL[0][0];
                output.D[7] = output.D[7] * Ccor(z, rl, hc14, zc14);
                // Chemistry correction
                double hcc14 = Data.PDM[6][7] * Data.PDL[0][4];
                double zcc14 = Data.PDM[6][6] * Data.PDL[0][3];
                double rc14 = Data.PDM[6][3] * Data.PDL[0][5];
                // Net density corrected at Alt
                output.D[7] = output.D[7] * Ccor(z, rc14, hcc14, zcc14);
            }

            // Anomalous OXYGEN DENSITY

            double g16h = flags.Sw[21] * Globe7(Data.PD[8], input, flags);
            double db16h = Data.PDM[7][0] * System.Math.Exp(g16h) * Data.PD[8][0];
            double tho = Data.PDM[7][9] * Data.PDL[0][6];
            dd = Densu(z, db16h, tho, tho, 16.0, alpha[8], ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
            double zsht = Data.PDM[7][5];
            double zmho = Data.PDM[7][4];
            double zsho = Scalh(zmho, 16.0, tho);
            output.D[8] = dd * System.Math.Exp(-zsht / zsho * (System.Math.Exp(-(z - zmho) / zsht) - 1.0));

            // total mass density (internal units: g/cm^3)
            output.D[5] = 1.66E-24 * (4.0 * output.D[0] + 16.0 * output.D[1] + 28.0 * output.D[2] + 32.0 * output.D[3] + 40.0 * output.D[4] + output.D[6] + 14.0 * output.D[7]);

            // temperature
            z = System.Math.Sqrt(input.Alt * input.Alt);
            double ddum = Densu(z, 1.0, tinf, tlb, 0.0, 0.0, ref output.T[1], Data.PTM[5], s, mn1, zn1, _mesoTn1, _mesoTgn1);
        }

        /// <summary>
        /// Calculates atmospheric densities and temperature using the NRLMSISE-00 model.
        /// </summary>
        /// <remarks>
        /// Neutral atmosphere empirical model from the surface to lower exosphere.
        /// Input uses SI units (meters, radians), output uses SI units (m^-3, kg/m^3, K).
        /// </remarks>
        public NrlmsiseOutput Calculate(NrlmsiseInput input, NrlmsiseFlags flags)
        {
            // Convert SI input units to internal units (km, degrees) and create a modified copy
            var internalInput = input with
            {
                Alt = input.Alt / 1000.0, // meters to kilometers
                GLat = input.GLat * Constants.Rad2Deg, // radians to degrees
                GLong = input.GLong * Constants.Rad2Deg // radians to degrees
            };

            int mn3 = 5;
            double[] zn3 = { 32.5, 20.0, 15.0, 10.0, 0.0 };
            int mn2 = 4;
            double[] zn2 = { 72.5, 55.0, 45.0, 32.5 };
            double zmix = 62.5;

            Tselec(flags);

            // Latitude variation of gravity (none for sw[2]=0)
            double xlat = internalInput.GLat;
            if (flags.Sw[2] == 0)
                xlat = 45.0;
            Glatf(xlat, out _gsurf, out _re);

            double xmm = Data.PDM[2][4];

            // THERMOSPHERE / MESOSPHERE (above zn2[0])
            double altt;
            if (internalInput.Alt > zn2[0])
                altt = internalInput.Alt;
            else
                altt = zn2[0];

            // Create a copy with the adjusted altitude for the thermosphere calculation
            var thermosphereInput = internalInput with { Alt = altt };
            NrlmsiseOutput soutput = new NrlmsiseOutput();
            Gts7Internal(thermosphereInput, flags, soutput);

            double dm28m = _dm28; // Internal units (g/cm^3)

            NrlmsiseOutput output = new NrlmsiseOutput();
            output.T[0] = soutput.T[0];
            output.T[1] = soutput.T[1];
            if (internalInput.Alt >= zn2[0])
            {
                for (int i = 0; i < 9; i++)
                    output.D[i] = soutput.D[i];

                // Convert output from internal units to SI units
                // Number densities: cm^-3 to m^-3 (multiply by 1E6)
                for (int i = 0; i < 9; i++)
                {
                    if (i != 5) // Skip mass density
                        output.D[i] *= 1.0E6;
                }

                // Mass density: g/cm^3 to kg/m^3 (multiply by 1000)
                output.D[5] *= 1000.0;
                return output;
            }

            // LOWER MESOSPHERE/UPPER STRATOSPHERE (between zn3[0] and zn2[0])
            // Temperature at nodes and gradients at end nodes
            // Inverse temperature a linear function of spherical harmonics
            _mesoTgn2[0] = _mesoTgn1[1];
            _mesoTn2[0] = _mesoTn1[4];
            _mesoTn2[1] = Data.PMA[0][0] * Data.PAVGM[0] / (1.0 - flags.Sw[20] * Glob7s(Data.PMA[0], internalInput, flags));
            _mesoTn2[2] = Data.PMA[1][0] * Data.PAVGM[1] / (1.0 - flags.Sw[20] * Glob7s(Data.PMA[1], internalInput, flags));
            _mesoTn2[3] = Data.PMA[2][0] * Data.PAVGM[2] / (1.0 - flags.Sw[20] * flags.Sw[22] * Glob7s(Data.PMA[2], internalInput, flags));
            _mesoTgn2[1] = Data.PAVGM[8] * Data.PMA[9][0] * (1.0 + flags.Sw[20] * flags.Sw[22] * Glob7s(Data.PMA[9], internalInput, flags)) * _mesoTn2[3] * _mesoTn2[3] /
                           (System.Math.Pow(Data.PMA[2][0] * Data.PAVGM[2], 2.0));
            _mesoTn3[0] = _mesoTn2[3];

            if (internalInput.Alt <= zn3[0])
            {
                // LOWER STRATOSPHERE AND TROPOSPHERE (below zn3[0])
                // Temperature at nodes and gradients at end nodes
                // Inverse temperature a linear function of spherical harmonics
                _mesoTgn3[0] = _mesoTgn2[1];
                _mesoTn3[1] = Data.PMA[3][0] * Data.PAVGM[3] / (1.0 - flags.Sw[22] * Glob7s(Data.PMA[3], internalInput, flags));
                _mesoTn3[2] = Data.PMA[4][0] * Data.PAVGM[4] / (1.0 - flags.Sw[22] * Glob7s(Data.PMA[4], internalInput, flags));
                _mesoTn3[3] = Data.PMA[5][0] * Data.PAVGM[5] / (1.0 - flags.Sw[22] * Glob7s(Data.PMA[5], internalInput, flags));
                _mesoTn3[4] = Data.PMA[6][0] * Data.PAVGM[6] / (1.0 - flags.Sw[22] * Glob7s(Data.PMA[6], internalInput, flags));
                _mesoTgn3[1] = Data.PMA[7][0] * Data.PAVGM[7] * (1.0 + flags.Sw[22] * Glob7s(Data.PMA[7], internalInput, flags)) * _mesoTn3[4] * _mesoTn3[4] /
                               (System.Math.Pow(Data.PMA[6][0] * Data.PAVGM[6], 2.0));
            }

            // LINEAR TRANSITION TO FULL MIXING BELOW zn2[0]
            double dmc = 0;
            if (internalInput.Alt > zmix)
                dmc = 1.0 - (zn2[0] - internalInput.Alt) / (zn2[0] - zmix);
            double dz28 = soutput.D[2];

            // N2 density
            double dmr = soutput.D[2] / dm28m - 1.0;
            double tz = 0;
            output.D[2] = Densm(internalInput.Alt, dm28m, xmm, ref tz, mn3, zn3, _mesoTn3, _mesoTgn3, mn2, zn2, _mesoTn2, _mesoTgn2);
            output.D[2] = output.D[2] * (1.0 + dmr * dmc);

            // HE density
            dmr = soutput.D[0] / (dz28 * Data.PDM[0][1]) - 1.0;
            output.D[0] = output.D[2] * Data.PDM[0][1] * (1.0 + dmr * dmc);

            // O density
            output.D[1] = 0;
            output.D[8] = 0;

            // O2 density
            dmr = soutput.D[3] / (dz28 * Data.PDM[3][1]) - 1.0;
            output.D[3] = output.D[2] * Data.PDM[3][1] * (1.0 + dmr * dmc);

            // AR density
            dmr = soutput.D[4] / (dz28 * Data.PDM[4][1]) - 1.0;
            output.D[4] = output.D[2] * Data.PDM[4][1] * (1.0 + dmr * dmc);

            // Hydrogen density
            output.D[6] = 0;

            // Atomic nitrogen density
            output.D[7] = 0;

            // Total mass density (internal units: g/cm^3)
            output.D[5] = 1.66E-24 * (4.0 * output.D[0] + 16.0 * output.D[1] + 28.0 * output.D[2] + 32.0 * output.D[3] + 40.0 * output.D[4] + output.D[6] + 14.0 * output.D[7]);

            // temperature at altitude
            output.T[1] = tz;

            // Convert output from internal units to SI units
            // Number densities: cm^-3 to m^-3 (multiply by 1E6)
            for (int i = 0; i < 9; i++)
            {
                if (i != 5) // Skip mass density
                    output.D[i] *= 1.0E6;
            }

            // Mass density: g/cm^3 to kg/m^3 (multiply by 1000)
            output.D[5] *= 1000.0;

            return output;
        }

        /// <summary>
        /// Calculates atmospheric densities and temperature with drag density using the NRLMSISE-00 model.
        /// </summary>
        /// <remarks>
        /// This version returns effective total mass density for drag calculations,
        /// which includes anomalous oxygen.
        /// Input uses SI units (meters, radians), output uses SI units (m^-3, kg/m^3, K).
        /// </remarks>
        public NrlmsiseOutput CalculateWithDrag(NrlmsiseInput input, NrlmsiseFlags flags)
        {
            NrlmsiseOutput output = Calculate(input, flags);

            // Convert number densities back to internal units temporarily for mass density calculation
            // m^-3 to cm^-3 (divide by 1E6)
            double d0_cm3 = output.D[0] / 1.0E6;
            double d1_cm3 = output.D[1] / 1.0E6;
            double d2_cm3 = output.D[2] / 1.0E6;
            double d3_cm3 = output.D[3] / 1.0E6;
            double d4_cm3 = output.D[4] / 1.0E6;
            double d6_cm3 = output.D[6] / 1.0E6;
            double d7_cm3 = output.D[7] / 1.0E6;
            double d8_cm3 = output.D[8] / 1.0E6;

            // Calculate mass density in internal units (g/cm^3), including anomalous oxygen
            double rho_gcm3 = 1.66E-24 * (4.0 * d0_cm3 + 16.0 * d1_cm3 + 28.0 * d2_cm3 +
                                          32.0 * d3_cm3 + 40.0 * d4_cm3 + d6_cm3 +
                                          14.0 * d7_cm3 + 16.0 * d8_cm3);

            // Convert to SI units: g/cm^3 to kg/m^3 (multiply by 1000)
            output.D[5] = rho_gcm3 * 1000.0;

            return output;
        }

        /// <summary>
        /// Finds the altitude corresponding to a given pressure level using the NRLMSISE-00 model.
        /// </summary>
        /// <remarks>
        /// Input uses SI units (meters, radians), output uses SI units (m^-3, kg/m^3, K).
        /// The calculated altitude for the given pressure is returned.
        /// Note: The input altitude is used as an initial guess but is not modified.
        /// </remarks>
        /// <param name="input">Input parameters (altitude is used as initial guess).</param>
        /// <param name="flags">Model flags.</param>
        /// <param name="output">Output densities and temperatures at the calculated altitude.</param>
        /// <param name="press">Pressure level in millibars.</param>
        /// <returns>Calculated altitude in meters for the given pressure level.</returns>
        public (double altitude, NrlmsiseOutput atmosphere) FindAltitudeAtPressure(NrlmsiseInput input, NrlmsiseFlags flags, double press)
        {
            // Convert input GLat from radians to degrees for initial calculation
            double glatDeg = input.GLat * Constants.Rad2Deg;

            double bm = 1.3806E-19;
            double rgas = 831.4;
            double test = 0.00043;
            int ltest = 12;

            double pl = System.Math.Log10(press);
            double zi; // Altitude in km (internal units)
            if (pl >= -5.0)
            {
                if (pl > 2.5)
                    zi = 18.06 * (3.00 - pl);
                else if ((pl > 0.075) && (pl <= 2.5))
                    zi = 14.98 * (3.08 - pl);
                else if ((pl > -1) && (pl <= 0.075))
                    zi = 17.80 * (2.72 - pl);
                else if ((pl > -2) && (pl <= -1))
                    zi = 14.28 * (3.64 - pl);
                else if ((pl > -4) && (pl <= -2))
                    zi = 12.72 * (4.32 - pl);
                else
                    zi = 25.3 * (0.11 - pl);

                double cl = glatDeg / 90.0;
                double cl2 = cl * cl;
                double cd;
                if (input.Doy < 182)
                    cd = (1.0 - (double)input.Doy) / 91.25;
                else
                    cd = ((double)input.Doy) / 91.25 - 3.0;

                double ca = 0;
                if ((pl > -1.11) && (pl <= -0.23))
                    ca = 1.0;
                if (pl > -0.23)
                    ca = (2.79 - pl) / (2.79 + 0.23);
                if ((pl <= -1.11) && (pl > -3))
                    ca = (-2.93 - pl) / (-2.93 + 1.11);
                zi = zi - 4.87 * cl * cd * ca - 1.64 * cl2 * ca + 0.31 * ca * cl;
            }
            else
            {
                zi = 22.0 * System.Math.Pow(pl + 4.0, 2.0) + 110.0;
            }

            // iteration loop
            int l = 0;
            do
            {
                l++;
                // Create input copy with current altitude estimate
                var iterationInput = input with { Alt = zi * 1000.0 }; // Convert zi from km to meters
                var output = Calculate(iterationInput, flags);
                // Calculate total number density (output.D is now in m^-3)
                double xn = output.D[0] + output.D[1] + output.D[2] + output.D[3] + output.D[4] + output.D[6] + output.D[7];
                // Calculate pressure (note: xn is in m^-3, bm converts to appropriate units)
                // bm * (particles/m^3) * K gives pressure in appropriate units
                // Need to convert from m^-3 to cm^-3 for pressure calculation
                double xn_cm3 = xn / 1.0E6; // Convert m^-3 to cm^-3
                double p = bm * xn_cm3 * output.T[1];

                double diff = pl - System.Math.Log10(p);
                if (System.Math.Sqrt(diff * diff) < test)
                    return (zi * 1000.0, output); // Return calculated altitude in meters
                if (l == ltest)
                {
                    throw new InvalidOperationException($"ERROR: ghp7 not converging for press {press}, diff {diff}");
                }

                // Calculate mean molecular mass
                // output.D[5] is now in kg/m^3, xn is in m^-3
                // Convert to get mean mass in amu
                double rho_gcm3 = output.D[5] / 1000.0; // kg/m^3 to g/cm^3
                double xm = rho_gcm3 / xn_cm3 / 1.66E-24;

                double g = _gsurf / (System.Math.Pow(1.0 + zi / _re, 2.0));
                double sh = rgas * output.T[1] / (xm * g);

                // new altitude estimate using scale height
                if (l < 6)
                    zi = zi - sh * diff * 2.302;
                else
                    zi = zi - sh * diff;
            } while (true);
        }
    }
}