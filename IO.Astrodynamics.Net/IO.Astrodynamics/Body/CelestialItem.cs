using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Window = IO.Astrodynamics.TimeSystem.Window;

namespace IO.Astrodynamics.Body;

public abstract class CelestialItem : ILocalizable, IEquatable<CelestialItem>
{
    private readonly IDataProvider _dataProvider;
    public ConcurrentDictionary<Time, StateVector> StateVectorsRelativeToICRF { get; } = new();
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
    public bool IsMoon => NaifId is > 100 and < 1000 && (NaifId % 100) != 99;

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
    /// Instantiate celestial item from naif id with orbital parameters at given frame and epoch
    /// </summary>
    /// <param name="naifId">Naif identifier</param>
    /// <param name="frame">Initial orbital parameters frame</param>
    /// <param name="epoch">Epoch</param>
    /// <param name="geopotentialModelParameters"></param>
    /// <param name="dataProvider"></param>
    protected CelestialItem(int naifId, Frame frame, in Time epoch, GeopotentialModelParameters geopotentialModelParameters = null, IDataProvider dataProvider = null)
    {
        _dataProvider = dataProvider ?? new SpiceDataProvider();
        ExtendedInformation = _dataProvider.GetCelestialBodyInfo(naifId);

        NaifId = naifId;
        Name = string.IsNullOrEmpty(ExtendedInformation.Name)
            ? throw new InvalidOperationException(
                "Celestial celestialItem name can't be defined, please check if you have loaded associated kernels")
            : ExtendedInformation.Name;

        Mass = ExtendedInformation.GM / Constants.G;
        GM = ExtendedInformation.GM;
        GravitationalField = geopotentialModelParameters != null
            ? new GeopotentialGravitationalField(geopotentialModelParameters.GeopotentialModelPath, geopotentialModelParameters.GeopotentialDegree)
            : new GravitationalField();

        if (NaifId == Barycenters.SOLAR_SYSTEM_BARYCENTER.NaifId) return;
        if (IsPlanet || IsMoon || IsBarycenter || IsSun)
            InitialOrbitalParameters = GetEphemeris(epoch, new Barycenter(ExtendedInformation.BarycenterOfMotionId), frame, Aberration.None);

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
    /// <param name="dataProvider"></param>
    protected CelestialItem(int naifId, string name, double mass, OrbitalParameters.OrbitalParameters initialOrbitalParameters,
        GeopotentialModelParameters geopotentialModelParameters = null, IDataProvider dataProvider = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("CelestialItem must have a name");
        }

        if (mass < 0)
        {
            throw new ArgumentException("CelestialItem can't have a negative mass");
        }

        _dataProvider = dataProvider ?? new SpiceDataProvider();

        NaifId = naifId;
        Name = name;
        Mass = mass;
        GM = mass * Constants.G;
        InitialOrbitalParameters = initialOrbitalParameters;
        (InitialOrbitalParameters?.Observer as CelestialBody)?._satellites.Add(this);
        GravitationalField = geopotentialModelParameters != null
            ? new GeopotentialGravitationalField(geopotentialModelParameters.GeopotentialModelPath, geopotentialModelParameters.GeopotentialDegree)
            : new GravitationalField();
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
        var occurences = (int)System.Math.Ceiling(searchWindow.Length / stepSize) + 1;
        var ephemeris = new List<OrbitalParameters.OrbitalParameters>(occurences);
        for (int i = 0; i < occurences; i++)
        {
            var epoch = searchWindow.StartDate + stepSize * i;
            ephemeris.Add(GetEphemeris(epoch, observer, frame, aberration));
        }

        return ephemeris;
    }

    /// <summary>
    /// GetEphemeris
    /// </summary>
    /// <param name="epoch"></param>
    /// <param name="observer"></param>
    /// <param name="frame"></param>
    /// <param name="aberration"></param>
    /// <returns></returns>
    public OrbitalParameters.OrbitalParameters GetEphemeris(in Time epoch, ILocalizable observer, Frame frame, Aberration aberration)
    {
        var targetGeometricState = this.GetGeometricStateFromICRF(epoch).ToStateVector();
        var observerGeometricStateFromSSB = observer.GetGeometricStateFromICRF(epoch).ToStateVector();
        var observerGeometricState = new StateVector(targetGeometricState.Position - observerGeometricStateFromSSB.Position, targetGeometricState.Velocity - observerGeometricStateFromSSB.Velocity,
            observer, epoch, Frame.ICRF);
        if (aberration is Aberration.LT or Aberration.XLT or Aberration.LTS or Aberration.XLTS)
        {
            var lightTime = TimeSpan.FromSeconds(observerGeometricState.Position.Magnitude() / Constants.C);

            Time newEpoch;
            if (aberration is Aberration.LT or Aberration.LTS)
                newEpoch = epoch - lightTime;
            else
                newEpoch = epoch + lightTime;

            observerGeometricState = _dataProvider.GetEphemeris(newEpoch, this, observer, Frame.ICRF, Aberration.None).ToStateVector();

            if (aberration == Aberration.LTS || aberration == Aberration.XLTS)
            {
                var voverc=observerGeometricStateFromSSB.Velocity.Magnitude()/Constants.C;
                var stellarAberration = observerGeometricStateFromSSB.Velocity * voverc;
                observerGeometricState.UpdatePosition(observerGeometricState.Position + stellarAberration);
            }
        }

        return observerGeometricState.ToFrame(frame);
    }

