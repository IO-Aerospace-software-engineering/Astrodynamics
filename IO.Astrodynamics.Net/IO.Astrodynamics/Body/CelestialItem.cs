using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Occultation;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Window = IO.Astrodynamics.TimeSystem.Window;

namespace IO.Astrodynamics.Body;

public abstract class CelestialItem : ILocalizable, IEquatable<CelestialItem>
{
    protected readonly IDataProvider _dataProvider;

    protected const int TITLE_WIDTH = 32;
    protected const int VALUE_WIDTH = 32;

    /// <summary>
    /// Gets the NAIF ID of the celestial item.
    /// </summary>
    public int NaifId { get; }

    /// <summary>
    /// Gets the name of the celestial item.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the mass of the celestial item.
    /// </summary>
    public double Mass { get; }

    /// <summary>
    /// Gets the standard gravitational parameter (GM) of the celestial item.
    /// </summary>
    public double GM { get; }

    /// <summary>
    /// Gets the gravitational field of the celestial item.
    /// </summary>
    protected GravitationalField GravitationalField { get; }

    /// <summary>The gravitational field of the body, for the description of a force model (CDM export).</summary>
    internal GravitationalField Field => GravitationalField;

    /// <summary>
    /// Gets or sets the initial orbital parameters of the celestial item.
    /// </summary>
    public OrbitalParameters.OrbitalParameters InitialOrbitalParameters { get; internal set; }

    /// <summary>
    /// A collection of satellites orbiting the celestial item.
    /// </summary>
    private readonly HashSet<CelestialItem> _satellites = new();

    /// <summary>
    /// Gets a read-only collection of satellites orbiting the celestial item.
    /// </summary>
    public IReadOnlyCollection<CelestialItem> Satellites => _satellites;

    /// <summary>
    /// Used for performance improvement and to avoid duplicated calls in the CelestialItem API.
    /// </summary>
    protected DTO.CelestialBody ExtendedInformation;

    private readonly GeometryFinder _geometryFinder = new GeometryFinder();

    /// <summary>
    /// Gets a value indicating whether the celestial item is a spacecraft.
    /// </summary>
    public bool IsSpacecraft => NaifId < 0;

    /// <summary>
    /// Gets a value indicating whether the celestial item is a barycenter.
    /// </summary>
    public bool IsBarycenter => NaifId is >= 0 and < 10;

    /// <summary>
    /// Gets a value indicating whether the celestial item is the Sun.
    /// </summary>
    public bool IsSun => NaifId == 10;

    /// <summary>
    /// Gets a value indicating whether the celestial item is a planet.
    /// </summary>
    public bool IsPlanet => NaifId is > 100 and < 1000 && (NaifId % 100) == 99;

    /// <summary>
    /// Gets a value indicating whether the celestial item is a moon.
    /// </summary>
    public bool IsMoon => (NaifId is > 100 and < 1000 && (NaifId % 100) != 99) && !IsLagrangePoint;

    /// <summary>
    /// Gets a value indicating whether the celestial item is an asteroid.
    /// </summary>
    public bool IsAsteroid => this.NaifId is > 1000 and < 1000000000;

    /// <summary>
    /// Gets a value indicating whether the celestial item is a star.
    /// </summary>
    public bool IsStar => this.NaifId > 1000000000;

    /// <summary>
    /// Gets a value indicating whether the celestial item is a Lagrange point.
    /// </summary>
    public bool IsLagrangePoint => this.NaifId is 391 or 392 or 393 or 394 or 395;

    /// <summary>
    /// Gets a value indicating whether this object's ephemeris is backed by SPICE kernels.
    /// Planets, moons, barycenters, Sun, and asteroids are always SPICE-backed.
    /// Spacecraft and stars are not by default (overridable).
    /// </summary>
    public virtual bool IsSpiceBacked => !IsSpacecraft && !IsStar;

    /// <summary>
    /// Gets the barycenter of motion identifier.
    /// </summary>
    public int BarycenterOfMotionId { get; protected set; }

    /// <summary>
    /// Gets the center of motion identifier.
    /// </summary>
    public int CenterOfMotionId { get; protected set; }

