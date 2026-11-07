/*
 * CSPICE references for the managed window search (GeometryFinder: #590, #461, #486, #487), the windows of gfdist_c
 * and gfposc_c with their convergence tolerance set to 1e-9 s (gfstol_c), finer than the 1 µs of the managed search and
 * of CSPICE's default, for the searches of GeometryFinderTests, SiteTests and CelestialBodyTests:
 *
 *   - the Earth-Moon distance from 2000-01-01 12:00 to 2000-04-01 12:00 TDB: > 400 000 km, its local and absolute
 *     extrema, the windows within 1000 km of the absolute minimum;
 *   - the distance of the Moon from DSS-13 > 400 000 km over SiteTests' window;
 *   - DSS-13 seen from the Moon in the Moon's frame, declination > 0, 2000-01-01 12:00 to 2000-02-01 12:00 TDB;
 *   - the Moon seen from DSS-13 in the topocentric frame that the library builds (Surface/SiteFrame.cs: north, west and
 *     the geodetic zenith of the station at J2000 TDB on the ellipsoid of pck00011, as tools/geodetic_reference
 *     /dss13_topo.c), 2000-01-01 12:00 to 2000-01-02 12:00 TDB: Z > 0, Z = 0, the local maxima and minima of the
 *     elevation;
 *   - the Sun seen from the site of SiteTests.FindWindowsOnIlluminationConstraint (longitude -116.79445837 deg,
 *     latitude 35.24716450 deg, altitude 1070.85059 m on pck00011, as the library places a site given by its
 *     coordinates), elevation > -0.8 deg (an incidence below 90.8 deg), 2021-05-17 12:00 to 2021-05-18 12:00 TDB, without
 *     aberration and with LT and LT+S.
 *
 * The station of the last item is an SPK of type 8 (constant ITRF93 states) and its frame a TK frame, written to the
 * scratch directory given and removed at the end. Each entry is "<label>": a list of [start, end] in TDB seconds from
 * J2000.
 *
 * Build and run from the repository root with the vendored CSPICE (N0067):
 *   gcc -O2 -I external-lib/includeLinux tools/geometry_reference/geometry_ref.c external-lib/cspice.a -lm -o geometry_ref
 *   ./geometry_ref IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/SolarSystem /tmp \
 *       > IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/GeometryReference/geometry_reference.json
 */
#include <dirent.h>
#include <math.h>
#include <stdio.h>
#include <string.h>
#include "SpiceUsr.h"

static int first_entry = 1;

static void print_windows(const char *label, SpiceCell *result) {
    SpiceInt count = wncard_c(result);
    printf("%s  \"%s\": [", first_entry ? "" : ",\n", label);
    first_entry = 0;
    for (SpiceInt i = 0; i < count; i++) {
        double a, b;
        wnfetd_c(result, i, &a, &b);
        printf("%s[%.17g, %.17g]", i == 0 ? "" : ", ", a, b);
    }
    printf("]");
}

static void distance(const char *label, const char *target, const char *observer, const char *relate, double refval_km,
                     double adjust_km, double first, double last, double step) {
    SPICEDOUBLE_CELL(cnfine, 2);
    SPICEDOUBLE_CELL(result, 2000);
    scard_c(0, &cnfine);
    scard_c(0, &result);
    wninsd_c(first, last, &cnfine);
    gfdist_c(target, "NONE", observer, relate, refval_km, adjust_km, step, 1000, &cnfine, &result);
    print_windows(label, &result);
}

static void coordinate(const char *label, const char *target, const char *frame, const char *abcorr, const char *observer,
                       const char *system, const char *coord, const char *relate, double refval, double first, double last,
                       double step) {
    SPICEDOUBLE_CELL(cnfine, 2);
    SPICEDOUBLE_CELL(result, 2000);
    scard_c(0, &cnfine);
    scard_c(0, &result);
    wninsd_c(first, last, &cnfine);
    gfposc_c(target, frame, abcorr, observer, system, coord, relate, refval, 0.0, step, 1000, &cnfine, &result);
    print_windows(label, &result);
}

