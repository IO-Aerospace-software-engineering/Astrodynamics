/*
 * Reference trajectories and CSPICE occultation windows for the managed occultation search (#277, decision 18 (b) of
 * #457).
 *
 *   ./occultation_ref <kernel directory> <output directory>
 *
 * Writes three two-body trajectories as type 5 SPKs (spkw05_c: states every 600 s, every hour for the GEO, and conic
 * propagation between them, exact for a two-body orbit), centered on the Earth, in J2000:
 *
 *   -470001 eclipse_001.bsp  the orbit of the conformance case eclipse 001: a = 7000 km, e = 0.001, i = 28.5 deg,
 *                            RAAN = 45 deg, argp = 0, M = 0 at 2026-03-20 00:00:00 TDB, GMAT's Earth GM
 *                            398600.4415 km^3/s^2 (the GM of the decided golden), over the case's 12 h window +- 600 s
 *   -470002 geo_season.bsp   a circular equatorial GEO (a = 42164.1696 km, the kernel GM), 2026-02-20 to 2026-03-08 TDB,
 *                            the start of the spring eclipse season: no eclipse, then penumbral-only eclipses (the
 *                            Sun grazes the limb), then umbral ones
 *   -470003 leo_grazing.bsp  a circular LEO (a = 7000 km, the kernel GM) whose plane makes 65.3 deg with the Sun at
 *                            2026-06-10 00:00:00 TDB and turns towards it: eclipses that shrink from 390 s to none in
 *                            a day, the last ones grazing, shorter than a 600 s step
 *
 * and prints, as JSON, the windows of CSPICE's gfoclt with its convergence tolerance set to 1e-9 s (gfstol_c), a
 * reference finer than the 1e-6 s of gfoclt's default and of the managed search, for the scenarios of
 * OccultationSearchValidationTests: these three trajectories, the reconstructed LRO trajectory of the tests (the Moon
 * occulting the Sun), and the solar eclipses of 2001-12-14 seen from the Earth's center and of 2005-10-03 seen from
 * DSS-13 and from DSS-65 (Madrid, where it was annular), there also with the Sun or the Moon as a point. Each entry is
 * "<scenario> <type> <abcorr> <front frame, or POINT>": a list of [start, end] in TDB seconds from J2000.
 *
 * The tests load the SPKs as kernel-backed spacecraft and compare the managed search with this reference and with
 * gfoclt run in the test process on the same kernels (1e-6 s); they never write an SPK.
 *
 * Build from the repository root with the vendored CSPICE (N0067):
 *   gcc -O2 -I external-lib/includeLinux tools/occultation_reference/occultation_ref.c external-lib/cspice.a -lm \
 *       -o occultation_ref
 *   ./occultation_ref IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data \
 *       IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/OccultationReference \
 *       > IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/OccultationReference/occultation_reference.json
 */
#include <math.h>
#include <stdio.h>
#include <string.h>
#include "SpiceUsr.h"

#define MAXSTATES 4000

static double ets[MAXSTATES];
static double states[MAXSTATES][6];

typedef void (*state_fn)(double et, double state[6]);

/* Eclipse 001: the case's Keplerian elements, propagated by conics_c with GMAT's GM */
static double ecl_et0;
static const double GM_GMAT = 398600.4415;

static void ecl_state(double et, double state[6]) {
    double a = 7000.0, e = 0.001;
    double elts[8] = {a * (1.0 - e), e, 28.5 * rpd_c(), 45.0 * rpd_c(), 0.0, 0.0, ecl_et0, GM_GMAT};
    conics_c(elts, et, state);
}

/* GEO: a circular equatorial orbit of J2000 */
static double geo_et0, gm_earth;

static void geo_state(double et, double state[6]) {
    double a = 42164.1696;
    double n = sqrt(gm_earth / (a * a * a));
    double th = n * (et - geo_et0);
    state[0] = a * cos(th);
    state[1] = a * sin(th);
    state[2] = 0.0;
    state[3] = -a * n * sin(th);
    state[4] = a * n * cos(th);
    state[5] = 0.0;
}

/* LEO grazing: a circular orbit whose normal h is 90 - 65.3 deg from the Sun at et0, ahead of it along its motion */
static double leo_et0;
static double leo_e1[3], leo_e2[3];