    /// <summary>
    /// Instantiate celestial item from naif id with orbital parameters at given frame and epoch
    /// </summary>
    /// <param name="naifId">Naif identifier</param>
    /// <param name="frame">Initial orbital parameters frame</param>
    /// <param name="epoch">Epoch</param>
    /// <param name="geopotentialModelParameters"></param>
    protected CelestialItem(int naifId, Frame frame, in Time epoch, GeopotentialModelParameters geopotentialModelParameters = null)
    {
        _dataProvider = Configuration.Instance.DataProvider;
        ExtendedInformation = _dataProvider.GetCelestialBodyInfo(naifId);

        NaifId = naifId;
        Name = string.IsNullOrEmpty(ExtendedInformation.Name)
            ? throw new InvalidOperationException(
                "Celestial celestialItem name can't be defined, please check if you have loaded associated kernels")
            : ExtendedInformation.Name;

        Mass = ExtendedInformation.GM / Constants.G;
        GM = ExtendedInformation.GM;
        GravitationalField = CreateGravitationalField(naifId, GM, geopotentialModelParameters);

        BarycenterOfMotionId = FindBarycenterOfMotionId(this);
        CenterOfMotionId = FindCenterOfMotionId(this);

        if (NaifId == 0) return;
        if (IsPlanet || IsMoon || IsBarycenter || IsSun)
            InitialOrbitalParameters = GetEphemeris(epoch, new Barycenter(BarycenterOfMotionId, epoch), frame, Aberration.None);

        (InitialOrbitalParameters?.Observer as CelestialItem)?._satellites.Add(this);
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="naifId"></param>
    /// <param name="name"></param>
    /// <param name="mass"></param>
    /// <param name="initialOrbitalParameters"></param>
    /// <param name="geopotentialModelParameters"></param>
    protected CelestialItem(int naifId, string name, double mass, OrbitalParameters.OrbitalParameters initialOrbitalParameters,
        GeopotentialModelParameters geopotentialModelParameters = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("CelestialItem must have a name");
        }

        if (mass < 0)
        {
            throw new ArgumentException("CelestialItem can't have a negative mass");
        }

        _dataProvider = Configuration.Instance.DataProvider;

        NaifId = naifId;
        Name = name;
        Mass = mass;
        GM = mass * Constants.G;
        InitialOrbitalParameters = initialOrbitalParameters;


        (InitialOrbitalParameters?.Observer as CelestialBody)?._satellites.Add(this);
        GravitationalField = CreateGravitationalField(naifId, GM, geopotentialModelParameters);
    }

    // The NAIF identifier of the Earth, whose geopotential is EGM2008 unless its parameters give other constants
    private const int EarthNaifId = 399;

    /// <summary>
    /// The gravitational field of a body: the point mass, or the geopotential of <paramref name="parameters"/>, its
    /// harmonics with the constants of the model. Without constants in the parameters, those of EGM2008 for the Earth
    /// (NAIF 399), the model shipped with the library, and the GM and the equatorial radius of the kernels for any
    /// other body; the central term with <paramref name="gm"/>, the GM of the body (decisions D2 and D3 of #444), or with
    /// the GM of the model when the parameters ask for it (<see cref="GeopotentialModelParameters.CentralTerm"/>,
    /// decision 18 of #457).
    /// </summary>
    private static GravitationalField CreateGravitationalField(int naifId, double gm,
        GeopotentialModelParameters parameters)
    {
        if (parameters == null)
        {
            return new GravitationalField();
        }

        double? gravitationalParameter = parameters.GravitationalParameter;
        double? referenceRadius = parameters.ReferenceRadius;
        string modelName = null;
        if (!gravitationalParameter.HasValue && naifId == EarthNaifId)
        {
            gravitationalParameter = GeopotentialModelParameters.Egm2008GravitationalParameter;
            referenceRadius = GeopotentialModelParameters.Egm2008ReferenceRadius;
            modelName = "EGM2008";
        }

        return new GeopotentialGravitationalField(parameters.GeopotentialModelPath, parameters.GeopotentialDegree,
            gravitationalParameter, referenceRadius, gm, modelName, parameters.CentralTerm);
    }

    public static CelestialItem Create(int naifId)
    {
        if (naifId < 10)
        {
            return new Barycenter(naifId);
        }

        if (LagrangePoints.L1.NaifId <= naifId && naifId <= LagrangePoints.L5.NaifId)
        {
            return new LagrangePoint(new NaifObject(naifId, $"L{naifId - 390}", null));
        }

        return new CelestialBody(naifId);
    }

    internal void AddSatellite(CelestialItem celestialItem)
    {
        _satellites.Add(celestialItem);
    }

    internal void RemoveSatellite(CelestialItem celestialItem)
    {
        _satellites.Remove(celestialItem);
    }

    /// <summary>
    /// Get ephemeris
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="observer"></param>
    /// <param name="frame"></param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <returns></returns>
    public IEnumerable<OrbitalParameters.OrbitalParameters> GetEphemeris(in Window searchWindow, ILocalizable observer, Frame frame, Aberration aberration, in TimeSpan stepSize)
    {
        var occurences = (int)((searchWindow.Length / stepSize) + 1);
        var ephemeris = new List<OrbitalParameters.OrbitalParameters>(occurences);
        for (int i = 0; i < occurences; i++)
        {
            var epoch = searchWindow.StartDate + stepSize * i;
            ephemeris.Add(GetEphemeris(epoch, observer, frame, aberration));
        }

        return ephemeris;
    }

    /// <summary>
    /// GetEphemeris. For SPICE-backed observer/target pairs, uses SPICE directly.
    /// For non-SPICE observers (Spacecraft, Site), uses reference-body approach.
    /// </summary>
    public virtual OrbitalParameters.OrbitalParameters GetEphemeris(in Time epoch, ILocalizable observer, Frame frame, Aberration aberration)
    {
        // For SPICE-backed CelestialItem observers, use SPICE directly (handles aberration natively).
        // Sites are not CelestialItems and handle their own SPICE delegation in Site.GetEphemeris.
        if (observer is CelestialItem && observer.IsSpiceBacked)
        {
            return _dataProvider.GetEphemeris(epoch, this, observer, frame, aberration);
        }

        // Reference-body approach for non-SPICE observers
        var cb = GetReferenceBodyFor(observer);
        var targetCB = this.GetGeometricStateRelativeTo(epoch, cb);
        var observerCB = observer.GetGeometricStateRelativeTo(epoch, cb);
        var relative = new StateVector(
            targetCB.Position - observerCB.Position,
            targetCB.Velocity - observerCB.Velocity,
            observer, epoch, Frame.ICRF);

        if (aberration is Aberration.LT or Aberration.XLT or Aberration.LTS or Aberration.XLTS)
            relative = CorrectFromAberrationCB(this, epoch, observer, aberration, relative, observerCB, cb);

        return relative.ToFrame(frame);
    }

