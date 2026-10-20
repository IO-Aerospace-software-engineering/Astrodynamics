/*
 * Reference values of the geodetic (planetodetic) coordinates for #411: on a grid of bodies, longitudes,
 * latitudes and altitudes, the body-fixed Cartesian position of a geodetic point and back, by two independent
 * implementations, CSPICE (georec_c, recgeo_c) and IAU SOFA (iauGd2gce, iauGc2gde, after Fukushima, 2006,
 * J. Geodesy 79, 689-693). Prints JSON on stdout. See README.md.
 */
#include <math.h>
#include <quadmath.h>
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
    {"HARTLEY 2", 340.0, 1160.0}, /* 1000041, prolate: f = -2.41 */
};

static const double LongitudesDeg[] = {-180.0, -116.79445837, 0.0, 17.2, 90.0, 135.0};
static const double LatitudesDeg[] = {-90.0, -89.999, -60.0, -45.0, -30.0, -10.0, 0.0, 10.0, 30.0,
                                      35.24716450, 45.0, 51.6, 60.0, 80.0, 89.999, 90.0};
static const double Altitudes[] = {-5000.0, 0.0, 1000.0, 5000.0, 100e3, 400e3, 800e3, 2000e3, 35786e3, 384400e3};

/* Points inside the evolute of the meridian ellipse, near the centre: distances as fractions of the equatorial
   radius, in directions of the meridian plane at these geocentric latitudes */
static const double InteriorFractions[] = {0.0, 1e-9, 1e-6, 1e-4, 1e-3, 3e-3, 6e-3, 1e-2, 1e-1, 0.5};
static const double InteriorLatitudesDeg[] = {-90.0, -30.0, 0.0, 1e-3, 10.0, 45.0, 80.0, 90.0};

#define COUNT(a) (sizeof(a) / sizeof((a)[0]))

/*
 * The geodetic latitude and altitude of the nearest foot point, in binary128 (113-bit significand), for the points
 * inside the evolute. The point (p, z) of the meridian half-plane, p >= 0, is taken to |z| (the latitude of (p, -z) is
 * the opposite; p is formed in binary128 from x and y) and the nearest point of the meridian ellipse is in the same quadrant. With the semi-axes e0 >= e1
 * along the coordinates y0 and y1 (e0 = a along p on an oblate body, e0 = c along z on a prolate one), the foot point is
 * x_i = e_i^2 y_i / (t + e_i^2) at the largest root t of F(t) = (e0 y0 / (t + e0^2))^2 + (e1 y1 / (t + e1^2))^2 - 1,
 * which is decreasing beyond -e1^2: D. Eberly, "Distance from a point to an ellipse, an ellipsoid, or a
 * hyperellipsoid", Geometric Tools (2013, revised 2020), its case of the ellipse (to be verified by S. Guillet). The root is
 * bracketed in s = t + e1^2 by [e1 y1, sqrt(e0^2 y0^2 + e1^2 y1^2)] and bisected to the last bit. When y1 = 0 and
 * y0 < (e0^2 - e1^2) / e0, on the segment of the major axis inside the evolute, the two nearest foot points are
 * symmetric and the one of positive y1 is taken (the north, as the library maps the equatorial disc and the centre).
 * The geodetic latitude is that of the normal at the foot point, atan2(z_f / c^2, p_f / a^2); the altitude is minus
 * the distance to it inside the ellipsoid. Only the four basic operations, the square root and atan2q of libquadmath
 * are used.
 */
