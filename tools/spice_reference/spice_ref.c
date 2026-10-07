/*
 * SPICE reference values for states carried into or out of a rotating frame (phase 2, lot C1).
 *
 *   ./spice_ref <kernel directory>
 *
 * The state transformation comes from sxform_c alone: the 6x6 matrix [[R, 0], [dR/dt, R]] applied to the state.
 * It is independent of the path of the library, which goes through a quaternion, the angular velocity of xf2rav_c and
 * v' = R v - (R w) x r'. The inputs (epochs, body-fixed site positions, launch parameters) are the values the
 * library computes for each test; the PR that introduced this harness does not change them.
 */
#include <stdio.h>
#include <math.h>
#include "SpiceUsr.h"

static void transform(const char *from, const char *to, double et, const double state[6], double out[6]) {
    double xform[6][6];
    sxform_c(from, to, et, xform);
    mxvg_c(xform, state, 6, 6, out);
}

static void print_state(const char *label, const double s[6]) {
    printf("  \"%s\": { \"position\": [%.17g, %.17g, %.17g], \"velocity\": [%.17g, %.17g, %.17g] }",
           label, s[0], s[1], s[2], s[3], s[4], s[5]);
}

/* Launch.GetNonInertialAscendingAzimuthLaunch, GetNonInertialDescendingAzimuthLaunch and
 * GetNonInertialInsertionVelocity, with the speed of the launch site taken from SPICE. */
static void print_launch(const char *label, const double siteBodyFixed[3], double inertialAscendingAzimuth,
                         double insertionVelocity) {
    double state[6] = {siteBodyFixed[0], siteBodyFixed[1], siteBodyFixed[2], 0.0, 0.0, 0.0};
    double inertial[6];
    transform("ITRF93", "J2000", 0.0, state, inertial);
    double siteSpeed = vnorm_c(inertial + 3);
    double vrotx = insertionVelocity * sin(inertialAscendingAzimuth) - siteSpeed;
    double vroty = insertionVelocity * cos(inertialAscendingAzimuth);
    double ascending = atan2(vrotx, vroty);
    if (ascending < 0.0) {
        ascending += twopi_c();
    }
    double descending = pi_c() - ascending;
    if (descending < 0.0) {
        descending += twopi_c();
    }
    printf("  \"%s\": { \"site_speed\": %.17g, \"non_inertial_ascending_azimuth_deg\": %.17g, "
           "\"non_inertial_descending_azimuth_deg\": %.17g, \"non_inertial_insertion_velocity\": %.17g }",
           label, siteSpeed, ascending * dpr_c(), descending * dpr_c(), sqrt(vrotx * vrotx + vroty * vroty));
}

int main(int argc, char **argv) {
    if (argc != 2) {
        fprintf(stderr, "usage: %s <kernel directory>\n", argv[0]);
        return 1;
    }

    char path[1024];
    snprintf(path, sizeof path, "%s/latest_leapseconds.tls", argv[1]);
    furnsh_c(path);
    snprintf(path, sizeof path, "%s/pck00011.tpc", argv[1]);
    furnsh_c(path);
    snprintf(path, sizeof path, "%s/earth_latest_high_prec.bpc", argv[1]);
    furnsh_c(path);

    printf("{\n");

    /* StateVectorTests.ToNonInertialFrame: ICRF state to ITRF93 at J2000 TDB (et = 0). */
    double toItrf[6] = {-26499033.67742509, 132757417.33833946, 57556718.47053819,
                        -29.79426007, -5.01805231, -2.17539380};
    double inItrf[6];
    transform("J2000", "ITRF93", 0.0, toItrf, inItrf);
    print_state("StateVectorTests.ToNonInertialFrame", inItrf);
    printf(",\n");

    /* ScenarioTests.PropagateSite: site S333 (30 deg E, 10 deg N, 1000 m), fixed in ITRF93, seen in ICRF. */
    double site[6] = {5441114.152206729, 3141428.720468037, 1100420.993647475, 0.0, 0.0, 0.0};
    double inIcrf[6];
    transform("ITRF93", "J2000", 0.0, site, inIcrf);
    print_state("ScenarioTests.PropagateSite et=0", inIcrf);
    printf(",\n");
    transform("ITRF93", "J2000", 18000.0, site, inIcrf);
    print_state("ScenarioTests.PropagateSite et=18000", inIcrf);
    printf(",\n");

    /* LaunchTests: launch sites at (81 deg W, 28.5 deg N) and (104 deg W, 41 deg S), altitude 0. */
    double north[3] = {877517.9049003018, -5540430.001218749, 3025316.612926567};
    print_launch("LaunchTests site 81W 28.5N", north, 0.7819940839618628, 7667.02684994822);
    printf(",\n");
    double south[3] = {-1166206.4432640779, -4677398.567210214, -4162422.9192307717};
    print_launch("LaunchTests site 104W 41S", south, 0.9624227825976552, 7667.02684994822);
    printf("\n}\n");

    return failed_c() ? 1 : 0;
}
