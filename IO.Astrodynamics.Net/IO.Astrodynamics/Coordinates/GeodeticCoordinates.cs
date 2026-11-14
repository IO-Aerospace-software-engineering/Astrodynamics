using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Coordinates;

/// <summary>
/// Exact conversions between geodetic (planetodetic) coordinates on an ellipsoid of revolution and body-fixed
/// Cartesian coordinates, at any altitude, for oblate, spherical and prolate bodies (#411).
/// </summary>
/// <remarks>
/// <para>
/// Geodetic to Cartesian, closed form: N = a / √(1 − e² sin² φ), x = (N + h) cos φ cos λ, y = (N + h) cos φ sin λ,
/// z = (N (1 − e²) + h) sin φ, with e² = f (2 − f). Reference: Explanatory Supplement to the Astronomical Almanac,
/// P. K. Seidelmann (ed.), University Science Books (1992), §4.22, as cited by SOFA <c>iauGd2gce</c> (confirmed by
/// S. Guillet, 2026-10-24; the book could not be read from the development environment).
/// </para>
/// <para>
/// Cartesian to geodetic, closed form without iteration: H. Vermeille, "Direct transformation from geocentric
/// coordinates to geodetic coordinates", Journal of Geodesy 76, 451–454 (2002), with the changes of C. F. F. Karney,
/// "Geodesics on an ellipsoid of revolution", arXiv:1102.1215v1 (2011), appendix B, that keep it accurate for every
/// finite point: the trigonometric root inside the evolute of the meridian ellipse (near the centre), the rearranged
/// expressions that avoid cancellation, the equatorial limit and prolate bodies (confirmed by S. Guillet, 2026-10-24; neither
/// paper could be read from the development environment; the formulas were read on the implementation of the same
/// method, GeographicLib <c>Geocentric::IntReverse</c>, MIT licence, commit 48959df, 2025-11-14, on 2026-10-19).
/// Being a closed form, the result is a smooth function of the position, which the density gradient of drag takes by
/// central differences.
/// </para>
/// <para>
/// Checked against CSPICE <c>georec_c</c> and <c>recgeo_c</c> and SOFA <c>iauGd2gce</c> and <c>iauGc2gde</c> on the
/// grid of <c>tools/geodetic_reference</c> (<c>GeodeticCoordinatesTests</c>). No allocation.
/// </para>
/// </remarks>
internal static class GeodeticCoordinates
{
    // Unit roundoff of a double, 2⁻⁵² (not double.Epsilon, the smallest subnormal)
    private const double MachineEpsilon = 2.220446049250313e-16;

    /// <summary>
    /// The body-fixed Cartesian position of a geodetic point.
    /// </summary>
    /// <param name="longitude">Geodetic longitude, rad.</param>
    /// <param name="latitude">Geodetic latitude, rad.</param>
    /// <param name="altitude">Height above the ellipsoid along its normal, m.</param>
    /// <param name="equatorialRadius">Equatorial radius a of the ellipsoid, m.</param>
    /// <param name="flattening">Flattening f = (a − c) / a, below 1; negative for a prolate body.</param>
    /// <exception cref="ArgumentOutOfRangeException">The radius is not positive or the flattening not below 1.</exception>
    internal static Vector3 ToBodyFixed(double longitude, double latitude, double altitude, double equatorialRadius,
        double flattening)
    {
        CheckEllipsoid(equatorialRadius, flattening);
        double e2 = flattening * (2.0 - flattening);
        double sinLatitude = System.Math.Sin(latitude);
        double cosLatitude = System.Math.Cos(latitude);
        double n = equatorialRadius / System.Math.Sqrt(1.0 - e2 * sinLatitude * sinLatitude);
        double distanceToAxis = (n + altitude) * cosLatitude;
        return new Vector3(distanceToAxis * System.Math.Cos(longitude), distanceToAxis * System.Math.Sin(longitude),
            ((1.0 - flattening) * (1.0 - flattening) * n + altitude) * sinLatitude);
    }

