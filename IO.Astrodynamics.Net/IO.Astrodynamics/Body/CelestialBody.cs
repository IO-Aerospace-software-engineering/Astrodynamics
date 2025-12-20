using System;
using System.Globalization;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Vector3 = IO.Astrodynamics.Math.Vector3;


namespace IO.Astrodynamics.Body;

public class CelestialBody : CelestialItem, IOrientable<Frame>
{
    /// <summary>
    /// Gets the polar radius of the celestial body.
    /// </summary>
    public double PolarRadius { get; }

    /// <summary>
    /// Gets the equatorial radius of the celestial body.
    /// </summary>
    public double EquatorialRadius { get; }

    /// <summary>
    /// Gets the flattening of the celestial body.
    /// </summary>
    public double Flattening { get; }

    /// <summary>
    /// Gets or sets the sphere of influence of the celestial body.
    /// </summary>
    public double SphereOfInfluence { get; private set; }

    /// <summary>
    /// Gets the reference frame of the celestial body.
    /// </summary>
    public Frame Frame { get; }

    /// <summary>
    /// Gets the J2 coefficient of the celestial body.
    /// </summary>
    public double J2 { get; }

    /// <summary>
    /// Gets the J3 coefficient of the celestial body.
    /// </summary>
    public double J3 { get; }

    /// <summary>
    /// Gets the J4 coefficient of the celestial body.
    /// </summary>
    public double J4 { get; }

    /// <summary>
    /// Gets the atmospheric model of the celestial body.
    /// </summary>
    protected AtmosphericModel AtmosphericModel { get; }

    /// <summary>
    /// Gets a value indicating whether the celestial body has an atmospheric model.
    /// </summary>
    public bool HasAtmosphericModel => AtmosphericModel != null;

    /// <summary>
    /// Instantiate celestial body from naif object with default parameters (Ecliptic J2000 at J2000 epoch)
    /// </summary>
    /// <param name="naifObject"></param>
    /// <param name="geopotentialModelParameters"></param>
    /// <param name="atmosphericModel"></param>
    public CelestialBody(NaifObject naifObject, GeopotentialModelParameters geopotentialModelParameters = null, AtmosphericModel atmosphericModel = null) : this(naifObject.NaifId,
        geopotentialModelParameters, atmosphericModel)
    {
    }

    /// <summary>
    /// Instantiate celestial body from naif id with default parameters (Ecliptic J2000 at J2000 epoch)
    /// </summary>
    /// <param name="naifId"></param>
    /// <param name="geopotentialModelParameters"></param>
    /// <param name="atmosphericModel"></param>
    public CelestialBody(int naifId, GeopotentialModelParameters geopotentialModelParameters = null, AtmosphericModel atmosphericModel = null) : this(naifId, Frame.ECLIPTIC_J2000,
        TimeSystem.Time.J2000TDB, geopotentialModelParameters, atmosphericModel)
    {
    }

    /// <summary>
    /// Instantiate celestial body from naif object with orbital parameters at given frame and epoch
    /// </summary>
    /// <param name="naifObject"></param>
    /// <param name="frame"></param>
    /// <param name="epoch"></param>
    /// <param name="geopotentialModelParameters"></param>
    /// <param name="atmosphericModel"></param>
    public CelestialBody(NaifObject naifObject, Frame frame, Time epoch, GeopotentialModelParameters geopotentialModelParameters = null, AtmosphericModel atmosphericModel = null)
        : this(naifObject.NaifId, frame, epoch, geopotentialModelParameters, atmosphericModel)
    {
    }

    /// <summary>
    /// Instantiate celestial body from naif id with orbital parameters at given frame and epoch
    /// </summary>
    /// <param name="naifId"></param>
    /// <param name="frame"></param>
    /// <param name="epoch"></param>
    /// <param name="geopotentialModelParameters"></param>
    /// <param name="atmosphericModel"></param>
    public CelestialBody(int naifId, Frame frame, Time epoch, GeopotentialModelParameters geopotentialModelParameters = null, AtmosphericModel atmosphericModel = null) :
        base(naifId, frame, epoch, geopotentialModelParameters)
    {
        PolarRadius = ExtendedInformation.Radii.Z;
        EquatorialRadius = ExtendedInformation.Radii.X;
        Flattening = (EquatorialRadius - PolarRadius) / EquatorialRadius;
        J2 = ExtendedInformation.J2;
        J3 = ExtendedInformation.J3;
        J4 = ExtendedInformation.J4;
        if (double.IsNaN(Flattening))
        {
            Flattening = 0.0;
        }

        Frame = string.IsNullOrEmpty(ExtendedInformation.FrameName)
            ? throw new InvalidOperationException(
                "Celestial celestialItem frame can't be defined, please check if you have loaded associated kernels")
            : new Frame(ExtendedInformation.FrameName);

        UpdateSphereOfInfluence();

        AtmosphericModel = atmosphericModel;
    }

