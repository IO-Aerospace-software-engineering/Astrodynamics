namespace IO.Astrodynamics.Coordinates
{
    public readonly record struct Planetodetic(double Longitude, double Latitude, double Altitude)
    {
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
        public Planetocentric ToPlanetocentric(double flattening, double equatorialRadius)
        {
            var position = GeodeticCoordinates.ToBodyFixed(Longitude, Latitude, Altitude, equatorialRadius, flattening);
            double distanceToAxis = System.Math.Sqrt(position.X * position.X + position.Y * position.Y);
            return new Planetocentric(Longitude, System.Math.Atan2(position.Z, distanceToAxis), position.Magnitude());
        }
    }
}