    /// <summary>
    /// Computes the geometric state of this object relative to a reference body in ICRF.
    /// Base implementation uses SPICE directly. Subclasses override for efficiency.
    /// </summary>
    public virtual StateVector GetGeometricStateRelativeTo(in Time epoch, CelestialItem referenceBody)
    {
        if (NaifId == referenceBody.NaifId)
            return new StateVector(Vector3.Zero, Vector3.Zero, referenceBody, epoch, Frame.ICRF);
        return _dataProvider.GetEphemeris(epoch, this, referenceBody, Frame.ICRF, Aberration.None).ToStateVector();
    }

    /// <summary>
    /// Determines the best reference body for computing ephemeris relative to an observer.
    /// </summary>
    internal static CelestialItem GetReferenceBodyFor(ILocalizable observer)
    {
        return observer switch
        {
            Spacecraft.Spacecraft sc => sc.InitialOrbitalParameters?.Observer as CelestialItem
                                        ?? Barycenters.SOLAR_SYSTEM_BARYCENTER,
            Surface.Site site => site.CelestialBody,
            _ => Barycenters.SOLAR_SYSTEM_BARYCENTER
        };
    }

    /// <summary>
    /// Correct from aberration using reference-body approach.
    /// Accounts for the reference body's motion between observation epoch and retarded/advanced epoch.
    /// </summary>
    internal static StateVector CorrectFromAberrationCB(
        ILocalizable target, Time epoch, ILocalizable observer, Aberration aberration,
        StateVector relativeState, StateVector observerCB, CelestialItem referenceBody)
    {
        var lightTime = TimeSpan.FromSeconds(relativeState.Position.Magnitude() / Constants.C);
        Time newEpoch = (aberration is Aberration.LT or Aberration.LTS)
            ? epoch - lightTime : epoch + lightTime;

        var targetNewCB = target.GetGeometricStateRelativeTo(newEpoch, referenceBody);

        // Account for reference body's motion between epochs.
        // target_inertial(newEpoch) = target_CB(newEpoch) + CB_SSB(newEpoch)
        // observer_inertial(epoch) = observer_CB(epoch) + CB_SSB(epoch)
        // corrected = target_CB(newEpoch) - observer_CB(epoch) + [CB_SSB(newEpoch) - CB_SSB(epoch)]
        Vector3 cbDeltaPos = Vector3.Zero;
        Vector3 cbDeltaVel = Vector3.Zero;
        if (referenceBody.NaifId != 0) // SSB is inertial, no correction needed
        {
            var cbAtNewEpoch = referenceBody.GetGeometricStateFromICRF(newEpoch).ToStateVector();
            var cbAtEpoch = referenceBody.GetGeometricStateFromICRF(epoch).ToStateVector();
            cbDeltaPos = cbAtNewEpoch.Position - cbAtEpoch.Position;
            cbDeltaVel = cbAtNewEpoch.Velocity - cbAtEpoch.Velocity;
        }

        var corrected = new StateVector(
            targetNewCB.Position + cbDeltaPos - observerCB.Position,
            targetNewCB.Velocity + cbDeltaVel - observerCB.Velocity,
            observer, epoch, Frame.ICRF);

        if (aberration is Aberration.LTS or Aberration.XLTS)
        {
            var cbSsbVel = referenceBody.GetGeometricStateFromICRF(epoch).ToStateVector().Velocity;
            var observerSsbVel = observerCB.Velocity + cbSsbVel;
            var voverc = observerSsbVel.Magnitude() / Constants.C;
            corrected.UpdatePosition(relativeState.Position + observerSsbVel * voverc);
        }

        return corrected;
    }

    /// <summary>
    /// Get geometric state from SSB in ICRF. Direct SPICE call, no caching.
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    internal virtual OrbitalParameters.OrbitalParameters GetGeometricStateFromICRF(in Time date)
    {
        return _dataProvider.GetEphemeris(date, this, Barycenters.SOLAR_SYSTEM_BARYCENTER, Frame.ICRF, Aberration.None).ToStateVector();
    }

    /// <summary>
    /// Return the angular size of a celestialItem relative to the distance
    /// </summary>
    /// <param name="distance"></param>
    /// <returns></returns>
    public double AngularSize(double distance)
    {
        return (this is CelestialBody body)
            ? 2.0 * System.Math.Asin((body.EquatorialRadius * 2.0) / (distance * 2.0))
            : 0.0;
    }

