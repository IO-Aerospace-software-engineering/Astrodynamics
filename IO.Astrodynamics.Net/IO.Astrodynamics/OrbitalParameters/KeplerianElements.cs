using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;


namespace IO.Astrodynamics.OrbitalParameters
{
    /// <summary>
    /// Represents the Keplerian elements of an orbit, which are a set of parameters that define the shape and orientation of an orbit.
    /// </summary>
    public class KeplerianElements : OrbitalParameters, IEquatable<KeplerianElements>
    {
        // The elements as given, the values SemiMajorAxis(), Eccentricity(), Inclination(), AscendingNode(),
        // ArgumentOfPeriapsis() and MeanAnomaly() return (the public properties A, E, I, RAAN, AOP and M before 11.0.0, which
        // repeated them, D2 of #668, lot L9)
        private readonly double _a;
        private readonly double _e;
        private readonly double _i;
        private readonly double _raan;
        private readonly double _aop;
        private readonly double _m;

        // Within 1e-6 of e = 1 the perigee radius given or derived at construction: the state of the band comes from it
        // (#459), and a = +Infinity does not fix it, so it is a value of the set for its equality and hash code (#475)
        private readonly double? _bandPerigeeRadius;

        // The true anomaly given to FromTrueAnomaly, as given: the state and the equinoctial elements are computed from it,
        // bit for bit the former constructor's trueAnomaly, while TrueAnomaly() returns it reduced to [0, 2π) (#668, R23).
        // Dropped with the caches when the epoch moves, as the true anomaly itself
        private double? _givenTrueAnomaly;

        private protected override double ConicTrueAnomaly() => _givenTrueAnomaly ?? TrueAnomaly();

        /// <inheritdoc />
        protected override void ResetCache()
        {
            base.ResetCache();
            _givenTrueAnomaly = null;
        }

        /// <summary>
        /// The rounding allowed between a semi-major axis and a perigee radius of one conic within 1e-6 of e = 1:
        /// |q/a − (1 − e)| ≤ 6 ε (1 + e). The elements the library builds there, from a state (−μ/(2ξ) and h²/(μ(1 + e))),
        /// through equinoctial elements, along the two-body motion and from SPICE's oscltx, measured at most 2.983 ε (1 + e)
        /// over 6400 states of the band (|e − 1| from 0 to 9.99e-7, q from 6600 km to 1 AU, |M| up to 2e6 rad) on Linux x64,
        /// Windows x64 and macOS arm64 alike (CI run 44225490669); twice that, rounded up (the review of #459).
        /// </summary>
        private const double OneConicTolerance = 6.0 * 2.220446049250313e-16;

        /// <summary>
        /// The Keplerian elements of a conic outside the near-parabolic band (|e − 1| ≥ 1e-6), its anomaly the mean
        /// anomaly. A parabola, or an orbit within 1e-6 of e = 1, is built from its perigee radius by
        /// <see cref="Parabolic"/>, and elements given with their true anomaly by <see cref="FromTrueAnomaly"/> (#668, R23:
        /// before 11.0.0 the optional parameters <c>trueAnomaly</c>, <c>period</c> and <c>perigeeRadius</c> of this
        /// constructor were stored unchecked beside the elements).
        /// </summary>
        /// <param name="semiMajorAxis">The semi-major axis, m: above 0 for an ellipse, below 0 for a hyperbola.</param>
        /// <param name="eccentricity">The eccentricity, finite and non-negative, |e − 1| ≥ 1e-6.</param>
        /// <param name="inclination">The inclination, rad, in [−π, π].</param>
        /// <param name="ascendingNode">The right ascension of the ascending node, rad (<c>rigthAscendingNode</c> before
        /// 11.0.0).</param>
        /// <param name="argumentOfPeriapsis">The argument of periapsis, rad.</param>
        /// <param name="meanAnomaly">The mean anomaly, rad.</param>
        /// <param name="observer">The centre of motion.</param>
        /// <param name="epoch">The epoch.</param>
        /// <param name="frame">The frame of the elements.</param>
        /// <param name="elementsType">Type of orbital elements (osculating or mean). Defaults to osculating.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="eccentricity"/> is not finite or is negative
        /// (#476).</exception>
        /// <exception cref="ArgumentException">An element is not finite (#726); the orbit is within 1e-6 of e = 1
        /// (<paramref name="eccentricity"/>: use <see cref="Parabolic"/>); a semi-major axis of the wrong sign, a
        /// (1 − e) ≤ 0 (a ≤ 0 for e &lt; 1, a ≥ 0 for e &gt; 1, <paramref name="semiMajorAxis"/>, #738; before 11.0.0,
        /// accepted with a NaN velocity); or <paramref name="inclination"/> is outside [−π, π]. The exception names the
        /// parameter.</exception>
        public KeplerianElements(double semiMajorAxis, double eccentricity, double inclination, double ascendingNode, double argumentOfPeriapsis,
            double meanAnomaly, ILocalizable observer, Time epoch, Frame frame, OrbitalElementsType elementsType = OrbitalElementsType.Osculating)
            : this(semiMajorAxis, eccentricity, inclination, ascendingNode, argumentOfPeriapsis, meanAnomaly, observer, epoch, frame, null, null,
                null, elementsType)
        {
        }

