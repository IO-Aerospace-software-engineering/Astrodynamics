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
 * Rows of #489: G as target (spkcpt_c) seen from the Moon, the Earth (its own body), the Sun, the Mars barycenter and LRO
 * (the reconstructed SPK of Data/LRO, -85), and three more sites given by their coordinates, on the equator (longitude
 * 0, latitude 0, altitude 0) and at the poles (longitude 0, latitude +-90 deg, altitude 0), seen from the Moon; the window
 * of the Site example of DEVELOPER_GUIDE.md (G within 400000 km of the Moon with LT, 2024-01-01 TDB to 2024-01-02 TDB),
 * from 60 s samples of spkcpt_c and a bisection to adjacent doubles (1.2e-7 s).
 *
 * Rows of #593: states in a non-inertial frame (ITRF93, centred on the Earth; MOON_ME, centred on the Moon; DSS-13_TOPO,
 * centred on DSS-13), whose orientation spkezr_c evaluates at et -+ the light time of the frame's centre (spkcpo_c and
 * spkcpt_c with the locus CENTER, which this program checked against spkezr_c from DSS-13 to 8e-6 m): the Moon from
 * DSS-13, from G and from S, S from G, G from S and from the Moon, the Sun from S, S from the Earth.
 *
 * Rows of the review of #593: states in a non-inertial frame centred on a third object, neither the observer nor the
 * target: the topocentric frames of G and of H, a second site given by its coordinates (longitude 10 deg, latitude 45
 * deg, altitude 200 m), each the TK frame the library writes for a site (Site.WriteFrameAsync: angles -longitude,
 * -(pi/2 - latitude), pi about the axes 3, 2, 3 of ITRF93) centred on the site, written with an SPK of type 8 of the site
 * fixed in ITRF93 (399491 and 399492) so that SPICE reads the centre; and S_TK, a frame of S oriented as ITRF93 (a TK
 * frame, the identity relative to ITRF93, centred on S), which the library gives S as an attitude record. Rows: the Moon
 * from S and from DSS-13 in G's frame; the Moon and S from G, and G from the Moon, in H's frame; the Moon from G and from
 * DSS-13 in S_TK; spkezr_c, or spkcpo_c and spkcpt_c with the locus CENTER.
 *
 * Rows of the review of #489 (items 2 and 4 of its tests): C, the copy of DSS-13 from its coordinates (its position in
 * ITRF93 at J2000 TDB, recgeo_c on the Earth ellipsoid of pck00011), seen from the Moon and from the Earth (spkcpt_c at
 * those coordinates); the windows of G seen from the Moon with LT, 2024-01-01 TDB to 2024-01-02 TDB, from 60 s samples of
 * spkcpt_c and a bisection to adjacent doubles: its range (J2000) above 405000 km, its declination (J2000) above -9.5 deg,
 * and its declination in the frame of H (spkcpt_c, locus CENTER) above 0 deg.
 *
 * Rows of #549: the Moon seen from a point of a LEO given by its state, as an attitude reads it (spkcvo_c, the observer's
 * state relative to the Earth in J2000 at the epoch): the LEO of the issue (a = 6778.137 km, e = 0, i = 51.6 deg, node,
 * argument of perigee and mean anomaly 0 at 2024-03-01 12:00:00 TDB, conics_c with BODY399_GM) every 600 s over 1 h, and
 * the state of InstrumentPointingAttitudeTests.Execute (6678 km on +x, 7.727 km/s on +y at 2021-01-01 13:00:00 TDB).
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

/* The thresholds of the coordinate windows of the review of #489 (km, deg), each crossed over the day */
#define RANGE_THRESHOLD 405000.0
#define DECLINATION_THRESHOLD -9.5
#define FRAME_THRESHOLD 0.0

static double epochs[NEPOCHS];
static double g_site[3];
static double *site = g_site;
static const char *frame = "J2000";
static const char *refloc = "OBSERVER";
static int first_row = 1;

typedef void (*state_fn)(const char *abcorr, double et, double state[6]);

static const char *target_name, *observer_name;

static void by_spkezr(const char *abcorr, double et, double state[6]) {
    double lt;
    spkezr_c(target_name, et, frame, abcorr, observer_name, state, &lt);
}