    /// <summary>
    /// Compute the angular separation between two localizable objects
    /// </summary>
    /// <param name="epoch"></param>
    /// <param name="target1"></param>
    /// <param name="target2"></param>
    /// <param name="aberration"></param>
    /// <returns></returns>
    public double AngularSeparation(in Time epoch, ILocalizable target1, ILocalizable target2, Aberration aberration)
    {
        var target1Position = target1.GetEphemeris(epoch, this, Frame.ICRF, aberration).ToStateVector().Position;
        var target2Position = target2.GetEphemeris(epoch, this, Frame.ICRF, aberration).ToStateVector().Position;
        return target1Position.Angle(target2Position);
    }

    /// <summary>
    /// Compute the angular separation between two localizable objects
    /// </summary>
    /// <param name="epoch"></param>
    /// <param name="target1"></param>
    /// <param name="fromPosition"></param>
    /// <param name="aberration"></param>
    /// <returns></returns>
    public double AngularSeparation(in Time epoch, ILocalizable target1, OrbitalParameters.OrbitalParameters fromPosition, Aberration aberration)
    {
        var target1Position = target1.GetEphemeris(epoch, fromPosition.Observer, fromPosition.Frame, aberration).ToStateVector().Position -
                              fromPosition.ToStateVector().Position;
        var target2Position = this.GetEphemeris(epoch, fromPosition.Observer, fromPosition.Frame, aberration).ToStateVector().Position -
                              fromPosition.ToStateVector().Position;
        return target1Position.Normalize().Angle(target2Position.Normalize());
    }


    /// <summary>
    /// Get all centers of motion from Solar system barycenter to the celestialItem
    /// </summary>
    /// <returns></returns>
    public IEnumerable<ILocalizable> GetCentersOfMotion()
    {
        List<ILocalizable> celestialBodies = new List<ILocalizable>();

        if (InitialOrbitalParameters?.Observer == null) return celestialBodies;
        celestialBodies.Add(InitialOrbitalParameters.Observer);
        celestialBodies.AddRange(InitialOrbitalParameters.Observer.GetCentersOfMotion());

        return celestialBodies;
    }

    /// <summary>
    /// Return the sub-observer coordinates based on ellipsoid interception 
    /// </summary>
    /// <param name="target"></param>
    /// <param name="epoch"></param>
    /// <param name="aberration"></param>
    /// <returns></returns>
    public Planetocentric SubObserverPoint(CelestialBody target, in Time epoch, Aberration aberration)
    {
        var position = GetEphemeris(epoch, target, target.Frame, aberration).ToStateVector().Position;

        var lon = System.Math.Atan2(position.Y, position.X);

        var lat = System.Math.Asin(position.Z / position.Magnitude());

        return new Planetocentric(lon, lat, position.Magnitude());
    }

    /// <summary>
    /// Get sub observer point
    /// </summary>
    /// <param name="position"></param>
    /// <param name="epoch"></param>
    /// <param name="aberration"></param>
    /// <returns></returns>
    public Planetocentric SubObserverPoint(in Vector3 position, in Time epoch, Aberration aberration)
    {
        var lon = System.Math.Atan2(position.Y, position.X);

        var lat = System.Math.Asin(position.Z / position.Magnitude());

        return new Planetocentric(lon, lat, position.Magnitude());
    }

    /// <summary>
    /// Know if the celestialItem is occulted by another celestialItem, as seen from a position.
    /// </summary>
    /// <remarks>
    /// The geometry of the occultation searches (<see cref="FindWindowsOnOccultationConstraint(in Window, ILocalizable, ShapeType, INaifObject, ShapeType, OccultationType, Aberration, in TimeSpan, OccultationSearchOptions)"/>):
    /// a <see cref="CelestialBody"/> is the ellipsoid of its radii oriented by its own frame, any other item a point. The
    /// positions are those of the bodies relative to the observer of <paramref name="from"/>, with the aberration
    /// correction, minus the position of <paramref name="from"/>; each frame is read at the epoch minus the light time
    /// from the position when the correction has one. Before #277 the bodies were spheres of their equatorial radius,
    /// in front of each other by the distance of their centers.
    /// </remarks>
    /// <param name="by">The occulting item.</param>
    /// <param name="from">The position of the observer.</param>
    /// <param name="aberration">The aberration correction.</param>
    /// <returns>The type of occultation of this item by <paramref name="by"/>; <see cref="OccultationType.None"/> when
    /// their discs do not overlap or this item is in front, or when both are points.</returns>
    /// <exception cref="InvalidOperationException">The position is inside one of the bodies.</exception>
    public OccultationType? IsOcculted(CelestialItem by, OrbitalParameters.OrbitalParameters from, Aberration aberration = Aberration.LT)
    {
        ArgumentNullException.ThrowIfNull(by);
        ArgumentNullException.ThrowIfNull(from);
        var backModel = new OccultationBodyModel(this, OccultationShape(this), null);
        var frontModel = new OccultationBodyModel(by, OccultationShape(by), null);
        if (backModel.IsPoint && frontModel.IsPoint)
        {
            return OccultationType.None;
        }

        var observer = from.ToStateVector().ToFrame(Frame.ICRF).ToStateVector().Position;
        var back = OccultationBodyAt(backModel, from, observer, aberration);
        var front = OccultationBodyAt(frontModel, from, observer, aberration);
        return OccultationGeometry.Evaluate(front, back, OccultationGeometry.SphereIsFront(front, back),
            OccultationMargin.Overlap | OccultationMargin.BackInside | OccultationMargin.FrontInside, true).Type;
    }

