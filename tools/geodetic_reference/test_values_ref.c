/*
 * Reference values of the unit tests of the geodetic conversions (#411), on the ellipsoid of the Earth of pck00011:
 * - PlanetocentricTests.ToPlanetodetic: the geodetic coordinates (recgeo_c) of a planetocentric point near DSS-13;
 * - PlanetodeticTests.ToPlanetocentric: the planetocentric coordinates (georec_c, reclat_c) of a geodetic point;
 * - the CLI test EphemerisTests.SubPointPlanetodetic: the geodetic coordinates of the sub-Moon point on the Earth at
 *   J2000 TDB (et = 0), the geometric position of the Moon relative to the Earth in the Earth-fixed frame, for the two
 *   Earth-fixed frames of the kernels, ITRF93 and IAU_EARTH.
 * Run from the repository root; see README.md.
 */
#include <stdio.h>
#include "SpiceUsr.h"

int main(void)
{
    const char *data = "IO.Astrodynamics.Net/IO.Astrodynamics.CLI.Tests/Data/SolarSystem/";
    const char *kernels[] = {"latest_leapseconds.tls", "pck00011.tpc", "de440s.bsp", "earth_latest_high_prec.bpc",
                             "earth_fixed.tf", "earth_assoc_itrf93.tf"};
    char path[512];
    for (size_t i = 0; i < sizeof(kernels) / sizeof(kernels[0]); i++)
    {
        snprintf(path, sizeof(path), "%s%s", data, kernels[i]);
        furnsh_c(path);
    }

    SpiceDouble radii[3];
    SpiceInt n;
    bodvrd_c("EARTH", "RADII", 3, &n, radii);
    double f = (radii[0] - radii[2]) / radii[0];
    {
        SpiceDouble position[3], lon, lat, alt;
        latrec_c(6372.12509695, -116.79445837 * rpd_c(), 35.06601815 * rpd_c(), position);
        recgeo_c(position, radii[0], f, &lon, &lat, &alt);
        printf("PlanetocentricTests point: geodetic longitude %.17g deg, latitude %.17g deg, altitude %.17g m\n",
               lon * dpr_c(), lat * dpr_c(), alt * 1000.0);
    }
    {
        SpiceDouble position[3], radius, lon, lat;
        georec_c(-116.79445837 * rpd_c(), 35.24716450 * rpd_c(), 1.07085, radii[0], f, position);
        reclat_c(position, &radius, &lon, &lat);
        printf("PlanetodeticTests point: planetocentric longitude %.17g deg, latitude %.17g deg, radius %.17g m\n",
               lon * dpr_c(), lat * dpr_c(), radius * 1000.0);
    }

    const char *frames[] = {"ITRF93", "IAU_EARTH"};
    for (size_t i = 0; i < 2; i++)
    {
        SpiceDouble position[3], lightTime, lon, lat, alt, radius, clon, clat;
        spkpos_c("MOON", 0.0, frames[i], "NONE", "EARTH", position, &lightTime);
        reclat_c(position, &radius, &clon, &clat);
        recgeo_c(position, radii[0], f, &lon, &lat, &alt);
        printf("%s: planetocentric latitude %.17g rad; geodetic longitude %.17g rad, latitude %.17g rad, "
               "altitude %.17g m\n", frames[i], clat, lon, lat, alt * 1000.0);
    }

    return 0;
}
