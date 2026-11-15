using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
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
    /// <summary>
    /// The data provider of the item, the global one when it was built. A static object of <see cref="Barycenters"/>,
    /// <see cref="PlanetsAndMoons"/> or <see cref="Stars"/> reads through <see cref="ItemProvider"/> instead: the provider
    /// it read its data from, or the global provider of the time of the read until its data are read.
    /// </summary>
    protected readonly IDataProvider _dataProvider;

    /// <summary>
    /// The provider the item reads through: <see cref="_dataProvider"/> for an item built by a constructor; for a static
    /// object of <see cref="Barycenters"/>, <see cref="PlanetsAndMoons"/> or <see cref="Stars"/>, the provider it read its
    /// data from, and until it has read them the global provider of the time of the read, so that a read that failed on
    /// one provider is tried again on the provider set since.
    /// </summary>
    private protected IDataProvider ItemProvider =>
        _deferred == null ? _dataProvider : Volatile.Read(ref _resolved)?.Provider ?? Configuration.Instance.DataProvider;

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

    private readonly double _mass;
    private readonly double _gm;
    private readonly GravitationalField _gravitationalField;
    private OrbitalParameters.OrbitalParameters _initialOrbitalParameters;

    /// <summary>
    /// Gets the mass of the celestial item.
    /// </summary>
    /// <remarks>
    /// For a static object of <see cref="Barycenters"/>, <see cref="PlanetsAndMoons"/> or <see cref="Stars"/>, read from
    /// the data provider at the first read, as <see cref="GM"/>, <see cref="InitialOrbitalParameters"/> and the
    /// provider-backed members of a <see cref="CelestialBody"/>: a read the provider cannot answer throws its exception
    /// (<see cref="ArgumentException"/> naming <c>naifId</c> for missing information) and is tried again at the next read,
    /// on the provider set at that time; once read, the object keeps the provider it read its data from.
    /// </remarks>
    public double Mass => _deferred == null ? _mass : Resolved.Mass;

    /// <summary>
    /// Gets the standard gravitational parameter (GM) of the celestial item.
    /// </summary>
    public double GM => _deferred == null ? _gm : Resolved.GM;

    /// <summary>
    /// Gets the gravitational field of the celestial item.
    /// </summary>
    protected GravitationalField GravitationalField => _deferred == null ? _gravitationalField : Resolved.GravitationalField;

    /// <summary>The gravitational field of the body, for the description of a force model (CDM export).</summary>
    internal GravitationalField Field => GravitationalField;

    /// <summary>
    /// Gets or sets the initial orbital parameters of the celestial item.
    /// </summary>
    public OrbitalParameters.OrbitalParameters InitialOrbitalParameters
    {
        get => _deferred == null ? _initialOrbitalParameters : Resolved.InitialOrbitalParameters;
        internal set
        {
            if (_deferred != null)
            {
                throw new InvalidOperationException("The orbit of a static solar system object is read from its data provider.");
            }

            _initialOrbitalParameters = value;
        }
    }

    /// <summary>
    /// The epoch and the frame of the orbit of a static object of <see cref="Barycenters"/>, <see cref="PlanetsAndMoons"/>
    /// or <see cref="Stars"/>, whose provider data are read at the first read of a member that needs them; null for any
    /// other item, whose constructor reads them.
    /// </summary>
    private readonly DeferredData _deferred;

    private sealed record DeferredData(Frame Frame, Time Epoch);

    /// <summary>
    /// The provider data of an item built by the natural-body constructor: the body information, the mass, the GM, the
    /// gravitational field and the orbit about the barycentre of its system.
    /// </summary>
    private protected class ProviderData
    {
        internal IDataProvider Provider { get; init; }
        internal DTO.CelestialBody Information { get; set; }
        internal double Mass { get; set; }
        internal double GM { get; set; }
        internal GravitationalField GravitationalField { get; set; }
        internal OrbitalParameters.OrbitalParameters InitialOrbitalParameters { get; set; }
    }

    // The provider data of a deferred item, once read; published once, never under a lock (#623)
    private ProviderData _resolved;

    /// <summary>Whether the item is a static object whose provider data are read at first use.</summary>
    private protected bool IsDeferred => _deferred != null;

    /// <summary>
    /// The provider data of a deferred item: read at the first call from the global provider of that time, built exactly
    /// as the natural-body constructor builds them; a read that throws caches nothing, so that the next call tries again,
    /// on the global provider of its own time. The data are published with a
    /// compare-exchange, never under a lock held across the provider call, so that a thread holding the SPICE lock never
    /// waits for another (#623); two threads that read at once may both read, and the first published is kept.
    /// </summary>
    private protected ProviderData Resolved => Volatile.Read(ref _resolved) ?? Resolve();

    private ProviderData Resolve()
    {
        var data = ReadProviderData(Configuration.Instance.DataProvider, _deferred.Frame, _deferred.Epoch);
        var published = Interlocked.CompareExchange(ref _resolved, data, null);
        if (published != null)
        {
            return published;
        }

        // Listed by the observer of its orbit, as the natural-body constructor lists its item
        ExtendedInformation = data.Information;
        (data.InitialOrbitalParameters?.Observer as CelestialItem)?.AddSatellite(this);
        return data;
    }

    /// <summary>
    /// Reads the provider data of a deferred item from <paramref name="provider"/>; a <see cref="CelestialBody"/> adds its own.
    /// </summary>
    private protected virtual ProviderData ReadProviderData(IDataProvider provider, Frame frame, in Time epoch) =>
        ReadItemData(new ProviderData { Provider = provider }, frame, epoch);

    /// <summary>
    /// Fills <paramref name="data"/> as the natural-body constructor reads its item: the information of its provider,
    /// the GM and mass, the point-mass field and the orbit about the barycentre of the system.
    /// </summary>
    private protected T ReadItemData<T>(T data, Frame frame, in Time epoch) where T : ProviderData
    {
        var information = data.Provider.GetCelestialBodyInfo(NaifId);
        if (string.IsNullOrEmpty(information.Name))
        {
            throw new InvalidOperationException(
                "Celestial celestialItem name can't be defined, please check if you have loaded associated kernels");
        }

        data.Information = information;
        data.Mass = information.GM / Constants.G;
        data.GM = information.GM;
        data.GravitationalField = CreateGravitationalField(NaifId, information.GM, null);
        data.InitialOrbitalParameters = NaifId != 0 && (IsPlanet || IsMoon || IsBarycenter || IsSun)
            ? ReadStateAboutBarycenter(epoch, frame)
            : null;
        return data;
    }

    /// <summary>
    /// The satellites orbiting the celestial item, held weakly, by instance (#676).
    /// </summary>
    private readonly WeakSatellites _satellites = new();

    /// <summary>
    /// Whether the item is listed by an observer of any kind (the natural-body constructor), or by a
    /// <see cref="CelestialBody"/> observer only (the constructor of custom parameters).
    /// </summary>
    private readonly bool _listedByAnyObserver;

    /// <summary>
    /// Gets a read-only collection of satellites orbiting the celestial item: the instances whose initial orbital
    /// parameters have it as observer, by reference, whose constructor completed (#676).
    /// </summary>
    /// <remarks>
    /// A live view: the collection holds its items weakly, so that an item referenced nowhere else leaves it when it is
    /// collected, and is safe across threads, constructions about one body on several threads included. Two items of one
    /// NAIF id are both listed. The order of the enumeration is not specified; <see cref="IReadOnlyCollection{T}.Count"/>
    /// enumerates. Before 11.0.0 it held its items strongly in a set compared by NAIF id, without a lock, and listed an
    /// item whose constructor then threw.
    /// </remarks>
    public IReadOnlyCollection<CelestialItem> Satellites => _satellites;

    /// <summary>
    /// Used for performance improvement and to avoid duplicated calls in the CelestialItem API.
    /// </summary>
    /// <remarks>
    /// Null for a static object of <see cref="Barycenters"/>, <see cref="PlanetsAndMoons"/> or <see cref="Stars"/> until
    /// its provider data are read, at the first read of a member that needs them.
    /// </remarks>
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
        _listedByAnyObserver = true;
        ExtendedInformation = _dataProvider.GetCelestialBodyInfo(naifId);

        NaifId = naifId;
        Name = string.IsNullOrEmpty(ExtendedInformation.Name)
            ? throw new InvalidOperationException(
                "Celestial celestialItem name can't be defined, please check if you have loaded associated kernels")
            : ExtendedInformation.Name;

        _mass = ExtendedInformation.GM / Constants.G;
        _gm = ExtendedInformation.GM;
        _gravitationalField = CreateGravitationalField(naifId, _gm, geopotentialModelParameters);

        BarycenterOfMotionId = FindBarycenterOfMotionId(this);
        CenterOfMotionId = FindCenterOfMotionId(this);

        if (NaifId == 0) return;
        if (IsPlanet || IsMoon || IsBarycenter || IsSun)
            InitialOrbitalParameters = ReadStateAboutBarycenter(epoch, frame);
    }

    /// <summary>
    /// A static object of <see cref="Barycenters"/>, <see cref="PlanetsAndMoons"/> or <see cref="Stars"/>: its identifiers
    /// and the name of its <paramref name="knownObject"/>, without any read of the data provider, whose data (information,
    /// GM, orbit about the barycentre at <paramref name="epoch"/> in <paramref name="frame"/>) are read at the first read
    /// of a member that needs them (<see cref="Resolved"/>), with the point-mass field.
    /// </summary>
    /// <remarks>
    /// Before, the static classes built their objects with the natural-body constructor, which read the provider in their
    /// type initializer: a missing datum threw a <see cref="TypeInitializationException"/> that the runtime keeps for the
    /// life of the process, so that every later use failed, even once the data existed.
    /// </remarks>
    private protected CelestialItem(NaifObject knownObject, Frame frame, in Time epoch)
    {
        _dataProvider = Configuration.Instance.DataProvider;
        _listedByAnyObserver = true;
        _deferred = new DeferredData(frame, epoch);
        NaifId = knownObject.NaifId;
        Name = knownObject.Name;
        BarycenterOfMotionId = FindBarycenterOfMotionId(this);
        CenterOfMotionId = FindCenterOfMotionId(this);
    }

    // The state about the barycenter of the body's system; null when the loaded kernels do not chain the body to it (Mars
    // to Pluto and their moons with de440s alone), and the sphere of influence of a CelestialBody is then +Infinity, as
    // for a body built from custom parameters. Before #454 the native read returned an undefined state there; a read
    // that needs the body's position still throws. A provider without information on the barycentre, which SPICE always
    // names (0 to 9), throws ArgumentException for its identifier (MemoryDataProvider, #680): the identifier is the
    // library's, not the caller's, and the barycentre's information is missing data, so the body builds without an orbit
    // as when the barycentre's state is missing, and as the same body built on SPICE
    private OrbitalParameters.OrbitalParameters ReadStateAboutBarycenter(in Time epoch, Frame frame)
    {
        Barycenter barycenter;
        try
        {
            barycenter = new Barycenter(BarycenterOfMotionId, epoch);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return null;
        }

        try
        {
            return GetEphemeris(epoch, barycenter, frame, Aberration.None);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
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
        _mass = mass;
        _gm = mass * Constants.G;
        InitialOrbitalParameters = initialOrbitalParameters;
        _gravitationalField = CreateGravitationalField(naifId, _gm, geopotentialModelParameters);
    }

    // The NAIF identifier of the Earth, whose geopotential is EGM2008 unless its parameters give other constants, and
    // whose atmosphere without a model is NRLMSISE-00 at nominal space weather
    private protected const int EarthNaifId = 399;

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

        // The coefficients of the parameters, read once and shared by every body built from them (#674)
        return new GeopotentialGravitationalField(parameters.ModelReader, parameters.GeopotentialDegree,
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

    /// <summary>
    /// Lists the item by the observer of its initial orbital parameters: any observer for the natural-body constructor, a
    /// <see cref="CelestialBody"/> only for the constructor of custom parameters. The last statement of each constructor
    /// of a derived class that does not chain to another one, after every check, so that a constructor that throws lists
    /// nothing (#676).
    /// </summary>
    private protected void RegisterWithObserver()
    {
        var observer = _listedByAnyObserver
            ? InitialOrbitalParameters?.Observer as CelestialItem
            : InitialOrbitalParameters?.Observer as CelestialBody;
        observer?.AddSatellite(this);
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
    /// <remarks>
    /// The states every <paramref name="stepSize"/> from the start of the window, placed in its calendar
    /// (<c>StartDate + i · stepSize</c>), up to its end. Across a leap second the window's <see cref="Window.Length"/> counts
    /// one second more than its calendar: the end bounds the samples, so that none lies past it (#682; before 11.0.0 the
    /// last state of such a window could lie one second past its end).
    /// </remarks>
    /// <param name="searchWindow"></param>
    /// <param name="observer"></param>
    /// <param name="frame"></param>
    /// <param name="aberration"></param>
    /// <param name="stepSize"></param>
    /// <returns></returns>
    public IEnumerable<OrbitalParameters.OrbitalParameters> GetEphemeris(in Window searchWindow, ILocalizable observer, Frame frame, Aberration aberration, in TimeSpan stepSize)
    {
        // An upper bound of the count: the Length counts the leap seconds the calendar placement does not
        var occurences = (int)((searchWindow.Length / stepSize) + 1);
        var ephemeris = new List<OrbitalParameters.OrbitalParameters>(occurences);
        for (int i = 0; i < occurences; i++)
        {
            var epoch = searchWindow.StartDate + stepSize * i;
            if (epoch > searchWindow.EndDate)
            {
                break;
            }

            ephemeris.Add(GetEphemeris(epoch, observer, frame, aberration));
        }

        return ephemeris;
    }

    /// <summary>
    /// GetEphemeris. For SPICE-backed observer/target pairs, uses SPICE directly.
    /// For non-SPICE observers (Spacecraft, Site), uses reference-body approach, where the nine aberration corrections are
    /// applied as CSPICE's <c>spkaps</c> applies them (<see cref="CorrectFromAberrationCB"/>, #491); the state is rotated
    /// to <paramref name="frame"/> at <paramref name="epoch"/> (#593).
    /// </summary>
    public virtual OrbitalParameters.OrbitalParameters GetEphemeris(in Time epoch, ILocalizable observer, Frame frame, Aberration aberration)
    {
        // For SPICE-backed CelestialItem observers, use SPICE directly (handles aberration natively).
        // Sites are not CelestialItems and handle their own SPICE delegation in Site.GetEphemeris.
        if (observer is CelestialItem && observer.IsSpiceBacked)
        {
            return ItemProvider.GetEphemeris(epoch, this, observer, frame, aberration);
        }

        // Reference-body approach for non-SPICE observers
        var cb = GetReferenceBodyFor(observer);
        var targetCB = this.GetGeometricStateRelativeTo(epoch, cb);
        var observerCB = observer.GetGeometricStateRelativeTo(epoch, cb);
        var relative = new StateVector(
            targetCB.Position - observerCB.Position,
            targetCB.Velocity - observerCB.Velocity,
            observer, epoch, Frame.ICRF);

        if (aberration != Aberration.None)
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
        return ItemProvider.GetEphemeris(epoch, this, referenceBody, Frame.ICRF, Aberration.None).ToStateVector();
    }

    /// <summary>
    /// Determines the best reference body for computing ephemeris relative to an observer.
    /// </summary>
    internal static CelestialItem GetReferenceBodyFor(ILocalizable observer)
    {
        return observer switch
        {
            // A kernel-backed spacecraft is read from the SSB, as CSPICE reads it, whatever the centre of its initial
            // orbital parameters (the Earth by default since #620)
            Spacecraft.Spacecraft { IsSpiceBacked: true } => Barycenters.SOLAR_SYSTEM_BARYCENTER,
            Spacecraft.Spacecraft sc => sc.InitialOrbitalParameters?.Observer as CelestialItem
                                        ?? Barycenters.SOLAR_SYSTEM_BARYCENTER,
            Surface.Site site => site.CelestialBody,
            _ => Barycenters.SOLAR_SYSTEM_BARYCENTER
        };
    }

    // SPKLTC's iterations for the converged Newtonian light time and its convergence threshold relative to the epoch; the
    // half-step of SPKACS's central difference of the observer's velocity (TDELTA, 1 s)
    private const int ConvergedLightTimeIterations = 5;
    private const double LightTimeConvergence = 1e-17;
    private const long AccelerationHalfStepTicks = TimeSpan.TicksPerSecond;

    /// <summary>
    /// The state of <paramref name="target"/> relative to <paramref name="observer"/> corrected as CSPICE corrects it
    /// (<c>spkaps</c>, N0067, read from its source; #491), on the reference-body path: the states are read relative to
    /// <paramref name="referenceBody"/>, plus that body's motion during the light time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Light time, as <c>spkltc</c>: from the geometric distance, one iteration for LT and XLT, up to five until the light
    /// time changes by less than 1e-17 of the epoch for CN and XCN; the target is read at t − lt (t + lt for the
    /// transmission cases), its position carried by its velocity from the 100 ns of a <see cref="Time"/> to the exact
    /// epoch. The velocity carries the rate of the light time: (1 ∓ dlt/dt)·v_target − v_observer, both from the solar
    /// system barycenter, with dlt/dt = û·(v_target − v_observer) / (c ± û·v_target).
    /// </para>
    /// <para>
    /// Stellar aberration of the four "+S" corrections, as <c>stelab</c> (<c>stlabx</c> for transmission) and
    /// <c>zzstelab</c>: the light-time corrected position rotated by asin(|û × v/c|) about û × v, v the observer's velocity
    /// from the solar system barycenter (−v for transmission), and the rate of that correction on the velocity, from the
    /// light-time corrected velocity and the observer's acceleration, the central difference of its velocity over ±1 s
    /// (<c>spkacs</c>).
    /// </para>
    /// <para>
    /// The state is in ICRF at the epoch; the output frame is applied by the caller at the epoch (#593).
    /// </para>
    /// </remarks>
    /// <param name="target">The target.</param>
    /// <param name="epoch">The epoch of the observation.</param>
    /// <param name="observer">The observer.</param>
    /// <param name="aberration">The correction; <see cref="Aberration.None"/> returns <paramref name="relativeState"/>.</param>
    /// <param name="relativeState">The geometric state of the target relative to the observer at the epoch, in ICRF.</param>
    /// <param name="observerCB">The geometric state of the observer relative to the reference body at the epoch, in ICRF.</param>
    /// <param name="referenceBody">The reference body.</param>
    internal static StateVector CorrectFromAberrationCB(
        ILocalizable target, Time epoch, ILocalizable observer, Aberration aberration,
        StateVector relativeState, StateVector observerCB, CelestialItem referenceBody)
    {
        (int iterations, double sign) = aberration switch
        {
            Aberration.None => (0, 0.0),
            Aberration.LT or Aberration.LTS => (1, -1.0),
            Aberration.CN or Aberration.CNS => (ConvergedLightTimeIterations, -1.0),
            Aberration.XLT or Aberration.XLTS => (1, 1.0),
            Aberration.XCN or Aberration.XCNS => (ConvergedLightTimeIterations, 1.0),
            _ => throw new ArgumentOutOfRangeException(nameof(aberration))
        };
        if (iterations == 0)
        {
            return relativeState;
        }

        bool inertialReference = referenceBody.NaifId == 0;
        var referenceAtEpoch = inertialReference ? null : referenceBody.GetGeometricStateFromICRF(epoch).ToStateVector();
        // The epochs as CSPICE sees them, TDB seconds in double: the corrected epoch is et ∓ lt in double, as spkltc forms
        // it, and a state read at the 100 ns of a Time is carried there by its velocity (the double of a 2024 epoch
        // resolves 1.2e-7 s, 3.6 mm at the 30 km/s of the Earth)
        double epochSeconds = epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds;

        var position = relativeState.Position;
        double lightTime = position.Magnitude() / Constants.C;
        Vector3 targetVelocity = Vector3.Zero; // the target's velocity relative to the reference body at the corrected epoch
        Vector3 referenceVelocityChange = Vector3.Zero; // the reference body's velocity there minus at the epoch
        for (int i = 0; i < iterations; i++)
        {
            double shift = sign * lightTime;
            var corrected = ApparentPositionReader.Shift(epoch, shift);
            double residual = (epochSeconds + shift) - corrected.ToTDB().TimeSpanFromJ2000().TotalSeconds;
            var targetState = target.GetGeometricStateRelativeTo(corrected, referenceBody);
            targetVelocity = targetState.Velocity;
            position = targetState.Position + targetState.Velocity * residual - observerCB.Position;
            if (!inertialReference)
            {
                var reference = referenceBody.GetGeometricStateFromICRF(corrected).ToStateVector();
                position += reference.Position + reference.Velocity * residual - referenceAtEpoch.Position;
                referenceVelocityChange = reference.Velocity - referenceAtEpoch.Velocity;
            }

            double previous = lightTime;
            lightTime = position.Magnitude() / Constants.C;
            if (System.Math.Abs(lightTime - previous) <= LightTimeConvergence * System.Math.Max(1.0, System.Math.Abs(epochSeconds + shift)))
            {
                break;
            }
        }

        // The velocities from the solar system barycenter: the target's at the corrected epoch, the observer's at the epoch;
        // their difference is formed relative to the reference body first, to keep its digits
        var referenceVelocity = inertialReference ? Vector3.Zero : referenceAtEpoch.Velocity;
        var targetSsbVelocity = targetVelocity + referenceVelocityChange + referenceVelocity;
        var observerSsbVelocity = observerCB.Velocity + referenceVelocity;
        var geometricVelocity = targetVelocity + referenceVelocityChange - observerCB.Velocity;

        // spkltc: the rate of the light time, and the velocity it scales
        double distance = position.Magnitude();
        double lightTimeRate = 0.0;
        if (distance > 0.0)
        {
            var direction = position / distance;
            lightTimeRate = (direction * geometricVelocity) / (Constants.C - sign * (direction * targetSsbVelocity));
        }

        var velocity = geometricVelocity + targetSsbVelocity * (sign * lightTimeRate);

        if (aberration is Aberration.LTS or Aberration.CNS or Aberration.XLTS or Aberration.XCNS)
        {
            (position, velocity) = ApplyStellarAberration(position, velocity, observerSsbVelocity, observer, epoch, referenceBody, sign > 0.0);
        }

        return new StateVector(position, velocity, observer, epoch, Frame.ICRF);
    }

    /// <summary>
    /// The stellar aberration of the "+S" corrections, as <c>stelab</c> (<c>stlabx</c> for transmission) and
    /// <c>zzstelab</c>: <paramref name="position"/> rotated by asin(|û × v/c|) about û × v, v the observer's velocity from
    /// the solar system barycenter (−v for transmission), and the rate of that correction added to
    /// <paramref name="velocity"/>, from the observer's acceleration, the central difference of its velocity over ±1 s
    /// (<c>spkacs</c>).
    /// </summary>
    internal static (Vector3 Position, Vector3 Velocity) ApplyStellarAberration(in Vector3 position, in Vector3 velocity,
        in Vector3 observerSsbVelocity, ILocalizable observer, in Time epoch, CelestialItem referenceBody, bool transmission)
    {
        // The observer's acceleration from the solar system barycenter, spkacs's central difference over ±1 s
        var acceleration = (ObserverSsbVelocity(observer, epoch.AddTicks(AccelerationHalfStepTicks), referenceBody)
                            - ObserverSsbVelocity(observer, epoch.AddTicks(-AccelerationHalfStepTicks), referenceBody)) * 0.5;
        var observerVelocity = transmission ? observerSsbVelocity.Inverse() : observerSsbVelocity;
        var observerAcceleration = transmission ? acceleration.Inverse() : acceleration;
        var aberrated = ApparentPositionReader.StellarAberration(position, observerVelocity);
        return (aberrated, velocity + StellarAberrationRate(position, velocity, observerVelocity, observerAcceleration));
    }

    /// <summary>The velocity of an observer from the solar system barycenter, in ICRF, through its reference body.</summary>
    private static Vector3 ObserverSsbVelocity(ILocalizable observer, in Time epoch, CelestialItem referenceBody)
    {
        var velocity = observer.GetGeometricStateRelativeTo(epoch, referenceBody).Velocity;
        return referenceBody.NaifId == 0 ? velocity : velocity + referenceBody.GetGeometricStateFromICRF(epoch).ToStateVector().Velocity;
    }

    /// <summary>
    /// The time derivative of the stellar aberration correction of <c>stelab</c>, as <c>zzstelab</c> gives it. The rotation
    /// of p = r·û by asin(s) about û × w, w = v/c and s = |û × w|, is r·[(√(1 − s²) − û·w)·û + w], so the correction is
    /// C = r·[(√(1 − s²) − 1 − û·w)·û + w], smooth where s = 0, and its derivative follows from ṙ = û·ṗ,
    /// dû/dt = (ṗ − ṙ·û)/r and dw/dt = a/c.
    /// </summary>
    /// <param name="position">The light-time corrected position p.</param>
    /// <param name="velocity">Its light-time corrected rate ṗ.</param>
    /// <param name="observerVelocity">The observer's velocity from the solar system barycenter (its opposite for
    /// transmission).</param>
    /// <param name="observerAcceleration">Its acceleration (its opposite for transmission).</param>
    internal static Vector3 StellarAberrationRate(in Vector3 position, in Vector3 velocity, in Vector3 observerVelocity, in Vector3 observerAcceleration)
    {
        double r = position.Magnitude();
        if (r == 0.0)
        {
            return Vector3.Zero;
        }

        var u = position / r;
        var w = observerVelocity / Constants.C;
        var wDot = observerAcceleration / Constants.C;
        double rDot = u * velocity;
        var uDot = (velocity - u * rDot) / r;
        double uw = u * w;
        double uwDot = uDot * w + u * wDot;
        double s2 = w * w - uw * uw;
        double s2Dot = 2.0 * (w * wDot - uw * uwDot);
        double k = System.Math.Sqrt(1.0 - s2);
        double kDot = -s2Dot / (2.0 * k);
        double a = k - 1.0 - uw;
        return (u * a + w) * rDot + (u * (kDot - uwDot) + uDot * a + wDot) * r;
    }

    /// <summary>
    /// Get geometric state from SSB in ICRF. Direct SPICE call, no caching.
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    internal virtual OrbitalParameters.OrbitalParameters GetGeometricStateFromICRF(in Time date)
    {
        return ItemProvider.GetEphemeris(date, this, Barycenters.SOLAR_SYSTEM_BARYCENTER, Frame.ICRF, Aberration.None).ToStateVector();
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
        return target1Position.NormalizeOrZero().Angle(target2Position.NormalizeOrZero());
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
        return model.ToBody(position, ApparentPositionReader.Shift(from.Epoch, lightTime));
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
    /// <param name="value">The distance, m.</param>
    /// <param name="aberration"></param>
    /// <param name="stepSize">The step of the samples. The library's search (<see cref="GeometryFinder"/>) finds every
    /// window and gap when it is at most half the time between two extremes of the quantity, down to the resolution of
    /// Brent's <c>localmin</c> (a window or gap narrower than about 2 √ε h + 2τ/3, h the step and τ 1 µs, can be missed,
    /// a tangency between samples is not found; the remarks of <see cref="GeometryFinder"/>); CSPICE's, when it is
    /// shorter than the shortest interval where the quantity is monotone.</param>
    /// <returns></returns>
    /// <remarks>When both objects come from the kernels and one of them is a spacecraft, CSPICE's <c>gfdist_c</c>
    /// searches; otherwise the library does (<see cref="GeometryFinder"/>). Both take the distance in metres (#466).</remarks>
    public IEnumerable<Window> FindWindowsOnDistanceConstraint(in Window searchWindow, ILocalizable observer,
        RelationnalOperator relationalOperator, double value, Aberration aberration, in TimeSpan stepSize)
    {
        // SpiceAPI takes metres, as this method: the native layer converts them to km for gfdist_c (#466)
        if (IsSpiceBacked && observer is CelestialItem ci1 && ci1.IsSpiceBacked
            && (IsSpacecraft || ci1.IsSpacecraft))
            return SpiceAPI.Instance.FindWindowsOnDistanceConstraint(
                searchWindow, observer.NaifId, NaifId, relationalOperator, value, aberration, stepSize);

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
    /// time between the extremes of the margins (a quarter of the period for the eclipses of a satellite), down to the
    /// resolution of Brent's <c>localmin</c>: a window or a gap narrower than about 2 √ε h + 2τ/3 (h the step, τ the
    /// convergence tolerance; 19 µs at a 10 min step with τ = 1 µs) can be missed, and a tangency between samples is not
    /// found (#668); see the reference page on occultations.
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
    /// <param name="stepSize">The step of the samples. The library's search (<see cref="GeometryFinder"/>) finds every
    /// window and gap when it is at most half the time between two extremes of the quantity, down to the resolution of
    /// Brent's <c>localmin</c> (a window or gap narrower than about 2 √ε h + 2τ/3, h the step and τ 1 µs, can be missed,
    /// a tangency between samples is not found; the remarks of <see cref="GeometryFinder"/>); CSPICE's, when it is
    /// shorter than the shortest interval where the quantity is monotone.</param>
    /// <returns></returns>
    /// <remarks>
    /// CSPICE's <c>gfposc_c</c> searches when the item and the observer are from the kernels, one of them a spacecraft, and
    /// the frame is one SPICE reads as the library does; in a frame whose reads are the library's (TEME, CIRS, TIRS, the
    /// frame of a site built from coordinates, of a site from the kernels whose frame id no loaded kernel defines, of a
    /// managed spacecraft with an attitude record) the library searches, on the reads in that frame (third review of #690;
    /// before, <c>gfposc_c</c> got a name SPICE does not know and returned no window).
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public IEnumerable<Window> FindWindowsOnCoordinateConstraint(in Window searchWindow, ILocalizable observer, Frame frame,
        CoordinateSystem coordinateSystem, Coordinate coordinate, RelationnalOperator relationalOperator, double value, double adjustValue, Aberration aberration,
        in TimeSpan stepSize)
    {
        // CSPICE searches only in a frame SPICE reads as the library does (third review of #690)
        if (IsSpiceBacked && observer is CelestialItem ci3 && ci3.IsSpiceBacked
            && (IsSpacecraft || ci3.IsSpacecraft) && !SpiceAPI.IsManagedFrame(frame))
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
                    // The angles in the search frame, as gfposc, not rotated to ICRF as ToEquatorial (#680)
                    var equatorial = Equatorial.FromPositionInFrame(stateVector.Position, stateVector.Epoch);
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
                // CSPICE's PLANETOGRAPHIC (#682): the sense of the longitude of the observer's body read once, at the start
                bool positiveWest = observer is CelestialBody observerBody &&
                                    GeodeticCoordinates.IsPlanetographicLongitudePositiveWest(observerBody, searchWindow.StartDate);
                evaluateCoordinates = date =>
                    GeodeticCoordinates.PlanetographicCoordinate(GetEphemeris(date, observer, frame, aberration).ToStateVector(), coordinate, positiveWest);
                break;
        }

        return _geometryFinder.FindWindowsWithCondition(searchWindow, evaluateCoordinates, relationalOperator, value, adjustValue, stepSize);
    }

    #endregion

    #region Operators

    /// <summary>
    /// The identifier, name, mass (kg) and GM (m^3/s^2) of the item, one "title : value" line each, every number in the
    /// shortest form that parses back to its property in the invariant culture (#681: the mass and GM were printed to 7
    /// significant digits, GM under "m^3.s^2").
    /// </summary>
    public override string ToString()
    {
        return $"{"Identifier :",TITLE_WIDTH} {NaifId,-VALUE_WIDTH}{Environment.NewLine}" +
               $"{"Name :",TITLE_WIDTH} {Name,-VALUE_WIDTH}{Environment.NewLine}" +
               $"{"Mass (kg) :",TITLE_WIDTH} {Mass.ToString(CultureInfo.InvariantCulture),-VALUE_WIDTH}{Environment.NewLine}" +
               $"{"GM (m^3/s^2) :",TITLE_WIDTH} {GM.ToString(CultureInfo.InvariantCulture),-VALUE_WIDTH}{Environment.NewLine}";
    }

    /// <summary>
    /// What differs between the models of this body and of <paramref name="other"/>, a body of the same NAIF identifier,
    /// for the message of a rejected pair; null when they give the same forces: the same GM, the same gravitational field
    /// (<see cref="GravitationalField.HasSameModel"/>) and, for two <see cref="CelestialBody"/>, the same radii and the same
    /// atmospheric model (#674). Albedo and thermal properties are not compared: the forces of a propagation built from
    /// its bodies do not read them.
    /// </summary>
    internal virtual string ModelDifference(CelestialItem other)
    {
        if (ReferenceEquals(this, other))
        {
            return null;
        }

        if (!GM.Equals(other.GM))
        {
            return string.Create(CultureInfo.InvariantCulture, $"GM ({GM:R} and {other.GM:R} m³/s²)");
        }

        if (!GravitationalField.HasSameModel(other.GravitationalField))
        {
            return "gravitational field";
        }

        return (this is CelestialBody) == (other is CelestialBody) ? null : "kind of body";
    }

    /// <summary>
    /// Rejects listed bodies of one NAIF identifier whose models differ, before any of them is dropped as a duplicate: the
    /// bodies of the central body's identifier against <paramref name="centralBody"/>, the others against the first of
    /// their identifier (#674). A propagation takes one model per body.
    /// </summary>
    /// <param name="centralBody">The central body of the propagation, the observer of the orbit or the Sun.</param>
    /// <param name="bodies">The listed bodies.</param>
    /// <param name="paramName">The name of the list, for the exception.</param>
    /// <exception cref="ArgumentException">Two bodies of one NAIF identifier have different models.</exception>
    internal static void RequireOneModelPerBody(CelestialItem centralBody, IReadOnlyList<CelestialItem> bodies,
        string paramName)
    {
        foreach (var group in bodies.Where(b => b != null).GroupBy(b => b.NaifId))
        {
            bool central = centralBody != null && group.Key == centralBody.NaifId;
            var reference = central ? centralBody : group.First();
            foreach (var body in group)
            {
                var difference = reference.ModelDifference(body);
                if (difference == null)
                {
                    continue;
                }

                throw new ArgumentException(central
                    ? $"The listed body {body.Name} (NAIF {body.NaifId}) has another {difference} than {reference.Name}, " +
                      "the central body of the orbit: a propagation takes the models of its central body. List the " +
                      "observer of the orbit itself, or build the orbit on the listed body."
                    : $"The listed bodies {reference.Name} and {body.Name} (NAIF {body.NaifId}) differ by their " +
                      $"{difference}: a propagation takes one model per body. List one of them.", paramName);
            }
        }
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