static void leo_setup(void) {
    double s0[6], s1[6], lt, u0[3], u1[3], mot[3], h[3], z[3] = {0.0, 0.0, 1.0}, tmp[3];
    spkezr_c("SUN", leo_et0, "J2000", "NONE", "EARTH", s0, &lt);
    spkezr_c("SUN", leo_et0 + 86400.0, "J2000", "NONE", "EARTH", s1, &lt);
    vhat_c(s0, u0);
    vhat_c(s1, u1);
    vlcom_c(1.0, u1, -vdot_c(u0, u1), u0, tmp);
    vhat_c(tmp, mot);
    double ang = (90.0 - 65.3) * rpd_c();
    vlcom_c(cos(ang), u0, sin(ang), mot, h);
    ucrss_c(h, z, leo_e1);
    vcrss_c(h, leo_e1, leo_e2);
}

static void leo_state(double et, double state[6]) {
    double a = 7000.0;
    double n = sqrt(gm_earth / (a * a * a));
    double th = n * (et - leo_et0);
    for (int i = 0; i < 3; i++) {
        state[i] = a * (cos(th) * leo_e1[i] + sin(th) * leo_e2[i]);
        state[3 + i] = a * n * (-sin(th) * leo_e1[i] + cos(th) * leo_e2[i]);
    }
}

static void write_spk(const char *dir, const char *file, int id, state_fn fn, double first, double last, double step, double gm) {
    char path[1024];
    snprintf(path, sizeof path, "%s/%s", dir, file);
    remove(path);
    int n = 0;
    for (double et = first; et <= last + 0.5 * step && n < MAXSTATES; et += step) {
        ets[n] = et;
        fn(et, states[n]);
        n++;
    }
    SpiceInt handle;
    spkopn_c(path, "IO occultation reference", 0, &handle);
    spkw05_c(handle, id, 399, "J2000", ets[0], ets[n - 1], file, gm, n, states, ets);
    spkcls_c(handle);
    furnsh_c(path);
}

static int first_entry = 1;

/* One entry of the reference: the windows of gfoclt over [first, last], step in seconds, the shapes given */
static void print_shaped_windows(const char *label, const char *type, const char *abcorr, int observer, const char *front, const char *fshape,
                                 const char *fframe, const char *back, const char *bshape, const char *bframe, double first, double last,
                                 double step) {
    SPICEDOUBLE_CELL(cnfine, 2);
    SPICEDOUBLE_CELL(result, 4000);
    char obs[32];
    snprintf(obs, sizeof obs, "%d", observer);
    scard_c(0, &cnfine);
    scard_c(0, &result);
    wninsd_c(first, last, &cnfine);
    gfoclt_c(type, front, fshape, fframe, back, bshape, bframe, abcorr, obs, step, &cnfine, &result);
    SpiceInt count = wncard_c(&result);
    printf("%s  \"%s %s %s %s\": [", first_entry ? "" : ",\n", label, type, abcorr, strcmp(fshape, "POINT") == 0 ? "POINT" : fframe);
    first_entry = 0;
    for (SpiceInt i = 0; i < count; i++) {
        double a, b;
        wnfetd_c(&result, i, &a, &b);
        printf("%s[%.17g, %.17g]", i == 0 ? "" : ", ", a, b);
    }
    printf("]");
}

/* One entry of the reference, both bodies ellipsoids */
static void print_windows(const char *label, const char *type, const char *abcorr, int observer, const char *front, const char *fframe,
                          const char *back, const char *bframe, double first, double last, double step) {
    print_shaped_windows(label, type, abcorr, observer, front, "ELLIPSOID", fframe, back, "ELLIPSOID", bframe, first, last, step);
}

static void earth_sun(const char *label, int observer, const char *type, const char *abcorr, const char *fframe, double first, double last,
                      double step) {
    print_windows(label, type, abcorr, observer, "EARTH", fframe, "SUN", "IAU_SUN", first, last, step);
}

static void moon_sun(const char *label, int observer, const char *type, const char *abcorr, double first, double last, double step) {
    print_windows(label, type, abcorr, observer, "MOON", "IAU_MOON", "SUN", "IAU_SUN", first, last, step);
}

