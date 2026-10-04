#include <stdio.h>
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
    printf("    \"lge_rad\": %.17e,\n", lge);
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

int main() {
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

    printf("\n]\n");
    return 0;
}