    private static ShapeType OccultationShape(CelestialItem item)
    {
        return item is CelestialBody body && body.Radii.X > 0.0 && body.Radii.Y > 0.0 && body.Radii.Z > 0.0 ? ShapeType.Ellipsoid : ShapeType.Point;
    }

    private static OccultationBody OccultationBodyAt(OccultationBodyModel model, OrbitalParameters.OrbitalParameters from, in Vector3 observer,
        Aberration aberration)
    {
        var position = model.Item.GetEphemeris(from.Epoch, from.Observer, Frame.ICRF, aberration).ToStateVector().Position - observer;
        double lightTime = aberration switch
        {
            Aberration.None => 0.0,
            Aberration.XLT or Aberration.XLTS or Aberration.XCN or Aberration.XCNS => position.Magnitude() / Constants.C,
            _ => -position.Magnitude() / Constants.C
        };
        return model.ToBody(position, OccultationEvaluator.Shift(from.Epoch, lightTime));
    }

    /// <summary>
    /// Know if the celestialItem is occulted by another celestialItem
    /// </summary>
    /// <param name="angularSeparation"></param>
    /// <param name="backSize"></param>
    /// <param name="bySize"></param>
    /// <returns></returns>
    public static OccultationType IsOcculted(double angularSeparation, double backSize, double bySize)
    {
        OccultationType occul = OccultationType.None;
        if (angularSeparation < (backSize + bySize) * 0.5)
        {
            occul = OccultationType.Partial;
            if (angularSeparation <= System.Math.Abs(bySize - backSize) * 0.5)
            {
                occul = OccultationType.Full;
                if (bySize < backSize)
                {
                    occul = OccultationType.Annular;
                }
            }
        }

        return occul;
    }

    /// <summary>
    /// Compute the shadow fraction caused by an occluding body: the fraction of the disc of the light source covered by
    /// the disc of the occluding body. Returns a value in [0, 1], guaranteed for finite inputs, where 0 = full
    /// illumination and 1 = total eclipse.
    /// </summary>
    /// <remarks>
    /// The regime comes from the angles: 0 when the discs do not overlap, 1 when the occluding disc covers the source
    /// disc, the ratio of their areas in an annular eclipse. In the penumbra the covered area is the sum of the two
    /// circular segments cut by the common chord, without cancellation near the contacts (#434), within a few 1e-16 of
    /// the exact fraction of the given angles; see <see cref="DiscOverlap"/> for the formulas and their references.
    /// Before #434 the closed form of the two-circle area lost its precision near the contacts (up to 1e-4 in LEO) and
    /// could return values outside [0, 1].
    /// </remarks>
    /// <param name="angularSeparation">Angular separation between the light source and occluding body as seen from the observer (radians)</param>
    /// <param name="backSize">Angular diameter of the background body (light source) as seen from the observer (radians)</param>
    /// <param name="bySize">Angular diameter of the occluding body as seen from the observer (radians)</param>
    /// <returns>Shadow fraction in [0, 1]</returns>
    public static double ShadowFraction(double angularSeparation, double backSize, double bySize)
    {
        double rSun = backSize * 0.5;  // angular radius of light source
        double rOcc = bySize * 0.5;    // angular radius of occluding body
        double d = angularSeparation;  // angular separation between centers

        return DiscOverlap.Regime(rSun, rOcc, d) switch
        {
            ShadowRegime.None => 0.0,
            ShadowRegime.Total => 1.0,
            ShadowRegime.Annular => (rOcc * rOcc) / (rSun * rSun),
            _ => new DiscOverlap(rSun, rOcc, d).Fraction
        };
    }

    /// <summary>
    /// Compute the shadow fraction caused by an occluding body on this celestial item as seen from a given position.
    /// Returns a value in [0, 1], guaranteed for finite inputs, where 0 = full illumination and 1 = total eclipse (see
    /// <see cref="ShadowFraction(double, double, double)"/>).
    /// </summary>
    /// <param name="by">The occluding body</param>
    /// <param name="from">The observer's orbital parameters</param>
    /// <param name="aberration">Aberration correction</param>
    /// <returns>Shadow fraction in [0, 1]</returns>
    public double ShadowFraction(CelestialItem by, OrbitalParameters.OrbitalParameters from, Aberration aberration = Aberration.LT)
    {
        double backSize = AngularSize((GetEphemeris(from.Epoch, from.Observer, from.Frame, aberration).ToStateVector().Position - from.ToStateVector().Position).Magnitude());
        double bySize = by.AngularSize((by.GetEphemeris(from.Epoch, from.Observer, from.Frame, aberration).ToStateVector().Position - from.ToStateVector().Position).Magnitude());
        var angularSeparation = this.AngularSeparation(from.Epoch, by, from, aberration);
        return ShadowFraction(angularSeparation, backSize, bySize);
    }

