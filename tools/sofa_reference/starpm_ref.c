/*
 * The reference of the space motion of a star (IO.Astrodynamics.Body.Star, #718).
 *
 * For each star and each horizon dt (Julian years of TDB from the catalog epoch), it prints one C# row of
 * StarTests.StarpmCases:
 *   - the inputs, as the doubles the test gives the library: right ascension and declination (rad), the proper motion in
 *     right ascension mu_alpha* = (dalpha/dt) cos(delta), a true angle, and in declination (rad per Julian year), the
 *     parallax (arcsec) and the catalog epoch (TDB days from J2000);
 *   - SOFA's iauStarpm (release 2023-10-11, external-lib/sofa-src/starpm.c), the reference: fed dalpha/dt =
 *     mu_alpha* / cos(delta) (its Note 4: the RA proper motion in coordinate angle), rv = 0, TDB dates (its Note 1); its
 *     direction (the unit vector of its right ascension and declination, evaluated in long double) and the ratio of the
 *     catalog parallax to its parallax at dt, the distance in units of the catalog distance. NaN where iauStarpm cannot be
 *     fed (a declination of +-pi/2, where dalpha/dt is undefined, status -9) or where it does not move the star (status
 *     2: a space velocity above its 0.5c, the synthetic stars of the test suite), its status printed;
 *   - the closed form of the straight line at constant speed (iauStarpm's Note 5, rv = 0), evaluated in long double (x87
 *     80-bit): with x = mu_alpha* dt and y = mu_delta dt, the position in units of the catalog distance is
 *     u0 + x e_alpha + y e_delta, e_alpha = (-sin a0, cos a0, 0) and e_delta = (-sin d0 cos a0, -sin d0 sin a0, cos d0):
 *     its unit vector and its length sqrt(1 + x^2 + y^2).
 *
 * The rows of StarTests are printed by this program, never edited by hand. It builds as every harness of this directory
 * (README.md), from the repository root, and takes no argument.
 *
 * The catalog values (VizieR, read on 2026-11-16, #718; to be verified by S. Guillet): Barnard's star from the Hipparcos
 * new reduction I/311 (J1991.25) and from Gaia DR3 4472832130942575872 (J2016.0), Polaris from I/311; the synthetic stars
 * of the issue: Barnard's I/311 motion and parallax at +89.5 deg and, mirrored, at -89.5 deg, both crossing a pole before
 * 1000 years; stars at +-pi/2 exactly; the synthetic stars of the test suite (1 rad and 0.1 rad/yr, 1.5 rad and 0.1 and
 * 0.05 rad/yr).
 */
#include <math.h>
#include <stdio.h>
#include "sofa.h"
#include "sofam.h"

/* The conversions of the test, in the same order of operations, so that the doubles are the same */
#define DEG (M_PI / 180.0)
#define MAS (M_PI / 180.0 / 3600.0 / 1000.0)

typedef struct
{
    const char *label;
    double ra, dec, pmras, pmdec, px, epochDays;
} star_t;

/* A double as a C# literal: %.17g reads back as the same double, NaN as double.NaN */
static const char *literal(double value, char *buffer)
{
    if (isnan(value))
    {
        return "double.NaN";
    }

    sprintf(buffer, "%.17g", value);
    return buffer;
}

static void row(const star_t *s, double years)
{
    double ra2 = NAN, dec2 = NAN, pmr2, pmd2, px2 = NAN, rv2;
    int status = -9;
    long double us[3] = {NAN, NAN, NAN};
    long double ratio = NAN;
    if (fabs(s->dec) < M_PI / 2.0)
    {
        status = iauStarpm(s->ra, s->dec, s->pmras / cos(s->dec), s->pmdec, s->px, 0.0, DJ00, s->epochDays, DJ00,
                           s->epochDays + years * DJY, &ra2, &dec2, &pmr2, &pmd2, &px2, &rv2);
        if (status == 0)
        {
            us[0] = cosl(dec2) * cosl(ra2);
            us[1] = cosl(dec2) * sinl(ra2);
            us[2] = sinl(dec2);
            ratio = (long double)s->px / px2;
        }
    }

    long double x = (long double)s->pmras * years, y = (long double)s->pmdec * years;
    long double ca = cosl(s->ra), sa = sinl(s->ra), cd = cosl(s->dec), sd = sinl(s->dec);
    long double e = cd - y * sd;
    long double p[3] = {ca * e - x * sa, sa * e + x * ca, sd + y * cd};
    long double n = sqrtl(p[0] * p[0] + p[1] * p[1] + p[2] * p[2]);
    long double length = sqrtl(1.0L + x * x + y * y);

    printf("        new(\"%s\", %.17g, %.17g, %.17g, %.17g, %.17g, %.17g, %.1f, %d,\n", s->label, s->ra, s->dec, s->pmras,
           s->pmdec, s->px, s->epochDays, years, status);
    char b0[32], b1[32], b2[32], b3[32];
    printf("            new Vector3(%s, %s, %s), %s,\n", literal((double)us[0], b0), literal((double)us[1], b1),
           literal((double)us[2], b2), literal((double)ratio, b3));
    printf("            new Vector3(%.17g, %.17g, %.17g), %.17g),\n", (double)(p[0] / n), (double)(p[1] / n),
           (double)(p[2] / n), (double)length);
}

int main(void)
{
    const double j1991 = -3195.9375, j2016 = 5844.0, j2001 = 366.5;
    const star_t stars[] = {
        {"Barnard I/311", 269.45402263 * DEG, 4.66828781 * DEG, -798.58 * MAS, 10328.12 * MAS, 548.31 / 1000.0, j1991},
        {"Barnard DR3", 269.44850252544 * DEG, 4.73942005111 * DEG, -801.551 * MAS, 10362.394 * MAS, 546.9759 / 1000.0,
         j2016},
        {"Polaris I/311", 37.946143 * DEG, 89.26413778 * DEG, 44.48 * MAS, -11.85 * MAS, 7.54 / 1000.0, j1991},
        {"Barnard I/311 at +89.5 deg", 269.45402263 * DEG, 89.5 * DEG, -798.58 * MAS, 10328.12 * MAS, 548.31 / 1000.0,
         j1991},
        {"Barnard I/311 at -89.5 deg", 269.45402263 * DEG, -89.5 * DEG, -798.58 * MAS, -10328.12 * MAS,
         548.31 / 1000.0, j1991},
        {"North pole", 1.0, M_PI / 2.0, 1.0e-5, 2.0e-5, 0.3792, j2001},
        {"South pole", 1.0, -M_PI / 2.0, 1.0e-5, -2.0e-5, 0.3792, j2001},
        {"1 rad, 0.1 rad/yr", 1.0, 1.0, 0.1, 0.1, 0.3792, j2001},
        {"1.5 rad, 0.1 and 0.05 rad/yr", 1.0, 1.5, 0.05, 0.1, 0.3792, j2001},
        {"-1.5 rad, -0.1 and 0.05 rad/yr", 1.0, -1.5, 0.05, -0.1, 0.3792, j2001},
    };
    const double horizons[] = {1.0, 10.0, 100.0, 1000.0};
    for (size_t i = 0; i < sizeof stars / sizeof stars[0]; i++)
    {
        for (size_t k = 0; k < sizeof horizons / sizeof horizons[0]; k++)
        {
            row(&stars[i], horizons[k]);
        }
    }

    return 0;
}