    /// <summary>
    /// Instantiate celestial body from custom parameters
    /// </summary>
    /// <param name="naifId"></param>
    /// <param name="name"></param>
    /// <param name="mass"></param>
    /// <param name="polarRadius"></param>
    /// <param name="equatorialRadius"></param>
    /// <param name="initialOrbitalParameters"></param>
    protected CelestialBody(int naifId, string name, double mass, double polarRadius = 0.0, double equatorialRadius = 0.0,
        OrbitalParameters.OrbitalParameters initialOrbitalParameters = null) : base(naifId, name, mass, initialOrbitalParameters)
    {
        PolarRadius = polarRadius;
        EquatorialRadius = equatorialRadius;
        Flattening = (EquatorialRadius - PolarRadius) / EquatorialRadius;
        if (double.IsNaN(Flattening))
        {
            Flattening = 0.0;
        }

        SphereOfInfluence = double.PositiveInfinity;
    }

    private void UpdateSphereOfInfluence()
    {
        SphereOfInfluence = double.PositiveInfinity;
        if (InitialOrbitalParameters == null) return;
        if (IsSun)
        {
            SphereOfInfluence = double.PositiveInfinity;
            return;
        }

        var mainBody = new CelestialBody(ExtendedInformation.CenterOfMotionId, Frame.ECLIPTIC_J2000, InitialOrbitalParameters.Epoch);
        var a = this.GetEphemeris(InitialOrbitalParameters.Epoch, mainBody, Frame.ECLIPTIC_J2000, Aberration.None).SemiMajorAxis();
        SphereOfInfluence = InitialOrbitalParameters != null ? SphereOfInluence(a, Mass, mainBody.Mass) : double.PositiveInfinity;
    }

    private static double SphereOfInluence(double a, double minorMass, double majorMass)
    {
        return a * System.Math.Pow(minorMass / majorMass, 2.0 / 5.0);
    }

    /// <summary>
    /// Compute celestialItem radius from geocentric latitude
    /// </summary>
    /// <param name="latitude">Geocentric latitude</param>
    /// <returns></returns>
    public double RadiusFromPlanetocentricLatitude(double latitude)
    {
        double r2 = EquatorialRadius * EquatorialRadius;
        double s2 = System.Math.Sin(latitude) * System.Math.Sin(latitude);
        double f2 = (1 - Flattening) * (1 - Flattening);
        return System.Math.Sqrt(r2 / (1 + (1 / f2 - 1) * s2));
    }


    /// <summary>
    /// Get orientation relative to reference frame
    /// </summary>
    /// <param name="referenceFrame"></param>
    /// <param name="epoch"></param>
    /// <returns></returns>
    public StateOrientation GetOrientation(Frame referenceFrame, in Time epoch)
    {
        return referenceFrame.ToFrame(Frame, epoch);
    }

    public TimeSpan SideralRotationPeriod(Time epoch)
    {
        return TimeSpan.FromSeconds(Constants._2PI / GetOrientation(Frame.ICRF, epoch).AngularVelocity.Magnitude());
    }

    public double AngularVelocity(Time epoch)
    {
        var siderealRotationPeriod = SideralRotationPeriod(epoch);
        return (2.0 * Constants.PI) / siderealRotationPeriod.TotalSeconds;
    }

