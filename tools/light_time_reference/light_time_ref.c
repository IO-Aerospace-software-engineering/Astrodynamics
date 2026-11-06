/*
 * CSPICE references for the light-time and stellar-aberration corrections of the reference-body path (#491): the states
 * that spkezr_c, spkcpo_c and spkcpt_c give, and the range, azimuth and elevation of azlcpo_c, for the nine aberration
 * corrections, at three epochs, for observers and targets that CSPICE has no ephemeris of in the library:
 *
 *   - G, a site given by its coordinates (longitude -117 deg, latitude 34 deg, altitude 1000 m on the Earth ellipsoid of
 *     pck00011, ITRF93, as the library places a site given by its coordinates), observer through spkcpo_c, target through
 *     spkcpt_c;
 *   - S, a spacecraft on a two-body orbit about the Earth with BODY399_GM (perigee radius 6800 km, eccentricity 0.01,
 *     inclination 51.6 deg, node 30 deg, argument of perigee 40 deg, mean anomaly 0 at E0), given to the library by its
 *     state at E0 only, written here as an SPK of type 5 (two-body propagation from that state) so that spkezr_c reads it;
 *   - DSS-13, a site of the kernels, through spkezr_c.
 *
 * Rows (target from observer, J2000): the Moon from DSS-13, the Moon, the Sun and S from G, S from the Earth, the Moon and
 * the Sun from S, G from S; azlcpo_c (ELLIPSOID, azimuth clockwise from north, elevation positive up) of S and of the
 * Moon from G. Epochs E0 = 2024-01-01 00:00:00 TDB, E0 + 7000 s and E0 + 43210 s. States in m and m/s; azimuth and
 * elevation in rad; range in m and range rate in m/s.
 *
 * Build and run from the repository root with the vendored CSPICE (N0067):
 *   gcc -O2 -I external-lib/includeLinux tools/light_time_reference/light_time_ref.c external-lib/cspice.a -lm -o light_time_ref
 *   ./light_time_ref IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/SolarSystem /tmp \
 *       > IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/LightTimeReference/light_time_reference.json
 */
#include <dirent.h>
#include <math.h>
#include <stdio.h>
#include <string.h>
#include "SpiceUsr.h"

#define NFLAGS 9
#define NEPOCHS 3
static const char *FLAGS[NFLAGS] = {"NONE", "LT", "LT+S", "CN", "CN+S", "XLT", "XLT+S", "XCN", "XCN+S"};
static const char *SC = "-1001";

static double epochs[NEPOCHS];
static double site[3];
static int first_row = 1;

typedef void (*state_fn)(const char *abcorr, double et, double state[6]);

static const char *target_name, *observer_name;

static void by_spkezr(const char *abcorr, double et, double state[6]) {
    double lt;
    spkezr_c(target_name, et, "J2000", abcorr, observer_name, state, &lt);
}

static void by_spkcpo(const char *abcorr, double et, double state[6]) {
    double lt;
    spkcpo_c(target_name, et, "J2000", "OBSERVER", abcorr, site, "EARTH", "ITRF93", state, &lt);
}

static void by_spkcpt(const char *abcorr, double et, double state[6]) {
    double lt;
    spkcpt_c(site, "EARTH", "ITRF93", et, "J2000", "OBSERVER", abcorr, observer_name, state, &lt);
}

static void row(const char *label, const char *target, const char *observer, state_fn fn) {
    target_name = target;
    observer_name = observer;
    printf("%s    \"%s\": {", first_row ? "" : ",\n", label);
    first_row = 0;
    for (int f = 0; f < NFLAGS; f++) {
        printf("%s\n      \"%s\": [", f == 0 ? "" : ",", FLAGS[f]);
        for (int e = 0; e < NEPOCHS; e++) {
            double state[6];
            fn(FLAGS[f], epochs[e], state);
            printf("%s[", e == 0 ? "" : ", ");
            for (int k = 0; k < 6; k++) printf("%s%.17g", k == 0 ? "" : ", ", state[k] * 1000.0);
            printf("]");
        }
        printf("]");
    }
    printf("\n    }");
}