    /// <summary>
    /// Evaluate gravitational acceleration at given position
    /// </summary>
    /// <param name="orbitalParameters"></param>
    /// <returns></returns>
    public Vector3 EvaluateGravitationalAcceleration(OrbitalParameters.OrbitalParameters orbitalParameters)
    {
        return GravitationalField.ComputeGravitationalAcceleration(RelativeToThis(orbitalParameters));
    }

    /// <summary>
    /// Adds the analytic ∂a/∂r of <see cref="EvaluateGravitationalAcceleration"/> to <paramref name="dadr"/>, if the
    /// gravitational field of this item has them.
    /// </summary>
    /// <param name="orbitalParameters">The state, relative to any observer: the partials do not depend on it, since
    /// changing the observer is a translation.</param>
    /// <param name="dadr">3×3 row-major block the partials are added to, in 1/s².</param>
    /// <param name="partialsDegree">For a geopotential, the highest degree of the partials of its harmonics, or null for
    /// the degree of the field (<see cref="GeopotentialGravitationalField.PartialsDegree"/>); ignored otherwise.</param>
    /// <returns>Whether the partials were added; when false, <paramref name="dadr"/> is untouched.</returns>
    internal bool TryAccumulateGravitationalPositionPartials(OrbitalParameters.OrbitalParameters orbitalParameters,
        Span<double> dadr, int? partialsDegree = null)
    {
        return GravitationalField.TryAccumulatePositionPartials(RelativeToThis(orbitalParameters), dadr,
            partialsDegree);
    }

    /// <summary>
    /// Checks that the gravitational partials can be computed at <paramref name="partialsDegree"/>, so that a
    /// propagation fails before it starts rather than at its first partials.
    /// </summary>
    /// <param name="partialsDegree">The requested degree of the partials of the harmonics, or null for the degree of
    /// the field.</param>
    /// <exception cref="NotSupportedException">The geopotential partials would be above
    /// <see cref="GeopotentialGravitationalField.MaxPartialsDegree"/>.</exception>
    internal void ValidateGravitationalPartialsDegree(int? partialsDegree)
    {
        if (GravitationalField is GeopotentialGravitationalField geopotential)
        {
            geopotential.PartialsDegree(partialsDegree);
        }
    }

    private StateVector RelativeToThis(OrbitalParameters.OrbitalParameters orbitalParameters)
    {
        return orbitalParameters.Observer as CelestialItem != this ? orbitalParameters.RelativeTo(this, Aberration.None).ToStateVector() : orbitalParameters.ToStateVector();
    }

    /// <summary>
    /// Write ephemeris. Must be overridden by subclasses that store state vectors (Spacecraft, Star).
    /// </summary>
    /// <param name="outputFile"></param>
    public virtual void WriteEphemeris(FileInfo outputFile)
    {
        throw new NotSupportedException(
            $"WriteEphemeris is not supported for {GetType().Name} '{Name}'. " +
            "CelestialBody ephemeris comes from SPICE kernels; only Spacecraft and Star support WriteEphemeris.");
    }

    /// <summary>
    /// Find barycenter of motion identifier
    /// </summary>
    /// <param name="celestialItem"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    internal static int FindBarycenterOfMotionId(CelestialItem celestialItem)
    {
        if (celestialItem.IsSun || celestialItem.IsBarycenter || celestialItem.IsAsteroid)
        {
            return 0;
        }

        if (celestialItem.IsPlanet || celestialItem.IsMoon)
        {
            return (int)(celestialItem.NaifId / 100);
        }

        if (celestialItem.IsLagrangePoint)
        {
            if (celestialItem.NaifId is 391 or 392)
            {
                return (int)(celestialItem.NaifId / 100);
            }

            return 0;
        }

        throw new ArgumentException("Invalid Naif Id : " + celestialItem.NaifId);
    }

    /// <summary>
    /// Find center of motion identifier
    /// </summary>
    /// <param name="celestialItem"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    internal static int FindCenterOfMotionId(CelestialItem celestialItem)
    {
        if (celestialItem.IsBarycenter)
        {
            return 0;
        }

        if (celestialItem.IsSun || celestialItem.IsPlanet || celestialItem.IsAsteroid)
        {
            return 10;
        }

        if (celestialItem.IsMoon)
        {
            return celestialItem.NaifId - (celestialItem.NaifId % 100) + 99;
        }

        if (celestialItem.IsLagrangePoint)
        {
            if (celestialItem.NaifId is 391 or 392)
            {
                return (int)(celestialItem.NaifId / 100);
            }

            return 10;
        }

        throw new ArgumentException("Invalid Naif Id : " + celestialItem.NaifId);
    }


    #region FindWindows

    /// <summary>
    /// Find windows on distance constraint
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="observer"></param>
    /// <param name="relationalOperator"></param>
    /// <param name="value"></param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <returns></returns>
    public IEnumerable<Window> FindWindowsOnDistanceConstraint(in Window searchWindow, ILocalizable observer,
        RelationnalOperator relationalOperator, double value, Aberration aberration, in TimeSpan stepSize)
    {
        if (IsSpiceBacked && observer is CelestialItem ci1 && ci1.IsSpiceBacked
            && (IsSpacecraft || ci1.IsSpacecraft))
            return SpiceAPI.Instance.FindWindowsOnDistanceConstraint(
                searchWindow, observer.NaifId, NaifId, relationalOperator, value / 1000.0, aberration, stepSize);

        Func<Time, double> calculateDistance = date => { return GetEphemeris(date, observer, Frame.ICRF, aberration).ToStateVector().Position.Magnitude(); };

        return _geometryFinder.FindWindowsWithCondition(searchWindow, calculateDistance, relationalOperator, value, stepSize);
    }