        /// <summary>
        /// The elements of a conic with the values the library has already computed for it, held as they are: the true
        /// anomaly, the period and the perigee radius (the library's own sets, its conversions and its two-body motion,
        /// bit for bit; #668, R23 made it internal). Within 1e-6 of e = 1 the perigee radius is required.
        /// </summary>
        /// <param name="semiMajorAxis">The semi-major axis, m: within 1e-6 of e = 1 that of the conic of the perigee radius,
        /// q / (1 − e) to rounding (|q/a − (1 − e)| ≤ 6 ε (1 + e)), +Infinity at e = 1 exactly (#459).</param>
        /// <param name="eccentricity">The eccentricity.</param>
        /// <param name="inclination">The inclination, rad.</param>
        /// <param name="ascendingNode">The right ascension of the ascending node, rad.</param>
        /// <param name="argumentOfPeriapsis">The argument of periapsis, rad.</param>
        /// <param name="meanAnomaly">The mean anomaly, rad.</param>
        /// <param name="observer">The centre of motion.</param>
        /// <param name="epoch">The epoch.</param>
        /// <param name="frame">The frame.</param>
        /// <param name="trueAnomaly">The true anomaly of the mean anomaly, or null.</param>
        /// <param name="period">The period of an ellipse, or null; <see cref="OrbitalParameters.Period"/> throws for an
        /// open orbit whatever is given (#663).</param>
        /// <param name="perigeeRadius">The perigee radius, m, or null: required within 1e-6 of e = 1, finite and above 0
        /// when given.</param>
        /// <param name="elementsType">Type of orbital elements.</param>
        internal KeplerianElements(double semiMajorAxis, double eccentricity, double inclination, double ascendingNode, double argumentOfPeriapsis,
            double meanAnomaly, ILocalizable observer, Time epoch, Frame frame, double? trueAnomaly, TimeSpan? period, double? perigeeRadius,
            OrbitalElementsType elementsType) : base(observer, epoch, frame, elementsType)
        {
            RequireEccentricity(eccentricity);
            bool parabolic = System.Math.Abs(eccentricity - 1.0) < 1E-06;

            // No conic has a non-finite element but the semi-major axis of a parabola (#726)
            if (double.IsNaN(semiMajorAxis) || double.IsNegativeInfinity(semiMajorAxis) ||
                (double.IsPositiveInfinity(semiMajorAxis) && !parabolic))
            {
                throw new ArgumentException(
                    $"The semi-major axis must be finite, +Infinity only for a parabola (|e − 1| < 1e-6): a = {semiMajorAxis:R} m, e = {eccentricity:R}.",
                    nameof(semiMajorAxis));
            }

            // Outside the band a conic has p = a (1 − e²) > 0: a > 0 for e < 1, a < 0 for e > 1, a ≠ 0 (#738; before, a NaN
            // velocity without an exception, and AtEpoch named the mean anomaly)
            if (!parabolic && !(semiMajorAxis * (1.0 - eccentricity) > 0.0))
            {
                throw new ArgumentException(
                    $"No conic has this semi-major axis: a = {semiMajorAxis:R} m, e = {eccentricity:R} (a must be above 0 for e < 1 and below 0 for e > 1).",
                    nameof(semiMajorAxis));
            }

            RequireInclination(inclination, nameof(inclination));
            RequireFinite(ascendingNode, nameof(ascendingNode));
            RequireFinite(argumentOfPeriapsis, nameof(argumentOfPeriapsis));
            RequireFinite(meanAnomaly, nameof(meanAnomaly));
            if (trueAnomaly.HasValue)
            {
                RequireFinite(trueAnomaly.Value, nameof(trueAnomaly));
            }

            if (perigeeRadius is { } q && !(double.IsFinite(q) && q > 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(perigeeRadius), q, "The perigee radius must be finite and above 0.");
            }

            // Within 1e-6 of e = 1 the anomalies are Barker's, read from the perigee radius: it is required (#368), and only
            // Parabolic and the library give it (#668, R23)
            if (parabolic && perigeeRadius is null)
            {
                throw new ArgumentException(
                    $"Within 1e-6 of e = 1 an orbit is defined by its perigee radius: use KeplerianElements.Parabolic (e = {eccentricity:R}).",
                    nameof(eccentricity));
            }

            // ... with the semi-major axis of the same conic, a (1 − e) = q, either side of e = 1 (#459; before, +Infinity
            // only): +Infinity at e = 1 exactly, and otherwise q/a = 1 − e to rounding, which a = +Infinity meets only within a
            // few ulps of e = 1 (the state of an exact parabola, whose e rounds off 1 where ξ rounds to 0). The semi-major
            // axis, the mean motion, the period and the apoapsis come from a, the state and P from q (the review of #459)
            if (parabolic && (eccentricity == 1.0
                    ? !double.IsPositiveInfinity(semiMajorAxis)
                    : !(System.Math.Abs(perigeeRadius!.Value / semiMajorAxis - (1.0 - eccentricity)) <= OneConicTolerance * (1.0 + eccentricity))))
            {
                throw new ArgumentException(
                    $"Within 1e-6 of e = 1 the semi-major axis must be that of the conic of the perigee radius, q / (1 − e), " +
                    $"+Infinity at e = 1: a = {semiMajorAxis:R} m, e = {eccentricity:R}, q = {perigeeRadius!.Value:R} m.",
                    nameof(semiMajorAxis));
            }

            _a = semiMajorAxis;
            _e = eccentricity;
            _i = inclination;
            _raan = SpecialFunctions.NormalizeAngle(ascendingNode);
            _aop = SpecialFunctions.NormalizeAngle(argumentOfPeriapsis);

            // An angle on an ellipse; on an open orbit it grows without bound, negative before the periapsis (#368)
            _m = eccentricity < 1.0 - 1E-06 ? SpecialFunctions.NormalizeAngle(meanAnomaly) : meanAnomaly;
            _period = period;
            _trueAnomaly = trueAnomaly;
            _perigeeRadius = perigeeRadius;
            _bandPerigeeRadius = parabolic ? perigeeRadius : null;
        }