    /// <summary>
    /// The geodetic coordinates of a body-fixed Cartesian position. The longitude is in (−π, π], 0 on the rotation
    /// axis; the latitude in [−π/2, π/2]; the centre of the body maps to the north pole.
    /// </summary>
    /// <param name="position">Body-fixed position, m.</param>
    /// <param name="equatorialRadius">Equatorial radius a of the ellipsoid, m.</param>
    /// <param name="flattening">Flattening f = (a − c) / a, below 1; negative for a prolate body.</param>
    /// <exception cref="ArgumentOutOfRangeException">The radius is not positive or the flattening not below 1.</exception>
    internal static Planetodetic FromBodyFixed(in Vector3 position, double equatorialRadius, double flattening)
    {
        CheckEllipsoid(equatorialRadius, flattening);
        double x = position.X, y = position.Y, z = position.Z;
        double distanceToAxis = System.Math.Sqrt(x * x + y * y);
        // atan2 of the signed zeros would give −π for y = −0 and x < 0, and ±π on the axis for x = −0
        double longitude = y == 0.0 ? (x < 0.0 ? System.Math.PI : 0.0) : System.Math.Atan2(y, x);
        double distance = System.Math.Sqrt(distanceToAxis * distanceToAxis + z * z);

        double e2 = flattening * (2.0 - flattening);
        if (distance > 2.0 * equatorialRadius / MachineEpsilon)
        {
            // So far away that the body is a point (beyond 2a/ε): the geocentric latitude and the distance
            return new Planetodetic(longitude, System.Math.Atan2(z, distanceToAxis), distance);
        }

        if (e2 == 0.0)
        {
            // Sphere; its centre maps to the north pole, as on an ellipsoid
            double latitude = System.Math.Atan2(distance == 0.0 ? 1.0 : z, distanceToAxis);
            return new Planetodetic(longitude, latitude, distance - equatorialRadius);
        }

        double e2m = (1.0 - flattening) * (1.0 - flattening); // 1 − e²
        double e2a = System.Math.Abs(e2);
        double e4 = e2 * e2;
        double p = distanceToAxis / equatorialRadius * (distanceToAxis / equatorialRadius);
        double q = e2m * (z / equatorialRadius) * (z / equatorialRadius);
        double r = (p + q - e4) / 6.0;
        if (flattening < 0.0)
        {
            // Prolate body: the roles of the axis distance and of z are exchanged, and back at the end
            (p, q) = (q, p);
        }

        if (!(e4 * q == 0.0 && r <= 0.0))
        {
            // Vermeille's s and t multiplied by r³ and r, so that r = 0 needs no division
            double s = e4 * p * q / 4.0;
            double r2 = r * r;
            double r3 = r * r2;
            double discriminant = s * (2.0 * r3 + s);
            double u = r;
            if (discriminant >= 0.0)
            {
                // The sign of the square root that maximizes |t³| r³ avoids the cancellation; u is unchanged
                double t3 = s + r3;
                t3 += t3 < 0.0 ? -System.Math.Sqrt(discriminant) : System.Math.Sqrt(discriminant);
                double t = System.Math.Cbrt(t3);
                u += t + (t != 0.0 ? r2 / t : 0.0);
            }
            else
            {
                // Inside the evolute (near the centre): the real root, by its trigonometric form
                double angle = System.Math.Atan2(System.Math.Sqrt(-discriminant), -(s + r3));
                u += 2.0 * r * System.Math.Cos(angle / 3.0);
            }

            double v = System.Math.Sqrt(u * u + e4 * q);
            // u + v, rearranged when u < 0 to avoid the cancellation
            double uv = u < 0.0 ? e4 * q / (v - u) : u + v;
            double w = System.Math.Max(0.0, e2a * (uv - q) / (2.0 * v));
            double k = uv / (System.Math.Sqrt(uv + w * w) + w);
            double k1 = flattening >= 0.0 ? k : k - e2;
            double k2 = flattening >= 0.0 ? k + e2 : k;
            double d = k1 * distanceToAxis / k2;
            double latitude = System.Math.Atan2(z / k1, distanceToAxis / k2);
            double altitude = (1.0 - e2m / k1) * System.Math.Sqrt(d * d + z * z);
            return new Planetodetic(longitude, latitude, altitude);
        }

        // On the equatorial disc inside the evolute (oblate), or on the axis inside it (prolate): the limits of the
        // general formulas, which would give 0/0
        double zz = System.Math.Sqrt((flattening >= 0.0 ? e4 - p : p) / e2m);
        double xx = System.Math.Sqrt(flattening < 0.0 ? e4 - p : p);
        double limitLatitude = System.Math.Atan2(z < 0.0 ? -zz : zz, xx);
        double limitAltitude = -equatorialRadius * (flattening >= 0.0 ? e2m : 1.0) *
                               System.Math.Sqrt(zz * zz + xx * xx) / e2a;
        return new Planetodetic(longitude, limitLatitude, limitAltitude);
    }

