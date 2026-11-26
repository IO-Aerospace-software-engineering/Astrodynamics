using System;
using System.Globalization;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.DataProvider;
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
    public double PolarRadius => IsDeferred ? BodyData.PolarRadius : _polarRadius;

    private readonly double _polarRadius;

    /// <summary>
    /// Gets the equatorial radius of the celestial body.
    /// </summary>
    public double EquatorialRadius => IsDeferred ? BodyData.EquatorialRadius : _equatorialRadius;

    private readonly double _equatorialRadius;

    /// <summary>
    /// Gets the flattening of the celestial body.
    /// </summary>
    public double Flattening => IsDeferred ? BodyData.Flattening : _flattening;

    private readonly double _flattening;

    /// <summary>
    /// Gets the semi-axes of the body's reference ellipsoid along the X, Y and Z axes of its body-fixed frame, in
    /// meters: the <c>BODYnnn_RADII</c> of the kernels, or (equatorial, equatorial, polar) for a body built from its
    /// radii. The occultation searches model the body as this ellipsoid.
    /// </summary>
    public Vector3 Radii => IsDeferred ? BodyData.Radii : _radii;

    private readonly Vector3 _radii;

    /// <summary>
    /// Gets or sets the sphere of influence of the celestial body.
    /// </summary>
    /// <remarks>
    /// +Infinity for a body without a heliocentric orbit: the Sun, a body built from custom parameters, and a body whose
    /// <see cref="CelestialItem.InitialOrbitalParameters"/> is null because the loaded kernels do not chain it to its
    /// system barycenter (#454).
    /// </remarks>
    public double SphereOfInfluence
    {
        get => IsDeferred ? BodyData.SphereOfInfluence : _sphereOfInfluence;
        private set => _sphereOfInfluence = value;
    }

    private double _sphereOfInfluence;

    /// <summary>
    /// Gets the reference frame of the celestial body.
    /// </summary>
    public Frame Frame => IsDeferred ? BodyData.Frame : _frame;

    private readonly Frame _frame;

    /// <summary>
    /// Gets the J2 coefficient of the celestial body.
    /// </summary>
    public double J2 => IsDeferred ? BodyData.J2 : _j2;

    private readonly double _j2;

    /// <summary>
    /// Gets the J3 coefficient of the celestial body.
    /// </summary>
    public double J3 => IsDeferred ? BodyData.J3 : _j3;

    private readonly double _j3;

    /// <summary>
    /// Gets the J4 coefficient of the celestial body.
    /// </summary>
    public double J4 => IsDeferred ? BodyData.J4 : _j4;

    private readonly double _j4;

    /// <summary>
    /// Gets the atmospheric model given to the celestial body, null when none was given.
    /// </summary>
    /// <remarks>
    /// The atmosphere of the body is this model, or for an Earth without one NRLMSISE-00 at nominal space weather: an
    /// Earth always has an atmosphere, wherever the library decides whether a body has one (drag about the central body,
    /// in SSB mode and from a listed non-central body, <see cref="Propagator.Forces.AtmosphericDrag"/>, and the getters
    /// with a full context). The getters of a model-less Earth given a context that holds the altitude but not the
    /// geodetic position and the epoch answer with the U.S. Standard Atmosphere 1976, the base atmosphere (decided by
    /// S. Guillet, 2026-11-14); a null context, which holds no altitude, gives no atmosphere. Another body without a model
    /// has none (#674).
    /// </remarks>
    protected IAtmosphericModel AtmosphericModel { get; }

    /// <summary>
    /// Gets the bond albedo of the celestial body (fraction of incident solar radiation reflected).
    /// Value of 0.0 means not configured. Typical values: Earth ~0.306, Moon ~0.12.
    /// </summary>
    public double Albedo { get; }

    /// <summary>
    /// Gets the effective radiative temperature of the celestial body in Kelvin.
    /// Value of 0.0 means not configured. Typical values: Earth ~254 K, Moon ~270 K.
    /// </summary>
    public double ThermalEffectiveTemperature { get; }

    /// <summary>
    /// Gets the average thermal emissivity of the celestial body (0.0-1.0).
    /// Value of 0.0 means not configured. Typical values: Earth ~0.95, Moon ~0.95.
    /// </summary>
    public double ThermalEmissivity { get; }

    /// <summary>
    /// Gets a value indicating whether the celestial body was given an atmospheric model, not whether it has an
    /// atmosphere: an Earth without one still has NRLMSISE-00 at nominal space weather, read by its drag, central or
    /// listed, and with a full context by its getters (#674), and the U.S. Standard Atmosphere 1976 for a context without
    /// the geodetic position or the epoch (decided by S. Guillet, 2026-11-14).
    /// </summary>
    public bool HasAtmosphericModel => AtmosphericModel != null;

    /// <summary>
    /// The atmosphere of the body, the one its drag reads: its atmospheric model, or for an Earth without one NRLMSISE-00
    /// at nominal space weather (one instance per thread); null for another body without a model. Read by
    /// <see cref="Propagator.Forces.AtmosphericDrag"/> and the description of a force model (CDM export) (#674), and
    /// reported by <see cref="Propagator.PropagationSolution.Options"/> (public since 11.0.0, R06 of #668, amendment of D4
    /// decided by S. Guillet, 2026-11-19).
    /// </summary>
    public IAtmosphericModel Atmosphere => AtmosphericModel ?? (NaifId == EarthNaifId ? EarthDefaultAtmosphere.Model : null);

    /// <summary>
    /// The model that answers <paramref name="context"/>: the atmospheric model of the body; for an Earth without one,
    /// NRLMSISE-00 at nominal space weather when the context holds the geodetic position and the epoch, which that model
    /// needs, and otherwise the U.S. Standard Atmosphere 1976, which needs the altitude only (an Earth always has at least
    /// the base atmosphere, decided by S. Guillet, 2026-11-14); null for a null context, which holds no altitude, and for
    /// another body without a model.
    /// </summary>
    private IAtmosphericModel AtmosphereFor(IAtmosphericContext context)
    {
        if (AtmosphericModel != null)
        {
            return AtmosphericModel;
        }

        if (NaifId != EarthNaifId || context == null)
        {
            return null;
        }

        return context is { GeodeticLatitude: not null, GeodeticLongitude: not null, Epoch: not null }
            ? EarthDefaultAtmosphere.Model
            : EarthDefaultAtmosphere.Standard;
    }

    /// <summary>
    /// Instantiate celestial body from naif object with default parameters (Ecliptic J2000 at J2000 epoch)
    /// </summary>
    /// <param name="naifObject"></param>
    /// <param name="geopotentialModelParameters"></param>
    /// <param name="atmosphericModel"></param>
    /// <param name="albedo">The bond albedo (<see cref="Albedo"/>), 0 when not configured.</param>
    /// <param name="thermalEffectiveTemperature">The effective radiative temperature in K (<see cref="ThermalEffectiveTemperature"/>), 0 when not configured.</param>
    /// <param name="thermalEmissivity">The thermal emissivity, 0 to 1 (<see cref="ThermalEmissivity"/>), 0 when not configured.</param>
    public CelestialBody(NaifObject naifObject, GeopotentialModelParameters geopotentialModelParameters = null, IAtmosphericModel atmosphericModel = null, double albedo = 0.0,
        double thermalEffectiveTemperature = 0.0, double thermalEmissivity = 0.0) : this(naifObject.NaifId,
        geopotentialModelParameters, atmosphericModel, albedo, thermalEffectiveTemperature, thermalEmissivity)
    {
    }

    /// <summary>
    /// Instantiate celestial body from naif id with default parameters (Ecliptic J2000 at J2000 epoch)
    /// </summary>
    /// <param name="naifId"></param>
    /// <param name="geopotentialModelParameters"></param>
    /// <param name="atmosphericModel"></param>
    /// <param name="albedo">The bond albedo (<see cref="Albedo"/>), 0 when not configured.</param>
    /// <param name="thermalEffectiveTemperature">The effective radiative temperature in K (<see cref="ThermalEffectiveTemperature"/>), 0 when not configured.</param>
    /// <param name="thermalEmissivity">The thermal emissivity, 0 to 1 (<see cref="ThermalEmissivity"/>), 0 when not configured.</param>
    public CelestialBody(int naifId, GeopotentialModelParameters geopotentialModelParameters = null, IAtmosphericModel atmosphericModel = null, double albedo = 0.0,
        double thermalEffectiveTemperature = 0.0, double thermalEmissivity = 0.0) : this(naifId, Frame.ECLIPTIC_J2000,
        TimeSystem.Time.J2000TDB, geopotentialModelParameters, atmosphericModel, albedo, thermalEffectiveTemperature, thermalEmissivity)
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
    /// <param name="albedo">The bond albedo (<see cref="Albedo"/>), 0 when not configured.</param>
    /// <param name="thermalEffectiveTemperature">The effective radiative temperature in K (<see cref="ThermalEffectiveTemperature"/>), 0 when not configured.</param>
    /// <param name="thermalEmissivity">The thermal emissivity, 0 to 1 (<see cref="ThermalEmissivity"/>), 0 when not configured.</param>
    public CelestialBody(NaifObject naifObject, Frame frame, Time epoch, GeopotentialModelParameters geopotentialModelParameters = null, IAtmosphericModel atmosphericModel = null, double albedo = 0.0,
        double thermalEffectiveTemperature = 0.0, double thermalEmissivity = 0.0)
        : this(naifObject.NaifId, frame, epoch, geopotentialModelParameters, atmosphericModel, albedo, thermalEffectiveTemperature, thermalEmissivity)
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
    /// <param name="albedo">The bond albedo (<see cref="Albedo"/>), 0 when not configured.</param>
    /// <param name="thermalEffectiveTemperature">The effective radiative temperature in K (<see cref="ThermalEffectiveTemperature"/>), 0 when not configured.</param>
    /// <param name="thermalEmissivity">The thermal emissivity, 0 to 1 (<see cref="ThermalEmissivity"/>), 0 when not configured.</param>
    public CelestialBody(int naifId, Frame frame, Time epoch, GeopotentialModelParameters geopotentialModelParameters = null, IAtmosphericModel atmosphericModel = null, double albedo = 0.0,
        double thermalEffectiveTemperature = 0.0, double thermalEmissivity = 0.0) :
        base(naifId, frame, epoch, geopotentialModelParameters)
    {
        var data = ReadBodyData(new BodyProviderData(), ExtendedInformation, Mass, InitialOrbitalParameters);
        _polarRadius = data.PolarRadius;
        _equatorialRadius = data.EquatorialRadius;
        _radii = data.Radii;
        _flattening = data.Flattening;
        _j2 = data.J2;
        _j3 = data.J3;
        _j4 = data.J4;
        _frame = data.Frame;
        _sphereOfInfluence = data.SphereOfInfluence;

        AtmosphericModel = atmosphericModel;
        Albedo = albedo;
        ThermalEffectiveTemperature = thermalEffectiveTemperature;
        ThermalEmissivity = thermalEmissivity;
        RegisterWithObserver();
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
    /// <param name="albedo">The bond albedo (<see cref="Albedo"/>), 0 when not configured.</param>
    /// <param name="thermalEffectiveTemperature">The effective radiative temperature in K (<see cref="ThermalEffectiveTemperature"/>), 0 when not configured.</param>
    /// <param name="thermalEmissivity">The thermal emissivity, 0 to 1 (<see cref="ThermalEmissivity"/>), 0 when not configured.</param>
    protected CelestialBody(int naifId, string name, double mass, double polarRadius = 0.0, double equatorialRadius = 0.0,
        OrbitalParameters.OrbitalParameters initialOrbitalParameters = null, double albedo = 0.0,
        double thermalEffectiveTemperature = 0.0, double thermalEmissivity = 0.0) : base(naifId, name, mass, initialOrbitalParameters)
    {
        _polarRadius = polarRadius;
        _equatorialRadius = equatorialRadius;
        _radii = new Vector3(equatorialRadius, equatorialRadius, polarRadius);
        _flattening = (equatorialRadius - polarRadius) / equatorialRadius;
        if (double.IsNaN(_flattening))
        {
            _flattening = 0.0;
        }

        SphereOfInfluence = double.PositiveInfinity;
        Albedo = albedo;
        ThermalEffectiveTemperature = thermalEffectiveTemperature;
        ThermalEmissivity = thermalEmissivity;
        RegisterWithObserver();
    }

    /// <summary>
    /// A static object of <see cref="PlanetsAndMoons"/> or <see cref="Stars"/>: its identifiers and name without any read
    /// of the data provider, at J2000 TDB in ECLIPJ2000, without geopotential or atmospheric model; its provider data
    /// (information, GM, radii, J2 to J4, frame, orbit and sphere of influence) are read at the first read of a member
    /// that needs them, retried on failure (see <see cref="CelestialItem.Mass"/>).
    /// </summary>
    internal static CelestialBody CreateDeferred(NaifObject naifObject) => CreateDeferred(naifObject, Frame.ECLIPTIC_J2000, TimeSystem.Time.J2000TDB);

    /// <summary>
    /// A body whose provider data are read at the first read of a member that needs them, as a static object's, its orbit
    /// about the barycentre of its system at <paramref name="epoch"/> in <paramref name="frame"/>: the Earth of a TLE,
    /// which reads no kernel when it is built (R21 of #668).
    /// </summary>
    internal static CelestialBody CreateDeferred(NaifObject naifObject, Frame frame, Time epoch) =>
        new(naifObject, frame, epoch, deferred: true);

    // The constructor of CreateDeferred: the parameter deferred tells it apart from the public constructors
    private CelestialBody(NaifObject naifObject, Frame frame, Time epoch, bool deferred) : base(naifObject, frame, epoch)
    {
        _ = deferred;
    }

    /// <summary>The provider data of a body: those of its item, its radii, J2 to J4, frame and sphere of influence.</summary>
    private sealed class BodyProviderData : ProviderData
    {
        internal double PolarRadius { get; set; }
        internal double EquatorialRadius { get; set; }
        internal Vector3 Radii { get; set; }
        internal double Flattening { get; set; }
        internal double J2 { get; set; }
        internal double J3 { get; set; }
        internal double J4 { get; set; }
        internal Frame Frame { get; set; }
        internal double SphereOfInfluence { get; set; }

        // The radii, J2 to J4 and the frame are those of the information; the sphere of influence also reads the mass and
        // the orbit of the main body
        internal override bool HasSameValues(ProviderData other) =>
            base.HasSameValues(other) && SphereOfInfluence.Equals(((BodyProviderData)other).SphereOfInfluence);
    }

    private BodyProviderData BodyData => (BodyProviderData)Resolved;

    /// <summary>The data of a static body: those of its item and its radii (<c>BODYnnn_RADII</c> on SPICE).</summary>
    private protected override void RequireKernelData(CelestialBodyInfo information)
    {
        base.RequireKernelData(information);
        if (!(information.Radii.X > 0.0 && information.Radii.Y > 0.0 && information.Radii.Z > 0.0))
        {
            throw MissingKernelData("radii", $"BODY{NaifId}_RADII", "a PCK such as pck00011.tpc");
        }
    }

    private protected override ProviderData ReadProviderData(IDataProvider provider, Frame frame, in Time epoch)
    {
        var data = ReadItemData(new BodyProviderData { Provider = provider }, frame, epoch);
        return ReadBodyData(data, data.Information, data.Mass, data.InitialOrbitalParameters);
    }

    /// <summary>
    /// Fills the members of a body from its <paramref name="information"/>, as the natural-body constructor reads them,
    /// and its sphere of influence from its <paramref name="mass"/> and <paramref name="orbit"/>.
    /// </summary>
    private BodyProviderData ReadBodyData(BodyProviderData data, CelestialBodyInfo information, double mass,
        OrbitalParameters.OrbitalParameters orbit)
    {
        data.PolarRadius = information.Radii.Z;
        data.EquatorialRadius = information.Radii.X;
        data.Radii = information.Radii;
        double flattening = (data.EquatorialRadius - data.PolarRadius) / data.EquatorialRadius;
        data.Flattening = double.IsNaN(flattening) ? 0.0 : flattening;
        data.J2 = information.J2;
        data.J3 = information.J3;
        data.J4 = information.J4;
        data.Frame = string.IsNullOrEmpty(information.FrameName)
            ? throw new InvalidOperationException(
                "Celestial celestialItem frame can't be defined, please check if you have loaded associated kernels")
            : new Frame(information.FrameName);
        data.SphereOfInfluence = ReadSphereOfInfluence(information, mass, orbit);
        return data;
    }

    // The sphere of influence of a body of the given mass and orbit about the barycentre of its system: +Infinity without
    // an orbit and for the Sun
    private double ReadSphereOfInfluence(CelestialBodyInfo information, double mass, OrbitalParameters.OrbitalParameters orbit)
    {
        if (orbit == null || IsSun)
        {
            return double.PositiveInfinity;
        }

        var mainBody = new CelestialBody(information.CenterOfMotionId, Frame.ECLIPTIC_J2000, orbit.Epoch);
        var a = this.GetEphemeris(orbit.Epoch, mainBody, Frame.ECLIPTIC_J2000, Aberration.None).SemiMajorAxis();
        return SphereOfInluence(a, mass, mainBody.Mass);
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

    /// <summary>
    /// The sidereal rotation period of the body at <paramref name="epoch"/>: 2π / |ω|, ω the angular velocity of its frame
    /// relative to ICRF, to the nearest tick (100 ns), ties to even (#637, decided by S. Guillet, 2026-11-17; truncated to
    /// the tick below before 11.0.0).
    /// </summary>
    public TimeSpan SideralRotationPeriod(Time epoch)
    {
        return Time.SecondsToSpan(SideralRotationSeconds(epoch));
    }

    /// <summary>
    /// The sidereal rotation period of the body at <paramref name="epoch"/> in seconds, 2π / |ω|, before its rounding to a
    /// tick: the value <see cref="SideralRotationPeriod"/> rounds, and whose half (exact in binary) the launch windows add
    /// before they round their offset (#637).
    /// </summary>
    internal double SideralRotationSeconds(Time epoch)
    {
        return Constants.TwoPi / GetOrientation(Frame.ICRF, epoch).AngularVelocity.Magnitude();
    }

    public double AngularVelocity(Time epoch)
    {
        var siderealRotationPeriod = SideralRotationPeriod(epoch);
        return (2.0 * Constants.PI) / siderealRotationPeriod.TotalSeconds;
    }

    /// <summary>
    /// Computes the Keplerian elements for a geosynchronous orbit at the specified longitude and latitude.
    /// The latitude parameter defines the orbital inclination — for non-zero latitude, the satellite follows
    /// an inclined geosynchronous orbit tracing a figure-eight ground track. The specified (longitude, latitude)
    /// is the sub-satellite point only at the given epoch.
    /// The period of the orbit is the sidereal rotation period of the body frame at the epoch,
    /// <see cref="SideralRotationPeriod"/>, which depends on the orientation data loaded (for the Earth, the binary PCK
    /// of ITRF93); <see cref="GeosynchronousOrbit(double, double, Time, TimeSpan)"/> takes a declared period instead.
    /// </summary>
    /// <param name="longitude">Planetocentric longitude in radians, in range [-π, π].</param>
    /// <param name="latitude">Planetocentric latitude in radians, in range [-π/2, π/2]. Defines the orbital inclination.</param>
    /// <param name="epoch">The epoch at which to compute the orbit.</param>
    /// <returns>Keplerian elements for the geosynchronous orbit.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when longitude or latitude is out of range.</exception>
    public KeplerianElements GeosynchronousOrbit(double longitude, double latitude, Time epoch)
    {
        CheckGeosynchronousPoint(longitude, latitude);
        return GeosynchronousOrbitOfPeriod(longitude, latitude, epoch, SideralRotationPeriod(epoch));
    }

    /// <summary>
    /// Computes the Keplerian elements for a geosynchronous orbit of a declared period at the specified longitude and
    /// latitude: the circular orbit of radius a = (GM T² / 4π²)^(1/3), GM the body's, whose position at the epoch is
    /// the point (longitude, latitude, a) of the body frame and whose velocity, of magnitude √(GM / a), is normal to the
    /// rotation axis of the body frame and to the position. As for
    /// <see cref="GeosynchronousOrbit(double, double, Time)"/>, the latitude defines the inclination, the point is the
    /// sub-satellite point at the epoch only, the elements are in ICRF, and the argument of periapsis is the argument
    /// of latitude, with a mean anomaly of 0.
    /// </summary>
    /// <param name="longitude">Planetocentric longitude in radians, in range [-π, π].</param>
    /// <param name="latitude">Planetocentric latitude in radians, in range [-π/2, π/2]. Defines the orbital inclination.</param>
    /// <param name="epoch">The epoch at which to compute the orbit.</param>
    /// <param name="siderealRotationPeriod">The period of the orbit, a sidereal rotation period of the body given by the
    /// caller (for instance a constant such as 86164.0998101 s for the Earth), so that the radius does not depend on the
    /// orientation data at the epoch. It is used at the resolution of <see cref="TimeSpan"/>, 100 ns.</param>
    /// <returns>Keplerian elements for the geosynchronous orbit.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when longitude or latitude is out of range, or when the
    /// period is not positive.</exception>
    public KeplerianElements GeosynchronousOrbit(double longitude, double latitude, Time epoch, TimeSpan siderealRotationPeriod)
    {
        CheckGeosynchronousPoint(longitude, latitude);
        if (siderealRotationPeriod <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(siderealRotationPeriod), siderealRotationPeriod, "The sidereal rotation period must be positive.");

        return GeosynchronousOrbitOfPeriod(longitude, latitude, epoch, siderealRotationPeriod);
    }

    private static void CheckGeosynchronousPoint(double longitude, double latitude)
    {
        if (longitude < -Constants.PI || longitude > Constants.PI)
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be in range [-π, π] radians.");
        if (latitude < -Constants.HalfPi || latitude > Constants.HalfPi)
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be in range [-π/2, π/2] radians.");
    }

    private KeplerianElements GeosynchronousOrbitOfPeriod(double longitude, double latitude, Time epoch, TimeSpan siderealRotationPeriod)
    {
        var sideralRotation2 = System.Math.Pow(siderealRotationPeriod.TotalSeconds, 2);
        var radius = System.Math.Cbrt((GM * sideralRotation2) / (4 * Constants.PI * Constants.PI));
        var bodyfFixedCoordinates = new Planetocentric(longitude, latitude, radius).ToCartesianCoordinates();
        var toIcrf = Frame.ToFrame(Frame.ICRF, epoch).Rotation;
        var icrfPos = bodyfFixedCoordinates.Rotate(toIcrf);
        var icrfRot = Vector3.VectorZ.Rotate(toIcrf);
        var inertialVelocity = icrfRot.Cross(icrfPos).NormalizeOrZero() * System.Math.Sqrt(GM / radius);
        var sv = new StateVector(icrfPos, inertialVelocity, this, epoch, Frame.ICRF);
        return new KeplerianElements(radius, 0.0, sv.Inclination(), sv.AscendingNode(), sv.ArgumentOfPeriapsis() + sv.MeanAnomaly(), 0.0, this, epoch, sv.Frame);
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
            raanLongitude += Constants.TwoPi;
        }

        var trueAnomaly = Constants.PI + Constants.HalfPi;

        double m = OrbitalParameters.OrbitalParameters.TrueAnomalyToMeanAnomaly(trueAnomaly, eccentricity,
            OrbitalParameters.OrbitalParameters.EllipticAnomaly(trueAnomaly, eccentricity));

        return new KeplerianElements(semiMajorAxis, eccentricity, i, raanLongitude, Constants.PI + Constants.HalfPi, m, this, epochAtDescendingNode, Frame.ICRF);
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
    /// the celestial body at the given epoch, each to the nearest tick (#637; truncated to the tick below before 11.0.0).
    /// </remarks>
    public TimeSpan TrueSolarDay(Time epoch)
    {
        if (!this.IsPlanet)
        {
            throw new InvalidOperationException("At this time, the computation of true solar day works only with planets");
        }

        CelestialBody sun = Stars.SUN_BODY;
        var sideralRotation = SideralRotationPeriod(epoch);
        var eph0 = this.GetEphemeris(epoch, sun, Frame.ECLIPTIC_J2000, Aberration.LT).Position;
        var eph1 = this.GetEphemeris(epoch + sideralRotation, sun, Frame.ECLIPTIC_J2000, Aberration.LT).Position;
        var angle = eph0.Angle(eph1);
        return sideralRotation + Time.SecondsToSpan(angle / GetOrientation(Frame.ICRF, epoch).AngularVelocity.Magnitude());
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
    /// Get temperature at given altitude (simple API for backward compatibility).
    /// </summary>
    /// <param name="altitude">Altitude in meters.</param>
    /// <returns>Temperature in Celsius.</returns>
    /// <remarks>
    /// The value of <see cref="GetAirTemperature(IAtmosphericContext)"/> for an altitude-only context: the configured model
    /// (NRLMSISE-00 throws without the geodetic position and the epoch), for an Earth without one the U.S. Standard
    /// Atmosphere 1976 (<see cref="EarthStandardAtmosphere"/>), which is not the NRLMSISE-00 its drag uses.
    /// </remarks>
    public double GetAirTemperature(double altitude)
    {
        return GetAirTemperature(AtmosphericContext.FromAltitude(altitude));
    }

    /// <summary>
    /// Get temperature with full atmospheric context.
    /// </summary>
    /// <param name="context">Atmospheric context with altitude, position, and time.</param>
    /// <returns>Temperature in Celsius; NaN without an atmosphere (see <see cref="GetAtmosphere(AtmosphericContext)"/>).</returns>
    public double GetAirTemperature(IAtmosphericContext context)
    {
        return AtmosphereFor(context)?.GetTemperature(context) ?? double.NaN;
    }

    /// <summary>
    /// Get air pressure at given altitude (simple API for backward compatibility).
    /// </summary>
    /// <param name="altitude">Altitude in meters.</param>
    /// <returns>Pressure in kPa.</returns>
    /// <remarks>
    /// The value of <see cref="GetAirPressure(IAtmosphericContext)"/> for an altitude-only context: the configured model
    /// (NRLMSISE-00 throws without the geodetic position and the epoch), for an Earth without one the U.S. Standard
    /// Atmosphere 1976 (<see cref="EarthStandardAtmosphere"/>), which is not the NRLMSISE-00 its drag uses.
    /// </remarks>
    public double GetAirPressure(double altitude)
    {
        return GetAirPressure(AtmosphericContext.FromAltitude(altitude));
    }

    /// <summary>
    /// Get air pressure with full atmospheric context.
    /// </summary>
    /// <param name="context">Atmospheric context with altitude, position, and time.</param>
    /// <returns>Pressure in kPa; 0 without an atmosphere (see <see cref="GetAtmosphere(AtmosphericContext)"/>).</returns>
    public double GetAirPressure(IAtmosphericContext context)
    {
        return AtmosphereFor(context)?.GetPressure(context) ?? 0.0;
    }

    /// <summary>
    /// Get air density at given altitude (simple API for backward compatibility).
    /// </summary>
    /// <param name="altitude">Altitude in meters.</param>
    /// <returns>Density in kg/m³.</returns>
    /// <remarks>
    /// The value of <see cref="GetAirDensity(IAtmosphericContext)"/> for an altitude-only context: the configured model
    /// (NRLMSISE-00 throws without the geodetic position and the epoch), for an Earth without one the U.S. Standard
    /// Atmosphere 1976 (<see cref="EarthStandardAtmosphere"/>), which is not the NRLMSISE-00 its drag uses.
    /// </remarks>
    public double GetAirDensity(double altitude)
    {
        return GetAirDensity(AtmosphericContext.FromAltitude(altitude));
    }

    /// <summary>
    /// Get air density with full atmospheric context.
    /// </summary>
    /// <param name="context">Atmospheric context with altitude, position, and time.</param>
    /// <returns>Density in kg/m³; 0 without an atmosphere (see <see cref="GetAtmosphere(AtmosphericContext)"/>).</returns>
    public double GetAirDensity(IAtmosphericContext context)
    {
        return AtmosphereFor(context)?.GetDensity(context) ?? 0.0;
    }

    /// <summary>
    /// Air density at a geodetic position and an epoch: the value of <see cref="GetAirDensity(IAtmosphericContext)"/>
    /// for the same context, without allocating when the model supports it (NRLMSISE-00): the context is full, so a
    /// model-less Earth gives NRLMSISE-00 at nominal space weather, the density of its drag.
    /// </summary>
    /// <param name="altitude">Altitude, in m.</param>
    /// <param name="geodeticLatitude">Geodetic latitude, in rad.</param>
    /// <param name="geodeticLongitude">Geodetic longitude, in rad.</param>
    /// <param name="epoch">Epoch.</param>
    /// <returns>Density in kg/m³; 0 without an atmospheric model.</returns>
    internal double GetAirDensity(double altitude, double geodeticLatitude, double geodeticLongitude, in Time epoch)
    {
        if (Atmosphere is IGeodeticDensityModel model)
        {
            return model.GetDensity(altitude, geodeticLatitude, geodeticLongitude, epoch);
        }

        return GetAirDensity(AtmosphericContext.FromPlanetodetic(new Coordinates.Planetodetic(geodeticLongitude, geodeticLatitude, altitude), epoch));
    }

    /// <summary>
    /// The density of <paramref name="model"/> at a geodetic position and an epoch, as
    /// <see cref="GetAirDensity(double, double, double, in Time)"/> reads the atmosphere of a body: the atmosphere of a
    /// propagation's options in place of the body's (R06 of #668).
    /// </summary>
    internal static double AirDensity(IAtmosphericModel model, double altitude, double geodeticLatitude,
        double geodeticLongitude, in Time epoch)
    {
        return model is IGeodeticDensityModel geodetic
            ? geodetic.GetDensity(altitude, geodeticLatitude, geodeticLongitude, epoch)
            : model.GetDensity(AtmosphericContext.FromPlanetodetic(
                new Coordinates.Planetodetic(geodeticLongitude, geodeticLatitude, altitude), epoch));
    }

    /// <summary>
    /// Get complete atmospheric data at given altitude (simple API).
    /// </summary>
    /// <param name="altitude">Altitude in meters.</param>
    /// <returns>Atmosphere record with temperature, pressure, and density.</returns>
    /// <remarks>
    /// The value of <see cref="GetAtmosphere(AtmosphericContext)"/> for an altitude-only context: the configured model, for
    /// an Earth without one the U.S. Standard Atmosphere 1976. With the geodetic position and the epoch, a model-less Earth
    /// gives the NRLMSISE-00 of its drag through <see cref="GetAtmosphere(AtmosphericContext)"/>.
    /// </remarks>
    public Atmosphere.Atmosphere GetAtmosphere(double altitude)
    {
        return GetAtmosphere(AtmosphericContext.FromAltitude(altitude));
    }

    /// <summary>
    /// Get complete atmospheric data with full atmospheric context.
    /// </summary>
    /// <param name="context">Atmospheric context with altitude, and optionally position and time.</param>
    /// <returns>Atmosphere record with temperature, pressure, density, and optional model-specific details.</returns>
    /// <remarks>
    /// The configured atmospheric model of the body answers, for every body. An Earth without one answers with
    /// NRLMSISE-00 at nominal space weather when the context holds the geodetic position and the epoch, the model its
    /// drag uses, with its molecular densities in <see cref="Atmosphere.Atmosphere.Details"/> (#674); without them, with the
    /// U.S. Standard Atmosphere 1976 (<see cref="EarthStandardAtmosphere"/>, −5 to 1000 km, continued above), its base
    /// atmosphere, which needs the altitude only (decided by S. Guillet, 2026-11-14). A null context, which holds no
    /// altitude, and any other body without a model give the temperature NaN and the pressure and density 0, as
    /// <see cref="GetAirTemperature(IAtmosphericContext)"/>, <see cref="GetAirPressure(IAtmosphericContext)"/> and
    /// <see cref="GetAirDensity(IAtmosphericContext)"/> give.
    /// </remarks>
    public Atmosphere.Atmosphere GetAtmosphere(AtmosphericContext context)
    {
        var model = AtmosphereFor(context);
        if (model == null)
        {
            return new Atmosphere.Atmosphere
            {
                Temperature = double.NaN,
                Pressure = 0.0,
                Density = 0.0
            };
        }

        return model.GetAtmosphere(context);
    }

    /// <inheritdoc />
    internal override string ModelDifference(CelestialItem other)
    {
        var difference = base.ModelDifference(other);
        if (difference != null || other is not CelestialBody body || ReferenceEquals(this, other))
        {
            return difference;
        }

        if (Radii != body.Radii)
        {
            return "radii";
        }

        return AtmosphericModelComparer.AreEquivalent(AtmosphericModel, body.AtmosphericModel)
            ? null
            : "atmospheric model";
    }

    /// <summary>
    /// The type of the body, the lines of <see cref="CelestialItem.ToString"/>, then its fixed frame, radii (m),
    /// flattening and J2 to J4, every number in the shortest form that parses back to its property in the invariant
    /// culture (#681: the radii were printed to 7 significant digits, the equatorial radius of the Earth 0.4 m off).
    /// </summary>
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
               $"{"Equatorial radius (m) :",TITLE_WIDTH} {EquatorialRadius.ToString(CultureInfo.InvariantCulture),-VALUE_WIDTH}{Environment.NewLine}" +
               $"{"Polar radius (m) :",TITLE_WIDTH} {PolarRadius.ToString(CultureInfo.InvariantCulture),-VALUE_WIDTH}{Environment.NewLine}" +
               $"{"Flattening :",TITLE_WIDTH} {Flattening.ToString(CultureInfo.InvariantCulture),-VALUE_WIDTH}{Environment.NewLine}" +
               $"{"J2 :",TITLE_WIDTH} {J2.ToString(CultureInfo.InvariantCulture),-VALUE_WIDTH}{Environment.NewLine}" +
               $"{"J3 :",TITLE_WIDTH} {J3.ToString(CultureInfo.InvariantCulture),-VALUE_WIDTH}{Environment.NewLine}" +
               $"{"J4 :",TITLE_WIDTH} {J4.ToString(CultureInfo.InvariantCulture),-VALUE_WIDTH}{Environment.NewLine}";
    }
}