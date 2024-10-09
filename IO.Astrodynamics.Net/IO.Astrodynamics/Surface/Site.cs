using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Surface
{
    public class Site : ILocalizable, IEquatable<Site>
    {
        private readonly IDataProvider _dataProvider;
        private readonly GeometryFinder _geometryFinder = new GeometryFinder();
        private readonly bool _isFromKernel = false;

        private SortedDictionary<Time, StateVector> _stateVectorsRelativeToICRF = new SortedDictionary<Time, StateVector>();
        public int Id { get; }
        public int NaifId { get; }
        public string Name { get; }
        public CelestialBody CelestialBody { get; }

        public Planetodetic Planetodetic { get; }

        public OrbitalParameters.OrbitalParameters InitialOrbitalParameters { get; }

        public SiteFrame Frame { get; }
        public double GM { get; } = 0.0;
        public double Mass { get; } = 0.0;

        /// <summary>
        /// Initializes a new instance of the <see cref="Site"/> class with default planetodetic coordinates.
        /// </summary>
        /// <param name="userId">The unique identifier for the site.</param>
        /// <param name="name">The name of the site.</param>
        /// <param name="celestialItem">The celestial body associated with the site.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="celestialItem"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="userId"/> is less than or equal to zero.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
        public Site(int userId, string name, CelestialBody celestialItem) : this(userId, name, celestialItem,
            new Planetodetic(double.NaN, double.NaN, double.NaN))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Site"/> class.
        /// </summary>
        /// <param name="userId">The unique identifier for the site.</param>
        /// <param name="name">The name of the site.</param>
        /// <param name="celestialItem">The celestial body associated with the site.</param>
        /// <param name="planetodetic">The planetodetic coordinates of the site.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="celestialItem"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="userId"/> is less than or equal to zero.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or empty.</exception>
        public Site(int userId, string name, CelestialBody celestialItem, Planetodetic planetodetic)
        {
            if (celestialItem == null) throw new ArgumentNullException(nameof(celestialItem));
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Value cannot be null or empty.", nameof(name));
            _dataProvider = Configuration.Instance.DataProvider;
            Name = name;
            CelestialBody = celestialItem;
            Id = userId;
            NaifId = celestialItem.NaifId * 1000 + userId;

            if (double.IsNaN(planetodetic.Latitude))
            {
                _isFromKernel = true;
                InitialOrbitalParameters = GetEphemeris(Time.J2000TDB, celestialItem, celestialItem.Frame, Aberration.None);
                Planetodetic = GetPlanetocentricCoordinates().ToPlanetodetic(CelestialBody.Flattening, CelestialBody.EquatorialRadius);
            }
            else
            {
                Planetodetic = planetodetic;
                InitialOrbitalParameters = GetEphemeris(Time.J2000TDB, CelestialBody, CelestialBody.Frame, Aberration.None);
            }

            Frame = new SiteFrame(name.ToUpper() + "_TOPO", this);
        }

        /// <summary>
        /// Return known center of motions
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
        /// Get site planetocentric coordinates
        /// </summary>
        /// <returns></returns>
        private Planetocentric GetPlanetocentricCoordinates()
        {
            var position = InitialOrbitalParameters.ToStateVector().Position;

            var lon = System.Math.Atan2(position.Y, position.X);

            var lat = System.Math.Asin(position.Z / position.Magnitude());

            return new Planetocentric(lon, lat, position.Magnitude());
        }

        /// <summary>
        /// Get horizontal coordinates
        /// </summary>
        /// <param name="epoch"></param>
        /// <param name="target"></param>
        /// <param name="aberration"></param>
        /// <returns></returns>
        public Horizontal GetHorizontalCoordinates(in Time epoch, ILocalizable target, Aberration aberration)
        {
            var position = target.GetEphemeris(epoch, this, Frame, aberration).ToStateVector().Position;

            var az = -System.Math.Atan2(position.Y, position.X);
            if (az < 0)
            {
                az += Constants._2PI;
            }

            var el = System.Math.Asin(position.Z / position.Magnitude());

            return new Horizontal(az, el, position.Magnitude(), epoch);
        }

        /// <summary>
        /// Get site ephemeris
        /// </summary>
        /// <param name="searchWindow"></param>
        /// <param name="observer"></param>
        /// <param name="frame"></param>
        /// <param name="aberration"></param>
        /// <param name="stepSize"></param>
        /// <returns></returns>
        public IEnumerable<OrbitalParameters.OrbitalParameters> GetEphemeris(in Window searchWindow, ILocalizable observer, Frame frame, Aberration aberration,
            in TimeSpan stepSize)
        {
            var ephemeris = new List<OrbitalParameters.OrbitalParameters>();
            for (Time i = searchWindow.StartDate; i <= searchWindow.EndDate; i += stepSize)
            {
                ephemeris.Add(GetEphemeris(i, observer, frame, aberration));
            }

            return ephemeris;
        }

        /// <summary>
        /// Get site ephemeris
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public OrbitalParameters.OrbitalParameters GetGeometricStateFromICRF(in Time date)
        {
            return _stateVectorsRelativeToICRF.GetOrAdd(date, dt =>
            {
                if (_isFromKernel)
                {
                    return _dataProvider.GetEphemerisFromICRF(dt, this, Frames.Frame.ICRF, Aberration.None).ToStateVector();
                }

                return GetEphemeris(dt, new Barycenter(0, dt), Frames.Frame.ICRF, Aberration.None).ToStateVector();
            });
        }

        /// <summary>
        /// Get site ephemeris
        /// </summary>
        /// <param name="date"></param>
        /// <param name="observer"></param>
        /// <param name="frame"></param>
        /// <param name="aberration"></param>
        /// <returns></returns>
        public OrbitalParameters.OrbitalParameters GetEphemeris(in Time date, ILocalizable observer, Frame frame, Aberration aberration)
        {
            if (_isFromKernel)
            {
                var targetGeometricStateFromSSB = this.GetGeometricStateFromICRF(date).ToStateVector();
                var observerGeometricStateFromSSB = observer.GetGeometricStateFromICRF(date).ToStateVector();
                var relativeState = new StateVector(targetGeometricStateFromSSB.Position - observerGeometricStateFromSSB.Position,
                    targetGeometricStateFromSSB.Velocity - observerGeometricStateFromSSB.Velocity,
                    observer, date, Frames.Frame.ICRF);
                if (aberration is Aberration.LT or Aberration.XLT or Aberration.LTS or Aberration.XLTS)
                {
                    relativeState = CorrectFromAberration(date, observer, aberration, relativeState, observerGeometricStateFromSSB);
                }

                return relativeState.ToFrame(frame);
            }

            var siteInFrame = new StateVector(Planetodetic.ToPlanetocentric(CelestialBody.Flattening, CelestialBody.EquatorialRadius).ToCartesianCoordinates(),
                Vector3.Zero, CelestialBody, date, CelestialBody.Frame).ToFrame(frame).ToStateVector();
            return siteInFrame.RelativeTo(observer, aberration);
        }
        
        protected StateVector CorrectFromAberration(Time epoch, ILocalizable observer, Aberration aberration, StateVector relativeState,
            StateVector observerGeometricStateFromSSB)
        {
            var lightTime = TimeSpan.FromSeconds(relativeState.Position.Magnitude() / Constants.C);

            Time newEpoch;
            if (aberration is Aberration.LT or Aberration.LTS)
                newEpoch = epoch - lightTime;
            else
                newEpoch = epoch + lightTime;

            var targetNewGeometricStateFromSSB = this.GetGeometricStateFromICRF(newEpoch).ToStateVector();
            var correctedGeometricState = new StateVector(targetNewGeometricStateFromSSB.Position - observerGeometricStateFromSSB.Position,
                targetNewGeometricStateFromSSB.Velocity - observerGeometricStateFromSSB.Velocity, observer, epoch, Frames.Frame.ICRF);

            if (aberration == Aberration.LTS || aberration == Aberration.XLTS)
            {
                var voverc = observerGeometricStateFromSSB.Velocity.Magnitude() / Constants.C;
                var stellarAberration = observerGeometricStateFromSSB.Velocity * voverc;
                correctedGeometricState.UpdatePosition(relativeState.Position + stellarAberration);
            }

            return correctedGeometricState;
        }

        /// <summary>
        /// Compute angular separation from site
        /// </summary>
        /// <param name="epoch"></param>
        /// <param name="target1"></param>
        /// <param name="target2"></param>
        /// <param name="aberration"></param>
        /// <returns></returns>
        public double AngularSeparation(in Time epoch, ILocalizable target1, ILocalizable target2, Aberration aberration)
        {
            var target1Position = target1.GetEphemeris(epoch, this, Frames.Frame.ICRF, aberration).ToStateVector().Position;
            var target2Position = target2.GetEphemeris(epoch, this, Frames.Frame.ICRF, aberration).ToStateVector().Position;
            return target1Position.Angle(target2Position);
        }


        /// <summary>
        /// Find when distance constraint occurs
        /// </summary>
        /// <param name="searchWindow"></param>
        /// <param name="observer"></param>
        /// <param name="relationalOperator"></param>
        /// <param name="value"></param>
        /// <param name="aberration"></param>
        /// <param name="stepSize"></param>
        /// <returns></returns>
        public IEnumerable<Window> FindWindowsOnDistanceConstraint(in Window searchWindow, ILocalizable observer, RelationnalOperator relationalOperator, double value,
            Aberration aberration, in TimeSpan stepSize)
        {
            Func<Time, double> calculateDistance = date => { return GetEphemeris(date, observer, Frames.Frame.ICRF, aberration).ToStateVector().Position.Magnitude(); };

            return _geometryFinder.FindWindowsWithCondition(searchWindow, calculateDistance, relationalOperator, value, stepSize);
        }

        /// <summary>
        /// Find when occultation constraint occurs
        /// </summary>
        /// <param name="searchWindow"></param>
        /// <param name="target"></param>
        /// <param name="targetShape"></param>
        /// <param name="frontBody"></param>
        /// <param name="frontShape"></param>
        /// <param name="occultationType"></param>
        /// <param name="aberration"></param>
        /// <param name="stepSize"></param>
        /// <returns></returns>
        public IEnumerable<Window> FindWindowsOnOccultationConstraint(in Window searchWindow, ILocalizable target, ShapeType targetShape, INaifObject frontBody,
            ShapeType frontShape, OccultationType occultationType, Aberration aberration, in TimeSpan stepSize)
        {
            return target.FindWindowsOnOccultationConstraint(searchWindow, this, targetShape, frontBody, frontShape, occultationType, aberration, stepSize);
        }

        /// <summary>
        /// Find when coordinate constraint occurs
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
        public IEnumerable<Window> FindWindowsOnCoordinateConstraint(in Window searchWindow, ILocalizable observer, Frame frame, CoordinateSystem coordinateSystem,
            Coordinate coordinate, RelationnalOperator relationalOperator, double value, double adjustValue, Aberration aberration, in TimeSpan stepSize)
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

        /// <summary>
        /// Find when illumination constraint occurs
        /// </summary>
        /// <param name="searchWindow"></param>
        /// <param name="observer"></param>
        /// <param name="illuminationType"></param>
        /// <param name="relationalOperator"></param>
        /// <param name="value"></param>
        /// <param name="adjustValue"></param>
        /// <param name="aberration"></param>
        /// <param name="stepSize"></param>
        /// <param name="illuminationSource"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        public IEnumerable<Window> FindWindowsOnIlluminationConstraint(in Window searchWindow, ILocalizable observer, IlluminationAngle illuminationType,
            RelationnalOperator relationalOperator, double value, double adjustValue, Aberration aberration, in TimeSpan stepSize, ILocalizable illuminationSource,
            string method = "Ellipsoid")
        {
            Func<Time, double> evaluateIllumination = null;

            switch (illuminationType)
            {
                case IlluminationAngle.Phase:
                    evaluateIllumination = date => IlluminationPhase(date, illuminationSource, observer, aberration);
                    break;
                case IlluminationAngle.Incidence:
                    evaluateIllumination = date => IlluminationIncidence(date, illuminationSource, aberration);
                    break;
                case IlluminationAngle.Emission:
                    evaluateIllumination = date => IlluminationEmission(date, observer, aberration);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(illuminationType), illuminationType, null);
            }

            return _geometryFinder.FindWindowsWithCondition(searchWindow, evaluateIllumination, relationalOperator, value, stepSize);
        }

        /// <summary>
        /// Get illumination emission angle
        /// </summary>
        /// <param name="date"></param>
        /// <param name="observer"></param>
        /// <param name="aberration"></param>
        /// <returns></returns>
        public double IlluminationEmission(Time date, ILocalizable observer, Aberration aberration)
        {
            return observer.GetEphemeris(date, this, Frame, aberration).ToStateVector().Position.Angle(Vector3.VectorZ);
        }

        /// <summary>
        /// Get illumination incidence angle
        /// </summary>
        /// <param name="date"></param>
        /// <param name="illuminationSource"></param>
        /// <param name="aberration"></param>
        /// <returns></returns>
        public double IlluminationIncidence(Time date, ILocalizable illuminationSource, Aberration aberration)
        {
            var illuminationPosition = illuminationSource.GetEphemeris(date, this, this.Frame, aberration).ToStateVector().Position;
            return Vector3.VectorZ.Angle(illuminationPosition);
        }

        /// <summary>
        /// Get illumination phase angle
        /// </summary>
        /// <param name="date"></param>
        /// <param name="illuminationSource"></param>
        /// <param name="observer"></param>
        /// <param name="aberration"></param>
        /// <returns></returns>
        public double IlluminationPhase(Time date, ILocalizable illuminationSource, ILocalizable observer, Aberration aberration)
        {
            var illuminationPosition = illuminationSource.GetEphemeris(date, this, Frames.Frame.ICRF, aberration).ToStateVector().Position;
            return observer.GetEphemeris(date, this, Frames.Frame.ICRF, aberration).ToStateVector().Position.Angle(illuminationPosition);
        }

        /// <summary>
        /// Get sub observer point of the site
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
        /// Write frame data
        /// </summary>
        /// <param name="outputFile"></param>
        public async Task WriteFrameAsync(FileInfo outputFile)
        {
            await Frame.WriteAsync(outputFile);
        }

        /// <summary>
        /// Write ephemeris data
        /// </summary>
        /// <param name="window"></param>
        /// <param name="outputFile"></param>
        public void WriteEphemeris(Window window, FileInfo outputFile)
        {
            var ephemeris = GetEphemeris(window, CelestialBody, CelestialBody.Frame, Aberration.None, TimeSpan.FromMinutes(1.0));
            API.Instance.WriteEphemeris(outputFile, this, ephemeris.Select(x => x.ToStateVector()).ToArray());
        }

        public IEnumerable<Window> FindDayWindows(in Window searchWindow, double twilight)
        {
            var sun = Stars.SUN_BODY;
            return FindWindowsOnIlluminationConstraint(searchWindow, sun, IlluminationAngle.Incidence, RelationnalOperator.Lower, Constants.PI2 + twilight, 0.0,
                Aberration.LTS, TimeSpan.FromMinutes(1.0), sun);
        }

        #region operators

        public bool Equals(Site other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return NaifId == other.NaifId;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == this.GetType() && Equals((Site)obj);
        }


        public override int GetHashCode()
        {
            return NaifId;
        }

        public static bool operator ==(Site left, Site right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Site left, Site right)
        {
            return !Equals(left, right);
        }

        #endregion
    }
}