/* A TK frame north, west, zenith of the geodetic coordinates, relative to ITRF93 (the library's SiteFrameTemplate.tf) */
static void topo_frame(const char *name, int id, int center, double lon, double lat) {
    char buf[256];
    snprintf(buf, sizeof buf, "FRAME_%s", name);
    pipool_c(buf, 1, &id);
    snprintf(buf, sizeof buf, "FRAME_%d_NAME", id);
    pcpool_c(buf, 1, (SpiceInt)strlen(name) + 1, name);
    int cls = 4;
    snprintf(buf, sizeof buf, "FRAME_%d_CLASS", id);
    pipool_c(buf, 1, &cls);
    snprintf(buf, sizeof buf, "FRAME_%d_CLASS_ID", id);
    pipool_c(buf, 1, &id);
    snprintf(buf, sizeof buf, "FRAME_%d_CENTER", id);
    pipool_c(buf, 1, &center);
    snprintf(buf, sizeof buf, "TKFRAME_%d_RELATIVE", id);
    pcpool_c(buf, 1, 7, "ITRF93");
    snprintf(buf, sizeof buf, "TKFRAME_%d_SPEC", id);
    pcpool_c(buf, 1, 7, "ANGLES");
    snprintf(buf, sizeof buf, "TKFRAME_%d_UNITS", id);
    pcpool_c(buf, 1, 8, "RADIANS");
    SpiceInt axes[3] = {3, 2, 3};
    snprintf(buf, sizeof buf, "TKFRAME_%d_AXES", id);
    pipool_c(buf, 3, axes);
    double angles[3] = {-lon, -(halfpi_c() - lat), pi_c()};
    snprintf(buf, sizeof buf, "TKFRAME_%d_ANGLES", id);
    pdpool_c(buf, 3, angles);
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
    gfstol_c(1e-9);

    double radii[3];
    SpiceInt n;
    bodvrd_c("EARTH", "RADII", 3, &n, radii);
    double f = (radii[0] - radii[2]) / radii[0];

    printf("{\n");

    /* CelestialBodyTests and GeometryFinderTests: the Earth-Moon distance */
    double et0, et1;
    str2et_c("2000-01-01 12:00:00 TDB", &et0);
    str2et_c("2000-04-01 12:00:00 TDB", &et1);
    distance("earth_moon_distance > 400000 km", "MOON", "EARTH", ">", 400000.0, 0.0, et0, et1, 86400.0);
    distance("earth_moon_distance LOCMIN", "MOON", "EARTH", "LOCMIN", 0.0, 0.0, et0, et1, 86400.0);
    distance("earth_moon_distance LOCMAX", "MOON", "EARTH", "LOCMAX", 0.0, 0.0, et0, et1, 86400.0);
    distance("earth_moon_distance ABSMIN", "MOON", "EARTH", "ABSMIN", 0.0, 0.0, et0, et1, 86400.0);
    distance("earth_moon_distance ABSMAX", "MOON", "EARTH", "ABSMAX", 0.0, 0.0, et0, et1, 86400.0);
    distance("earth_moon_distance ABSMIN adjust 1000 km", "MOON", "EARTH", "ABSMIN", 0.0, 1000.0, et0, et1, 86400.0);

    /* SiteTests.FindWindowsOnDistanceConstraint */
    distance("dss13_moon_distance > 400000 km", "MOON", "DSS-13", ">", 400000.0, 0.0, 220881665.18391809, 228657665.18565452,
             3600.0);

    /* SiteTests.FindWindowsOnCoordinateConstraint: DSS-13 from the Moon in the Moon's frame */
    SpiceInt frcode;
    SpiceChar moonFrame[33];
    SpiceBoolean found;
    cidfrm_c(301, 33, &frcode, moonFrame, &found);
    double et2;
    str2et_c("2000-02-01 12:00:00 TDB", &et2);
    coordinate("moon_dss13_declination > 0", "DSS-13", moonFrame, "NONE", "MOON", "RA/DEC", "DECLINATION", ">", 0.0, et0, et2,
               60.0);

    /* The Moon from DSS-13 in the library's frame of the kernel station, on pck00011 at J2000 TDB */
    double station[3], lt, lon, lat, alt;
    spkpos_c("DSS-13", et0, "ITRF93", "NONE", "EARTH", station, &lt);
    recgeo_c(station, radii[0], f, &lon, &lat, &alt);
    topo_frame("DSS-13_LIB_TOPO", 1399913, 399013, lon, lat);
    double day = et0 + 86400.0;
    coordinate("dss13_moon_z > 0", "MOON", "DSS-13_LIB_TOPO", "NONE", "DSS-13", "RECTANGULAR", "Z", ">", 0.0, et0, day, 60.0);
    coordinate("dss13_moon_z = 0", "MOON", "DSS-13_LIB_TOPO", "NONE", "DSS-13", "RECTANGULAR", "Z", "=", 0.0, et0, day, 60.0);
    coordinate("dss13_moon_elevation LOCMAX", "MOON", "DSS-13_LIB_TOPO", "NONE", "DSS-13", "LATITUDINAL", "LATITUDE", "LOCMAX",
               0.0, et0, day, 60.0);
    coordinate("dss13_moon_elevation LOCMIN", "MOON", "DSS-13_LIB_TOPO", "NONE", "DSS-13", "LATITUDINAL", "LATITUDE", "LOCMIN",
               0.0, et0, day, 60.0);

    /* The site of SiteTests.FindWindowsOnIlluminationConstraint, given by its coordinates */
    double slon = -116.79445837 * rpd_c(), slat = 35.24716450 * rpd_c(), salt = 1.07085059;
    double site[6] = {0.0, 0.0, 0.0, 0.0, 0.0, 0.0};
    georec_c(slon, slat, salt, radii[0], f, site);
    snprintf(path, sizeof path, "%s/geometry_ref_site.bsp", argv[2]);
    remove(path);
    SpiceInt handle;
    double states[2][6];
    memcpy(states[0], site, sizeof site);
    memcpy(states[1], site, sizeof site);
    double s0 = 674524800.0, s1 = 674611200.0;
    spkopn_c(path, "site", 0, &handle);
    spkw08_c(handle, 399913, 399, "ITRF93", s0 - 3600.0, s1 + 3600.0, "site", 1, 2, states, s0 - 3600.0, s1 - s0 + 7200.0);
    spkcls_c(handle);
    furnsh_c(path);
    topo_frame("SITE_LIB_TOPO", 1399914, 399913, slon, slat);
    double mask = -0.8 * rpd_c();
    coordinate("site_sun_elevation > -0.8 deg NONE", "SUN", "SITE_LIB_TOPO", "NONE", "399913", "LATITUDINAL", "LATITUDE", ">",
               mask, s0, s1, 3600.0);
    coordinate("site_sun_elevation > -0.8 deg LT", "SUN", "SITE_LIB_TOPO", "LT", "399913", "LATITUDINAL", "LATITUDE", ">",
               mask, s0, s1, 3600.0);
    coordinate("site_sun_elevation > -0.8 deg LT+S", "SUN", "SITE_LIB_TOPO", "LT+S", "399913", "LATITUDINAL", "LATITUDE", ">",
               mask, s0, s1, 3600.0);
    unload_c(path);
    remove(path);

    printf("\n}\n");
    return 0;
}
