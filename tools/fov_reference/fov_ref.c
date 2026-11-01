/*
 * Reference kernels and CSPICE field-of-view windows for the managed field-of-view search (#515, choice 9 (a)).
 *
 *   ./fov_ref <kernel directory> <output directory>
 *
 * Writes, in the output directory, the kernels of three spacecraft, read by the tests as kernel-backed spacecraft:
 *
 *   -470101 FOVLEO     an inclined LEO (a = 6800 km, e = 0.001, i = 51.6 deg, RAAN = 30 deg, argp = 0, M = 0 at T0),
 *                      whose attitude turns: its frame rotates about the axis k = (sin 20 deg, 0, cos 20 deg) of J2000 at
 *                      one turn in 10 800 s, through a type 3 CK with its angular velocity (fovleo.bc, fovleo.tsc, fovleo.tf)
 *   -470103 FOVPOLAR   a polar LEO (a = 7000 km, e = 0, i = 90 deg, RAAN = 0), a constant attitude (its frame is J2000),
 *                      through a type 3 CK (fovpolar.bc, fovpolar.tsc, fovpolar.tf): the Earth's limb crosses its fields
 *                      near the pole
 *   -470102 FOVTARGET  a LEO (a = 7200 km, e = 0.002, i = 45 deg, RAAN = 60 deg), an SPK only: a spacecraft target
 *
 * with T0 = 2026-04-01 00:00:00 TDB. The trajectories are two-body orbits about the Earth with the kernel GM, written as
 * type 5 SPKs (spkw05_c: states every 60 s and conic propagation between them, exact for a two-body orbit) over
 * [T0 - 600 s, T0 + 10 800 s + 600 s], in J2000; the CKs are written every 10 s over the same span (a rotation at a
 * constant rate about a fixed axis, which the linear interpolation of a type 3 segment reproduces). Each spacecraft has
 * its clock (SCLK type 1, the library's template, 65 536 ticks a second), its frame (CK frame "<NAME>_FRAME", identifier
 * NAIF ID x 1000, the library's template) and its instruments: a TK frame "<NAME>_<INSTRUMENT>" relative to the
 * spacecraft frame (Euler angles 1-2-3, zero or about one axis only, where the library's rotation vector agrees) and an
 * IK of ANGLES class.
 *
 * The boresights of the Moon, Sun and star fields are computed here, from the apparent (LT+S) directions of those bodies
 * at chosen epochs, and written to the JSON with every instrument's definition (17 significant digits), from which the
 * tests build the same instruments. The star FOVSTAR1 is a direction of J2000 (right ascension and declination), crossed
 * by the field of FOVLEO's STAR1.
 *
 * Prints, as JSON, on the standard output:
 *   "gftfov": the windows of CSPICE's gftfov at a step of 1 s, its convergence tolerance set to 1e-9 s (gfstol_c), a
 *             reference finer than the 1e-6 s of gftfov's default and of the managed search; key
 *             "<instrument> <target> <POINT, or the target frame> <abcorr>", a list of [start, end] in TDB seconds;
 *   "gfrfov": the windows of gfrfov for the star, key "<instrument> <star> <abcorr>" (NONE, S, XS);
 *   "fovtrg": fovtrg POINT on a grid, key "<instrument> <target> <abcorr>": its first epoch, its step and a string of
 *             '0' and '1' (in view or not at each epoch of the grid); "fovray": fovray for the star, likewise.
 *   "rayscan": FOVPOLAR's rectangle against the Earth's ellipsoid, which gftfov cannot search there, by gfudb on a scan of
 *             the rays of the faces of its pyramid (rayscan below), key "<instrument> EARTH ITRF93 <abcorr>".
 *
 * Build from the repository root with the vendored CSPICE (N0067):
 *   gcc -O2 -I external-lib/includeLinux tools/fov_reference/fov_ref.c external-lib/cspice.a -lm -o fov_ref
 *   ./fov_ref IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data \
 *       IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/FieldOfViewReference \
 *       > IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/FieldOfViewReference/fov_reference.json
 */
#include <math.h>
#include <stdio.h>
#include <string.h>
#include "SpiceUsr.h"

#define MAXSTATES 400
#define MAXQUATS 1400
#define MAXINST 32
#define SPAN 10800.0
#define MARGIN 600.0
#define GRIDSTEP 30.0
#define REFSTEP 1.0

static double t0, gm_earth;

static double ets[MAXSTATES];
static double states[MAXSTATES][6];
static double sclk[MAXQUATS];
static double quats[MAXQUATS][4];
static double avvs[MAXQUATS][3];

/* The turning attitude of FOVLEO: R(t) = rotation of vectors about k by w (t - T0), from the spacecraft frame to J2000 */
static double spin_axis[3];
static const double spin_rate = 2.0 * 3.14159265358979323846 / 10800.0;

typedef struct {
    int id;
    const char *name;
    double elts[8];
    int turning;
    int attitude;
} craft;

static craft crafts[3];

