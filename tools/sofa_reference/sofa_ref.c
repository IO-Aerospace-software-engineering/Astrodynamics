/*
 * SOFA reference values for the IAU 2006 / 2000B frame chain (IO.Astrodynamics/Frames).
 *
 *   ./sofa_ref           Detailed JSON at a few named epochs (matrices, fundamental arguments, ...).
 *   ./sofa_ref --sweep   Compact JSON sweep from 1990 to 2040, written to
 *                        IO.Astrodynamics.Tests/Data/SofaReference/iau2006_sweep.json and read by
 *                        Iau2006SofaSweepTests.
 */
#include <stdio.h>
#include <string.h>
#include <math.h>
#include "sofa.h"
#include "sofam.h"

static void print_matrix(const char *name, double m[3][3]) {
    printf("  \"%s\": [\n", name);
    for (int i = 0; i < 3; i++) {
        printf("    [%.18e, %.18e, %.18e]%s\n",
               m[i][0], m[i][1], m[i][2], i < 2 ? "," : "");
    }
    printf("  ]");
}

static void compute_epoch(double jd1, double jd2, const char *label,
                          double xp, double yp, double dut1) {
    double x, y, s, era, sp;
    double rc2i[3][3], rc2t[3][3], rpom[3][3];
    double rbpn[3][3], rb[3][3], rp[3][3], rbp[3][3], rn[3][3];
    double dpsi, deps, epsa;

    /* TT centuries from J2000 */
    double t = ((jd1 - DJ00) + jd2) / DJC;

    /* CIP X, Y from series */
    iauXy06(jd1, jd2, &x, &y);

    /* CIO locator s */
    s = iauS06(jd1, jd2, x, y);

    /* Earth rotation angle (needs UT1) */
    double ut1_jd1 = jd1;
    double ut1_jd2 = jd2 + dut1 / 86400.0;
    era = iauEra00(ut1_jd1, ut1_jd2);

    /* TIO locator */
    sp = iauSp00(jd1, jd2);

    /* CIO-based GCRS-to-CIRS matrix */
    iauC2ixys(x, y, s, rc2i);

    /* Polar motion matrix */
    iauPom00(xp, yp, sp, rpom);

    /* Full GCRS-to-ITRS matrix */
    iauC2t06a(jd1, jd2, ut1_jd1, ut1_jd2, xp, yp, rc2t);

    /* Frame bias, precession, nutation matrices */
    iauPn06a(jd1, jd2, &dpsi, &deps, &epsa, rb, rp, rbp, rn, rbpn);

    /* Fundamental arguments */
    double el  = iauFal03(t);
    double elp = iauFalp03(t);
    double f   = iauFaf03(t);
    double d   = iauFad03(t);
    double om  = iauFaom03(t);
    double lve = iauFave03(t);
    double lea = iauFae03(t);
    double lge = iauFapa03(t);

    printf("{\n");
    printf("  \"label\": \"%s\",\n", label);
    printf("  \"jd1\": %.1f,\n", jd1);
    printf("  \"jd2\": %.17e,\n", jd2);
    printf("  \"t_centuries_tt\": %.17e,\n", t);
    printf("  \"cip_x_rad\": %.17e,\n", x);
    printf("  \"cip_y_rad\": %.17e,\n", y);
    printf("  \"cio_s_rad\": %.17e,\n", s);
    printf("  \"era_rad\": %.17e,\n", era);
    printf("  \"sp_rad\": %.17e,\n", sp);
    printf("  \"dpsi_rad\": %.17e,\n", dpsi);
    printf("  \"deps_rad\": %.17e,\n", deps);
    printf("  \"epsa_rad\": %.17e,\n", epsa);
    printf("  \"xp_rad\": %.17e,\n", xp);
    printf("  \"yp_rad\": %.17e,\n", yp);
    printf("  \"dut1_s\": %.17e,\n", dut1);
    printf("  \"fundamental_args\": {\n");
    printf("    \"l_rad\": %.17e,\n", el);
    printf("    \"lp_rad\": %.17e,\n", elp);
    printf("    \"f_rad\": %.17e,\n", f);
    printf("    \"d_rad\": %.17e,\n", d);
    printf("    \"om_rad\": %.17e,\n", om);
    printf("    \"lve_rad\": %.17e,\n", lve);
    printf("    \"lea_rad\": %.17e,\n", lea);
    printf("    \"lge_rad\": %.17e\n", lge);
    printf("  },\n");
    print_matrix("rc2i_gcrs_to_cirs", rc2i);
    printf(",\n");
    print_matrix("rb_frame_bias", rb);
    printf(",\n");
    print_matrix("rp_precession", rp);
    printf(",\n");
    print_matrix("rbpn_bias_prec_nut", rbpn);
    printf(",\n");
    print_matrix("rpom_polar_motion", rpom);
    printf(",\n");
    print_matrix("rc2t_gcrs_to_itrs", rc2t);
    printf("\n}\n");
}

