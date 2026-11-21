using Vector3 = IO.Astrodynamics.Math.Vector3;

using IO.Astrodynamics.Body;

namespace IO.Astrodynamics.Coordinates
{
    /// <summary>
    /// Planetocentric coordinates: the longitude (rad, positive east), the planetocentric latitude (rad) and the distance to
    /// the centre of the body (m).
    /// </summary>
    /// <param name="Longitude">The longitude, rad.</param>
    /// <param name="Latitude">The planetocentric latitude, rad.</param>
    /// <param name="Radius">The distance to the centre of the body, m.</param>
    public readonly record struct Planetocentric(double Longitude, double Latitude, double Radius)
    {
        /// <summary>
        /// Convert to planetodetic (geodetic) coordinates on the ellipsoid of <paramref name="body"/>, its equatorial radius
        /// and flattening, exactly at any altitude (CSPICE <c>recgeo_c</c>; R20 of #668: before 11.0.0 the ellipsoid was
        /// given as a flattening and a radius, in the order opposite to that of
        /// <c>RadiusFromPlanetocentricLatitude(equatorialRadius, flattening)</c>).
        /// </summary>
        /// <param name="body">The body of the ellipsoid.</param>
        /// <returns>The geodetic longitude and latitude (rad) and altitude (m) of the point.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="body"/> is null.</exception>
        public Planetodetic ToPlanetodetic(CelestialBody body)
        {
            System.ArgumentNullException.ThrowIfNull(body);
            return ToPlanetodetic(body.Flattening, body.EquatorialRadius);
        }

        /// <summary>
        /// The radius of the ellipsoid of <paramref name="body"/> at the planetocentric latitude of these coordinates:
        /// <see cref="CelestialBody.RadiusFromPlanetocentricLatitude(double)"/> of <see cref="Latitude"/> (R20 of #668).
        /// </summary>
        /// <param name="body">The body of the ellipsoid.</param>
        /// <returns>The distance from the centre to the surface of the ellipsoid along the latitude, m.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="body"/> is null.</exception>
        public double RadiusFromPlanetocentricLatitude(CelestialBody body)
        {
            System.ArgumentNullException.ThrowIfNull(body);
            return body.RadiusFromPlanetocentricLatitude(Latitude);
        }

        /// <summary>
        /// Convert to planetodetic (geodetic) coordinates on the ellipsoid of revolution of the body, exactly at any
        /// altitude: the latitude of the normal to the ellipsoid through the point and the height along it.
        /// </summary>
        /// <param name="flattening">Flattening f = (a − c) / a of the body, below 1; negative for a prolate body.</param>
        /// <param name="equatorialRadius">Equatorial radius a of the body, m.</param>
        /// <returns>The geodetic longitude and latitude (rad) and altitude (m) of the point.</returns>
        /// <remarks>Closed form of Vermeille (2002) with the changes of Karney (2011); see
        /// <see cref="GeodeticCoordinates"/>. Before #411 the latitude came from the relation of the surface,
        /// tan φ = tan φc / (1 − f)², which put the altitude up to 5 km off in LEO.</remarks>
        /// <exception cref="System.ArgumentOutOfRangeException">The radius is not positive or the flattening not below 1.</exception>
        internal Planetodetic ToPlanetodetic(double flattening, double equatorialRadius)
        {
            // The longitude is the same in both systems: kept as given, also on the axis
            return GeodeticCoordinates.FromBodyFixed(ToCartesianCoordinates(), equatorialRadius, flattening) with
            {
                Longitude = Longitude
            };
        }

        /// <summary>
        /// Compute radius to ellipsoid from planetocentric latitude
        /// </summary>
        /// <param name="equatorialRadius"></param>
        /// <param name="flattening"></param>
        /// <returns></returns>
        internal double RadiusFromPlanetocentricLatitude(double equatorialRadius, double flattening)
        {
            return RadiusFromPlanetocentricLatitude(Latitude, equatorialRadius, flattening);
        }

        /// <summary>
        /// Compute radius to ellipsoid from planetocentric latitude
        /// </summary>
        /// <param name="latitude"></param>
        /// <param name="equatorialRadius"></param>
        /// <param name="flattening"></param>
        /// <returns></returns>
        internal static double RadiusFromPlanetocentricLatitude(double latitude, double equatorialRadius, double flattening)
        {
            double r2 = equatorialRadius * equatorialRadius;
            double s2 = System.Math.Sin(latitude) * System.Math.Sin(latitude);
            double f2 = (1 - flattening) * (1 - flattening);
            return System.Math.Sqrt(r2 / (1 + (1 / f2 - 1) * s2));
        }

        /// <summary>
        /// Convert to cartesian coordinates: (r cos φc cos λ, r cos φc sin λ, r sin φc) in the frame of the angles.
        /// </summary>
        /// <returns>The position, m.</returns>
        public Vector3 ToCartesianCoordinates()
        {
            var x = Radius * System.Math.Cos(Longitude) * System.Math.Cos(Latitude);
            var y = Radius * System.Math.Sin(Longitude) * System.Math.Cos(Latitude);
            var z = Radius * System.Math.Sin(Latitude);
            return new Vector3(x, y, z);
        }
    }
}