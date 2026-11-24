using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Tests
{
    internal static class TestHelpers
    {
        internal static CelestialBody Sun => new(Stars.Sun);

        internal static CelestialBody Earth => new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, new TimeSystem.Time(2021, 1, 1, TimeScale.TDB), atmosphericModel: new EarthStandardAtmosphere());

        internal static CelestialBody Moon => new(PlanetsAndMoons.MOON, Frames.Frame.ICRF, new TimeSystem.Time(2021, 1, 1, TimeScale.TDB));

        internal static CelestialBody EarthAtJ2000 => new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, new TimeSystem.Time(2000, 1, 1, 12, 0, 0, TimeScale.TDB));

        internal static Barycenter SsbAtJ2000 => new(0, new TimeSystem.Time(2000, 1, 1, 12, 0, 0, TimeScale.TDB));

        internal static CelestialBody EarthWithAtmAndGeoAtJ2000 => new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, new TimeSystem.Time(2000, 1, 1, 12, 0, 0, TimeScale.TDB),
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10), new EarthStandardAtmosphere());

        internal static CelestialBody MoonAtJ2000 => new(PlanetsAndMoons.MOON, Frames.Frame.ICRF, new TimeSystem.Time(2000, 1, 1, 12, 0, 0, TimeScale.TDB));

        internal static CelestialBody MoonAt20011214 => new(PlanetsAndMoons.MOON, Frames.Frame.ICRF, new TimeSystem.Time(2001, 12, 14, 0, 0, 0, TimeScale.TDB));

        internal static Spacecraft Spacecraft => new Spacecraft(-666, "GenericSpacecraft", 100.0, new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), Earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF), 1000.0,
            new Clock("GenericClk", 65536));

        /// <summary>
        /// Rotation of a frame transform <paramref name="dt"/> seconds later, from its angular velocity under the SPICE
        /// convention of <see cref="Frames.Frame.GetStateOrientationToICRF"/>: R(t + dt) = R(t) exp(-[w×] dt), where w
        /// is the angular velocity of ICRF relative to the frame, in the frame axes.
        /// </summary>
        internal static Quaternion RotateWithFrameAngularVelocity(StateOrientation orientation, double dt)
        {
            var w = orientation.AngularVelocity;
            return orientation.Rotation * new Quaternion(w.Normalize(), -w.Magnitude() * dt);
        }

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

        /// <summary>The difference between two angles, rad, modulo 2π: 0 and 2π are the same angle.</summary>
        internal static double AngleDifference(double a1, double a2)
        {
            return System.Math.Abs(System.Math.IEEERemainder(a1 - a2, IO.Astrodynamics.Constants.TwoPi));
        }

        // The size of two orbits: their semi-major axes, or within 1e-6 of e = 1 their perigee radii, relative: there the
        // semi-major axis is the conic's since #459, ±1e20 m for the state of an exact parabola, whose e rounding moves off 1,
        // and +Infinity for the parabola itself
        private static bool SameShape(IO.Astrodynamics.OrbitalParameters.OrbitalParameters o1,
            IO.Astrodynamics.OrbitalParameters.OrbitalParameters o2, double a1, double a2, double tolerance)
        {
            if (o1.IsParabolic() && o2.IsParabolic())
            {
                return System.Math.Abs(o1.PerigeeRadius() - o2.PerigeeRadius()) <= 1e-12 * o1.PerigeeRadius();
            }

            return double.IsPositiveInfinity(a1) && double.IsPositiveInfinity(a2) || System.Math.Abs(a1 - a2) < tolerance;
        }

        internal static bool KeplerComparer(KeplerianElements k1, KeplerianElements k2)
        {
            var tolerance = 1E-06;
            return SameShape(k1, k2, k1.A, k2.A, tolerance) &&
                   System.Math.Abs(k1.E - k2.E) < tolerance && System.Math.Abs(k1.I - k2.I) < tolerance &&
                   AngleDifference(k1.RAAN, k2.RAAN) < tolerance && AngleDifference(k1.AOP, k2.AOP) < tolerance && AngleDifference(k1.M, k2.M) < tolerance &&
                   k1.Epoch == k2.Epoch && k1.Frame == k2.Frame && Equals(k1.Observer, k2.Observer);
        }

        /// <summary>
        /// Compares the derived Keplerian angles, defined for regular elements only (e > 0 and i > 0); tests of circular
        /// or equatorial elements compare P, F, G, H, K, L0 or the states (#452).
        /// </summary>
        internal static bool EquinoctialComparer(EquinoctialElements e1, EquinoctialElements e2)
        {
            var tolerance = 1E-06;
            // P too, relative (#462: it was NaN or ±∞ for every parabola, unseen)
            return System.Math.Abs(e1.P - e2.P) <= tolerance * System.Math.Max(1.0, System.Math.Abs(e1.P)) &&
                   SameShape(e1, e2, e1.SemiMajorAxis(), e2.SemiMajorAxis(), tolerance) &&
                   System.Math.Abs(e1.Eccentricity() - e2.Eccentricity()) < tolerance &&
                   System.Math.Abs(e1.Inclination() - e2.Inclination()) < tolerance && AngleDifference(e1.AscendingNode(), e2.AscendingNode()) < tolerance &&
                   AngleDifference(e1.ArgumentOfPeriapsis(), e2.ArgumentOfPeriapsis()) < tolerance && AngleDifference(e1.MeanAnomaly(), e2.MeanAnomaly()) < tolerance &&
                   e1.Epoch == e2.Epoch && e1.Frame == e2.Frame && Equals(e1.Observer, e2.Observer);
        }

        internal static bool TimeComparer(TimeSystem.Time v1, TimeSystem.Time v2)
        {
            return Equals(v1.Scale, v2.Scale) && System.Math.Abs((v1 - v2).TotalSeconds) < 1E-03;
        }
        
        /// <summary>
        /// Helper method to normalize whitespace in strings for comparison.
        /// Removes all whitespace characters (spaces, tabs, newlines, carriage returns, etc.)
        /// </summary>
        internal static string NormalizeWhitespace(string input)
        {
            return System.Text.RegularExpressions.Regex.Replace(input, @"\s+", "");
        }
    }
}