        /// <summary>
        /// Private constructor for creating mean Keplerian elements from mean motion.
        /// Semi-major axis is computed from mean motion using Kepler's third law.
        /// </summary>
        /// <param name="meanMotion">Mean motion in rad/s</param>
        /// <param name="eccentricity">Eccentricity</param>
        /// <param name="inclination">Inclination in radians</param>
        /// <param name="rightAscendingNode">Right ascension of ascending node in radians</param>
        /// <param name="argumentOfPeriapsis">Argument of periapsis in radians</param>
        /// <param name="meanAnomaly">Mean anomaly in radians</param>
        /// <param name="observer">Central body</param>
        /// <param name="epoch">Epoch time</param>
        /// <param name="frame">Reference frame</param>
        /// <param name="elementsType">Type of orbital elements</param>
        /// <param name="inclinationName">The name of the inclination as the caller took it, for its exception.</param>
        /// <param name="rightAscendingNodeName">The name of the node as the caller took it.</param>
        /// <param name="argumentOfPeriapsisName">The name of the argument of periapsis as the caller took it.</param>
        /// <param name="meanAnomalyName">The name of the mean anomaly as the caller took it.</param>
        private KeplerianElements(double meanMotion, double eccentricity, double inclination, double rightAscendingNode,
            double argumentOfPeriapsis, double meanAnomaly, ILocalizable observer, Time epoch, Frame frame,
            OrbitalElementsType elementsType, string inclinationName, string rightAscendingNodeName,
            string argumentOfPeriapsisName, string meanAnomalyName)
            : base(observer, epoch, frame, elementsType)
        {
            // A mean motion is finite and above 0: 0 gave a = +Infinity, a negative one the a of its opposite (#726)
            if (!(double.IsFinite(meanMotion) && meanMotion > 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(meanMotion), meanMotion, "The mean motion must be finite and above 0.");
            }

            RequireEccentricity(eccentricity);
            RequireInclination(inclination, inclinationName);
            RequireFinite(rightAscendingNode, rightAscendingNodeName);
            RequireFinite(argumentOfPeriapsis, argumentOfPeriapsisName);
            RequireFinite(meanAnomaly, meanAnomalyName);

            // Compute semi-major axis from mean motion using Kepler's third law: n = sqrt(GM/a^3)
            _a = System.Math.Cbrt(Constants.G * observer.Mass / (meanMotion * meanMotion));
            _e = eccentricity;
            _i = inclination;
            _raan = SpecialFunctions.NormalizeAngle(rightAscendingNode);
            _aop = SpecialFunctions.NormalizeAngle(argumentOfPeriapsis);
            _m = SpecialFunctions.NormalizeAngle(meanAnomaly);

            // Cache the original mean motion to preserve precision
            _meanMotion = meanMotion;
        }

