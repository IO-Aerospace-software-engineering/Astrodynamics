using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    public class Site : ILocalizable, IEquatable<Site>, IDisposable
    {
        private readonly IDataProvider _dataProvider;
        private readonly GeometryFinder _geometryFinder = new GeometryFinder();
        public ConcurrentDictionary<Time, StateVector> StateVectorsRelativeToICRF { get; } = new ConcurrentDictionary<Time, StateVector>();
        public int Id { get; }
        public int NaifId { get; }
        public string Name { get; }
        public CelestialBody CelestialBody { get; }
        public Planetodetic Planetodetic { get; }
        public OrbitalParameters.OrbitalParameters InitialOrbitalParameters { get; }
        public DirectoryInfo PropagationOutput { get; private set; }
        public bool IsPropagated => PropagationOutput != null;

        public SiteFrame Frame { get; }
        public double GM { get; } = 0.0;
        public double Mass { get; } = 0.0;

        public Site(int id, string name, CelestialBody celestialItem, IDataProvider dataProvider = null) : this(id, name, celestialItem,
            new Planetodetic(double.NaN, double.NaN, double.NaN), dataProvider)
        {
        }

        public Site(int id, string name, CelestialBody celestialItem, Planetodetic planetodetic, IDataProvider dataProvider = null)
        {
            if (celestialItem == null) throw new ArgumentNullException(nameof(celestialItem));
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Value cannot be null or empty.", nameof(name));
            _dataProvider = dataProvider ?? new SpiceDataProvider();
            Name = name;
            CelestialBody = celestialItem;
            Id = id;
            NaifId = celestialItem.NaifId * 1000 + id;

            if (double.IsNaN(planetodetic.Latitude))
            {
                InitialOrbitalParameters = API.Instance.ReadEphemeris(Time.J2000TDB, CelestialBody, this, CelestialBody.Frame, Aberration.None);
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

        public OrbitalParameters.OrbitalParameters GetGeometricStateFromICRF(in Time date)
        {
            return StateVectorsRelativeToICRF.GetOrAdd(date,
                x => _dataProvider.GetEphemeris(x, this, new Barycenter(Barycenters.SOLAR_SYSTEM_BARYCENTER.NaifId), Frames.Frame.ICRF, Aberration.None).ToStateVector());
        }

        /// <summary>
        /// Get site ephemeris
        /// </summary>
        /// <param name="epoch"></param>
        /// <param name="observer"></param>
        /// <param name="frame"></param>
        /// <param name="aberration"></param>
        /// <returns></returns>
        public OrbitalParameters.OrbitalParameters GetEphemeris(in Time epoch, ILocalizable observer, Frame frame, Aberration aberration)
        {
            var siteInFrame = new StateVector(Planetodetic.ToPlanetocentric(CelestialBody.Flattening, CelestialBody.EquatorialRadius).ToCartesianCoordinates(),
                Vector3.Zero, CelestialBody, epoch, CelestialBody.Frame).ToFrame(frame).ToStateVector();
            return siteInFrame.RelativeTo(observer, aberration);
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
            Func<Time, double> calculateDistance = date =>
            {
                return GetEphemeris(date, observer, Frames.Frame.ICRF, aberration).ToStateVector().Position.Magnitude();
            };

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
            RelationnalOperator relationalOperator, double value, double adjustValue, Aberration aberration, in TimeSpan stepSize, INaifObject illuminationSource,
            string method = "Ellipsoid")
        {
            return API.Instance.FindWindowsOnIlluminationConstraint(searchWindow, observer, CelestialBody, CelestialBody.Frame, Planetodetic, illuminationType, relationalOperator,
                value, adjustValue, aberration, stepSize, illuminationSource, method);
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
        /// <param name="outputFile"></param>
        /// <param name="stateVectors"></param>
        public void WriteEphemeris(FileInfo outputFile, IEnumerable<StateVector> stateVectors)
        {
            API.Instance.WriteEphemeris(outputFile, this, stateVectors);
        }

        /// <summary>
        /// Propagate site
        /// </summary>
        /// <param name="window"></param>
        /// <param name="sitesDirectory"></param>
        /// <param name="stepSize"></param>
        public async Task PropagateAsync(Window window, TimeSpan stepSize, DirectoryInfo sitesDirectory)
        {
            ResetPropagation();
            PropagationOutput = sitesDirectory.CreateSubdirectory(Name);
            var siteEphemeris = GetEphemeris(window, CelestialBody, Frames.Frame.ICRF, Aberration.None, stepSize).Select(x => x.ToStateVector());
            await WriteFrameAsync(new FileInfo(Path.Combine(PropagationOutput.CreateSubdirectory("Frames").FullName, Name + ".tf")));
            WriteEphemeris(new FileInfo(Path.Combine(PropagationOutput.CreateSubdirectory("Ephemeris").FullName, Name + ".spk")), siteEphemeris);
            API.Instance.LoadKernels(PropagationOutput);
        }

        /// <summary>
        /// Reset propagation elements
        /// </summary>
        private void ResetPropagation()
        {
            if (IsPropagated)
            {
                API.Instance.UnloadKernels(PropagationOutput);
                PropagationOutput.Delete(true);
                PropagationOutput = null;
            }
        }

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

        private void ReleaseUnmanagedResources()
        {
            if (IsPropagated)
            {
                API.Instance.UnloadKernels(PropagationOutput);
            }
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~Site()
        {
            ReleaseUnmanagedResources();
        }
    }
}