static void by_spkcpo(const char *abcorr, double et, double state[6]) {
    double lt;
    spkcpo_c(target_name, et, frame, refloc, abcorr, site, "EARTH", "ITRF93", state, &lt);
}

static void by_spkcpt(const char *abcorr, double et, double state[6]) {
    double lt;
    spkcpt_c(site, "EARTH", "ITRF93", et, frame, refloc, abcorr, observer_name, state, &lt);
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

/* The distance of G from the Moon with LT minus 400000 km, km */
static double guide_distance(double et) {
    double state[6], lt;
    spkcpt_c(g_site, "EARTH", "ITRF93", et, "J2000", "OBSERVER", "LT", "MOON", state, &lt);
    return vnorm_c(state) - 400000.0;
}

/* #489: the window of the Site example of DEVELOPER_GUIDE.md, 60 s samples and a bisection of each sign change to adjacent doubles */
static void guide_window(void) {
    double start, end;
    str2et_c("2024-01-01 00:00:00 TDB", &start);
    str2et_c("2024-01-02 00:00:00 TDB", &end);
    printf("  \"guide_window_lt\": [");
    int first = 1;
    double previous = start, fprevious = guide_distance(start);
    if (fprevious < 0.0) {
        printf("%.17g", start);
        first = 0;
    }
    for (double t = start + 60.0; t <= end + 1e-9; t += 60.0) {
        double ft = guide_distance(t);
        if ((fprevious < 0.0) != (ft < 0.0)) {
            double a = previous, b = t, fa = fprevious;
            for (;;) {
                double m = 0.5 * (a + b);
                if (m <= a || m >= b) break;
                double fm = guide_distance(m);
                if ((fa < 0.0) == (fm < 0.0)) {
                    a = m;
                    fa = fm;
                } else {
                    b = m;
                }
            }
            printf("%s%.17g", first ? "" : ", ", fabs(guide_distance(a)) <= fabs(guide_distance(b)) ? a : b);
            first = 0;
        }
        previous = t;
        fprevious = ft;
    }
    if (fprevious < 0.0) printf("%s%.17g", first ? "" : ", ", end);
    printf("]");
}

/* The review of #489: a coordinate of G seen from the Moon with LT, minus a threshold */
static const char *window_frame;
static const char *window_refloc;
static int window_declination;
static double window_value;

static double window_function(double et) {
    double state[6], lt, range, ra, dec;
    spkcpt_c(g_site, "EARTH", "ITRF93", et, window_frame, window_refloc, "LT", "MOON", state, &lt);
    recrad_c(state, &range, &ra, &dec);
    return (window_declination ? dec * dpr_c() : range) - window_value;
}

/* The windows where window_function is above 0: 60 s samples and a bisection of each sign change to adjacent doubles */
static void coordinate_window(const char *label, const char *frame_name, const char *locus, int declination, double value) {
    double start, end;
    window_frame = frame_name;
    window_refloc = locus;
    window_declination = declination;
    window_value = value;
    str2et_c("2024-01-01 00:00:00 TDB", &start);
    str2et_c("2024-01-02 00:00:00 TDB", &end);
    printf("%s    \"%s\": {\"value\": %.17g, \"windows\": [", first_row ? "" : ",\n", label, value);
    first_row = 0;
    int first = 1;
    double previous = start, fprevious = window_function(start);
    double lowest = fprevious, highest = fprevious;
    if (fprevious > 0.0) {
        printf("%.17g", start);
        first = 0;
    }
    for (double t = start + 60.0; t <= end + 1e-9; t += 60.0) {
        double ft = window_function(t);
        if (ft < lowest) lowest = ft;
        if (ft > highest) highest = ft;
        if ((fprevious > 0.0) != (ft > 0.0)) {
            double a = previous, b = t, fa = fprevious;
            for (;;) {
                double m = 0.5 * (a + b);
                if (m <= a || m >= b) break;
                double fm = window_function(m);
                if ((fa > 0.0) == (fm > 0.0)) {
                    a = m;
                    fa = fm;
                } else {
                    b = m;
                }
            }
            printf("%s%.17g", first ? "" : ", ", fabs(window_function(a)) <= fabs(window_function(b)) ? a : b);
            first = 0;
        }
        previous = t;
        fprevious = ft;
    }
    if (fprevious > 0.0) printf("%s%.17g", first ? "" : ", ", end);
    printf("]}");
    fprintf(stderr, "%s: samples from %.6f to %.6f about the threshold\n", label, lowest, highest);
}

/* A site given by its coordinates as an SPK object: fixed in ITRF93 about the Earth (type 8, two equal states) */
static void write_site_spk(SpiceInt handle, int id, const double position[3], double first, double last) {
    double states[2][6] = {{position[0], position[1], position[2], 0.0, 0.0, 0.0}, {position[0], position[1], position[2], 0.0, 0.0, 0.0}};
    spkw08_c(handle, id, 399, "ITRF93", first, last, "SITE", 1, 2, states, first, last - first);
}

/* Kernel pool assignments, one per line */
static void pool(int count, const char *lines[]) {
    char buffer[16][160];
    for (int k = 0; k < count; k++) snprintf(buffer[k], 160, "%s", lines[k]);
    lmpool_c(buffer, 160, count);
}

/* The TK frame the library writes for a site (SiteFrameTemplate.tf), centred on the site's SPK object */
static void define_site_frame(const char *name, int frame_id, int center, double longitude, double latitude) {
    char b[10][160];
    snprintf(b[0], 160, "FRAME_%s = %d", name, frame_id);
    snprintf(b[1], 160, "FRAME_%d_NAME = '%s'", frame_id, name);
    snprintf(b[2], 160, "FRAME_%d_CLASS = 4", frame_id);
    snprintf(b[3], 160, "FRAME_%d_CLASS_ID = %d", frame_id, frame_id);
    snprintf(b[4], 160, "FRAME_%d_CENTER = %d", frame_id, center);
    snprintf(b[5], 160, "TKFRAME_%d_SPEC = 'ANGLES'", frame_id);
    snprintf(b[6], 160, "TKFRAME_%d_RELATIVE = 'ITRF93'", frame_id);
    snprintf(b[7], 160, "TKFRAME_%d_ANGLES = (%.17g, %.17g, 3.141592653589793116)", frame_id, -longitude, -(halfpi_c() - latitude));
    snprintf(b[8], 160, "TKFRAME_%d_AXES = (3, 2, 3)", frame_id);
    snprintf(b[9], 160, "TKFRAME_%d_UNITS = 'RADIANS'", frame_id);
    const char *lines[10];
    for (int k = 0; k < 10; k++) lines[k] = b[k];
    pool(10, lines);
}

/* #549: the Moon from a state relative to the Earth in J2000, for the nine corrections */
static void pointing_state(double et, const double obssta[6]) {
    printf("%s    {\"epoch_tdb\": %.17g, \"state\": [", first_row ? "" : ",\n", et);
    first_row = 0;
    for (int k = 0; k < 6; k++) printf("%s%.17g", k == 0 ? "" : ", ", obssta[k] * 1000.0);
    printf("], \"moon\": {");
    for (int f = 0; f < NFLAGS; f++) {
        double state[6], lt;
        spkcvo_c("MOON", et, "J2000", "OBSERVER", FLAGS[f], obssta, et, "EARTH", "J2000", state, &lt);
        printf("%s\"%s\": [%.17g, %.17g, %.17g]", f == 0 ? "" : ", ", FLAGS[f], state[0] * 1000.0, state[1] * 1000.0,
               state[2] * 1000.0);
    }
    printf("}}");
}

static void pointing(double gm) {
    double start;
    str2et_c("2024-03-01 12:00:00 TDB", &start);
    double elts[8] = {6778.137, 0.0, 51.6 * rpd_c(), 0.0, 0.0, 0.0, start, gm};
    printf("  \"pointing\": [\n");
    first_row = 1;
    for (int k = 0; k <= 6; k++) {
        double et = start + 600.0 * k, obssta[6];
        conics_c(elts, et, obssta);
        pointing_state(et, obssta);
    }
    double execute;
    str2et_c("2021-01-01 13:00:00 TDB", &execute);
    double executeState[6] = {6678.0, 0.0, 0.0, 0.0, 7.727, 0.0};
    pointing_state(execute, executeState);
    printf("\n  ],\n");
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
    snprintf(path, sizeof path, "%s/../LRO/lro-reconstructed-2023349_2024075_v01_little_endian.bsp", argv[1]);
    furnsh_c(path);

    double radii[3], gm;
    SpiceInt n;
    bodvrd_c("EARTH", "RADII", 3, &n, radii);
    bodvrd_c("EARTH", "GM", 1, &n, &gm);
    double f = (radii[0] - radii[2]) / radii[0];
    georec_c(-117.0 * rpd_c(), 34.0 * rpd_c(), 1.0, radii[0], f, g_site);
    double equator_site[3], north_site[3], south_site[3];
    georec_c(0.0, 0.0, 0.0, radii[0], f, equator_site);
    georec_c(0.0, 90.0 * rpd_c(), 0.0, radii[0], f, north_site);
    georec_c(0.0, -90.0 * rpd_c(), 0.0, radii[0], f, south_site);

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

    /* The review of #593: G and H as SPK objects (399491, 399492), their frames, and S_TK */
    double h_site[3];
    georec_c(10.0 * rpd_c(), 45.0 * rpd_c(), 0.2, radii[0], f, h_site);
    char site_path[1024];
    snprintf(site_path, sizeof site_path, "%s/light_time_ref_sites.bsp", argv[2]);
    remove(site_path);
    spkopn_c(site_path, "sites", 0, &handle);
    write_site_spk(handle, 399491, g_site, epochs[0] - 86400.0, epochs[0] + 2.0 * 86400.0);
    write_site_spk(handle, 399492, h_site, epochs[0] - 86400.0, epochs[0] + 2.0 * 86400.0);
    spkcls_c(handle);
    furnsh_c(site_path);
    define_site_frame("G491_TOPO", 1399491, 399491, -117.0 * rpd_c(), 34.0 * rpd_c());
    define_site_frame("H491_TOPO", 1399492, 399492, 10.0 * rpd_c(), 45.0 * rpd_c());
    const char *s_tk[] = {"FRAME_S_TK = -1001000", "FRAME_-1001000_NAME = 'S_TK'", "FRAME_-1001000_CLASS = 4",
                          "FRAME_-1001000_CLASS_ID = -1001000", "FRAME_-1001000_CENTER = -1001",
                          "TKFRAME_-1001000_SPEC = 'MATRIX'", "TKFRAME_-1001000_RELATIVE = 'ITRF93'",
                          "TKFRAME_-1001000_MATRIX = (1.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0)"};
    pool(8, s_tk);

    /* The review of #489: C, the copy of DSS-13 from its coordinates at J2000 TDB */
    double dss13[3], lt0, copy_lon, copy_lat, copy_alt, copy_site[3];
    spkpos_c("DSS-13", 0.0, "ITRF93", "NONE", "EARTH", dss13, &lt0);
    recgeo_c(dss13, radii[0], f, &copy_lon, &copy_lat, &copy_alt);
    georec_c(copy_lon, copy_lat, copy_alt, radii[0], f, copy_site);

    printf("{\n  \"epochs_tdb\": [%.17g, %.17g, %.17g],\n", epochs[0], epochs[1], epochs[2]);
    printf("  \"spacecraft_state_e0\": [");
    for (int k = 0; k < 6; k++) printf("%s%.17g", k == 0 ? "" : ", ", s0[k] * 1000.0);
    printf("],\n  \"site\": {\"longitude_deg\": -117.0, \"latitude_deg\": 34.0, \"altitude_m\": 1000.0},\n");
    printf("  \"other_site\": {\"longitude_deg\": 10.0, \"latitude_deg\": 45.0, \"altitude_m\": 200.0},\n");
    printf("  \"dss13_copy\": {\"longitude_rad\": %.17g, \"latitude_rad\": %.17g, \"altitude_m\": %.17g},\n", copy_lon, copy_lat,
           copy_alt * 1000.0);
    printf("  \"states\": {\n");
    row("moon_from_dss13", "MOON", "DSS-13", by_spkezr);
    row("moon_from_site", "MOON", NULL, by_spkcpo);
    row("sun_from_site", "SUN", NULL, by_spkcpo);
    row("spacecraft_from_site", SC, NULL, by_spkcpo);
    row("spacecraft_from_earth", SC, "EARTH", by_spkezr);
    row("moon_from_spacecraft", "MOON", SC, by_spkezr);
    row("sun_from_spacecraft", "SUN", SC, by_spkezr);
    row("site_from_spacecraft", NULL, SC, by_spkcpt);
    /* #489: G and the other coordinate sites as targets */
    row("site_from_moon", NULL, "MOON", by_spkcpt);
    row("site_from_earth", NULL, "EARTH", by_spkcpt);
    row("site_from_sun", NULL, "SUN", by_spkcpt);
    row("site_from_mars_barycenter", NULL, "MARS BARYCENTER", by_spkcpt);
    row("site_from_lro", NULL, "-85", by_spkcpt);
    site = equator_site;
    row("equator_site_from_moon", NULL, "MOON", by_spkcpt);
    site = north_site;
    row("north_pole_site_from_moon", NULL, "MOON", by_spkcpt);
    site = south_site;
    row("south_pole_site_from_moon", NULL, "MOON", by_spkcpt);
    site = g_site;
    /* #593: non-inertial output frames, evaluated as spkezr_c evaluates them (refloc CENTER of spkcpo_c and spkcpt_c) */
    refloc = "CENTER";
    frame = "ITRF93";
    row("moon_from_dss13_itrf93", "MOON", "DSS-13", by_spkezr);
    row("moon_from_site_itrf93", "MOON", NULL, by_spkcpo);
    row("moon_from_spacecraft_itrf93", "MOON", SC, by_spkezr);
    row("spacecraft_from_site_itrf93", SC, NULL, by_spkcpo);
    row("site_from_spacecraft_itrf93", NULL, SC, by_spkcpt);
    row("site_from_moon_itrf93", NULL, "MOON", by_spkcpt);
    row("sun_from_spacecraft_itrf93", "SUN", SC, by_spkezr);
    row("spacecraft_from_earth_itrf93", SC, "EARTH", by_spkezr);
    frame = "MOON_ME";
    row("moon_from_dss13_moon_me", "MOON", "DSS-13", by_spkezr);
    row("moon_from_site_moon_me", "MOON", NULL, by_spkcpo);
    row("moon_from_spacecraft_moon_me", "MOON", SC, by_spkezr);
    row("spacecraft_from_site_moon_me", SC, NULL, by_spkcpo);
    row("site_from_moon_moon_me", NULL, "MOON", by_spkcpt);
    frame = "DSS-13_TOPO";
    row("moon_from_dss13_topo", "MOON", "DSS-13", by_spkezr);
    /* The review of #593: a frame centred on a third object */
    frame = "G491_TOPO";
    row("moon_from_spacecraft_in_site_frame", "MOON", SC, by_spkezr);
    row("moon_from_dss13_in_site_frame", "MOON", "DSS-13", by_spkezr);
    frame = "H491_TOPO";
    row("moon_from_site_in_other_site_frame", "MOON", NULL, by_spkcpo);
    row("spacecraft_from_site_in_other_site_frame", SC, NULL, by_spkcpo);
    row("site_from_moon_in_other_site_frame", NULL, "MOON", by_spkcpt);
    frame = "S_TK";
    row("moon_from_site_in_spacecraft_frame", "MOON", NULL, by_spkcpo);
    row("moon_from_dss13_in_spacecraft_frame", "MOON", "DSS-13", by_spkezr);
    frame = "J2000";
    refloc = "OBSERVER";
    /* The review of #489: C, the copy of DSS-13 */
    site = copy_site;
    row("dss13_copy_from_moon", NULL, "MOON", by_spkcpt);
    row("dss13_copy_from_earth", NULL, "EARTH", by_spkcpt);
    site = g_site;
    printf("\n  },\n  \"horizontal\": {\n");
    first_row = 1;
    horizontal("spacecraft_from_site", SC);
    horizontal("moon_from_site", "MOON");
    printf("\n  },\n");
    pointing(gm);
    guide_window();
    printf(",\n  \"coordinate_windows_lt\": {\n");
    first_row = 1;
    coordinate_window("range_above", "J2000", "OBSERVER", 0, RANGE_THRESHOLD);
    coordinate_window("declination_above", "J2000", "OBSERVER", 1, DECLINATION_THRESHOLD);
    coordinate_window("declination_in_other_site_frame_above", "H491_TOPO", "CENTER", 1, FRAME_THRESHOLD);
    printf("\n  }\n}\n");

    unload_c(site_path);
    remove(site_path);
    unload_c(path);
    remove(path);
    return 0;
}