        /// <summary>
        /// Creates mean Keplerian elements from OMM (Orbit Mean-elements Message) data, the path of
        /// <see cref="CCSDS.OMM.Omm.ToMeanElements"/> and <see cref="CCSDS.OMM.Omm.ToTle"/> (internal since 11.0.0, R22 of
        /// #668: the one SI-side member that took degrees and rev/day).
        /// This factory method accepts values in OMM native units and handles all conversions internally.
        /// The resulting elements will have <see cref="OrbitalParameters.ElementsType"/> set to <see cref="OrbitalElementsType.Mean"/>.
        /// </summary>
        /// <param name="meanMotion">Mean motion in revolutions per day</param>
        /// <param name="eccentricity">Eccentricity (dimensionless)</param>
        /// <param name="inclination">Inclination in degrees</param>
        /// <param name="raan">Right ascension of ascending node in degrees</param>
        /// <param name="argumentOfPeriapsis">Argument of periapsis in degrees</param>
        /// <param name="meanAnomaly">Mean anomaly in degrees</param>
        /// <param name="observer">Central body (typically Earth)</param>
        /// <param name="epoch">Epoch time</param>
        /// <param name="frame">Reference frame (typically TEME for TLE-compatible OMM)</param>
        /// <returns>Mean Keplerian elements with preserved mean motion precision</returns>
        /// <exception cref="ArgumentOutOfRangeException">The mean motion is not finite and above 0 (#726), or the
        /// eccentricity is not finite or is negative (#476).</exception>
        /// <exception cref="ArgumentException">An angle is not finite (#726), or the inclination is out of range; the
        /// exception names the parameter.</exception>
        /// <remarks>
        /// The mean motion in rad/s is (n × 2π) / 86400 in double arithmetic. Above about 2.86e307 rev/day, where the
        /// product n × 2π overflows, the same two roundings are taken on n × 2⁻¹⁰ and 86400 × 2⁻¹⁰ (exact power-of-two
        /// scalings), so the result is the one an unbounded exponent would give, finite, not +Infinity (#729); below, the
        /// result is unchanged, bit for bit.
        /// </remarks>
        internal static KeplerianElements FromOMM(
            double meanMotion,
            double eccentricity,
            double inclination,
            double raan,
            double argumentOfPeriapsis,
            double meanAnomaly,
            ILocalizable observer,
            Time epoch,
            Frame frame)
        {
            // Convert OMM units to internal units
            // Mean motion: rev/day -> rad/s
            double meanMotionRadPerSec = RevolutionsPerDayToRadiansPerSecond(meanMotion);

            // Angles: degrees -> radians
            return new KeplerianElements(
                meanMotionRadPerSec,
                eccentricity,
                inclination * Constants.Deg2Rad,
                raan * Constants.Deg2Rad,
                argumentOfPeriapsis * Constants.Deg2Rad,
                meanAnomaly * Constants.Deg2Rad,
                observer,
                epoch,
                frame,
                OrbitalElementsType.Mean,
                nameof(inclination), nameof(raan), nameof(argumentOfPeriapsis), nameof(meanAnomaly));
        }