typedef struct {
    int spacecraft;
    int id;
    char name[32];
    const char *shape;
    double ref, cross;
    double boresight[3], refvec[3], orientation[3];
} instrument;

static instrument insts[MAXINST];
static int ninst = 0;

static double star_ra, star_dec;

/* The rotation from J2000 to the frame of a spacecraft (the C-matrix) and its angular velocity in J2000 */
static void attitude(const craft *c, double et, double cmat[3][3], double av[3]) {
    if (c->turning) {
        double r[3][3];
        axisar_c(spin_axis, spin_rate * (et - t0), r);
        xpose_c(r, cmat);
        vscl_c(spin_rate, spin_axis, av);
    } else {
        ident_c(cmat);
        vpack_c(0.0, 0.0, 0.0, av);
    }
}

static void write_text(const char *dir, const char *file, const char *text) {
    char path[1024];
    snprintf(path, sizeof path, "%s/%s", dir, file);
    FILE *f = fopen(path, "w");
    fputs(text, f);
    fclose(f);
}

static void write_spk(const char *dir, const char *file, const craft *c) {
    char path[1024];
    snprintf(path, sizeof path, "%s/%s", dir, file);
    remove(path);
    int n = 0;
    for (double et = t0 - MARGIN; et <= t0 + SPAN + MARGIN + 30.0 && n < MAXSTATES; et += 60.0) {
        ets[n] = et;
        conics_c(c->elts, et, states[n]);
        n++;
    }
    SpiceInt handle;
    spkopn_c(path, "IO field-of-view reference", 0, &handle);
    spkw05_c(handle, c->id, 399, "J2000", ets[0], ets[n - 1], file, gm_earth, n, states, ets);
    spkcls_c(handle);
    furnsh_c(path);
}

/* The clock and the frame of a spacecraft, as the library's templates (ClockTemplate.tsc, FrameTemplate.tf) */
static void write_clock_and_frame(const char *dir, const char *stem, const craft *c) {
    char text[4096], file[256];
    int id = c->id;
    snprintf(text, sizeof text,
             "KPL/SCLK\n\\begindata\nSCLK_KERNEL_ID           = ( @1957-01-01/00:00:00.0 )\nSCLK_DATA_TYPE_%d        = ( 1 )\n"
             "SCLK01_TIME_SYSTEM_%d    = ( 1 )\nSCLK01_N_FIELDS_%d       = ( 2 )\nSCLK01_MODULI_%d         = ( 4294967296 65536 )\n"
             "SCLK01_OFFSETS_%d        = ( 0 0 )\nSCLK01_OUTPUT_DELIM_%d   = ( 2 )\nSCLK_PARTITION_START_%d  = ( 0.0000000000000E+00 )\n"
             "SCLK_PARTITION_END_%d    = ( 2.8147497671065E+14 )\n"
             "SCLK01_COEFFICIENTS_%d   = ( 0.0000000000000E+00     -1.3569552000000E+09     1.0000000000000E+00 )\n\\begintext\n",
             -id, -id, -id, -id, -id, -id, -id, -id, -id);
    snprintf(file, sizeof file, "%s.tsc", stem);
    write_text(dir, file, text);

    int frame = id * 1000;
    snprintf(text, sizeof text,
             "KPL/FK\n\\begindata\nFRAME_%s_FRAME   = %d\nFRAME_%d_NAME      = '%s_FRAME'\nFRAME_%d_CLASS     =  3\n"
             "FRAME_%d_CLASS_ID  = %d\nFRAME_%d_CENTER    = %d\nCK_%d_SCLK         = %d\nCK_%d_SPK          = %d\n"
             "OBJECT_%d_FRAME       = '%s_FRAME'\nNAIF_BODY_NAME              += '%s_FRAME'\nNAIF_BODY_CODE              += %d\n"
             "NAIF_BODY_NAME              += '%s'\nNAIF_BODY_CODE              += %d\n\\begintext\n",
             c->name, frame, frame, c->name, frame, frame, frame, frame, id, frame, id, frame, id, id, c->name, c->name, frame, c->name, id);
    snprintf(file, sizeof file, "%s.tf", stem);
    write_text(dir, file, text);
}

/* The frames and kernels of the instruments of a spacecraft, as the library's templates (InstrumentFrameTemplate.tf and
 * InstrumentKernel*Template.ti) */
