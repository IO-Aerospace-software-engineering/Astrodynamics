namespace IO.Astrodynamics.Coordinates
{
    public readonly record struct Planetodetic(double Longitude, double Latitude, double Altitude)
    {
        /// <summary>
        /// Convert to planetocentric coordinates
        /// </summary>
        /// <param name="flattening"></param>
        /// <param name="equatorialRadius"></param>
        /// <returns></returns>
        public Planetocentric ToPlanetocentric(double flattening, double equatorialRadius)
        {
            double f2 = (1 - flattening) * (1 - flattening);
            double lat = System.Math.Atan(f2 * System.Math.Tan(Latitude));
            double n = equatorialRadius / System.Math.Sqrt(1.0 - 0.00669437999014 * System.Math.Sin(Latitude) * System.Math.Sin(Latitude));
            double x = (n + Altitude) * System.Math.Cos(Latitude) * System.Math.Cos(Longitude);
            double y = (n + Altitude) * System.Math.Cos(Latitude) * System.Math.Sin(Longitude);
            double z = (n * (1.0 - (2 * flattening - flattening * flattening)) + Altitude) * System.Math.Sin(Latitude);
            return new Planetocentric(Longitude, lat, System.Math.Sqrt(x * x + y * y + z * z));
        }
    }
}