        /// <summary>
        /// The Keplerian elements whose true anomaly is <paramref name="trueAnomaly"/> (#668, R23): the mean anomaly is
        /// M(ν) of the closed-form eccentric anomaly (<see cref="OrbitalParameters.TrueAnomalyToMeanAnomaly"/>), and
        /// <see cref="OrbitalParameters.TrueAnomaly()"/> returns ν reduced to [0, 2π), the range of the true anomaly
        /// computed from M (ν as given when already in it), while the state and the equinoctial elements are computed from ν
        /// as given (its reduction would lose up to half an ulp of 2π). The conic is one of the constructor, outside the
        /// near-parabolic band.
        /// </summary>
        /// <param name="semiMajorAxis">The semi-major axis, m: above 0 for an ellipse, below 0 for a hyperbola.</param>
        /// <param name="eccentricity">The eccentricity, finite and non-negative, |e − 1| ≥ 1e-6.</param>
        /// <param name="inclination">The inclination, rad, in [−π, π].</param>
        /// <param name="ascendingNode">The right ascension of the ascending node, rad.</param>
        /// <param name="argumentOfPeriapsis">The argument of periapsis, rad.</param>
        /// <param name="trueAnomaly">The true anomaly, rad, an angle (any multiple of 2π added gives the same elements): on a
        /// hyperbola, its reduction to (−π, π] inside the asymptotes, |ν| &lt; arccos(−1/e).</param>
        /// <param name="observer">The centre of motion.</param>
        /// <param name="epoch">The epoch.</param>
        /// <param name="frame">The frame of the elements.</param>
        /// <returns>Osculating Keplerian elements.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="eccentricity"/> is not finite or is negative, or
        /// the reduction to (−π, π] of <paramref name="trueAnomaly"/> of a hyperbola is on or beyond its asymptotes.</exception>
        /// <exception cref="ArgumentException">An element is not finite, the orbit is within 1e-6 of e = 1, or the conic is
        /// not one (the constructor's rules); the exception names the parameter.</exception>
        public static KeplerianElements FromTrueAnomaly(double semiMajorAxis, double eccentricity, double inclination, double ascendingNode,
            double argumentOfPeriapsis, double trueAnomaly, ILocalizable observer, Time epoch, Frame frame)
        {
            RequireFinite(trueAnomaly, nameof(trueAnomaly));
            var shape = new KeplerianElements(semiMajorAxis, eccentricity, inclination, ascendingNode, argumentOfPeriapsis, 0.0, observer, epoch,
                frame);
            double meanAnomaly = TrueAnomalyToMeanAnomaly(trueAnomaly, eccentricity, shape.EccentricAnomaly(trueAnomaly));
            if (!double.IsFinite(meanAnomaly))
            {
                throw new ArgumentOutOfRangeException(nameof(trueAnomaly), trueAnomaly,
                    $"The true anomaly is on or beyond the asymptotes of the hyperbola: its reduction to (−π, π] has |ν| ≥ arccos(−1/e) (e = {eccentricity:R}).");
            }

            // ν is an angle: the true anomaly reported is in [0, 2π), as the one TrueAnomaly() computes from M, so equal elements
            // give the same angle (#668, R23); M is that of ν as given, tan(ν/2) periodic in 2π, and so are the state and the
            // equinoctial elements (the reduction would cost up to half an ulp of 2π, which the state near an apsis or an
            // asymptote amplifies: 1.6e-15 of the position at e = 1.001, ν = −2.79, and 8.4e-13 near the asymptote, against
            // 4.5e-16 and 3.7e-15 from ν as given, ConicStateTests)
            return new KeplerianElements(semiMajorAxis, eccentricity, inclination, ascendingNode, argumentOfPeriapsis, meanAnomaly, observer,
                epoch, frame, SpecialFunctions.NormalizeAngle(trueAnomaly), null, null, OrbitalElementsType.Osculating)
            {
                _givenTrueAnomaly = trueAnomaly
            };
        }