    public KeplerianElements GeosynchronousOrbit(double longitude, double latitude, Time epoch)
    {
        var sideralRotation2 = System.Math.Pow(SideralRotationPeriod(epoch).TotalSeconds, 2);
        var radius = System.Math.Cbrt((GM * sideralRotation2) / (4 * Constants.PI * Constants.PI));
        var bodyfFixedCoordinates = new Planetocentric(longitude, latitude, radius).ToCartesianCoordinates();
        var icrfPos = bodyfFixedCoordinates.Rotate(Frame.ToFrame(Frame.ICRF, epoch).Rotation);
        var icrfRot = Vector3.VectorZ.Rotate(Frame.ToFrame(Frame.ICRF, epoch).Rotation);
        var inertialVelocity = icrfRot.Cross(icrfPos).Normalize() * System.Math.Sqrt(GM / radius);
        var sv = new StateVector(icrfPos, inertialVelocity, this, epoch, Frame.ICRF);
        return new KeplerianElements(radius, 0.0, sv.Inclination(), sv.AscendingNode(), (sv.ArgumentOfPeriapsis() + sv.MeanAnomaly()) % Constants._2PI, 0.0, this, epoch, sv.Frame);
    }

    /// <summary>
    /// Calculates the Keplerian elements for a heliosynchronous
    /// orbit.
    /// </summary>
    /// <param name="semiMajorAxis">The semi-major axis of the orbit.</param>
    /// <param name="eccentricity">The eccentricity of the orbit.</param>
    /// <param name="epochAtDescendingNode">The epoch at the descending node.</param>
    /// <returns>The Keplerian elements for the heliosynchronous orbit.</returns>
    /// <exception cref="System.ArgumentException">Thrown when
    /// the orbit perigee is lower than the equatorial radius.</exception>
    public KeplerianElements HelioSynchronousOrbit(double semiMajorAxis, double eccentricity, Time epochAtDescendingNode)
    {
        CelestialBody sun = Stars.SUN_BODY;
        double p = semiMajorAxis * (1 - eccentricity);
        if (p < EquatorialRadius)
        {
            throw new ArgumentException("Orbit perigee is lower than equatorial radius");
        }

        double a72 = System.Math.Pow(semiMajorAxis, 3.5);
        double e2 = eccentricity * eccentricity;
        double e22 = (1 - e2) * (1 - e2);
        double sqrtGM = System.Math.Sqrt(GM);
        double re2 = EquatorialRadius * EquatorialRadius;
        var ephemeris = GetEphemeris(epochAtDescendingNode, sun, Frame.ICRF, Aberration.LT);
        double i = System.Math.Acos((2.0 * a72 * e22 * ephemeris.MeanMotion()) / (3.0 * sqrtGM * -J2 * re2));

        var sunVector = ephemeris.ToStateVector().Position.Inverse();
        Math.Plane sunPlane = new Math.Plane(Vector3.VectorZ.Rotate(Frame.ToFrame(Frame.ICRF, epochAtDescendingNode).Rotation).Cross(sunVector), 0.0);
        double raanLongitude = sunPlane.GetAngle(Vector3.VectorY);

        if (sunVector.Y > 0.0)
        {
            raanLongitude *= -1.0;
        }

        //Make raan in range 0.0->2PI
        if (raanLongitude < 0.0)
        {
            raanLongitude += Constants._2PI;
        }

        var trueAnomaly = Constants.PI + Constants.PI2;

        double m = OrbitalParameters.OrbitalParameters.TrueAnomalyToMeanAnomaly(trueAnomaly, eccentricity,
            OrbitalParameters.OrbitalParameters.EllipticAnomaly(trueAnomaly, eccentricity));

        return new KeplerianElements(semiMajorAxis, eccentricity, i, raanLongitude, Constants.PI + Constants.PI2, m, this, epochAtDescendingNode, Frame.ICRF);
    }

    /// <summary>
    /// Calculate the true solar day for a given epoch.
    /// </summary>
    /// <param name="epoch">The epoch for which to calculate the true solar day.</param>
    /// <returns>The duration of the true solar day.</returns>
    /// <remarks>
    /// This method only works with planets.
    /// It throws an <see cref="InvalidOperationException"/> if the current celestial body is not a planet.
    /// It calculates the sideral rotation period and uses it to determine the angle of rotation between two points in the celestial body
    /// 's ephemeris.
    /// Finally, it returns the sideral rotation period plus the time it takes to rotate toward the sun using the angular velocity of
    /// the celestial body at the given epoch.
    /// </remarks>
    public TimeSpan TrueSolarDay(Time epoch)
    {
        if (!this.IsPlanet)
        {
            throw new InvalidOperationException("At this time, the computation of true solar day works only with planets");
        }

        CelestialBody sun = Stars.SUN_BODY;
        var sideralRotation = SideralRotationPeriod(epoch);
        var eph0 = this.GetEphemeris(epoch, sun, Frame.ECLIPTIC_J2000, Aberration.LT).ToStateVector().Position;
        var eph1 = this.GetEphemeris(epoch + sideralRotation, sun, Frame.ECLIPTIC_J2000, Aberration.LT).ToStateVector().Position;
        var angle = eph0.Angle(eph1);
        return sideralRotation + TimeSpan.FromSeconds(angle / GetOrientation(Frame.ICRF, epoch).AngularVelocity.Magnitude());
    }

