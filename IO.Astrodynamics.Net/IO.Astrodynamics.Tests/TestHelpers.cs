using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Physics;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Time;

namespace IO.Astrodynamics.Tests
{
    internal static class TestHelpers
    {
        internal static CelestialBody Sun => new(Stars.Sun);

        internal static CelestialBody Earth => new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, new DateTime(2021, 1, 1), atmosphericModel: new EarthAtmosphericModel());

        internal static CelestialBody Moon => new(PlanetsAndMoons.MOON, Frames.Frame.ICRF, new DateTime(2021, 1, 1));

        internal static CelestialBody EarthAtJ2000 => new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, new DateTime(2000, 1, 1, 12, 0, 0));

        internal static CelestialBody EarthWithAtmAndGeoAtJ2000 => new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, new DateTime(2000, 1, 1, 12, 0, 0),
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10), new EarthAtmosphericModel());

        internal static CelestialBody MoonAtJ2000 => new(PlanetsAndMoons.MOON, Frames.Frame.ICRF, new DateTime(2000, 1, 1, 12, 0, 0));

        internal static CelestialBody MoonAt20011214 => new(PlanetsAndMoons.MOON, Frames.Frame.ICRF, new DateTime(2001, 12, 14, 0, 0, 0));
        private static object LockObj = new object();

        internal static Spacecraft Spacecraft => new Spacecraft(-666, "GenericSpacecraft", 100.0, 1000.0, new Clock("GenericClk", 65536),
            new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), Earth, DateTimeExtension.J2000, Frames.Frame.ICRF));

        internal static bool VectorComparer(Vector3 v1, Vector3 v2)
        {
            lock (LockObj)
            {
                return System.Math.Abs(v1.X - v2.X) < 1E-03 && System.Math.Abs(v1.Y - v2.Y) < 1E-03 && System.Math.Abs(v1.Z - v2.Z) < 1E-03;
            }
        }
        
        internal static bool StateVectorComparer(StateVector sv1, StateVector sv2)
        {
            lock (LockObj)
            {
                return VectorComparer(sv1.Position,sv2.Position) && VectorComparer(sv1.Velocity,sv2.Velocity) && sv1.Frame==sv2.Frame && sv1.Epoch==sv2.Epoch && Equals(sv1.Observer, sv2.Observer);
            }
        }
    }
}