int main(int argc, char **argv) {
    if (argc != 3) {
        fprintf(stderr, "usage: %s <kernel directory> <output directory>\n", argv[0]);
        return 1;
    }

    const char *kernels[] = {"SolarSystem/latest_leapseconds.tls", "SolarSystem/de440s.bsp", "SolarSystem/pck00011.tpc",
                             "SolarSystem/gm_de440.tpc", "SolarSystem/earth_latest_high_prec.bpc",
                             "SolarSystem/earthstns_itrf93_201023.bsp", "LRO/lro-reconstructed-2023349_2024075_v01_little_endian.bsp"};
    char path[1024];
    for (int i = 0; i < 7; i++) {
        snprintf(path, sizeof path, "%s/%s", argv[1], kernels[i]);
        furnsh_c(path);
    }

    SpiceInt dim;
    bodvrd_c("EARTH", "GM", 1, &dim, &gm_earth);

    str2et_c("2026-03-20 00:00:00 TDB", &ecl_et0);
    write_spk(argv[2], "eclipse_001.bsp", -470001, ecl_state, ecl_et0 - 600.0, ecl_et0 + 43200.0 + 600.0, 600.0, GM_GMAT);

    double geo_last;
    str2et_c("2026-02-20 00:00:00 TDB", &geo_et0);
    str2et_c("2026-03-08 00:00:00 TDB", &geo_last);
    write_spk(argv[2], "geo_season.bsp", -470002, geo_state, geo_et0 - 3600.0, geo_last + 3600.0, 3600.0, gm_earth);

    str2et_c("2026-06-10 00:00:00 TDB", &leo_et0);
    leo_setup();
    write_spk(argv[2], "leo_grazing.bsp", -470003, leo_state, leo_et0 - 600.0, leo_et0 + 86400.0 + 600.0, 600.0, gm_earth);

    /* The reference: gfoclt converged to 1e-9 s */
    gfstol_c(1e-9);
    double ecl_last = ecl_et0 + 43200.0, leo_last = leo_et0 + 86400.0;
    const char *types[] = {"ANY", "FULL", "PARTIAL", "ANNULAR"};
    printf("{\n");
    for (int i = 0; i < 4; i++) {
        earth_sun("eclipse_001", -470001, types[i], "NONE", "IAU_EARTH", ecl_et0, ecl_last, 1.0);
    }
    const char *corrections[] = {"LT", "CN", "XLT", "XCN"};
    for (int i = 0; i < 4; i++) {
        earth_sun("eclipse_001", -470001, "ANY", corrections[i], "IAU_EARTH", ecl_et0, ecl_last, 1.0);
    }
    for (int i = 0; i < 2; i++) {
        earth_sun("eclipse_001", -470001, types[i], "NONE", "ITRF93", ecl_et0, ecl_last, 1.0);
    }
    for (int i = 0; i < 3; i++) {
        earth_sun("geo_season", -470002, types[i], "NONE", "IAU_EARTH", geo_et0, geo_last, 10.0);
        earth_sun("leo_grazing", -470003, types[i], "NONE", "IAU_EARTH", leo_et0, leo_last, 1.0);
    }

    double lro_first, lro_last, center_first = 61473664.183390938, center_last = 61646464.183445148, site_first, site_last;
    str2et_c("2023-12-15 00:05:00 UTC", &lro_first);
    str2et_c("2023-12-15 12:00:00 UTC", &lro_last);
    str2et_c("2005-10-03 00:00:00 TDB", &site_first);
    str2et_c("2005-10-04 00:00:00 TDB", &site_last);
    for (int i = 0; i < 3; i++) {
        moon_sun("lro", -85, types[i], "NONE", lro_first, lro_last, 1.0);
    }
    for (int i = 0; i < 4; i++) {
        moon_sun("earth_center_2001", 399, types[i], "NONE", center_first, center_last, 10.0);
        moon_sun("dss65_2005", 399065, types[i], "NONE", site_first, site_last, 10.0);
    }
    moon_sun("dss13_2005", 399013, "ANY", "NONE", site_first, site_last, 10.0);
    moon_sun("dss13_2005", 399013, "PARTIAL", "NONE", site_first, site_last, 10.0);
    moon_sun("dss13_2005", 399013, "ANY", "LT", site_first, site_last, 10.0);
    /* Point bodies (gfoclt accepts only ANY): the Sun's center hidden by the Moon, the Moon's center across the Sun */
    print_shaped_windows("dss65_2005_point_back", "ANY", "NONE", 399065, "MOON", "ELLIPSOID", "IAU_MOON", "SUN", "POINT", " ", site_first,
                         site_last, 10.0);
    print_shaped_windows("dss65_2005_point_front", "ANY", "NONE", 399065, "MOON", "POINT", " ", "SUN", "ELLIPSOID", "IAU_SUN", site_first,
                         site_last, 10.0);
    printf("\n}\n");
    return 0;
}
