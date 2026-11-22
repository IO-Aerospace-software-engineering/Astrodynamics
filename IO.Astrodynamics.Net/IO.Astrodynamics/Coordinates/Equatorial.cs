using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Coordinates
{
    /// <summary>
    /// Equatorial coordinates: a right ascension and a declination, with a distance when it is known, at an epoch.
    /// </summary>
    public readonly record struct Equatorial
    {
        /// <summary>The declination, rad.</summary>
        public double Declination { get; }

        /// <summary>The right ascension, rad.</summary>
        public double RightAscension { get; }

        /// <summary>The distance, m; NaN when it is not known.</summary>
        public double Distance { get; }

        /// <summary>The epoch of the coordinates.</summary>
        public Time Epoch { get; }

        private Equatorial(double rightAscension, double declination, double distance, Time epoch)
        {
            Declination = declination;
            RightAscension = rightAscension;
            Distance = distance;
            Epoch = epoch;
        }

        /// <summary>
        /// The coordinates of a right ascension and a declination, in the order of the name, without a distance (NaN). They
        /// replace the constructor <c>Equatorial(declination, rightAscension, epoch)</c> of 10.x, whose order was the
        /// reverse of the name of the type's members: swapped, two angles compiled and gave another direction (R20 of #668).
        /// </summary>
        /// <param name="rightAscension">The right ascension, rad.</param>
        /// <param name="declination">The declination, rad.</param>
        /// <param name="epoch">The epoch of the coordinates.</param>
        /// <returns>The equatorial coordinates.</returns>
        public static Equatorial FromRightAscensionDeclination(double rightAscension, double declination, Time epoch) =>
            new(rightAscension, declination, double.NaN, epoch);

        /// <summary>
        /// The coordinates of a right ascension, a declination and a distance, in the order of the name. They replace the
        /// constructor <c>Equatorial(declination, rightAscension, distance, epoch)</c> of 10.x (R20 of #668).
        /// </summary>
        /// <param name="rightAscension">The right ascension, rad.</param>
        /// <param name="declination">The declination, rad.</param>
        /// <param name="distance">The distance, m.</param>
        /// <param name="epoch">The epoch of the coordinates.</param>
        /// <returns>The equatorial coordinates.</returns>
        public static Equatorial FromRightAscensionDeclination(double rightAscension, double declination, double distance, Time epoch) =>
            new(rightAscension, declination, distance, epoch);

        /// <summary>
        /// The right ascension, declination and distance of the position of <paramref name="stateVector"/> in ICRF,
        /// whatever the frame the state is expressed in (#680).
        /// </summary>
        /// <remarks>
        /// <para>
        /// A state in ICRF is taken as it is; a state in another frame has its position rotated to ICRF at its epoch, the
        /// rotation <see cref="OrbitalParameters.OrbitalParameters.ToFrame(Frame)"/> applies, then RA = atan2(y, x) in
        /// [0, 2π) and Dec = asin(z / |r|). Before 11.0.0 the angles were those of the position in the state's frame: an
        /// Earth-fixed longitude in ITRF93, an ecliptic longitude and latitude in ECLIPJ2000. The coordinate searches of
        /// <see cref="CoordinateSystem.RaDec"/> keep the angles in their search frame, as CSPICE <c>gfposc</c>.
        /// </para>
        /// <para>
        /// For a light-time corrected state read in a frame centred on its target (the Moon in MOON_ME), SPICE evaluates
        /// that frame at the epoch minus the light time, while the rotation is taken at the state's epoch: the angles then
        /// differ from those of the same state read in ICRF (3.05e-6 rad for the Moon in MOON_ME). Read such states in ICRF.
        /// </para>
        /// </remarks>
        /// <param name="stateVector">The state; its position is relative to its observer.</param>
        /// <exception cref="InvalidOperationException">The data do not give the orientation of the state's frame at its
        /// epoch (ITRF93 outside the loaded PCK, for example).</exception>
        public Equatorial(StateVector stateVector)
            : this(stateVector.Frame == Frame.ICRF
                ? stateVector.Position
                : stateVector.Position.Rotate(stateVector.Frame.ToFrame(Frame.ICRF, stateVector.Epoch).Rotation), stateVector.Epoch)
        {
            // The distance of the position as given, which the rotation leaves unchanged but for its rounding
            Distance = stateVector.Position.Magnitude();
        }

        // The angles of a position in whatever frame it is expressed in
        private Equatorial(in Vector3 position, Time epoch)
        {
            Distance = position.Magnitude();
            RightAscension = System.Math.Atan2(position.Y, position.X);
            if (RightAscension < 0)
                RightAscension += Constants.TwoPi;

            Declination = System.Math.Asin(position.Z / Distance);
            Epoch = epoch;
        }

        /// <summary>
        /// The right ascension, declination and distance of <paramref name="position"/> in the frame it is expressed in,
        /// not rotated to ICRF: the angles of the coordinate searches of <see cref="CoordinateSystem.RaDec"/>, which CSPICE
        /// <c>gfposc</c> computes in the frame it is given.
        /// </summary>
        internal static Equatorial FromPositionInFrame(in Vector3 position, Time epoch) => new(position, epoch);

        /// <summary>
        /// Convert to cartesian coordinates, in the frame of the angles: ICRF for the coordinates of a state (#680; before
        /// 11.0.0 the frame of the state)
        /// </summary>
        /// <returns></returns>
        public Vector3 ToCartesian()
        {
            double x = Distance * System.Math.Cos(Declination) * System.Math.Cos(RightAscension);
            double y = Distance * System.Math.Cos(Declination) * System.Math.Sin(RightAscension);
            double z = Distance * System.Math.Sin(Declination);
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// The unit direction, in the frame of the angles: ICRF for the coordinates of a state (#680; before 11.0.0 the frame
        /// of the state)
        /// </summary>
        public Vector3 ToDirection()
        {
            double x = System.Math.Cos(Declination) * System.Math.Cos(RightAscension);
            double y = System.Math.Cos(Declination) * System.Math.Sin(RightAscension);
            double z = System.Math.Sin(Declination);
            return new Vector3(x, y, z);
        }
    }
}