        /// <summary>
        /// The Keplerian elements of a parabola, or of an orbit within 1e-6 of e = 1, from its perigee radius (#668, R23:
        /// the <c>perigeeRadius</c> parameter of the constructor before 11.0.0): the semi-major axis is that of the conic,
        /// q / (1 − e), +Infinity at e = 1, and the anomaly Barker's mean anomaly (#368, #459).
        /// </summary>
        /// <param name="perigeeRadius">The perigee radius q, m, finite and above 0.</param>
        /// <param name="inclination">The inclination, rad, in [−π, π].</param>
        /// <param name="ascendingNode">The right ascension of the ascending node, rad.</param>
        /// <param name="argumentOfPeriapsis">The argument of periapsis, rad.</param>
        /// <param name="meanAnomaly">Barker's mean anomaly, rad.</param>
        /// <param name="observer">The centre of motion.</param>
        /// <param name="epoch">The epoch.</param>
        /// <param name="frame">The frame of the elements.</param>
        /// <param name="eccentricity">The eccentricity, within 1e-6 of 1 (|e − 1| &lt; 1e-6); 1 by default.</param>
        /// <returns>Osculating Keplerian elements.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="perigeeRadius"/> is not finite and above 0, or
        /// <paramref name="eccentricity"/> is not within 1e-6 of 1.</exception>
        /// <exception cref="ArgumentException">An angle is not finite, or <paramref name="inclination"/> is outside
        /// [−π, π]; the exception names the parameter.</exception>
        public static KeplerianElements Parabolic(double perigeeRadius, double inclination, double ascendingNode, double argumentOfPeriapsis,
            double meanAnomaly, ILocalizable observer, Time epoch, Frame frame, double eccentricity = 1.0)
        {
            if (!(System.Math.Abs(eccentricity - 1.0) < 1E-06))
            {
                throw new ArgumentOutOfRangeException(nameof(eccentricity), eccentricity,
                    "A parabola of the band has an eccentricity within 1e-6 of 1; use the constructor outside it.");
            }

            if (!(double.IsFinite(perigeeRadius) && perigeeRadius > 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(perigeeRadius), perigeeRadius, "The perigee radius must be finite and above 0.");
            }

            double semiMajorAxis = eccentricity == 1.0 ? double.PositiveInfinity : perigeeRadius / (1.0 - eccentricity);
            return new KeplerianElements(semiMajorAxis, eccentricity, inclination, ascendingNode, argumentOfPeriapsis, meanAnomaly, observer,
                epoch, frame, null, null, perigeeRadius, OrbitalElementsType.Osculating);
        }

        /// <summary>
        /// An eccentricity is finite and non-negative (#476): NaN took no conic branch, +Infinity gave a state of NaN.
        /// −0.0 is accepted, as 0.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The eccentricity is not finite or is negative.</exception>
        private static void RequireEccentricity(double eccentricity)
        {
            if (!double.IsFinite(eccentricity) || eccentricity < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(eccentricity), eccentricity,
                    "The eccentricity must be finite and non-negative.");
            }
        }