    /// <summary>
    /// Calculates the Keplerian elements of a phased heliosynchronous
    /// orbit
    /// </summary>
    /// <param name="eccentricity">The eccentricity of the orbit</param>
    /// <param name="epochAtDescendingNode">The epoch at the descending node</param>
    /// <param name="nbOrbitPerDay">The number of orbits per day</param>
    /// <returns>The calculated Keplerian elements</returns>
    public KeplerianElements HelioSynchronousOrbit(double eccentricity, Time epochAtDescendingNode, int nbOrbitPerDay)
    {
        var trueSolarDay = TrueSolarDay(epochAtDescendingNode);
        double t = trueSolarDay.TotalSeconds / nbOrbitPerDay;
        double a = System.Math.Cbrt(((t * t) * GM) / (4 * Constants.PI * Constants.PI));
        return HelioSynchronousOrbit(a, eccentricity, epochAtDescendingNode);
    }


    /// <summary>
    /// Get temperature at given altitude
    /// </summary>
    /// <param name="altitude"></param>
    /// <returns></returns>
    public double GetAirTemperature(double altitude)
    {
        return AtmosphericModel?.GetTemperature(altitude) ?? double.NaN;
    }

    /// <summary>
    /// Get air pressure at given altitude
    /// </summary>
    /// <param name="altitude"></param>
    /// <returns></returns>
    public double GetAirPressure(double altitude)
    {
        return AtmosphericModel?.GetPressure(altitude) ?? 0.0;
    }

    /// <summary>
    /// Get air density at given altitude
    /// </summary>
    /// <param name="altitude"></param>
    /// <returns></returns>
    public double GetAirDensity(double altitude)
    {
        return AtmosphericModel?.GetDensity(altitude) ?? 0.0;
    }

    public override string ToString()
    {
        string bodyType = string.Empty;
        if (IsPlanet)
        {
            bodyType = "Planet";
        }
        else if (IsMoon)
        {
            bodyType = "Moon";
        }
        else if (IsAsteroid)
        {
            bodyType = "Asteroid";
        }
        else if (IsBarycenter)
        {
            bodyType = "Barycenter";
        }
        else if (IsStar || IsSun)
        {
            bodyType = "Star";
        }
        else if (IsLagrangePoint)
        {
            bodyType = "Lagrange point";
        }

        return $"{"Type :",TITLE_WIDTH} {bodyType,-VALUE_WIDTH}{Environment.NewLine}" +
               base.ToString() +
               $"{"Fixed frame :",TITLE_WIDTH} {Frame.Name,-VALUE_WIDTH}{Environment.NewLine}" +
               $"{"Equatorial radius (m) :",TITLE_WIDTH} {EquatorialRadius.ToString("E", CultureInfo.InvariantCulture),-VALUE_WIDTH:E}{Environment.NewLine}" +
               $"{"Polar radius (m) :",TITLE_WIDTH} {PolarRadius.ToString("E", CultureInfo.InvariantCulture),-VALUE_WIDTH:E}{Environment.NewLine}" +
               $"{"Flattening :",TITLE_WIDTH} {Flattening.ToString(CultureInfo.InvariantCulture),-VALUE_WIDTH}{Environment.NewLine}" +
               $"{"J2 :",TITLE_WIDTH} {J2.ToString(CultureInfo.InvariantCulture),-VALUE_WIDTH}{Environment.NewLine}" +
               $"{"J3 :",TITLE_WIDTH} {J3.ToString(CultureInfo.InvariantCulture),-VALUE_WIDTH}{Environment.NewLine}" +
               $"{"J4 :",TITLE_WIDTH} {J4.ToString(CultureInfo.InvariantCulture),-VALUE_WIDTH}{Environment.NewLine}";
    }
}