static void write_instruments(const char *dir, const char *stem, const craft *c) {
    char text[2048], file[256], path[1024];
    snprintf(file, sizeof file, "%s_instruments.tf", stem);
    snprintf(path, sizeof path, "%s/%s", dir, file);
    FILE *fk = fopen(path, "w");
    snprintf(file, sizeof file, "%s_instruments.ti", stem);
    snprintf(path, sizeof path, "%s/%s", dir, file);
    FILE *ik = fopen(path, "w");
    for (int i = 0; i < ninst; i++) {
        const instrument *in = &insts[i];
        if (in->spacecraft != c->id) {
            continue;
        }
        snprintf(text, sizeof text,
                 "KPL/FK\n\\begindata\nFRAME_%s_%s             = %d\nFRAME_%d_NAME        = '%s_%s'\nFRAME_%d_CLASS       = 4\n"
                 "FRAME_%d_CLASS_ID    = %d\nFRAME_%d_CENTER      = %d\nTKFRAME_%d_SPEC      = 'ANGLES'\n"
                 "TKFRAME_%d_RELATIVE  = '%s_FRAME'\nTKFRAME_%d_ANGLES    = ( %.17g,%.17g,%.17g )\n"
                 "TKFRAME_%d_AXES      = ( 1,    2,   3   )\nTKFRAME_%d_UNITS     = 'RADIANS'\n"
                 "NAIF_BODY_NAME              += '%s_%s'\nNAIF_BODY_CODE              += %d\n\\begintext\n",
                 c->name, in->name, in->id, in->id, c->name, in->name, in->id, in->id, in->id, in->id, c->id, in->id, in->id, c->name, in->id,
                 in->orientation[0], in->orientation[1], in->orientation[2], in->id, in->id, c->name, in->name, in->id);
        fputs(text, fk);
        snprintf(text, sizeof text,
                 "KPL/IK\n\\begindata\nINS%d_FOV_CLASS_SPEC       = 'ANGLES'\nINS%d_FOV_SHAPE            = '%s'\n"
                 "INS%d_FOV_FRAME            = '%s_%s'\nINS%d_BORESIGHT            = ( %.17g, %.17g, %.17g )\n"
                 "INS%d_FOV_REF_VECTOR       = ( %.17g, %.17g, %.17g )\nINS%d_FOV_REF_ANGLE        = %.17g\n",
                 in->id, in->id, in->shape, in->id, c->name, in->name, in->id, in->boresight[0], in->boresight[1], in->boresight[2], in->id,
                 in->refvec[0], in->refvec[1], in->refvec[2], in->id, in->ref);
        fputs(text, ik);
        if (strcmp(in->shape, "CIRCLE") != 0) {
            fprintf(ik, "INS%d_FOV_CROSS_ANGLE      = %.17g\n", in->id, in->cross);
        }
        fprintf(ik, "INS%d_FOV_ANGLE_UNITS      = 'RADIANS'\n\\begintext\n", in->id);
    }
    fclose(fk);
    fclose(ik);
}

static void write_ck(const char *dir, const char *file, const craft *c) {
    char path[1024];
    snprintf(path, sizeof path, "%s/%s", dir, file);
    remove(path);
    int n = 0;
    for (double et = t0 - MARGIN; et <= t0 + SPAN + MARGIN + 5.0 && n < MAXQUATS; et += 10.0) {
        double cmat[3][3];
        attitude(c, et, cmat, avvs[n]);
        m2q_c(cmat, quats[n]);
        if (n > 0 && vdotg_c(quats[n], quats[n - 1], 4) < 0.0) {
            vminug_c(quats[n], 4, quats[n]);
        }
        sce2c_c(c->id, et, &sclk[n]);
        n++;
    }
    SpiceInt handle;
    ckopn_c(path, "IO field-of-view reference", 0, &handle);
    double starts[1] = {sclk[0]};
    ckw03_c(handle, sclk[0], sclk[n - 1], c->id * 1000, "J2000", SPICETRUE, file, n, sclk, quats, avvs, 1, starts);
    ckcls_c(handle);
    furnsh_c(path);
}

static void add(int spacecraft, int number, const char *name, const char *shape, double ref, double cross, const double b[3], const double r[3],
                const double o[3]) {
    instrument *in = &insts[ninst++];
    in->spacecraft = spacecraft;
    in->id = spacecraft * 1000 - number;
    snprintf(in->name, sizeof in->name, "%s", name);
    in->shape = shape;
    in->ref = ref;
    in->cross = cross;
    vequ_c(b, in->boresight);
    vequ_c(r, in->refvec);
    vequ_c(o, in->orientation);
}

/* The apparent direction of a body, a site or a spacecraft from FOVLEO at an epoch, in its turning frame, moved towards the
 * spin axis by an angle: the field then sees it pass that far from its boresight, across the reference plane */
static void direction_in_frame(const char *target, double et, double offset, double out[3]) {
    double pos[3], lt, dir[3], towards[3], cmat[3][3], av[3];
    spkpos_c(target, et, "J2000", "LT+S", "-470101", pos, &lt);
    vhat_c(pos, dir);
    vperp_c(spin_axis, dir, towards);
    vhat_c(towards, towards);
    vlcom_c(cos(offset), dir, sin(offset), towards, dir);
    attitude(&crafts[0], et, cmat, av);
    mxv_c(cmat, dir, out);
}

static int first_entry;

static void key_open(const char *name) {
    printf(",\n  \"%s\": {", name);
    first_entry = 1;
}

static void key_close(void) {
    printf("\n  }");
}