    /// <summary>
    /// The planetographic coordinates of a body-fixed Cartesian position, as CSPICE's <c>recpgr_c</c> gives them
    /// (Detailed_Output <c>lon</c>, <c>lat</c>, <c>alt</c>, https://naif.jpl.nasa.gov/pub/naif/toolkit_docs/C/cspice/recpgr_c.html):
    /// the latitude and the altitude are the geodetic ones on the spheroid (<see cref="FromBodyFixed"/>, "The planetographic
    /// definition of latitude is identical to the planetodetic"); the longitude is in [0, 2π] (2π only by rounding, as in
    /// <c>recpgr_c</c>), increasing west when <paramref name="positiveWest"/> (from +X toward −Y), east otherwise (#682).
    /// </summary>
    /// <param name="position">Body-fixed position, m.</param>
    /// <param name="equatorialRadius">Equatorial radius a of the spheroid, m.</param>
    /// <param name="flattening">Flattening f = (a − c) / a, below 1.</param>
    /// <param name="positiveWest">Whether the longitude increases west (<see cref="IsPlanetographicLongitudePositiveWest"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException">The radius is not positive or the flattening not below 1.</exception>
    internal static Planetodetic ToPlanetographic(in Vector3 position, double equatorialRadius, double flattening, bool positiveWest)
    {
        var geodetic = FromBodyFixed(position, equatorialRadius, flattening);
        double longitude = positiveWest ? -geodetic.Longitude : geodetic.Longitude;
        return geodetic with { Longitude = longitude < 0.0 ? longitude + Constants._2PI : longitude };
    }

    /// <summary>
    /// Whether the planetographic longitude of a body increases west, the default sense of CSPICE (<c>recpgr_c</c>
    /// Particulars): west for a prograde spin, east for a retrograde one, "the spin is retrograde if [the first degree term
    /// of W] is negative and prograde otherwise", and east for the Earth, the Moon and the Sun, its exceptions. The rate of W
    /// is read as the opposite of the z component of the angular velocity of the body's frame at <paramref name="epoch"/>
    /// (<see cref="Frames.Frame.GetStateOrientationToICRF"/>: that of ICRF relative to the body, in its axes). The kernel
    /// variable <c>BODY&lt;id&gt;_PGR_POSITIVE_LON</c>, by which a kernel overrides that sense for <c>recpgr_c</c> and
    /// <c>gfposc_c</c> (GF Required Reading, "Meridian crossing"), is not read: no kernel of the library's data defines it.
    /// </summary>
    internal static bool IsPlanetographicLongitudePositiveWest(CelestialBody body, in Time epoch)
    {
        if (body.NaifId is 10 or 399 or 301)
        {
            return false;
        }

        return body.Frame.GetStateOrientationToICRF(epoch).AngularVelocity.Z <= 0.0;
    }

    /// <summary>
    /// The coordinate of <see cref="CoordinateSystem.Planetographic"/> that the managed searches of
    /// <see cref="CelestialItem"/> and <see cref="Surface.Site"/> evaluate, CSPICE's PLANETOGRAPHIC (<c>gfposc_c</c>
    /// Detailed_Input <c>coord</c>: LONGITUDE, LATITUDE, ALTITUDE): the position of the state in the body-fixed frame of
    /// its observer, a <see cref="CelestialBody"/> (as the geodetic case), on its spheroid (<see cref="ToPlanetographic"/>).
    /// <c>gfposc_c</c> takes the body of the frame's centre; the two agree for a frame centred on the observer.
    /// </summary>
    /// <param name="stateVector">The state of the target relative to the observer.</param>
    /// <param name="coordinate">The coordinate: longitude, latitude or altitude.</param>
    /// <param name="positiveWest">The sense of the longitude of the observer's body.</param>
    /// <exception cref="ArgumentOutOfRangeException">Another coordinate: PLANETOGRAPHIC has no radius.</exception>
    internal static double PlanetographicCoordinate(StateVector stateVector, Coordinate coordinate, bool positiveWest)
    {
        var body = (CelestialBody)stateVector.Observer;
        var position = stateVector.ToFrame(body.Frame).ToStateVector().Position;
        var planetographic = ToPlanetographic(position, body.EquatorialRadius, body.Flattening, positiveWest);
        return coordinate switch
        {
            Coordinate.Longitude => planetographic.Longitude,
            Coordinate.Latitude => planetographic.Latitude,
            Coordinate.Altitude => planetographic.Altitude,
            _ => throw new ArgumentOutOfRangeException(nameof(coordinate), coordinate,
                "The planetographic coordinates are the longitude, the latitude and the altitude.")
        };
    }

    private static void CheckEllipsoid(double equatorialRadius, double flattening)
    {
        if (!(equatorialRadius > 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(equatorialRadius), equatorialRadius,
                "The equatorial radius must be positive.");
        }

        if (!(flattening < 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(flattening), flattening,
                "The flattening must be below 1.");
        }
    }
}