/*
 * Sweep from 1990-01-01 to 2040-01-01 UTC, every 61 days, at a time of day that changes from one
 * epoch to the next, with a sub-second part printed to 0.1 microsecond (the .NET tick, so that the
 * same instant is exactly representable on the .NET side). UT1 is taken equal to UTC (no EOP, as
 * with NullEop). For each epoch: TT centuries, CIP X/Y
 * (iauXy06, IAU 2006/2000A), CIO locator s (iauS06), ERA (iauEra00) and the GCRS-to-TIRS matrix
 * R3(ERA) * C2I (iauC2tcio with an identity polar motion matrix, so s' is excluded too).
 */
static void sweep(void) {
    int first = 1;
    printf("[\n");
    for (int k = 0; ; k++) {
        double mjd = 47892.0 + 61.0 * k;            /* 1990-01-01 + 61 k days */
        if (mjd > 66154.0) break;                    /* 2040-01-01 */
        int seconds = (int) ((k * 27449L) % 86400L); /* time of day, whole seconds */
        long ticks = (k * 7919L * 1009L) % 10000000L;  /* sub-second part, in 0.1 us */

        int iy, im, id;
        double fd;
        iauJd2cal(DJM0, mjd, &iy, &im, &id, &fd);
        int hh = seconds / 3600, mm = (seconds / 60) % 60, ss = seconds % 60;

        double utc1, utc2, tai1, tai2, tt1, tt2, ut11, ut12;
        double sec = ss + ticks * 1e-7;
        iauDtf2d("UTC", iy, im, id, hh, mm, sec, &utc1, &utc2);
        iauUtctai(utc1, utc2, &tai1, &tai2);
        iauTaitt(tai1, tai2, &tt1, &tt2);
        iauUtcut1(utc1, utc2, 0.0, &ut11, &ut12);

        double t = ((tt1 - DJ00) + tt2) / DJC;
        double x, y, rc2i[3][3], rpom[3][3], rc2t[3][3];
        iauXy06(tt1, tt2, &x, &y);
        double s = iauS06(tt1, tt2, x, y);
        double era = iauEra00(ut11, ut12);
        iauC2ixys(x, y, s, rc2i);
        iauIr(rpom);
        iauC2tcio(rc2i, era, rpom, rc2t);

        printf("%s  {\"utc\": \"%04d-%02d-%02dT%02d:%02d:%02d.%07ld\", \"t_tt\": %.17e, "
               "\"cip_x\": %.17e, \"cip_y\": %.17e, \"cio_s\": %.17e, \"era\": %.17e, "
               "\"gcrs_to_tirs\": [%.17e, %.17e, %.17e, %.17e, %.17e, %.17e, %.17e, %.17e, %.17e]}",
               first ? "" : ",\n", iy, im, id, hh, mm, ss, ticks, t, x, y, s, era,
               rc2t[0][0], rc2t[0][1], rc2t[0][2],
               rc2t[1][0], rc2t[1][1], rc2t[1][2],
               rc2t[2][0], rc2t[2][1], rc2t[2][2]);
        first = 0;
    }
    printf("\n]\n");
}

int main(int argc, char **argv) {
    if (argc > 1 && strcmp(argv[1], "--sweep") == 0) {
        sweep();
        return 0;
    }

    printf("[\n");

    /* Epoch 1: J2000.0 TT (zero-offset test) */
    compute_epoch(DJ00, 0.0, "J2000.0_TT", 0.0, 0.0, 0.0);
    printf(",\n");

    /* Epoch 2: SOFA test date (MJD 53736 TT) */
    compute_epoch(DJM0, 53736.0, "SOFA_test_MJD53736",
                  2.55060238e-7, 1.860359247e-6, 0.0);
    printf(",\n");

    /* Epoch 3: Same SOFA test date with no polar motion */
    compute_epoch(DJM0, 53736.0, "SOFA_test_MJD53736_no_pm",
                  0.0, 0.0, 0.0);
    printf(",\n");

    /* Epoch 4: 2024-01-01 12:00:00 TT */
    compute_epoch(DJ00, 8766.5, "2024_Jan_01_TT",
                  0.0, 0.0, 0.0);
    printf(",\n");

    /* Epoch 5: 2010-06-15 12:00:00 TT */
    double mjd_2010 = 55362.0;
    compute_epoch(DJM0, mjd_2010, "2010_Jun_15_TT",
                  0.0, 0.0, 0.0);
    printf(",\n");

    /* Epochs 6 and 7: |sin(Omega)| = 1, where the periodic terms of the CIO locator s peak
       (the 2640.73 uas sin(Omega) term). 2011-02-14 TT and 2020-06-14 TT. */
    compute_epoch(DJM0, 55605.0, "2011_Feb_14_TT_sin_om_minus_1",
                  0.0, 0.0, 0.0);
    printf(",\n");
    compute_epoch(DJM0, 59005.0, "2020_Jun_14_TT_sin_om_plus_1",
                  0.0, 0.0, 0.0);

    printf("\n]\n");
    return 0;
}
