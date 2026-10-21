/*
 * Reference horizontal coordinates for SiteTests (#411): the Moon seen from DSS-13 in its topocentric frame
 * DSS-13_TOPO (earth_topo_201023.tf: Z toward the zenith, X toward the north, built from the published geodetic
 * coordinates of the station), geometric (no aberration), at the epochs of the tests (TDB). Azimuth from the north
 * toward the east. Also the same quantities in the frame that the library builds (Surface/SiteFrame.cs): north, west
 * and zenith of the geodetic coordinates of the station at J2000 TDB on the ellipsoid of pck00011 (the radii of the
 * library), relative to ITRF93, instead of the WGS84 spheroid of the frame kernel. Run from the repository root; see
 * README.md.
 */
#include <math.h>
#include <stdio.h>
#include "SpiceUsr.h"

int main(void)
{
    const char *data = "IO.Astrodynamics.Net/IO.Astrodynamics.Tests/Data/SolarSystem/";
    const char *kernels[] = {"latest_leapseconds.tls", "pck00011.tpc", "de440s.bsp", "earth_latest_high_prec.bpc",
                             "earth_fixed.tf", "earth_assoc_itrf93.tf", "earth_topo_201023.tf",
                             "earthstns_itrf93_201023.bsp"};
    char path[512];
    for (size_t i = 0; i < sizeof(kernels) / sizeof(kernels[0]); i++)
    {
        snprintf(path, sizeof(path), "%s%s", data, kernels[i]);
        furnsh_c(path);
    }

    /* Geodetic coordinates of the station on the ellipsoid of pck00011, as the library computes them */
    SpiceDouble et0, station[3], lt0, radii[3];
    SpiceInt n;
    str2et_c("2000-01-01 12:00:00 TDB", &et0);
    spkpos_c("DSS-13", et0, "ITRF93", "NONE", "EARTH", station, &lt0);
    bodvrd_c("EARTH", "RADII", 3, &n, radii);
    double f = (radii[0] - radii[2]) / radii[0];
    SpiceDouble lon, lat, alt;
    recgeo_c(station, radii[0], f, &lon, &lat, &alt);
    printf("DSS-13 on the ellipsoid of pck00011: longitude %.17g deg, latitude %.17g deg, altitude %.17g m\n",
           lon * dpr_c(), lat * dpr_c(), alt * 1000.0);
    /* Rows: north, west, zenith, in ITRF93 */
    SpiceDouble topo[3][3] = {{-sin(lat) * cos(lon), -sin(lat) * sin(lon), cos(lat)},
                              {sin(lon), -cos(lon), 0.0},
                              {cos(lat) * cos(lon), cos(lat) * sin(lon), sin(lat)}};

    const char *epochs[] = {"2000-01-01 12:00:00 TDB", "2000-01-05 12:00:00 TDB", "2000-01-10 12:00:00 TDB",
                            "2000-01-15 12:00:00 TDB"};
    for (size_t i = 0; i < sizeof(epochs) / sizeof(epochs[0]); i++)
    {
        SpiceDouble et, position[3], lightTime;
        str2et_c(epochs[i], &et);
        spkpos_c("MOON", et, "DSS-13_TOPO", "NONE", "DSS-13", position, &lightTime);
        double range = vnorm_c(position);
        double azimuth = atan2(-position[1], position[0]) * dpr_c();
        if (azimuth < 0.0)
        {
            azimuth += 360.0;
        }

        double elevation = asin(position[2] / range) * dpr_c();
        printf("%s: azimuth %.17g deg, elevation %.17g deg, range %.17g m\n", epochs[i], azimuth, elevation,
               range * 1000.0);

        SpiceDouble fixed[3], local[3];
        spkpos_c("MOON", et, "ITRF93", "NONE", "DSS-13", fixed, &lightTime);
        mxv_c(topo, fixed, local);
        double localAzimuth = atan2(-local[1], local[0]) * dpr_c();
        if (localAzimuth < 0.0)
        {
            localAzimuth += 360.0;
        }

        printf("    pck00011 frame: azimuth %.17g deg, elevation %.17g deg\n", localAzimuth,
               asin(local[2] / vnorm_c(local)) * dpr_c());
    }

    /* SiteTests.Illumination*: the angle of the Sun and of the Moon to the zenith of the frame of the library at J2000
       TDB */
    const char *targets[] = {"SUN", "MOON"};
    for (size_t i = 0; i < 2; i++)
    {
        SpiceDouble fixed[3], local[3], lt;
        spkpos_c(targets[i], et0, "ITRF93", "NONE", "DSS-13", fixed, &lt);
        mxv_c(topo, fixed, local);
        SpiceDouble zenith[3] = {0.0, 0.0, 1.0};
        printf("%s at J2000 TDB, angle to the zenith (pck00011 frame): %.17g rad\n", targets[i], vsep_c(local, zenith));
    }

    return 0;
}