static void print_window_list(SpiceCell *result) {
    SpiceInt count = wncard_c(result);
    printf("[");
    for (SpiceInt i = 0; i < count; i++) {
        double a, b;
        wnfetd_c(result, i, &a, &b);
        printf("%s[%.17g, %.17g]", i == 0 ? "" : ", ", a, b);
    }
    printf("]");
}

static void gftfov(const instrument *in, const char *target, const char *tframe, const char *abcorr) {
    SPICEDOUBLE_CELL(cnfine, 2);
    SPICEDOUBLE_CELL(result, 4000);
    char inst[32], obs[32];
    snprintf(inst, sizeof inst, "%d", in->id);
    snprintf(obs, sizeof obs, "%d", in->spacecraft);
    scard_c(0, &cnfine);
    scard_c(0, &result);
    wninsd_c(t0, t0 + SPAN, &cnfine);
    int point = tframe == NULL;
    gftfov_c(inst, target, point ? "POINT" : "ELLIPSOID", point ? " " : tframe, abcorr, obs, REFSTEP, &cnfine, &result);
    printf("%s\n    \"%d %s %s %s\": ", first_entry ? "" : ",", in->id, target, point ? "POINT" : tframe, abcorr);
    first_entry = 0;
    print_window_list(&result);
}

static void gfrfov(const instrument *in, const char *star, const double ray[3], const char *abcorr) {
    SPICEDOUBLE_CELL(cnfine, 2);
    SPICEDOUBLE_CELL(result, 4000);
    char inst[32], obs[32];
    snprintf(inst, sizeof inst, "%d", in->id);
    snprintf(obs, sizeof obs, "%d", in->spacecraft);
    scard_c(0, &cnfine);
    scard_c(0, &result);
    wninsd_c(t0, t0 + SPAN, &cnfine);
    gfrfov_c(inst, ray, "J2000", abcorr, obs, REFSTEP, &cnfine, &result);
    printf("%s\n    \"%d %s %s\": ", first_entry ? "" : ",", in->id, star, abcorr);
    first_entry = 0;
    print_window_list(&result);
}

/* A grid of fovtrg or fovray: the whole search window every 30 s, or, for the narrow fields of the Moon, the Sun and the
 * star, every second over the 200 s about the epoch their boresight was built on */
static double grid_first(const instrument *in) {
    int number = in->spacecraft * 1000 - in->id;
    return number < 20 || in->spacecraft != -470101 ? t0 : t0 + (number < 30 ? 2000.0 : number < 40 ? 6000.0 : 4000.0) - 100.0;
}

static double grid_step(const instrument *in) {
    return grid_first(in) == t0 ? GRIDSTEP : 1.0;
}

static int grid_count(const instrument *in) {
    return grid_first(in) == t0 ? (int)(SPAN / GRIDSTEP) + 1 : 201;
}

static void grid_open(const instrument *in, const char *target, const char *abcorr) {
    printf("%s\n    \"%d %s %s\": {\"first\": %.17g, \"step\": %.17g, \"inView\": \"", first_entry ? "" : ",", in->id, target, abcorr,
           grid_first(in), grid_step(in));
    first_entry = 0;
}

static void fovtrg(const instrument *in, const char *target, const char *abcorr) {
    char inst[32], obs[32];
    snprintf(inst, sizeof inst, "%d", in->id);
    snprintf(obs, sizeof obs, "%d", in->spacecraft);
    grid_open(in, target, abcorr);
    for (int k = 0; k < grid_count(in); k++) {
        double et = grid_first(in) + k * grid_step(in);
        SpiceBoolean visible;
        double e = et;
        fovtrg_c(inst, target, "POINT", " ", abcorr, obs, &e, &visible);
        putchar(visible ? '1' : '0');
    }
    printf("\"}");
}

static void fovray(const instrument *in, const char *star, const double ray[3], const char *abcorr) {
    char inst[32], obs[32];
    snprintf(inst, sizeof inst, "%d", in->id);
    snprintf(obs, sizeof obs, "%d", in->spacecraft);
    grid_open(in, star, abcorr);
    for (int k = 0; k < grid_count(in); k++) {
        double et = grid_first(in) + k * grid_step(in);
        SpiceBoolean visible;
        double e = et;
        fovray_c(inst, ray, "J2000", abcorr, obs, &e, &visible);
        putchar(visible ? '1' : '0');
    }
    printf("\"}");
}

/* The rectangle of FOVPOLAR against the Earth's ellipsoid, which gftfov cannot search (SPICE(INVALIDELLIPSE), above),
 * searched by gfudb on a condition built from surfpt alone: a convex pyramid and a convex ellipsoid in front of the
 * observer meet when a ray of a face of the pyramid meets the ellipsoid, or when the ellipsoid lies inside the pyramid,
 * its center then inside. Each face is scanned by SCANRAYS rays between its corners (getfov), so that a contact on a
 * face is missed by the sagitta of the limb between two rays, (d / 2)^2 / (2 R), d the angle between two rays and R the
 * angular radius of curvature of the limb: about 1e-11 rad at 1e5 rays, 1e-8 s at the limb's motion of 1e-3 rad/s; a
 * contact at a corner is on a ray. The target is read as gftfov reads it: its center by spkezp with the correction, its
 * ellipsoid oriented by the body-fixed frame at the light-time corrected epoch */
