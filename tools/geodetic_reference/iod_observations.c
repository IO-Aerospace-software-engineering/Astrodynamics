/*
 * Reference observations for InitialOrbitDeterminationTests.FromTheExactSite (#411, review H3 N3): a reference orbit
 * seen from the site of the University of Maryland (geodetic longitude -76.95667 deg, latitude 39.00167 deg, altitude
 * 53 m, the site of the COBE observations of the test) at three epochs three minutes apart. The site is placed by
 * georec_c on the ellipsoid of pck00011 and turned to J2000 by pxform_c from ITRF93 (earth_latest_high_prec.bpc);
 * the object by prop2b_c (two-body, GM of gm_de440.tpc) from its state at the middle epoch. The direction is
 * geometric, since the method of Gauss takes the object and the site at the same epoch. Prints the site positions and
 * the right ascension, declination and range of each observation (J2000, m and rad), then the outer observations at
 * 90, 45 and 22.5 s from the middle epoch, for the order of the method. Run from the repository root; see
 * README.md.
 */
#include <math.h>
#include <stdio.h>
#include "SpiceUsr.h"

/* The site position and the geometric right ascension, declination and range of the object at et */
static void Observe(double gm, const SpiceDouble reference[6], SpiceDouble siteFixed[3], SpiceDouble etMiddle,
                    SpiceDouble et)
{
    SpiceDouble state[6], rotation[3][3], site[3], rho[3], range, ra, dec;
    prop2b_c(gm, reference, et - etMiddle, state);
    pxform_c("ITRF93", "J2000", et, rotation);
    mxv_c(rotation, siteFixed, site);
    vsub_c(state, site, rho);
    recrad_c(rho, &range, &ra, &dec);
    printf("%+.17g s from the middle epoch: site [%.17g, %.17g, %.17g] m; right ascension %.17g rad, declination "
           "%.17g rad, range %.17g m\n",
           et - etMiddle, site[0] * 1000.0, site[1] * 1000.0, site[2] * 1000.0, ra, dec, range * 1000.0);
}

int main(void)
{
    const char *data = "IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/SolarSystem/";
    const char *kernels[] = {"latest_leapseconds.tls", "pck00011.tpc", "gm_de440.tpc", "earth_latest_high_prec.bpc",
                             "earth_fixed.tf", "earth_assoc_itrf93.tf"};
    char path[512];
    for (size_t i = 0; i < sizeof(kernels) / sizeof(kernels[0]); i++)
    {
        snprintf(path, sizeof(path), "%s%s", data, kernels[i]);
        furnsh_c(path);
    }

    SpiceDouble radii[3], gm;
    SpiceInt n;
    bodvrd_c("EARTH", "RADII", 3, &n, radii);
    bodvrd_c("EARTH", "GM", 1, &n, &gm);
    double f = (radii[0] - radii[2]) / radii[0];
    SpiceDouble siteFixed[3];
    georec_c(-76.95667 * rpd_c(), 39.00167 * rpd_c(), 0.053, radii[0], f, siteFixed);

    /* The reference orbit at the middle epoch, J2000, km and km/s */
    const SpiceDouble reference[6] = {3520.0, -4310.0, 4645.0, -4.026, 2.616, 5.468};
    const char *epochs[] = {"2000-11-06 22:31:29 UTC", "2000-11-06 22:34:30 UTC", "2000-11-06 22:37:30 UTC"};
    SpiceDouble etMiddle;
    str2et_c(epochs[1], &etMiddle);
    printf("GM %.17g km3/s2, reference state at %s: [%.17g, %.17g, %.17g] km, [%.17g, %.17g, %.17g] km/s\n", gm,
           epochs[1], reference[0], reference[1], reference[2], reference[3], reference[4], reference[5]);
    for (size_t i = 0; i < 3; i++)
    {
        SpiceDouble et;
        str2et_c(epochs[i], &et);
        Observe(gm, reference, siteFixed, etMiddle, et);
    }

    /* The convergence of the method: the outer observations at 90, 45 and 22.5 s from the middle epoch */
    const double spacings[] = {90.0, 45.0, 22.5};
    for (size_t k = 0; k < sizeof(spacings) / sizeof(spacings[0]); k++)
    {
        Observe(gm, reference, siteFixed, etMiddle, etMiddle - spacings[k]);
        Observe(gm, reference, siteFixed, etMiddle, etMiddle + spacings[k]);
    }

    return 0;
}