    public OrbitalParameters.OrbitalParameters GetGeometricStateFromICRF(in Time date)
    {
        return StateVectorsRelativeToICRF.GetOrAdd(date,
            x => _dataProvider.GetEphemeris(x, this, new Barycenter(Barycenters.SOLAR_SYSTEM_BARYCENTER.NaifId), Frame.ICRF, Aberration.None).ToStateVector());
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

    public double AngularSeparation(in Time epoch, ILocalizable target1, OrbitalParameters.OrbitalParameters fromPosition, Aberration aberration)
    {
        var target1Position = target1.GetEphemeris(epoch, fromPosition.Observer, fromPosition.Frame, aberration).ToStateVector().Position -
                              fromPosition.ToStateVector().Position;
        var target2Position = this.GetEphemeris(epoch, fromPosition.Observer, fromPosition.Frame, aberration).ToStateVector().Position -
                              fromPosition.ToStateVector().Position;
        return target1Position.Normalize().Angle(target2Position.Normalize());
    }


    //Return all centers of motion up to the root 
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

    public Planetocentric SubObserverPoint(in Vector3 position, in Time epoch, Aberration aberration)
    {
        var lon = System.Math.Atan2(position.Y, position.X);

        var lat = System.Math.Asin(position.Z / position.Magnitude());

        return new Planetocentric(lon, lat, position.Magnitude());
    }

    public OccultationType? IsOcculted(CelestialItem by, OrbitalParameters.OrbitalParameters from, Aberration aberration = Aberration.LT)
    {
        double backSize = AngularSize((GetEphemeris(from.Epoch, from.Observer, from.Frame, aberration).ToStateVector().Position - from.ToStateVector().Position).Magnitude());
        double bySize = by.AngularSize((by.GetEphemeris(from.Epoch, from.Observer, from.Frame, aberration).ToStateVector().Position - from.ToStateVector().Position)
            .Magnitude());
        var angularSeparation = this.AngularSeparation(from.Epoch, by, from, aberration);
        return IsOcculted(angularSeparation, backSize, bySize);
    }

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
    /// Evaluate gravitational acceleration at given position
    /// </summary>
    /// <param name="orbitalParameters"></param>
    /// <returns></returns>
    public Vector3 EvaluateGravitationalAcceleration(OrbitalParameters.OrbitalParameters orbitalParameters)
    {
        var sv = orbitalParameters.Observer as CelestialItem != this ? orbitalParameters.RelativeTo(this, Aberration.None).ToStateVector() : orbitalParameters.ToStateVector();

        return GravitationalField.ComputeGravitationalAcceleration(sv);
    }

    #region FindWindows

    public IEnumerable<Window> FindWindowsOnDistanceConstraint(in Window searchWindow, ILocalizable observer,
        RelationnalOperator relationalOperator, double value, Aberration aberration, in TimeSpan stepSize)
    {
        Func<Time, double> calculateDistance = date => { return GetEphemeris(date, observer, Frame.ICRF, aberration).ToStateVector().Position.Magnitude(); };

        return _geometryFinder.FindWindowsWithCondition(searchWindow, calculateDistance, relationalOperator, value, stepSize);
    }

    public IEnumerable<Window> FindWindowsOnOccultationConstraint(in Window searchWindow, ILocalizable observer,
        ShapeType targetShape, INaifObject frontBody, ShapeType frontShape, OccultationType occultationType, Aberration aberration, in TimeSpan stepSize)
    {
        Func<Time, bool> calculateOccultation = date =>
        {
            var occultation = IsOcculted(frontBody as CelestialItem, new StateVector(Vector3.Zero, Vector3.Zero, observer, date, Frame.ICRF), aberration);
            if (occultationType == OccultationType.Any)
            {
                return occultation is OccultationType.Partial or OccultationType.Annular or OccultationType.Full;
            }

            return occultation == occultationType;
        };

        return _geometryFinder.FindWindowsWithCondition(searchWindow, calculateOccultation, RelationnalOperator.Equal, true, stepSize);
    }

    public IEnumerable<Window> FindWindowsOnCoordinateConstraint(in Window searchWindow, ILocalizable observer, Frame frame,
        CoordinateSystem coordinateSystem, Coordinate coordinate, RelationnalOperator relationalOperator, double value, double adjustValue, Aberration aberration,
        in TimeSpan stepSize)
    {
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