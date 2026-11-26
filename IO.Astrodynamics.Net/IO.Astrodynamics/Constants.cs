namespace IO.Astrodynamics;

public static class Constants
{
    /// <summary>
    /// Gravitational constant m3/kg/s2 (<c>G</c> before 11.0.0, R27 of #668).
    /// </summary>
    public const double GravitationalConstant = 6.67430e-11;

    public const double AU = 149597870700.0;
    public const double Deg2Rad = System.Math.PI / 180.0;
    public const double Rad2Deg = 180.0 / System.Math.PI;
    /// <summary>
    /// 2π (<c>_2PI</c> before 11.0.0, R24 of #668).
    /// </summary>
    public const double TwoPi = 2.0 * System.Math.PI;
    /// <summary>
    /// π/2 (<c>PI2</c> before 11.0.0, R24 of #668).
    /// </summary>
    public const double HalfPi = 0.5 * System.Math.PI;
    public const double PI = System.Math.PI;
    /// <summary>
    /// Standard gravity g0 in m/s2 (<c>g0</c> before 11.0.0, R27 of #668).
    /// </summary>
    public const double StandardGravity = 9.80665;
    public const double AngularTolerance = 0.00174533;
    public const double CivilTwilight = 6.0 * Deg2Rad;
    public const double NauticalTwilight = 12.0 * Deg2Rad;
    public const double AstronomicalTwilight = 18.0 * Deg2Rad;

    /// <summary>
    /// The parsec in meters: 648000/π astronomical units (IAU 2015 Resolution B2) of 149597870700 m (IAU 2012 Resolution
    /// B2), 30856775814913672.789 m, the nearest double. Before 11.0.0 it was 3.085677581e16 m, 1.59e-10 relative too small
    /// (#733).
    /// </summary>
    public const double Parsec2Meters = 3.0856775814913673E+16;
    public const double SolarMeanRadiativeLuminosity = 3.828E+26;
    /// <summary>
    /// Speed of light in vacuum in m/s (<c>C</c> before 11.0.0, R27 of #668).
    /// </summary>
    public const double SpeedOfLight = 299792458.0;
    public const double StefanBoltzmann = 5.670374419e-8; // W m^-2 K^-4
    /// <summary>
    /// 0 °C in kelvin, the offset between the two scales (<c>Kelvin</c> before 11.0.0, R27 of #668).
    /// </summary>
    public const double ZeroCelsiusInKelvin = 273.15;
}