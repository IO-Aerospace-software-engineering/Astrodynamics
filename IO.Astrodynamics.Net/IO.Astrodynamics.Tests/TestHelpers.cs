using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Physics;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Tests
{
    internal static class TestHelpers
    {
        internal static CelestialBody Sun => new(Stars.Sun);

        internal static CelestialBody Earth => new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, new TimeSystem.Time(2021, 1, 1), atmosphericModel: new EarthAtmosphericModel());

        internal static CelestialBody Moon => new(PlanetsAndMoons.MOON, Frames.Frame.ICRF, new TimeSystem.Time(2021, 1, 1));

        internal static CelestialBody EarthAtJ2000 => new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, new TimeSystem.Time(2000, 1, 1, 12, 0, 0));

        internal static CelestialBody EarthWithAtmAndGeoAtJ2000 => new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, new TimeSystem.Time(2000, 1, 1, 12, 0, 0),
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10), new EarthAtmosphericModel());

        internal static CelestialBody MoonAtJ2000 => new(PlanetsAndMoons.MOON, Frames.Frame.ICRF, new TimeSystem.Time(2000, 1, 1, 12, 0, 0));

        internal static CelestialBody MoonAt20011214 => new(PlanetsAndMoons.MOON, Frames.Frame.ICRF, new TimeSystem.Time(2001, 12, 14, 0, 0, 0));

        internal static Spacecraft Spacecraft => new Spacecraft(-666, "GenericSpacecraft", 100.0, 1000.0, new Clock("GenericClk", 65536),
            new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), Earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF));

        internal static bool VectorComparer(Vector3 v1, Vector3 v2)
        {
            return System.Math.Abs(v1.X - v2.X) < 1E-03 && System.Math.Abs(v1.Y - v2.Y) < 1E-03 && System.Math.Abs(v1.Z - v2.Z) < 1E-03;
        }

        internal static bool VelocityVectorComparer(Vector3 v1, Vector3 v2)
        {
            return System.Math.Abs(v1.X - v2.X) < 1E-09 && System.Math.Abs(v1.Y - v2.Y) < 1E-09 && System.Math.Abs(v1.Z - v2.Z) < 1E-09;
        }

        internal static bool QuaternionComparer(Quaternion q1, Quaternion q2)
        {
            return System.Math.Abs(q1.W - q2.W) < 1E-06 && VectorComparer(q1.VectorPart, q2.VectorPart);
        }

        internal static bool StateVectorComparer(StateVector sv1, StateVector sv2)
        {
            return VectorComparer(sv1.Position, sv2.Position) && VectorComparer(sv1.Velocity, sv2.Velocity) && sv1.Frame == sv2.Frame && sv1.Epoch == sv2.Epoch &&
                   Equals(sv1.Observer, sv2.Observer);
        }

        internal static bool StateOrientationComparer(StateOrientation so1, StateOrientation so2)
        {
            return QuaternionComparer(so1.Rotation, so2.Rotation) && VectorComparer(so1.AngularVelocity, so2.AngularVelocity) && so1.Epoch == so2.Epoch &&
                   so1.ReferenceFrame == so2.ReferenceFrame;
        }

        internal static bool KeplerComparer(KeplerianElements k1, KeplerianElements k2)
        {
            var tolerance = 1E-06;
            return (double.IsPositiveInfinity(k1.A) && double.IsPositiveInfinity(k2.A) || System.Math.Abs(k1.A - k2.A) < tolerance) &&
                   System.Math.Abs(k1.E - k2.E) < tolerance && System.Math.Abs(k1.I - k2.I) < tolerance &&
                   System.Math.Abs(k1.RAAN - k2.RAAN) < tolerance && System.Math.Abs(k1.AOP - k2.AOP) < tolerance && System.Math.Abs(k1.M - k2.M) < tolerance &&
                   k1.Epoch == k2.Epoch && k1.Frame == k2.Frame && Equals(k1.Observer, k2.Observer);
        }

        internal static bool EquinoctialComparer(EquinoctialElements e1, EquinoctialElements e2)
        {
            var tolerance = 1E-06;
            return (double.IsPositiveInfinity(e1.SemiMajorAxis()) && double.IsPositiveInfinity(e2.SemiMajorAxis()) ||
                    System.Math.Abs(e1.SemiMajorAxis() - e2.SemiMajorAxis()) < tolerance) && System.Math.Abs(e1.Eccentricity() - e2.Eccentricity()) < tolerance &&
                   System.Math.Abs(e1.Inclination() - e2.Inclination()) < tolerance && System.Math.Abs(e1.AscendingNode() - e2.AscendingNode()) < tolerance &&
                   System.Math.Abs(e1.ArgumentOfPeriapsis() - e2.ArgumentOfPeriapsis()) < tolerance && System.Math.Abs(e1.MeanAnomaly() - e2.MeanAnomaly()) < tolerance &&
                   e1.Epoch == e2.Epoch && e1.Frame == e2.Frame && Equals(e1.Observer, e2.Observer);
        }

        internal static bool TimeComparer(TimeSystem.Time v1, TimeSystem.Time v2)
        {
            return Equals(v1.Frame, v2.Frame) && System.Math.Abs((v1 - v2).TotalSeconds) < 1E-03;
        }
    }
}