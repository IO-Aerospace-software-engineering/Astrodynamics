using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Coordinates
{
    public readonly record struct Equatorial
    {
        public double Declination { get; }
        public double RightAscension { get; }
        public double Distance { get; }
        public Time Epoch { get; }

        public Equatorial(double declination, double rightAscension, double distance, Time epoch)
        {
            Declination = declination;
            RightAscension = rightAscension;
            Distance = distance;
            Epoch = epoch;
        }

        public Equatorial(double declination, double rightAscension, Time epoch) : this(declination, rightAscension, Double.NaN, epoch)
        {
        }

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
                RightAscension += Constants._2PI;

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