static void horizontal(const char *label, const char *target) {
    printf("%s    \"%s\": {", first_row ? "" : ",\n", label);
    first_row = 0;
    for (int f = 0; f < NFLAGS; f++) {
        printf("%s\n      \"%s\": [", f == 0 ? "" : ",", FLAGS[f]);
        for (int e = 0; e < NEPOCHS; e++) {
            double azlsta[6], lt;
            azlcpo_c("ELLIPSOID", target, epochs[e], FLAGS[f], SPICEFALSE, SPICETRUE, site, "EARTH", "ITRF93", azlsta, &lt);
            /* range (m), azimuth, elevation (rad), range rate (m/s) */
            printf("%s[%.17g, %.17g, %.17g, %.17g]", e == 0 ? "" : ", ", azlsta[0] * 1000.0, azlsta[1], azlsta[2],
                   azlsta[3] * 1000.0);
        }
        printf("]");
    }
    printf("\n    }");
}

int main(int argc, char **argv) {
    if (argc != 3) {
        fprintf(stderr, "usage: %s <kernel directory> <scratch directory>\n", argv[0]);
        return 1;
    }

    /* Every kernel of the directory, as the tests load it */
    DIR *dir = opendir(argv[1]);
    struct dirent *entry;
    char path[1024];
    while ((entry = readdir(dir)) != NULL) {
        const char *ext = strrchr(entry->d_name, '.');
        if (ext == NULL || !(strcmp(ext, ".tls") == 0 || strcmp(ext, ".bsp") == 0 || strcmp(ext, ".tpc") == 0 ||
                             strcmp(ext, ".bpc") == 0 || strcmp(ext, ".tf") == 0 || strcmp(ext, ".ker") == 0)) {
            continue;
        }
        snprintf(path, sizeof path, "%s/%s", argv[1], entry->d_name);
        furnsh_c(path);
    }
    closedir(dir);

    double radii[3], gm;
    SpiceInt n;
    bodvrd_c("EARTH", "RADII", 3, &n, radii);
    bodvrd_c("EARTH", "GM", 1, &n, &gm);
    double f = (radii[0] - radii[2]) / radii[0];
    georec_c(-117.0 * rpd_c(), 34.0 * rpd_c(), 1.0, radii[0], f, site);

    str2et_c("2024-01-01 00:00:00 TDB", &epochs[0]);
    epochs[1] = epochs[0] + 7000.0;
    epochs[2] = epochs[0] + 43210.0;

    /* S: its state at E0 from the elements, and an SPK of type 5 that propagates it */
    double elts[8] = {6800.0, 0.01, 51.6 * rpd_c(), 30.0 * rpd_c(), 40.0 * rpd_c(), 0.0, epochs[0], gm};
    double s0[6];
    conics_c(elts, epochs[0], s0);
    snprintf(path, sizeof path, "%s/light_time_ref_sc.bsp", argv[2]);
    remove(path);
    SpiceInt handle;
    spkopn_c(path, "sc", 0, &handle);
    spkw05_c(handle, -1001, 399, "J2000", epochs[0] - 86400.0, epochs[0] + 2.0 * 86400.0, "S", gm, 1, &s0, &epochs[0]);
    spkcls_c(handle);
    furnsh_c(path);

    printf("{\n  \"epochs_tdb\": [%.17g, %.17g, %.17g],\n", epochs[0], epochs[1], epochs[2]);
    printf("  \"spacecraft_state_e0\": [");
    for (int k = 0; k < 6; k++) printf("%s%.17g", k == 0 ? "" : ", ", s0[k] * 1000.0);
    printf("],\n  \"site\": {\"longitude_deg\": -117.0, \"latitude_deg\": 34.0, \"altitude_m\": 1000.0},\n");
    printf("  \"states\": {\n");
    row("moon_from_dss13", "MOON", "DSS-13", by_spkezr);
    row("moon_from_site", "MOON", NULL, by_spkcpo);
    row("sun_from_site", "SUN", NULL, by_spkcpo);
    row("spacecraft_from_site", SC, NULL, by_spkcpo);
    row("spacecraft_from_earth", SC, "EARTH", by_spkezr);
    row("moon_from_spacecraft", "MOON", SC, by_spkezr);
    row("sun_from_spacecraft", "SUN", SC, by_spkezr);
    row("site_from_spacecraft", NULL, SC, by_spkcpt);
    printf("\n  },\n  \"horizontal\": {\n");
    first_row = 1;
    horizontal("spacecraft_from_site", SC);
    horizontal("moon_from_site", "MOON");
    printf("\n  }\n}\n");

    unload_c(path);
    remove(path);
    return 0;
}
