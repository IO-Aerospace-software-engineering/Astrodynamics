﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Occultation;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Surface
{
    public class Site : ILocalizable, IEquatable<Site>
    {
        private readonly IDataProvider _dataProvider;
        private readonly bool _isFromKernel = false;

        public int Id { get; }
        public int NaifId { get; }
        public string Name { get; }
        public CelestialBody CelestialBody { get; }

        /// <summary>
        /// The planetodetic coordinates of the site: longitude and latitude in rad, altitude in m above the ellipsoid of
        /// its body. For a site from the kernels, those of its position at J2000 TDB, which a search of
        /// <see cref="FindIlluminationWindows"/> and <see cref="FindDayWindows"/> by CSPICE takes as the
        /// site (#488), and whose ellipsoid normal the illumination angles take. The frame of a site from the kernels is
        /// not built from them when a loaded kernel defines the frame of its id (<see cref="SiteFrame"/>, #690).
        /// </summary>
        public Planetodetic Planetodetic { get; }

        public OrbitalParameters.OrbitalParameters InitialOrbitalParameters { get; }

        /// <summary>
        /// The topocentric frame of the site, named <c>&lt;NAME&gt;_TOPO</c>, the name upper-cased in the invariant culture
        /// whatever the culture of the thread (#798, decision 101 of #668, decided by S. Guillet, 2026-11-25: before, tr-TR or
        /// az named the frame of a site "dss_i" <c>DSS_İ_TOPO</c>).
        /// </summary>
        public SiteFrame Frame { get; }
        public double GM { get; } = 0.0;
        public double Mass { get; } = 0.0;
        public bool IsSpiceBacked => _isFromKernel;

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
        /// Initializes a site from the kernels from its entry of a catalogue, <see cref="GroundStations"/> for instance:
        /// <c>new Site(GroundStations.DSS_13, earth)</c>, the site 13 of the Earth, named <c>DSS-13</c> (R02 of #668).
        /// </summary>
        /// <param name="station">The NAIF identifier of the site, its body's times 1000 plus its own (399013 for DSS-13), and its
        /// SPICE name.</param>
        /// <param name="celestialItem">The body of the site, whose NAIF identifier times 1000 the station's holds.</param>
        /// <remarks>
        /// The constructor of <paramref name="station"/>'s user identifier (its NAIF identifier minus 1000 times the body's)
        /// and name, which reads the site's position from the kernels. NOTO (398989) and NEW NORCIA (398990) are not sites of
        /// a body in this numbering (no body 398): read them as objects of the kernels by their identifiers.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="celestialItem"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the identifier of <paramref name="station"/> is not that of a site
        /// of <paramref name="celestialItem"/> (from its identifier times 1000 plus 1 to plus 999), or its name is null or
        /// empty.</exception>
        public Site(NaifObject station, CelestialBody celestialItem) : this(SiteId(station, celestialItem), station.Name, celestialItem)
        {
        }

        // The user identifier of a catalogue site on its body, checked before any read
        private static int SiteId(NaifObject station, CelestialBody celestialItem)
        {
            if (celestialItem == null) throw new ArgumentNullException(nameof(celestialItem));
            long id = station.NaifId - 1000L * celestialItem.NaifId;
            if (id is < 1 or > 999)
            {
                throw new ArgumentException(
                    $"{station.Name} ({station.NaifId}) is not a site of {celestialItem.Name} ({celestialItem.NaifId}): a site of it has an identifier from " +
                    $"{1000L * celestialItem.NaifId + 1} to {1000L * celestialItem.NaifId + 999}.", nameof(station));
            }

            if (string.IsNullOrEmpty(station.Name)) throw new ArgumentException("The name of the station cannot be null or empty.", nameof(station));
            return (int)id;
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
                Planetodetic = GetPlanetocentricCoordinates().ToPlanetodetic(CelestialBody);
            }
            else
            {
                Planetodetic = planetodetic;
                InitialOrbitalParameters = GetEphemeris(Time.J2000TDB, CelestialBody, CelestialBody.Frame, Aberration.None);
            }

            Frame = new SiteFrame(name.ToUpperInvariant() + "_TOPO", this);
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
            var position = target.GetEphemeris(epoch, this, Frame, aberration).Position;

            var az = -System.Math.Atan2(position.Y, position.X);
            if (az < 0)
            {
                az += Constants.TwoPi;
            }

            var el = System.Math.Asin(position.Z / position.Magnitude());

            return new Horizontal(az, el, position.Magnitude(), epoch);
        }

        /// <summary>
        /// The geometric state of the site seen from <paramref name="observer"/> in ICRF at <paramref name="epoch"/>: the read
        /// of <see cref="GetEphemeris(in Time, ILocalizable, Frame, Aberration)"/> with <see cref="Frames.Frame.ICRF"/> and
        /// <see cref="Aberration.None"/>, bit for bit (R20 of #668).
        /// </summary>
        /// <param name="epoch">The epoch of the state.</param>
        /// <param name="observer">The origin of the state.</param>
        /// <returns>The state vector of the site relative to <paramref name="observer"/>, in ICRF, without correction.</returns>
        public StateVector GetEphemeris(in Time epoch, ILocalizable observer)
        {
            return GetEphemeris(epoch, observer, Frames.Frame.ICRF, Aberration.None);
        }

        /// <summary>
        /// The geometric states of the site seen from <paramref name="observer"/> in ICRF every <paramref name="stepSize"/>
        /// over <paramref name="searchWindow"/>: the read of
        /// <see cref="GetEphemeris(in Window, ILocalizable, Frame, Aberration, in TimeSpan)"/> with <see cref="Frames.Frame.ICRF"/>
        /// and <see cref="Aberration.None"/>, bit for bit (R20 of #668).
        /// </summary>
        /// <param name="searchWindow">The window of the states.</param>
        /// <param name="observer">The origin of the states.</param>
        /// <param name="stepSize">The step between two states, from the start of the window.</param>
        /// <returns>The state vectors of the site relative to <paramref name="observer"/>, in ICRF, without correction.</returns>
        public IEnumerable<StateVector> GetEphemeris(in Window searchWindow, ILocalizable observer, in TimeSpan stepSize)
        {
            return GetEphemeris(searchWindow, observer, Frames.Frame.ICRF, Aberration.None, stepSize);
        }

        /// <summary>
        /// Get site ephemeris
        /// </summary>
        /// <param name="searchWindow"></param>
        /// <param name="observer"></param>
        /// <param name="frame"></param>
        /// <param name="aberration"></param>
        /// <param name="stepSize">The step between two states, positive: the points of <see cref="Window.Sample"/>, from the
        /// start up to the end.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="stepSize"/> is zero or negative (R11 of #668: before
        /// 11.0.0 a zero step read without end).</exception>
        public IEnumerable<StateVector> GetEphemeris(in Window searchWindow, ILocalizable observer, Frame frame, Aberration aberration,
            in TimeSpan stepSize)
        {
            CelestialItem.RequirePositiveEphemerisStep(stepSize);
            var ephemeris = new List<StateVector>();
            foreach (var epoch in searchWindow.Sample(stepSize))
            {
                ephemeris.Add(GetEphemeris(epoch, observer, frame, aberration));
            }

            return ephemeris;
        }

        /// <summary>
        /// Computes the geometric state relative to a reference body.
        /// Kernel sites use SPICE; custom sites use body-fixed→ICRF + chain.
        /// </summary>
        public StateVector GetGeometricStateRelativeTo(in Time epoch, CelestialItem referenceBody)
        {
            if (_isFromKernel)
            {
                return _dataProvider.GetEphemeris(epoch, this, referenceBody, Frames.Frame.ICRF, Aberration.None);
            }

            // Custom site: body-fixed → ICRF
            var siteOnBody = new StateVector(
                Planetodetic.ToCartesianCoordinates(CelestialBody),
                Vector3.Zero, CelestialBody, epoch, CelestialBody.Frame)
                .ToFrame(Frames.Frame.ICRF).ToStateVector();

            if (referenceBody.NaifId == CelestialBody.NaifId)
                return siteOnBody;

            var bodyFromRef = CelestialBody.GetGeometricStateRelativeTo(epoch, referenceBody);
            return new StateVector(
                bodyFromRef.Position + siteOnBody.Position,
                bodyFromRef.Velocity + siteOnBody.Velocity,
                referenceBody, epoch, Frames.Frame.ICRF);
        }

        /// <summary>
        /// The state of the site seen from <paramref name="observer"/> in <paramref name="frame"/>, corrected by
        /// <paramref name="aberration"/>: a site of the kernels seen from a body, a barycenter or a spacecraft of the
        /// kernels, by SPICE (<c>spkezr_c</c>); otherwise relative to the site's body, as CSPICE's <c>spkcpt_c</c> corrects
        /// a site given by its coordinates (the site itself at t ∓ lt, the light time of <c>spkltc</c>, the stellar
        /// aberration of <c>stelab</c> and <c>stlabx</c>, a non-inertial frame at the epoch of its centre;
        /// <see cref="CelestialItem.ApparentStateFromReferenceBody"/>).
        /// </summary>
        /// <remarks>
        /// A site given by its coordinates seen from an observer of the kernels with a correction other than
        /// <see cref="Aberration.None"/> is corrected as <c>spkcpt_c</c> corrects it, the observer its own body included
        /// (#489). Before 11.0.0 the correction was that of the observer seen from the site's body
        /// (<see cref="OrbitalParameters.OrbitalParameters.RelativeTo"/>), the site never moved back in time: 82.7 km
        /// from <c>spkcpt_c</c> seen from the Moon with LT, about 15000 km from the Sun, and the geometric state seen from
        /// its own body (640 m with LT). Without correction, the geometric state is unchanged, bit for bit.
        /// </remarks>
        public StateVector GetEphemeris(in Time date, ILocalizable observer, Frame frame, Aberration aberration)
        {
            // For SPICE-backed CelestialItem observers (planets, moons, barycenters, kernel-backed spacecraft)
            if (observer is CelestialItem && observer.IsSpiceBacked)
            {
                if (_isFromKernel)
                {
                    return _dataProvider.GetEphemeris(date, this, observer, frame, aberration);
                }

                if (aberration == Aberration.None)
                {
                    // Custom site, geometric: body-fixed computation with RelativeTo for observer change
                    var siteInFrame = new StateVector(
                        Planetodetic.ToCartesianCoordinates(CelestialBody),
                        Vector3.Zero, CelestialBody, date, CelestialBody.Frame).ToFrame(frame).ToStateVector();
                    return siteInFrame.RelativeTo(observer, Aberration.None);
                }
            }

            // Reference-body approach: non-SPICE observers (Spacecraft, other Sites), and a custom site corrected for any
            // observer (#489)
            return CelestialItem.ApparentStateFromReferenceBody(this, date, observer, frame, aberration, CelestialBody);
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
            var target1Position = target1.GetEphemeris(epoch, this, Frames.Frame.ICRF, aberration).Position;
            var target2Position = target2.GetEphemeris(epoch, this, Frames.Frame.ICRF, aberration).Position;
            return target1Position.Angle(target2Position);
        }


        /// <summary>
        /// Whether CSPICE runs a search of this site, the target, seen from <paramref name="observer"/>: the site and the
        /// observer from the kernels, the observer a spacecraft (the routing of the searches on the observer, R15 of #668,
        /// lot L6).
        /// </summary>
        internal bool SearchesOnSpice(ILocalizable observer) =>
            IsSpiceBacked && observer is CelestialItem item && item.IsSpiceBacked && item.IsSpacecraft;

        // The illumination search, its arguments checked: gfilum_c for a site of the kernels, the library's search otherwise,
        // to the convergence tolerance given (1 µs when null), which gfilum_c cannot take
        private IReadOnlyList<Window> IlluminationWindows(in Window searchWindow, IlluminationAngle illuminationType, RelationnalOperator relationalOperator,
            double value, double adjustValue, Aberration aberration, in TimeSpan stepSize, ILocalizable illuminationSource, TimeSpan? tolerance)
        {
            if (_isFromKernel && illuminationSource is INaifObject src)
            {
                WindowSearchSettings.RejectToleranceOnCspice(tolerance, nameof(WindowSearchOptions), "gfilum_c");
                return SpiceAPI.Instance.FindWindowsOnIlluminationConstraint(
                    searchWindow, NaifId, CelestialBody.NaifId, CelestialBody.Frame,
                    Planetodetic, illuminationType, relationalOperator, value, adjustValue,
                    aberration, stepSize, src.NaifId, "ELLIPSOID");
            }

            if (illuminationType != IlluminationAngle.Incidence)
            {
                throw new ArgumentOutOfRangeException(nameof(illuminationType), illuminationType, null);
            }

            return GeometryFinder.Find(searchWindow, date => IlluminationIncidence(date, illuminationSource, aberration), relationalOperator, value, adjustValue,
                stepSize, GeometryFinder.ToleranceOf(tolerance));
        }

        /// <summary>
        /// The windows where the illumination angle of <paramref name="source"/> at the site satisfies the relation: the
        /// incidence, the angle between the direction of the source and the normal to the ellipsoid of the body at
        /// <see cref="Planetodetic"/> (R15 of #668, lot L6, decided by S. Guillet, 2026-11-19). CSPICE's <c>gfilum_c</c>
        /// searches for a site of the kernels, the library otherwise (<see cref="GeometryFinder"/>).
        /// </summary>
        /// <remarks>
        /// The emission and phase angles are angles at the point towards an observer, which this search does not take: they
        /// throw (#682). With an observer, search them with <see cref="SpiceAPI.FindWindowsOnIlluminationConstraint"/>
        /// (objects of the kernels) or with <see cref="GeometryFinder.FindWindows(Window, Func{Time, double}, RelationnalOperator, double, WindowSearchOptions)"/>
        /// on <see cref="IlluminationEmission"/> or <see cref="IlluminationPhase"/>.
        /// </remarks>
        /// <param name="window">The search window.</param>
        /// <param name="angle">The angle: <see cref="IlluminationAngle.Incidence"/>.</param>
        /// <param name="relationalOperator">The relation.</param>
        /// <param name="value">The angle, rad; ignored by the extremes.</param>
        /// <param name="source">The source of the illumination, the Sun for the day.</param>
        /// <param name="options">The step (required), the aberration, the convergence tolerance and the adjustment value.</param>
        /// <returns>The windows, in time order, labelled in the scale of the search window's start (<see cref="Body.WindowSearches"/>).</returns>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="angle"/> is <see cref="IlluminationAngle.Emission"/> or
        /// <see cref="IlluminationAngle.Phase"/>; the step is not given, an adjustment value is given with a relation other
        /// than an absolute extreme, or a convergence tolerance on CSPICE's search (<c>options</c>).</exception>
        /// <exception cref="ArgumentOutOfRangeException">With a comparison operator, <paramref name="value"/> is outside
        /// [0, π], or NaN: the angles are in radians (R11 of #668).</exception>
        /// <exception cref="InvalidOperationException">On the path of CSPICE, SPICE cannot run the search, with its
        /// message.</exception>
        public IReadOnlyList<Window> FindIlluminationWindows(in Window window, IlluminationAngle angle, RelationnalOperator relationalOperator, double value,
            ILocalizable source, WindowSearchOptions options = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (angle is IlluminationAngle.Emission or IlluminationAngle.Phase)
            {
                throw new ArgumentException(
                    $"The {angle} angle of {Name} is measured towards an observer, which this search does not take (it would be the site itself, where the angle is undefined). Search it with SpiceAPI.FindWindowsOnIlluminationConstraint and the observer's NAIF id, or with GeometryFinder.FindWindows on IlluminationEmission or IlluminationPhase with an observer.",
                    nameof(angle));
            }

            GeometryFinder.CheckIlluminationThreshold(relationalOperator, value);
            options ??= WindowSearchOptions.Default;
            WindowSearchSettings.CheckAdjustValue(options, relationalOperator);
            var step = WindowSearchSettings.RequireStep(options.Step, nameof(WindowSearchOptions));
            return WindowSearchSettings.InScaleOf(IlluminationWindows(window, angle, relationalOperator, value, options.AdjustValue ?? 0.0,
                options.Aberration ?? Aberration.None, step, source, options.ConvergenceTolerance), window);
        }

        /// <summary>
        /// The windows where a horizontal coordinate of <paramref name="target"/> seen from the site, in its topocentric
        /// frame, satisfies the relation, as <see cref="GetHorizontalCoordinates"/> gives it (R15 of #668, lot L6, choice
        /// 3, decided by S. Guillet, 2026-11-19): the elevation is the declination in <see cref="Frame"/>, the range its
        /// range, and the azimuth, −atan2(y, x) in [0, 2π), 2π minus the right ascension, its relation mirrored (a
        /// threshold of 0, the north, the right ascension 0 with the same relation). The library searches
        /// (<see cref="GeometryFinder"/>), whatever the target: the coordinate search of
        /// <see cref="WindowSearches.FindCoordinateWindows"/> with the site as observer.
        /// </summary>
        /// <param name="window">The search window.</param>
        /// <param name="target">The target.</param>
        /// <param name="coordinate">The horizontal coordinate.</param>
        /// <param name="relationalOperator">The relation.</param>
        /// <param name="value">The value, rad or m; ignored by the extremes.</param>
        /// <param name="options">The step (required), the aberration, the convergence tolerance and the adjustment value.</param>
        /// <returns>The windows, in time order, labelled in the scale of the search window's start (<see cref="Body.WindowSearches"/>).</returns>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">With a comparison operator, an azimuth outside [0, 2π] or an
        /// elevation outside [−π/2, π/2], or NaN: the angles are in radians (R11 of #668).</exception>
        /// <exception cref="ArgumentException">The step is not given, or an adjustment value is given with a relation other
        /// than an absolute extreme (<c>options</c>).</exception>
        public IReadOnlyList<Window> FindHorizontalWindows(in Window window, ILocalizable target, HorizontalCoordinate coordinate,
            RelationnalOperator relationalOperator, double value, WindowSearchOptions options = null)
        {
            bool extreme = GeometryFinder.IsExtremeOperator(relationalOperator);
            switch (coordinate)
            {
                case HorizontalCoordinate.Elevation:
                    if (!extreme) GeometryFinder.CheckAngle(value, -Constants.HalfPi, Constants.HalfPi, "[−π/2, π/2]", "elevation threshold", nameof(value));
                    return this.FindCoordinateWindows(window, target, Frame, CoordinateSystem.RaDec, Coordinate.Declination, relationalOperator, value, options);
                case HorizontalCoordinate.Range:
                    return this.FindCoordinateWindows(window, target, Frame, CoordinateSystem.RaDec, Coordinate.Range, relationalOperator, value, options);
                case HorizontalCoordinate.Azimuth:
                    if (!extreme) GeometryFinder.CheckAngle(value, 0.0, Constants.TwoPi, "[0, 2π]", "azimuth threshold", nameof(value));
                    // az = 2π − ra for ra > 0, az = 0 for ra = 0: a relation to 0 is the same relation of ra to 0
                    bool north = !extreme && value == 0.0;
                    return this.FindCoordinateWindows(window, target, Frame, CoordinateSystem.RaDec, Coordinate.RightAscension,
                        north ? relationalOperator : Mirrored(relationalOperator), north ? 0.0 : Constants.TwoPi - value, options);
                default:
                    throw new ArgumentOutOfRangeException(nameof(coordinate), coordinate, null);
            }
        }

        // The relation of the right ascension to 2π minus a value, of the azimuth to the value
        private static RelationnalOperator Mirrored(RelationnalOperator relationalOperator) => relationalOperator switch
        {
            RelationnalOperator.Greater => RelationnalOperator.Lower,
            RelationnalOperator.Lower => RelationnalOperator.Greater,
            RelationnalOperator.GreaterOrEqual => RelationnalOperator.LowerOrEqual,
            RelationnalOperator.LowerOrEqual => RelationnalOperator.GreaterOrEqual,
            RelationnalOperator.AbsoluteMin => RelationnalOperator.AbsoluteMax,
            RelationnalOperator.AbsoluteMax => RelationnalOperator.AbsoluteMin,
            RelationnalOperator.LocalMin => RelationnalOperator.LocalMax,
            RelationnalOperator.LocalMax => RelationnalOperator.LocalMin,
            _ => relationalOperator
        };

        /// <summary>
        /// The emission angle at the site: the angle between the direction of <paramref name="observer"/> and the normal to
        /// the ellipsoid of the body at <see cref="Planetodetic"/>, the normal CSPICE's <c>gfilum_c</c> takes for the site
        /// (<see cref="FindIlluminationWindows"/>, #488).
        /// </summary>
        /// <param name="date">The epoch.</param>
        /// <param name="observer">The observer.</param>
        /// <param name="aberration">The aberration correction of the direction.</param>
        /// <returns>The emission angle, rad.</returns>
        /// <remarks>
        /// For a site from coordinates the normal is the Z axis of <see cref="Frame"/>. A site from the kernels whose frame
        /// kernel is loaded has the kernel frame (#690), whose zenith departs from that normal (7.8e-8 rad for DSS-13): the
        /// angle keeps the normal at <see cref="Planetodetic"/>, so that it agrees with the illumination searches.
        /// </remarks>
        public double IlluminationEmission(Time date, ILocalizable observer, Aberration aberration)
        {
            return observer.GetEphemeris(date, this, Frames.Frame.ICRF, aberration).Position.Angle(EllipsoidNormalInIcrf(date));
        }

        /// <summary>
        /// The incidence angle at the site: the angle between the direction of <paramref name="illuminationSource"/> and the
        /// normal to the ellipsoid of the body at <see cref="Planetodetic"/>, the normal CSPICE's <c>gfilum_c</c> takes for
        /// the site (<see cref="FindIlluminationWindows"/>, #488).
        /// </summary>
        /// <param name="date">The epoch.</param>
        /// <param name="illuminationSource">The source of illumination.</param>
        /// <param name="aberration">The aberration correction of the direction.</param>
        /// <returns>The incidence angle, rad.</returns>
        /// <remarks>As <see cref="IlluminationEmission"/>: the normal at <see cref="Planetodetic"/>, the Z axis of
        /// <see cref="Frame"/> for a site from coordinates (#690).</remarks>
        public double IlluminationIncidence(Time date, ILocalizable illuminationSource, Aberration aberration)
        {
            var illuminationPosition = illuminationSource.GetEphemeris(date, this, Frames.Frame.ICRF, aberration).Position;
            return EllipsoidNormalInIcrf(date).Angle(illuminationPosition);
        }

        // The normal to the ellipsoid of the body at the planetodetic coordinates of the site, fixed in the frame of the body,
        // in ICRF at the epoch
        private Vector3 EllipsoidNormalInIcrf(in Time date)
        {
            double cosLatitude = System.Math.Cos(Planetodetic.Latitude);
            var normal = new Vector3(cosLatitude * System.Math.Cos(Planetodetic.Longitude), cosLatitude * System.Math.Sin(Planetodetic.Longitude),
                System.Math.Sin(Planetodetic.Latitude));
            return normal.Rotate(CelestialBody.Frame.ToFrame(Frames.Frame.ICRF, date).Rotation);
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
            var illuminationPosition = illuminationSource.GetEphemeris(date, this, Frames.Frame.ICRF, aberration).Position;
            return observer.GetEphemeris(date, this, Frames.Frame.ICRF, aberration).Position.Angle(illuminationPosition);
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
            var position = GetEphemeris(epoch, target, target.Frame, aberration).Position;

            var lon = System.Math.Atan2(position.Y, position.X);

            var lat = System.Math.Asin(position.Z / position.Magnitude());

            return new Planetocentric(lon, lat, position.Magnitude());
        }

        /// <summary>
        /// Writes the frame kernel of the topocentric frame of the site (<see cref="SiteFrame.Write"/>): the frame of
        /// <see cref="Planetodetic"/>, or for a site from the kernels whose frame id the loaded kernels define, that kernel
        /// frame under its kernel name (#690). Synchronous since 11.0.0, <c>WriteFrameAsync</c> before (decision 78 of #668,
        /// decided by S. Guillet, 2026-11-23).
        /// A file it replaces is unloaded first, under every name SPICE holds it by (a meta-kernel's included), and the new file is left unloaded, as the SPK and CK writers do (#799(c), decision 103 of #668, decided by S. Guillet, 2026-11-25).
        /// </summary>
        /// <param name="outputFile">The frame kernel to write.</param>
        /// <exception cref="InvalidOperationException">See <see cref="SiteFrame.Write"/>; nothing is written.</exception>
        /// <exception cref="Exceptions.SpiceException">The file it replaces is loaded and its unload fails (SPICE's error): the
        /// file is kept as it was and nothing is written (#799(c), decision 103 of #668).</exception>
        public void WriteFrame(FileInfo outputFile)
        {
            Frame.Write(outputFile);
        }

        /// <summary>
        /// Writes the states of the site relative to its body, in the body's frame, every minute of the window, as an SPK
        /// through the native writer (<see cref="SpiceAPI.WriteEphemeris"/>).
        /// </summary>
        /// <param name="window">The window, sampled every minute from its start.</param>
        /// <param name="outputFile">The SPK to write; a file it replaces that <see cref="SpiceAPI.LoadKernels(FileSystemInfo)"/> loaded is
        /// unloaded first, and the new one is left unloaded (#706).</param>
        /// <exception cref="ArgumentException">The window gives fewer than three states (shorter than two minutes): the
        /// native writer writes three or more. The exception names <c>stateVectors</c>, the parameter of
        /// <see cref="SpiceAPI.WriteEphemeris"/> (#706: before, a C++ exception crossed P/Invoke and ended the process). See
        /// <see cref="SpiceAPI.WriteEphemeris"/> for the others.</exception>
        public void WriteEphemeris(Window window, FileInfo outputFile)
        {
            var ephemeris = GetEphemeris(window, CelestialBody, CelestialBody.Frame, Aberration.None, TimeSpan.FromMinutes(1.0));
            SpiceAPI.Instance.WriteEphemeris(outputFile, NaifId, ephemeris.Select(x => x.ToStateVector()).ToArray());
        }

        /// <summary>
        /// The windows where the Sun's incidence at the site is below 90° plus the twilight angle, with light time and
        /// stellar aberration, sampled every minute: <see cref="FindIlluminationWindows"/>, its windows labelled in the scale
        /// of the search window's start (R15 of #668, lot L6; TDB on the path of <c>gfilum_c</c> before).
        /// </summary>
        /// <remarks>For a site from the kernels CSPICE's <c>gfilum_c</c> searches; for a site built from its coordinates
        /// the library does (<see cref="GeometryFinder"/>), and a day or a night shorter than the minute (the Sun grazing
        /// the twilight angle) is found as an extreme of the incidence between two samples (#678).</remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="twilight"/> is outside [−π/2, π/2], or NaN: the angles are in radians (R11 of #668, lot L6).</exception>
        /// <exception cref="InvalidOperationException">On the path of CSPICE's <c>gfilum_c</c>, SPICE cannot run the search (a
        /// body or an epoch without ephemeris data, a frame it does not know, an epoch outside the coverage of the frame data,
        /// more windows than the 10000 of its result cell, #653), with SPICE's message (fourth round of #668; before 11.0.0 the
        /// search returned no window, and its result cell held 1000 windows, #653).</exception>
        public IReadOnlyList<Window> FindDayWindows(in Window searchWindow, double twilight)
        {
            GeometryFinder.CheckAngle(twilight, -Constants.HalfPi, Constants.HalfPi, "[−π/2, π/2]", "twilight", nameof(twilight));
            return FindIlluminationWindows(searchWindow, IlluminationAngle.Incidence, RelationnalOperator.Lower, Constants.HalfPi + twilight, Stars.SUN_BODY,
                new WindowSearchOptions { Step = TimeSpan.FromMinutes(1.0), Aberration = Aberration.LTS });
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