static void ExactGeodetic(double equatorialRadius, double flattening, const double xyz[3], double *latitude,
                          double *altitude)
{
    __float128 a = equatorialRadius;
    __float128 c = a * (1 - (__float128)flattening);
    __float128 x = xyz[0], y = xyz[1], z = xyz[2];
    __float128 pq = sqrtq(x * x + y * y), zq = fabsq(z);
    int oblate = a >= c;
    __float128 e0 = oblate ? a : c, e1 = oblate ? c : a;
    __float128 y0 = oblate ? pq : zq, y1 = oblate ? zq : pq;
    __float128 d0 = e0 * e0 - e1 * e1;
    __float128 x0, x1;
    if (y1 == 0)
    {
        if (y0 * e0 < d0)
        {
            x0 = e0 * e0 * y0 / d0;
            x1 = e1 * sqrtq(1 - (x0 / e0) * (x0 / e0));
        }
        else
        {
            x0 = e0;
            x1 = 0;
        }
    }
    else
    {
        __float128 lo = e1 * y1, hi = sqrtq(e0 * e0 * y0 * y0 + e1 * e1 * y1 * y1);
        for (int i = 0; i < 400; i++)
        {
            __float128 s = (lo + hi) / 2;
            if (s == lo || s == hi)
            {
                break;
            }

            __float128 g0 = e0 * y0 / (s + d0), g1 = e1 * y1 / s;
            if (g0 * g0 + g1 * g1 - 1 > 0)
            {
                lo = s;
            }
            else
            {
                hi = s;
            }
        }

        __float128 s = (lo + hi) / 2;
        x0 = e0 * e0 * y0 / (s + d0);
        x1 = e1 * e1 * y1 / s;
    }

    __float128 pf = oblate ? x0 : x1, zf = oblate ? x1 : x0;
    __float128 lat = atan2q(zf * a * a, pf * c * c);
    __float128 distance = sqrtq((pq - pf) * (pq - pf) + (zq - zf) * (zq - zf));
    int inside = (pq / a) * (pq / a) + (zq / c) * (zq / c) < 1;
    *latitude = (double)(z < 0 ? -lat : lat);
    *altitude = (double)(inside ? -distance : distance);
}

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
                    /* Depths beyond a tenth of the smaller radius leave the region where the geodetic coordinates
                       of a point are unique */
                    if (h < -0.1 * fmin(body->equatorialRadius, body->polarRadius))
                    {
                        continue;
                    }

                    /* Geodetic to body-fixed Cartesian */
                    SpiceDouble spice[3];
                    georec_c(lon, lat, h, body->equatorialRadius, f, spice);
                    /* SOFA rejects a negative flattening (prolate body): null there */
                    int hasSofa = f >= 0.0;
                    double sofa[3] = {0.0, 0.0, 0.0};
                    if (hasSofa && iauGd2gce(body->equatorialRadius, f, lon, lat, h, sofa) != 0)
                    {
                        fprintf(stderr, "iauGd2gce failed\n");
                        return 1;
                    }

                    /* Back to geodetic, from the SPICE position */
                    SpiceDouble spiceLon, spiceLat, spiceAlt;
                    recgeo_c(spice, body->equatorialRadius, f, &spiceLon, &spiceLat, &spiceAlt);
                    double sofaLon = 0.0, sofaLat = 0.0, sofaAlt = 0.0;
                    if (hasSofa && iauGc2gde(body->equatorialRadius, f, spice, &sofaLon, &sofaLat, &sofaAlt) != 0)
                    {
                        fprintf(stderr, "iauGc2gde failed\n");
                        return 1;
                    }

                    printf("%s    {\"body\": \"%s\", \"equatorial_radius\": %.17g, \"flattening\": %.17g, "
                           "\"longitude\": %.17g, \"latitude\": %.17g, \"altitude\": %.17g,\n"
                           "     \"spice_xyz\": [%.17g, %.17g, %.17g], \"spice_geodetic\": [%.17g, %.17g, %.17g],\n",
                           first ? "" : ",\n", body->name, body->equatorialRadius, f, lon, lat, h,
                           spice[0], spice[1], spice[2], spiceLon, spiceLat, spiceAlt);
                    if (hasSofa)
                    {
                        printf("     \"sofa_xyz\": [%.17g, %.17g, %.17g], \"sofa_geodetic\": [%.17g, %.17g, %.17g]}",
                               sofa[0], sofa[1], sofa[2], sofaLon, sofaLat, sofaAlt);
                    }
                    else
                    {
                        printf("     \"sofa_xyz\": null, \"sofa_geodetic\": null}");
                    }
                    first = 0;
                }
            }
        }
    }
    printf("\n  ],\n  \"interior\": [\n");
    first = 1;
    for (size_t b = 0; b < COUNT(Bodies); b++)
    {
        const Body *body = &Bodies[b];
        double f = (body->equatorialRadius - body->polarRadius) / body->equatorialRadius;
        for (size_t i = 0; i < COUNT(InteriorFractions); i++)
        {
            for (size_t j = 0; j < COUNT(InteriorLatitudesDeg); j++)
            {
                if (InteriorFractions[i] == 0.0 && j > 0)
                {
                    continue;
                }

                double distance = InteriorFractions[i] * body->equatorialRadius;
                double latc = InteriorLatitudesDeg[j] * rpd_c();
                double lon = 0.3;
                SpiceDouble xyz[3] = {distance * cos(latc) * cos(lon), distance * cos(latc) * sin(lon),
                                      distance * sin(latc)};
                SpiceDouble gLon, gLat, gAlt;
                recgeo_c(xyz, body->equatorialRadius, f, &gLon, &gLat, &gAlt);
                SpiceDouble back[3];
                georec_c(gLon, gLat, gAlt, body->equatorialRadius, f, back);
                double exactLat, exactAlt;
                ExactGeodetic(body->equatorialRadius, f, xyz, &exactLat, &exactAlt);
                printf("%s    {\"body\": \"%s\", \"equatorial_radius\": %.17g, \"flattening\": %.17g, "
                       "\"xyz\": [%.17g, %.17g, %.17g],\n"
                       "     \"spice_geodetic\": [%.17g, %.17g, %.17g], \"spice_back\": [%.17g, %.17g, %.17g],\n"
                       "     \"exact_geodetic\": [%.17g, %.17g]}",
                       first ? "" : ",\n", body->name, body->equatorialRadius, f, xyz[0], xyz[1], xyz[2],
                       gLon, gLat, gAlt, back[0], back[1], back[2], exactLat, exactAlt);
                first = 0;
            }
        }
    }
    printf("\n  ]\n}\n");
    return 0;
}