    /// <summary>
    /// Find the windows when <paramref name="frontBody"/> occults this item (the target, or back body) as
    /// <paramref name="observer"/> sees them, with the bodies' own frames and a convergence tolerance of 1 µs; see the
    /// overload with <see cref="OccultationSearchOptions"/>.
    /// </summary>
    /// <param name="searchWindow">The search window.</param>
    /// <param name="observer">The observer.</param>
    /// <param name="targetShape">The shape of this item.</param>
    /// <param name="frontBody">The occulting body.</param>
    /// <param name="frontShape">The shape of the occulting body.</param>
    /// <param name="occultationType">The type of occultation.</param>
    /// <param name="aberration">The aberration correction.</param>
    /// <param name="stepSize">The step of the search.</param>
    /// <returns>The windows, in TDB.</returns>
    public IEnumerable<Window> FindWindowsOnOccultationConstraint(in Window searchWindow, ILocalizable observer,
        ShapeType targetShape, INaifObject frontBody, ShapeType frontShape, OccultationType occultationType, Aberration aberration, in TimeSpan stepSize)
    {
        return FindWindowsOnOccultationConstraint(searchWindow, observer, targetShape, frontBody, frontShape, occultationType, aberration, stepSize,
            OccultationSearchOptions.Default);
    }

    /// <summary>
    /// Find the windows when <paramref name="frontBody"/> occults this item (the target, or back body) as
    /// <paramref name="observer"/> sees them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The types have the meaning of CSPICE's <c>gfoclt</c>: <see cref="OccultationType.Full"/>, the target is hidden;
    /// <see cref="OccultationType.Annular"/>, the front body is seen within the target's disc;
    /// <see cref="OccultationType.Partial"/>, the front body hides part of the target's limb;
    /// <see cref="OccultationType.Any"/>, any of them; and <see cref="OccultationType.None"/>, the rest of the search
    /// window. A body modeled as <see cref="ShapeType.Ellipsoid"/> is the ellipsoid of its radii
    /// (<see cref="CelestialBody.Radii"/>) oriented by its body-fixed frame (<see cref="OccultationSearchOptions"/>). A
    /// point target can only be hidden (<see cref="OccultationType.Full"/> or <see cref="OccultationType.Any"/>), a
    /// point front body can only transit (<see cref="OccultationType.Any"/> or <see cref="OccultationType.Annular"/>);
    /// the other types give no window.
    /// </para>
    /// <para>
    /// When the observer and this item are in the kernels and one of them is a spacecraft, CSPICE's <c>gfoclt</c>
    /// searches. Otherwise, for a propagated spacecraft, a site or two natural bodies, the library searches: the
    /// geometry of the ellipsoids (the method of CSPICE's <c>zzocced</c>) gives continuous margins, located by Brent's
    /// root finder to <see cref="OccultationSearchOptions.ConvergenceTolerance"/>; it reads the states and the
    /// orientations, never writes a kernel, and keeps no state, so searches on separate propagations may run
    /// concurrently. Its step rule: every window, even shorter than the step, is found when the step is at most half the
    /// time between the extremes of the margins (a quarter of the period for the eclipses of a satellite); see the
    /// reference page on occultations.
    /// </para>
    /// </remarks>
    /// <param name="searchWindow">The search window.</param>
    /// <param name="observer">The observer.</param>
    /// <param name="targetShape">The shape of this item.</param>
    /// <param name="frontBody">The occulting body.</param>
    /// <param name="frontShape">The shape of the occulting body.</param>
    /// <param name="occultationType">The type of occultation.</param>
    /// <param name="aberration">The aberration correction: none or a light time (LT, CN, XLT, XCN); the stellar
    /// aberration of the "+S" corrections is ignored, as <c>gfoclt</c> does.</param>
    /// <param name="stepSize">The step of the search.</param>
    /// <param name="options">The frames of the ellipsoids and the convergence tolerance; null for the defaults.</param>
    /// <returns>The windows, in TDB.</returns>
    /// <exception cref="ArgumentException">Both bodies are points, or a body modeled as an ellipsoid has no radii.</exception>
    public IEnumerable<Window> FindWindowsOnOccultationConstraint(in Window searchWindow, ILocalizable observer,
        ShapeType targetShape, INaifObject frontBody, ShapeType frontShape, OccultationType occultationType, Aberration aberration, in TimeSpan stepSize,
        OccultationSearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(frontBody);
        if (IsSpiceBacked && observer is CelestialItem ci2 && ci2.IsSpiceBacked
            && frontBody is ILocalizable fb && fb.IsSpiceBacked
            && (IsSpacecraft || ci2.IsSpacecraft))
            return OccultationSearch.Spice(searchWindow, observer.NaifId, NaifId, targetShape, frontBody.NaifId, frontShape, occultationType, aberration,
                stepSize, options);

        return OccultationSearch.Managed(searchWindow, observer, this, targetShape, frontBody, frontShape, occultationType, aberration, stepSize, options);
    }

