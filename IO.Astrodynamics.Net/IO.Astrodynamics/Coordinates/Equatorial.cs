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

        public Equatorial(StateVector stateVector)
        {
            Distance = stateVector.Position.Magnitude();
            RightAscension = System.Math.Atan2(stateVector.Position.Y, stateVector.Position.X);
            if (RightAscension < 0)
                RightAscension += Constants._2PI;

            Declination = System.Math.Asin(stateVector.Position.Z / Distance);
            Epoch = stateVector.Epoch;
        }

        /// <summary>
        /// Convert to cartesian coordinates
        /// </summary>
        /// <returns></returns>
        public Vector3 ToCartesian()
        {
            double x = Distance * System.Math.Cos(Declination) * System.Math.Cos(RightAscension);
            double y = Distance * System.Math.Cos(Declination) * System.Math.Sin(RightAscension);
            double z = Distance * System.Math.Sin(Declination);
            return new Vector3(x, y, z);
        }
    }
}