#define SCANRAYS 100000

static SpiceInt scan_id;
static double scan_bounds[4][3], scan_boresight[3], scan_normals[4][3], scan_radius;
static char scan_frame[64];
static const char *scan_abcorr;
static double scan_radii[3];

static void scan_value(SpiceDouble et, SpiceDouble *value) {
    (void)et;
    *value = 0.0;
}

static void scan_in_view(void (*udfuns)(SpiceDouble et, SpiceDouble *value), SpiceDouble et, SpiceBoolean *in_view) {
    (void)udfuns;
    double pos[3], lt, to_body[3][3], to_j2000[3][3], obs[3], dir[3];
    spkezp_c(399, et, "J2000", scan_abcorr, scan_id / 1000, pos, &lt);
    pxform_c("J2000", "ITRF93", et - lt, to_body);
    pxform_c(scan_frame, "J2000", et, to_j2000);
    /* The center in the instrument frame; far from the field, no ray meets the ellipsoid */
    mtxv_c(to_j2000, pos, dir);
    double reach = scan_radius + asin(fmin(1.0, scan_radii[0] / vnorm_c(pos))) + 1e-3;
    *in_view = SPICEFALSE;
    if (vsep_c(dir, scan_boresight) > reach) {
        return;
    }

    int inside = 1;
    for (int i = 0; i < 4; i++) {
        inside = inside && vdot_c(scan_normals[i], dir) >= 0.0;
    }

    if (inside) {
        *in_view = SPICETRUE;
        return;
    }

    /* The observer from the center, and the rays, in the body-fixed frame */
    double rotation[3][3];
    mxv_c(to_body, pos, obs);
    vminus_c(obs, obs);
    mxm_c(to_body, to_j2000, rotation);
    for (int i = 0; i < 4; i++) {
        double a[3], b[3];
        mxv_c(rotation, scan_bounds[i], a);
        mxv_c(rotation, scan_bounds[(i + 1) % 4], b);
        for (int k = 0; k < SCANRAYS; k++) {
            double ray[3], point[3];
            SpiceBoolean found;
            double f = (double)k / SCANRAYS;
            vlcom_c(1.0 - f, a, f, b, ray);
            surfpt_c(obs, ray, scan_radii[0], scan_radii[1], scan_radii[2], point, &found);
            if (found) {
                *in_view = SPICETRUE;
                return;
            }
        }
    }
}

static void rayscan(const instrument *in, const char *abcorr) {
    SPICEDOUBLE_CELL(cnfine, 2);
    SPICEDOUBLE_CELL(result, 4000);
    char shape[32];
    SpiceInt n, dim;
    scan_id = in->id;
    scan_abcorr = abcorr;
    getfov_c(in->id, 4, sizeof shape, sizeof scan_frame, shape, scan_frame, scan_boresight, &n, scan_bounds);
    bodvrd_c("EARTH", "RADII", 3, &dim, scan_radii);
    scan_radius = 0.0;
    for (int i = 0; i < 4; i++) {
        scan_radius = fmax(scan_radius, vsep_c(scan_bounds[i], scan_boresight));
        vcrss_c(scan_bounds[i], scan_bounds[(i + 1) % 4], scan_normals[i]);
        if (vdot_c(scan_normals[i], scan_boresight) < 0.0) {
            vminus_c(scan_normals[i], scan_normals[i]);
        }
    }

    scard_c(0, &cnfine);
    scard_c(0, &result);
    wninsd_c(t0, t0 + SPAN, &cnfine);
    gfudb_c(scan_value, scan_in_view, 10.0, &cnfine, &result);
    printf("%s\n    \"%d EARTH ITRF93 %s\": ", first_entry ? "" : ",", in->id, abcorr);
    first_entry = 0;
    print_window_list(&result);
}

static const instrument *find(const char *name) {
    for (int i = 0; i < ninst; i++) {
        if (strcmp(insts[i].name, name) == 0) {
            return &insts[i];
        }
    }
    return NULL;
}

