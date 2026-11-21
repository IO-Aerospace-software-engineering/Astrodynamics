using IO.Astrodynamics.Body;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Coordinates
{
    /// <summary>
    /// Planetodetic (geodetic) coordinates on the ellipsoid of revolution of a body: the longitude (rad, positive east),
    /// the latitude of the normal to the ellipsoid through the point (rad) and the height along it (m).
    /// </summary>
    public readonly record struct Planetodetic
    {
        /// <summary>The longitude, rad, positive east.</summary>
        public double Longitude { get; init; }

        /// <summary>The geodetic latitude, rad.</summary>
        public double Latitude { get; init; }

        /// <summary>The height above the ellipsoid, m.</summary>
        public double Altitude { get; init; }

        /// <summary>
        /// The coordinates of a longitude, a geodetic latitude and an altitude (R20 of #668: camelCase parameters, before
        /// 11.0.0 the PascalCase parameters of a positional record).
        /// </summary>
        /// <param name="longitude">The longitude, rad, positive east.</param>
        /// <param name="latitude">The geodetic latitude, rad.</param>
        /// <param name="altitude">The height above the ellipsoid, m.</param>
        public Planetodetic(double longitude, double latitude, double altitude)
        {
            Longitude = longitude;
            Latitude = latitude;
            Altitude = altitude;
        }

        /// <summary>Deconstruct into the longitude, the geodetic latitude and the altitude.</summary>
        /// <param name="longitude">The longitude, rad.</param>
        /// <param name="latitude">The geodetic latitude, rad.</param>
        /// <param name="altitude">The height above the ellipsoid, m.</param>
        public void Deconstruct(out double longitude, out double latitude, out double altitude)
        {
            longitude = Longitude;
            latitude = Latitude;
            altitude = Altitude;
        }

        /// <summary>
        /// Convert to planetocentric coordinates on the ellipsoid of <paramref name="body"/>, its equatorial radius and
        /// flattening: the longitude, the planetocentric latitude and the distance to the centre of the body of the point
        /// itself, at its altitude (R20 of #668: before 11.0.0 the ellipsoid was given as a flattening and a radius).
        /// </summary>
        /// <param name="body">The body of the ellipsoid.</param>
        /// <returns>The planetocentric longitude and latitude (rad) and radius (m) of the point.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="body"/> is null.</exception>
        public Planetocentric ToPlanetocentric(CelestialBody body)
        {
            System.ArgumentNullException.ThrowIfNull(body);
            return ToPlanetocentric(body.Flattening, body.EquatorialRadius);
        }

        /// <summary>
        /// The position of the point in the body-fixed frame of <paramref name="body"/>, m, on its ellipsoid (CSPICE
        /// <c>georec_c</c>): <see cref="ToPlanetocentric(CelestialBody)"/> then
        /// <see cref="Planetocentric.ToCartesianCoordinates()"/>, bit for bit, in one call (R20 of #668).
        /// </summary>
        /// <param name="body">The body of the ellipsoid.</param>
        /// <returns>The body-fixed position, m.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="body"/> is null.</exception>
        public Vector3 ToCartesianCoordinates(CelestialBody body)
        {
            return ToPlanetocentric(body).ToCartesianCoordinates();
        }

        /// <summary>
        /// Convert to planetocentric coordinates: the longitude, the planetocentric latitude and the distance to the
        /// centre of the body of the point itself, at its altitude.
        /// </summary>
        /// <param name="flattening">Flattening f = (a − c) / a of the body, below 1; negative for a prolate body.</param>
        /// <param name="equatorialRadius">Equatorial radius a of the body, m.</param>
        /// <returns>The planetocentric longitude and latitude (rad) and radius (m) of the point.</returns>
        /// <remarks>Exact closed form with e² = f (2 − f) of the body; see <see cref="GeodeticCoordinates"/>. Before
        /// #411 the latitude was that of the surface point below and e² that of WGS84 whatever the body, which put
        /// a site 3.4 m off at 1 km of altitude on the Earth and kilometres off on Mars or the Moon.</remarks>
        /// <exception cref="System.ArgumentOutOfRangeException">The radius is not positive or the flattening not below 1.</exception>
        internal Planetocentric ToPlanetocentric(double flattening, double equatorialRadius)
        {
            var position = GeodeticCoordinates.ToBodyFixed(Longitude, Latitude, Altitude, equatorialRadius, flattening);
            double distanceToAxis = System.Math.Sqrt(position.X * position.X + position.Y * position.Y);
            return new Planetocentric(Longitude, System.Math.Atan2(position.Z, distanceToAxis), position.Magnitude());
        }
    }
}