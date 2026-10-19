/*
 * Reference values of the geodetic (planetodetic) coordinates for #411: on a grid of bodies, longitudes,
 * latitudes and altitudes, the body-fixed Cartesian position of a geodetic point and back, by two independent
 * implementations, CSPICE (georec_c, recgeo_c) and IAU SOFA (iauGd2gce, iauGc2gde, after Fukushima, 2006,
 * J. Geodesy 79, 689-693). Prints JSON on stdout. See README.md.
 */
#include <math.h>
#include <stdio.h>
#include "SpiceUsr.h"
#include "sofa.h"

typedef struct
{
    const char *name;
    double equatorialRadius; /* m, BODYnnn_RADII of pck00011.tpc */
    double polarRadius;      /* m */
} Body;

static const Body Bodies[] = {
    {"EARTH", 6378136.6, 6356751.9},
    {"MARS", 3396190.0, 3376200.0},
    {"MOON", 1737400.0, 1737400.0},
};

static const double LongitudesDeg[] = {-180.0, -116.79445837, 0.0, 17.2, 90.0, 135.0};
static const double LatitudesDeg[] = {-90.0, -89.999, -60.0, -45.0, -30.0, -10.0, 0.0, 10.0, 30.0,
                                      35.24716450, 45.0, 51.6, 60.0, 80.0, 89.999, 90.0};
static const double Altitudes[] = {-5000.0, 0.0, 1000.0, 5000.0, 100e3, 400e3, 800e3, 2000e3, 35786e3, 384400e3};

#define COUNT(a) (sizeof(a) / sizeof((a)[0]))

int main(void)
{
    int first = 1;
    printf("{\n  \"generated_by\": \"tools/geodetic_reference/geodetic_ref.c, CSPICE N0067 and SOFA 2023-10-11\",\n");
    printf("  \"units\": \"m, rad\",\n  \"points\": [\n");
    for (size_t b = 0; b < COUNT(Bodies); b++)
    {
        const Body *body = &Bodies[b];
        double f = (body->equatorialRadius - body->polarRadius) / body->equatorialRadius;
        for (size_t i = 0; i < COUNT(LongitudesDeg); i++)
        {
            for (size_t j = 0; j < COUNT(LatitudesDeg); j++)
            {
                for (size_t k = 0; k < COUNT(Altitudes); k++)
                {
                    double lon = LongitudesDeg[i] * rpd_c();
                    double lat = LatitudesDeg[j] * rpd_c();
                    double h = Altitudes[k];

                    /* Geodetic to body-fixed Cartesian */
                    SpiceDouble spice[3];
                    georec_c(lon, lat, h, body->equatorialRadius, f, spice);
                    double sofa[3];
                    if (iauGd2gce(body->equatorialRadius, f, lon, lat, h, sofa) != 0)
                    {
                        fprintf(stderr, "iauGd2gce failed\n");
                        return 1;
                    }

                    /* Back to geodetic, from the SPICE position */
                    SpiceDouble spiceLon, spiceLat, spiceAlt;
                    recgeo_c(spice, body->equatorialRadius, f, &spiceLon, &spiceLat, &spiceAlt);
                    double sofaLon, sofaLat, sofaAlt;
                    if (iauGc2gde(body->equatorialRadius, f, spice, &sofaLon, &sofaLat, &sofaAlt) != 0)
                    {
                        fprintf(stderr, "iauGc2gde failed\n");
                        return 1;
                    }

                    printf("%s    {\"body\": \"%s\", \"equatorial_radius\": %.17g, \"flattening\": %.17g, "
                           "\"longitude\": %.17g, \"latitude\": %.17g, \"altitude\": %.17g,\n"
                           "     \"spice_xyz\": [%.17g, %.17g, %.17g], \"sofa_xyz\": [%.17g, %.17g, %.17g],\n"
                           "     \"spice_geodetic\": [%.17g, %.17g, %.17g], \"sofa_geodetic\": [%.17g, %.17g, %.17g]}",
                           first ? "" : ",\n", body->name, body->equatorialRadius, f, lon, lat, h,
                           spice[0], spice[1], spice[2], sofa[0], sofa[1], sofa[2],
                           spiceLon, spiceLat, spiceAlt, sofaLon, sofaLat, sofaAlt);
                    first = 0;
                }
            }
        }
    }
    printf("\n  ]\n}\n");
    return 0;
}