int main(int argc, char **argv) {
    if (argc != 3) {
        fprintf(stderr, "usage: %s <kernel directory> <output directory>\n", argv[0]);
        return 1;
    }

    const char *kernels[] = {"SolarSystem/latest_leapseconds.tls", "SolarSystem/de440s.bsp", "SolarSystem/pck00011.tpc",
                             "SolarSystem/gm_de440.tpc", "SolarSystem/earth_latest_high_prec.bpc", "SolarSystem/earthstns_itrf93_201023.bsp",
                             "SolarSystem/earth_fixed.tf", "SolarSystem/moon_080317.tf", "SolarSystem/moon_pa_de421_1900-2050.bpc",
                             "SolarSystem/L1_de431.bsp"};
    char path[1024];
    for (int i = 0; i < 10; i++) {
        snprintf(path, sizeof path, "%s/%s", argv[1], kernels[i]);
        furnsh_c(path);
    }

    SpiceInt dim;
    bodvrd_c("EARTH", "GM", 1, &dim, &gm_earth);
    str2et_c("2026-04-01 00:00:00 TDB", &t0);
    vpack_c(sin(20.0 * rpd_c()), 0.0, cos(20.0 * rpd_c()), spin_axis);

    craft leo = {-470101, "FOVLEO", {6800.0 * (1.0 - 0.001), 0.001, 51.6 * rpd_c(), 30.0 * rpd_c(), 0.0, 0.0, t0, gm_earth}, 1, 1};
    craft target = {-470102, "FOVTARGET", {7200.0 * (1.0 - 0.002), 0.002, 45.0 * rpd_c(), 60.0 * rpd_c(), 0.0, 0.0, t0, gm_earth}, 0, 0};
    craft polar = {-470103, "FOVPOLAR", {7000.0, 0.0, 90.0 * rpd_c(), 0.0, 0.0, 0.0, t0, gm_earth}, 0, 1};
    crafts[0] = leo;
    crafts[1] = target;
    crafts[2] = polar;

    const char *dir = argv[2];
    write_spk(dir, "fovleo.bsp", &crafts[0]);
    write_spk(dir, "fovtarget.bsp", &crafts[1]);
    write_spk(dir, "fovpolar.bsp", &crafts[2]);

    /* The instruments of FOVLEO. The Earth: boresights fixed in the turning frame */
    double zero[3] = {0.0, 0.0, 0.0}, x[3] = {1.0, 0.0, 0.0}, y[3] = {0.0, 1.0, 0.0}, z[3] = {0.0, 0.0, 1.0};
    double turned[3] = {0.0, sin(30.0 * rpd_c()), cos(30.0 * rpd_c())}, oblique[3] = {0.5, 0.3, 1.0};
    double about_y[3] = {0.0, halfpi_c(), 0.0};
    int s = -470101;
    add(s, 1, "CIRCLE1", "CIRCLE", 0.4, 0.4, x, z, zero);
    add(s, 2, "ELLIPSE2", "ELLIPSE", 0.4, 0.15, x, z, zero);           /* REF > CROSS */
    add(s, 3, "ELLIPSE3", "ELLIPSE", 0.15, 0.4, x, z, zero);           /* REF < CROSS */
    add(s, 4, "ELLIPSE4", "ELLIPSE", 0.3, 0.003, x, z, zero);          /* an aspect ratio of 100 */
    add(s, 5, "RECTANGLE5", "RECTANGLE", 0.4, 0.15, x, z, zero);
    add(s, 6, "RECTANGLE6", "RECTANGLE", 0.15, 0.4, x, z, zero);
    add(s, 7, "RECTANGLE7", "RECTANGLE", 0.4, 0.15, x, turned, zero);  /* turned 30 deg about the boresight */
    add(s, 8, "CIRCLE8", "CIRCLE", 0.05, 0.05, x, z, zero);            /* narrower than the Earth */
    add(s, 9, "CIRCLE9", "CIRCLE", 1.4, 1.4, x, z, zero);              /* the Earth inside the field */
    add(s, 10, "ELLIPSE10", "ELLIPSE", 0.4, 0.15, x, oblique, zero);   /* reference vector not perpendicular */
    add(s, 11, "CIRCLE11", "CIRCLE", 0.4, 0.4, z, x, about_y);         /* boresight Z, the frame turned about Y */

    /* The Moon, the Sun and the star: boresights on their apparent directions */
    double b[3];
    direction_in_frame("MOON", t0 + 2000.0, 0.003, b);
    add(s, 21, "CIRCLE21", "CIRCLE", 0.01, 0.01, b, z, zero);
    add(s, 22, "ELLIPSE22", "ELLIPSE", 0.012, 0.004, b, z, zero);
    double moon_ref[3] = {0.3, 0.0, 1.0};
    add(s, 23, "RECTANGLE23", "RECTANGLE", 0.012, 0.004, b, moon_ref, zero);
    add(s, 24, "CIRCLE24", "CIRCLE", 0.002, 0.002, b, z, zero);        /* narrower than the Moon */
    add(s, 25, "CIRCLE25", "CIRCLE", 0.05, 0.05, b, z, zero);          /* the Moon inside the field */
    direction_in_frame("SUN", t0 + 6000.0, 0.001, b);
    add(s, 31, "CIRCLE31", "CIRCLE", 0.002, 0.002, b, z, zero);        /* narrower than the Sun */
    add(s, 32, "CIRCLE32", "CIRCLE", 0.01, 0.01, b, z, zero);
    add(s, 33, "ELLIPSE33", "ELLIPSE", 0.01, 0.003, b, z, zero);
    add(s, 34, "RECTANGLE34", "RECTANGLE", 0.01, 0.003, b, moon_ref, zero);
    add(s, 35, "CIRCLE35", "CIRCLE", 0.05, 0.05, b, z, zero);          /* the Sun inside; and the Lagrange point L1 */
    add(s, 41, "STAR41", "CIRCLE", 0.005, 0.005, y, z, zero);
    add(s, 42, "RECTANGLE42", "RECTANGLE", 0.005, 0.002, y, z, zero);

    /* An ellipse and a rectangle on the apparent direction of DSS-13, and two on that of FOVTARGET, which the fields of
     * the Earth do not cross: the other shapes against a site and a spacecraft */
    direction_in_frame("399013", t0 + 3000.0, 0.05, b);
    add(s, 12, "ELLIPSE12", "ELLIPSE", 0.3, 0.1, b, z, zero);
    add(s, 13, "RECTANGLE13", "RECTANGLE", 0.3, 0.1, b, turned, zero);
    direction_in_frame("-470102", t0 + 5000.0, 0.05, b);
    add(s, 14, "ELLIPSE14", "ELLIPSE", 0.3, 0.1, b, z, zero);
    add(s, 15, "RECTANGLE15", "RECTANGLE", 0.3, 0.1, b, turned, zero);

    /* The star: the direction of STAR41's boresight at T0 + 4000 s, 0.002 rad towards the spin axis */
    double cmat[3][3], av[3], star[3], towards[3], radius;
    attitude(&crafts[0], t0 + 4000.0, cmat, av);
    mtxv_c(cmat, y, star);
    vperp_c(spin_axis, star, towards);
    vhat_c(towards, towards);
    vlcom_c(cos(0.002), star, sin(0.002), towards, star);
    recrad_c(star, &radius, &star_ra, &star_dec);
    double ray[3];
    radrec_c(1.0, star_ra, star_dec, ray);

    /* FOVPOLAR: boresights along J2000's Z, the Earth's limb near its pole */
    int p = -470103;
    add(p, 1, "CIRCLE1", "CIRCLE", 0.5, 0.5, z, x, zero);
    add(p, 2, "ELLIPSE2", "ELLIPSE", 0.5, 0.2, z, y, zero);
    add(p, 3, "RECTANGLE3", "RECTANGLE", 0.5, 0.2, z, y, zero);

    write_clock_and_frame(dir, "fovleo", &crafts[0]);
    write_clock_and_frame(dir, "fovpolar", &crafts[2]);
    write_instruments(dir, "fovleo", &crafts[0]);
    write_instruments(dir, "fovpolar", &crafts[2]);
    const char *texts[] = {"fovleo.tsc", "fovleo.tf", "fovleo_instruments.tf", "fovleo_instruments.ti",
                           "fovpolar.tsc", "fovpolar.tf", "fovpolar_instruments.tf", "fovpolar_instruments.ti"};
    for (int i = 0; i < 8; i++) {
        snprintf(path, sizeof path, "%s/%s", dir, texts[i]);
        furnsh_c(path);
    }
    write_ck(dir, "fovleo.bc", &crafts[0]);
    write_ck(dir, "fovpolar.bc", &crafts[2]);

    /* The header: epochs, spacecraft, instruments, star */
    printf("{\n  \"epoch\": %.17g,\n  \"window\": [%.17g, %.17g],\n", t0, t0, t0 + SPAN);
    printf("  \"spinAxis\": [%.17g, %.17g, %.17g],\n  \"spinRate\": %.17g,\n", spin_axis[0], spin_axis[1], spin_axis[2], spin_rate);
    printf("  \"spacecraft\": [");
    for (int i = 0; i < 3; i++) {
        printf("%s\n    {\"id\": %d, \"name\": \"%s\"}", i == 0 ? "" : ",", crafts[i].id, crafts[i].name);
    }
    printf("\n  ],\n  \"star\": {\"name\": \"FOVSTAR1\", \"rightAscension\": %.17g, \"declination\": %.17g},\n", star_ra, star_dec);
    printf("  \"instruments\": [");
    for (int i = 0; i < ninst; i++) {
        const instrument *in = &insts[i];
        printf("%s\n    {\"spacecraft\": %d, \"id\": %d, \"name\": \"%s\", \"shape\": \"%s\", \"refAngle\": %.17g, \"crossAngle\": %.17g, "
               "\"boresight\": [%.17g, %.17g, %.17g], \"refVector\": [%.17g, %.17g, %.17g], \"orientation\": [%.17g, %.17g, %.17g]}",
               i == 0 ? "" : ",", in->spacecraft, in->id, in->name, in->shape, in->ref, in->cross, in->boresight[0], in->boresight[1],
               in->boresight[2], in->refvec[0], in->refvec[1], in->refvec[2], in->orientation[0], in->orientation[1], in->orientation[2]);
    }
    printf("\n  ]");

    /* gftfov at 1e-9 s */
    gfstol_c(1e-9);
    const char *three[] = {"NONE", "LT", "LT+S"};
    const char *others[] = {"CN", "CN+S", "XLT", "XLT+S", "XCN", "XCN+S"};
    key_open("gftfov");
    for (int i = 0; i < ninst; i++) {
        const instrument *in = &insts[i];
        int number = in->spacecraft * 1000 - in->id;
        if (strncmp(in->name, "STAR", 4) == 0 || number == 42) {
            continue;
        }
        const char *body = number < 20 ? "EARTH" : number < 30 ? "MOON" : "SUN";
        const char *frame = number < 20 ? "ITRF93" : number < 30 ? "MOON_ME" : "IAU_SUN";
        for (int a = 0; a < 3; a++) {
            gftfov(in, body, NULL, three[a]);
            /* Seen from above the pole, the limb of the Earth is almost a circle, whose semi-axes edlimb returns up to
             * 1.3e-9 rad from orthogonal: gftfov stops on SPICE(INVALIDELLIPSE) in the INELPL of the rectangle's zzelvupy
             * (N0067), whatever the rectangle's orientation. FOVPOLAR's rectangle has no ELLIPSOID reference */
            if (!(in->spacecraft == -470103 && strcmp(in->shape, "RECTANGLE") == 0)) {
                gftfov(in, body, frame, three[a]);
            }
        }
    }
    for (int a = 0; a < 6; a++) {
        gftfov(find("CIRCLE1"), "EARTH", NULL, others[a]);
        gftfov(find("CIRCLE1"), "EARTH", "ITRF93", others[a]);
        gftfov(find("RECTANGLE5"), "EARTH", "ITRF93", others[a]);
        gftfov(find("CIRCLE21"), "MOON", "MOON_ME", others[a]);
        gftfov(find("ELLIPSE33"), "SUN", NULL, others[a]);
        gftfov(find("CIRCLE1"), "399013", NULL, others[a]);
        gftfov(find("CIRCLE11"), "-470102", NULL, others[a]);
    }
    for (int a = 0; a < 3; a++) {
        gftfov(find("CIRCLE1"), "EARTH", "IAU_EARTH", three[a]);
    }
    const char *point_fields[] = {"CIRCLE1", "ELLIPSE2", "RECTANGLE5", "RECTANGLE7", "ELLIPSE10", "CIRCLE11"};
    for (int i = 0; i < 6; i++) {
        for (int a = 0; a < 3; a++) {
            gftfov(find(point_fields[i]), "399013", NULL, three[a]);
            gftfov(find(point_fields[i]), "-470102", NULL, three[a]);
            gftfov(find(point_fields[i]), "3", NULL, three[a]);
        }
    }
    for (int a = 0; a < 3; a++) {
        gftfov(find("CIRCLE35"), "391", NULL, three[a]);
    }
    for (int a = 0; a < 3; a++) {
        gftfov(find("ELLIPSE12"), "399013", NULL, three[a]);
        gftfov(find("RECTANGLE13"), "399013", NULL, three[a]);
        gftfov(find("ELLIPSE14"), "-470102", NULL, three[a]);
        gftfov(find("RECTANGLE15"), "-470102", NULL, three[a]);
    }
    key_close();

    /* gfrfov for the star */
    const char *ray_corrections[] = {"NONE", "S", "XS"};
    key_open("gfrfov");
    for (int a = 0; a < 3; a++) {
        gfrfov(find("STAR41"), "FOVSTAR1", ray, ray_corrections[a]);
        gfrfov(find("RECTANGLE42"), "FOVSTAR1", ray, ray_corrections[a]);
    }
    key_close();

    /* fovtrg POINT on the grid, every instrument of FOVLEO against its body, DSS-13 and FOVTARGET; FOVPOLAR's against the
     * Earth */
    key_open("fovtrg");
    for (int i = 0; i < ninst; i++) {
        const instrument *in = &insts[i];
        int number = in->spacecraft * 1000 - in->id;
        if (strncmp(in->name, "STAR", 4) == 0 || number == 42) {
            continue;
        }
        const char *body = number < 20 ? "EARTH" : number < 30 ? "MOON" : "SUN";
        for (int a = 0; a < 3; a++) {
            fovtrg(in, body, three[a]);
            if (in->spacecraft == s && number < 20) {
                fovtrg(in, "399013", three[a]);
                fovtrg(in, "-470102", three[a]);
            }
        }
    }
    key_close();
    /* The ray scan of FOVPOLAR's rectangle against the Earth's ellipsoid, at gftfov's tolerance (gfstol, above) */
    key_open("rayscan");
    for (int a = 0; a < 3; a++) {
        rayscan(&insts[ninst - 1], three[a]);
    }
    key_close();
    key_open("fovray");
    for (int a = 0; a < 3; a++) {
        fovray(find("STAR41"), "FOVSTAR1", ray, ray_corrections[a]);
        fovray(find("RECTANGLE42"), "FOVSTAR1", ray, ray_corrections[a]);
    }
    key_close();
    printf("\n}\n");
    return 0;
}