    /// <summary>
    /// Find windows on coordinate constraint
    /// </summary>
    /// <param name="searchWindow"></param>
    /// <param name="observer"></param>
    /// <param name="frame"></param>
    /// <param name="coordinateSystem"></param>
    /// <param name="coordinate"></param>
    /// <param name="relationalOperator"></param>
    /// <param name="value"></param>
    /// <param name="adjustValue"></param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public IEnumerable<Window> FindWindowsOnCoordinateConstraint(in Window searchWindow, ILocalizable observer, Frame frame,
        CoordinateSystem coordinateSystem, Coordinate coordinate, RelationnalOperator relationalOperator, double value, double adjustValue, Aberration aberration,
        in TimeSpan stepSize)
    {
        if (IsSpiceBacked && observer is CelestialItem ci3 && ci3.IsSpiceBacked
            && (IsSpacecraft || ci3.IsSpacecraft))
        {
            // SPICE uses km for distance coordinates; framework uses m. Angles are radians in both.
            bool isDistanceCoord = coordinate is Coordinate.X or Coordinate.Y or Coordinate.Z
                or Coordinate.Range or Coordinate.Radius or Coordinate.Altitude;
            double spiceValue = isDistanceCoord ? value / 1000.0 : value;
            double spiceAdjust = isDistanceCoord ? adjustValue / 1000.0 : adjustValue;
            return SpiceAPI.Instance.FindWindowsOnCoordinateConstraint(
                searchWindow, observer.NaifId, NaifId, frame,
                coordinateSystem, coordinate, relationalOperator, spiceValue, spiceAdjust, aberration, stepSize);
        }

        Func<Time, double> evaluateCoordinates = null;
        switch (coordinateSystem)
        {
            case CoordinateSystem.Rectangular:
                evaluateCoordinates = date =>
                {
                    var stateVector = GetEphemeris(date, observer, frame, aberration).ToStateVector();
                    return coordinate switch
                    {
                        Coordinate.X => stateVector.Position.X,
                        Coordinate.Y => stateVector.Position.Y,
                        Coordinate.Z => stateVector.Position.Z,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                };
                break;
            case CoordinateSystem.RaDec:
                evaluateCoordinates = date =>
                {
                    var stateVector = GetEphemeris(date, observer, frame, aberration).ToStateVector();
                    var equatorial = stateVector.ToEquatorial();
                    return coordinate switch
                    {
                        Coordinate.Declination => equatorial.Declination,
                        Coordinate.RightAscension => equatorial.RightAscension,
                        Coordinate.Range => equatorial.Distance,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                };
                break;
            case CoordinateSystem.Geodetic:
                evaluateCoordinates = date =>
                {
                    var stateVector = GetEphemeris(date, observer, frame, aberration).ToStateVector();
                    var celestialBody = stateVector.Observer as CelestialBody;
                    var geodetic = stateVector.ToPlanetocentric(aberration).ToPlanetodetic(celestialBody.Flattening, celestialBody.EquatorialRadius);
                    return coordinate switch
                    {
                        Coordinate.Latitude => geodetic.Latitude,
                        Coordinate.Longitude => geodetic.Longitude,
                        Coordinate.Altitude => geodetic.Altitude,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                };
                break;
            case CoordinateSystem.Planetographic:
                evaluateCoordinates = date =>
                {
                    var stateVector = GetEphemeris(date, observer, frame, aberration).ToStateVector();
                    var planetographic = stateVector.ToPlanetocentric(aberration);
                    return coordinate switch
                    {
                        Coordinate.Latitude => planetographic.Latitude,
                        Coordinate.Longitude => planetographic.Longitude,
                        Coordinate.Radius => planetographic.Radius,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                };
                break;
        }

        return _geometryFinder.FindWindowsWithCondition(searchWindow, evaluateCoordinates, relationalOperator, value, stepSize);
    }

    #endregion

    #region Operators

    public override string ToString()
    {
        return $"{"Identifier :",TITLE_WIDTH} {NaifId,-VALUE_WIDTH}{Environment.NewLine}" +
               $"{"Name :",TITLE_WIDTH} {Name,-VALUE_WIDTH}{Environment.NewLine}" +
               $"{"Mass (kg) :",TITLE_WIDTH} {Mass.ToString("E", CultureInfo.InvariantCulture),-VALUE_WIDTH:E}{Environment.NewLine}" +
               $"{"GM (m^3.s^2):",TITLE_WIDTH} {GM.ToString("E", CultureInfo.InvariantCulture),-VALUE_WIDTH:E}{Environment.NewLine}";
    }

    public bool Equals(CelestialItem other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return NaifId == other.NaifId;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((CelestialItem)obj);
    }

    public override int GetHashCode()
    {
        return NaifId;
    }

    public static bool operator ==(CelestialItem left, CelestialItem right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(CelestialItem left, CelestialItem right)
    {
        return !Equals(left, right);
    }

    #endregion
}