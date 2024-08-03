namespace IO.Astrodynamics;

public static class Constants
{
    /// <summary>
    /// Gravitational constant m3/kg/s2
    /// </summary>
    public const double G = 6.67430e-11;

    public const double Deg2Rad = System.Math.PI / 180.0;
    public const double Rad2Deg = 180.0 / System.Math.PI;
    public const double _2PI = 2.0 * System.Math.PI;
    public const double PI2 = 0.5 * System.Math.PI;
    public const double PI = System.Math.PI;
    public const double g0 = 9.80665;
    public const double AngularTolerance = 0.00174533;
    public const double CivilTwilight = 6.0 * Deg2Rad;
    public const double NauticalTwilight = 12.0 * Deg2Rad;
    public const double AstronomicalTwilight = 18.0 * Deg2Rad;
    public const double Parsec2Meters = 3.085677581E+16;
    public const double SolarMeanRadiativeLuminosity = 3.828E+26;
    public const double C = 299792458.0;
    public const double Kelvin = 273.15;
}