        /// <exception cref="ArgumentException">The inclination is not finite or is outside [−π, π].</exception>
        private static void RequireInclination(double inclination, string name)
        {
            RequireFinite(inclination, name);
            if (inclination is < -Constants.PI or > Constants.PI)
            {
                throw new ArgumentException("Inclination must be in range [-PI,PI] ", name);
            }
        }

        /// <exception cref="ArgumentException">The value is not finite (#726).</exception>
        private static void RequireFinite(double value, string name)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentException($"The element {name} must be finite: {value:R}.", name);
            }
        }

        // (n × 2π) / 86400, the two roundings of the former conversion. Where n × 2π overflows (|n| above about 2.86e307
        // rev/day), the product and the quotient are scaled by 2^-10, which is exact for these magnitudes (no subnormal), so
        // round(round(n × 2^-10 × 2π) / 84.375) = round(round(n × 2π) / 86400) with an unbounded exponent, a finite value
        // (#729: +Infinity before)
        private static double RevolutionsPerDayToRadiansPerSecond(double meanMotion)
        {
            double product = meanMotion * Constants.TwoPi;
            if (double.IsFinite(product) || !double.IsFinite(meanMotion))
            {
                return product / 86400.0;
            }

            const double scale = 1.0 / 1024.0;
            return meanMotion * scale * Constants.TwoPi / (86400.0 * scale);
        }

        /// <summary>
        /// Converts the current instance to Keplerian elements.
        /// </summary>
        /// <returns>The current instance as Keplerian elements.</returns>
        public override KeplerianElements ToKeplerianElements()
        {
            return this;
        }

        /// <summary>
        /// Gets the semi-major axis of the orbit.
        /// </summary>
        /// <returns>The semi-major axis.</returns>
        public override double SemiMajorAxis()
        {
            return _a;
        }

        /// <summary>
        /// Gets the eccentricity of the orbit.
        /// </summary>
        /// <returns>The eccentricity.</returns>
        public override double Eccentricity()
        {
            return _e;
        }

        /// <summary>
        /// Gets the inclination of the orbit.
        /// </summary>
        /// <returns>The inclination.</returns>
        public override double Inclination()
        {
            return _i;
        }

        /// <summary>
        /// Gets the right ascension of the ascending node.
        /// </summary>
        /// <returns>The right ascension of the ascending node.</returns>
        public override double AscendingNode()
        {
            return _raan;
        }

        /// <summary>
        /// Gets the argument of periapsis.
        /// </summary>
        /// <returns>The argument of periapsis.</returns>
        public override double ArgumentOfPeriapsis()
        {
            return _aop;
        }

        /// <summary>
        /// Gets the mean anomaly.
        /// </summary>
        /// <returns>The mean anomaly.</returns>
        public override double MeanAnomaly()
        {
            return _m;
        }
        
        
        
        #region Operator

        public bool Equals(KeplerianElements other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return GetType() == other.GetType() && _a.Equals(other._a) && _e.Equals(other._e) && _i.Equals(other._i) && _raan.Equals(other._raan) &&
                   _aop.Equals(other._aop) && _m.Equals(other._m) && Nullable.Equals(_bandPerigeeRadius, other._bandPerigeeRadius) &&
                   HasSameReference(other);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((KeplerianElements)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), _a, _e, _i, _raan, _aop, _m, _bandPerigeeRadius);
        }

        public static bool operator ==(KeplerianElements left, KeplerianElements right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(KeplerianElements left, KeplerianElements right)
        {
            return !Equals(left, right);
        }

        public override string ToString()
        {
            return $"Epoch : {Epoch.ToString()} A : {_a}, Ecc. : {_e}, Inc. : {_i}, AN : {_raan}, AOP : {_aop}, M : {_m}, Frame : {Frame.Name}";
        }
        #endregion
    }
}