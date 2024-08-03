using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Coordinates
{
    public readonly record struct Planetocentric(double Longitude, double Latitude, double Radius)
    {
        /// <summary>
        /// Convert to planetodetic coordinates
        /// </summary>
        /// <param name="flattening"></param>
        /// <param name="equatorialRadius"></param>
        /// <returns></returns>
        public Planetodetic ToPlanetodetic(double flattening, double equatorialRadius)
        {
            double f2 = (1 - flattening) * (1 - flattening);
            double lat = System.Math.Atan((1.0 / f2) * System.Math.Tan(Latitude));
            var geocentricCoords = ToCartesianCoordinates();
            double x = geocentricCoords.X;
            double y = geocentricCoords.Y;
            double e2 = 2 * flattening - System.Math.Pow(flattening, 2);
            double p = System.Math.Sqrt(System.Math.Pow(x, 2) + System.Math.Pow(y, 2));
            double altitude = (p / System.Math.Cos(lat)) - equatorialRadius / System.Math.Sqrt(1 - e2 * System.Math.Pow(System.Math.Sin(lat), 2));
            return new Planetodetic(Longitude, lat, altitude);
        }

        /// <summary>
        /// Compute radius to ellipsoid from planetocentric latitude
        /// </summary>
        /// <param name="equatorialRadius"></param>
        /// <param name="flattening"></param>
        /// <returns></returns>
        public double RadiusFromPlanetocentricLatitude(double equatorialRadius, double flattening)
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
        public static double RadiusFromPlanetocentricLatitude(double latitude, double equatorialRadius, double flattening)
        {
            double r2 = equatorialRadius * equatorialRadius;
            double s2 = System.Math.Sin(latitude) * System.Math.Sin(latitude);
            double f2 = (1 - flattening) * (1 - flattening);
            return System.Math.Sqrt(r2 / (1 + (1 / f2 - 1) * s2));
        }

        /// <summary>
        /// Convert to cartesian coordinates
        /// </summary>
        /// <returns></returns>
        public Vector3 ToCartesianCoordinates()
        {
            var x = Radius * System.Math.Cos(Longitude) * System.Math.Cos(Latitude);
            var y = Radius * System.Math.Sin(Longitude) * System.Math.Cos(Latitude);
            var z = Radius * System.Math.Sin(Latitude);
            return new Vector3(x, y, z);
        }
    }
}