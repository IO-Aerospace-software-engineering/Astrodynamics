// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.DTO;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using KeplerianElements = IO.Astrodynamics.OrbitalParameters.KeplerianElements;
using Launch = IO.Astrodynamics.Maneuver.Launch;
using Planetodetic = IO.Astrodynamics.Coordinates.Planetodetic;
using Quaternion = IO.Astrodynamics.Math.Quaternion;
using Site = IO.Astrodynamics.Surface.Site;
using StateOrientation = IO.Astrodynamics.OrbitalParameters.StateOrientation;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;
using Window = IO.Astrodynamics.TimeSystem.Window;
using OrbitalElementsType = IO.Astrodynamics.OrbitalParameters.OrbitalElementsType;

namespace IO.Astrodynamics.Converters;

internal static class ProfilesConfiguration
{
    internal static Vector3 Convert(this in Vector3D vector3D)
    {
        return new Vector3(vector3D.X, vector3D.Y, vector3D.Z);
    }

    internal static Vector3D Convert(this in Vector3 vector3D)
    {
        return new Vector3D(vector3D.X, vector3D.Y, vector3D.Z);
    }

    internal static Quaternion Convert(this in DTO.Quaternion quaternion)
    {
        return new Quaternion(quaternion.W, quaternion.X, quaternion.Y, quaternion.Z);
    }

    internal static DTO.Quaternion Convert(this in Quaternion quaternion)
    {
        return new DTO.Quaternion(quaternion.W, quaternion.VectorPart.X, quaternion.VectorPart.Y, quaternion.VectorPart.Z);
    }

    internal static DTO.StateVector Convert(this StateVector stateVector)
    {
        return new DTO.StateVector(stateVector.Observer.NaifId, stateVector.Epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds, stateVector.Frame.Name, stateVector.Position.Convert(),
            stateVector.Velocity.Convert());
    }

    /// <summary>
    /// The state a two-body call of <see cref="SpiceAPI"/> returns, about the <paramref name="observer"/> and in the
    /// <paramref name="frame"/> of its input (#725): before, a new <see cref="CelestialBody"/> built from the centre's
    /// identifier, read again from the data provider under the SpiceAPI lock (#664), and a frame rebuilt from its name, so
    /// that the caller's models, a barycentre or a body from custom parameters were lost.
    /// </summary>
    internal static StateVector Convert(this DTO.StateVector stateVector, ILocalizable observer, Frame frame)
    {
        return new StateVector(stateVector.Position.Convert(), stateVector.Velocity.Convert(), observer,
            Time.Create(stateVector.Epoch, TimeScale.TDB), frame);
    }

    internal static DTO.StateOrientation Convert(this StateOrientation stateOrientation)
    {
        return new DTO.StateOrientation(stateOrientation.Rotation.Convert(), stateOrientation.AngularVelocity.Convert(), stateOrientation.Epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds,
            stateOrientation.ReferenceFrame.Name);
    }

    internal static DTO.Window Convert(this Window window)
    {
        return new DTO.Window(window.StartDate.ToTDB().TimeSpanFromJ2000().TotalSeconds, window.EndDate.ToTDB().TimeSpanFromJ2000().TotalSeconds);
    }

    internal static Window Convert(this in DTO.Window window)
    {
        return new Window(Time.Create(window.Start, TimeScale.TDB), Time.Create(window.End, TimeScale.TDB));
    }

    internal static DTO.Planetodetic Convert(this Planetodetic planetodetic)
    {
        return new DTO.Planetodetic(planetodetic.Longitude, planetodetic.Latitude, planetodetic.Altitude);
    }

    internal static DTO.Site Convert(this Site site)
    {
        return new DTO.Site(site.NaifId, site.CelestialBody.NaifId, site.Planetodetic.Convert(), site.Name, string.Empty);
    }

    internal static DTO.Launch Convert(this Launch launch)
    {
        return new DTO.Launch(launch.LaunchSite.Convert(), launch.RecoverySite.Convert(), launch.LaunchByDay ?? false, Parameters.FindLaunchResolution.TotalSeconds,
            launch.TargetOrbit.ToStateVector().Convert(), new DTO.Window());
    }

    internal static DTO.KeplerianElements Convert(this IO.Astrodynamics.OrbitalParameters.KeplerianElements keplerianElements)
    {
        return new DTO.KeplerianElements(keplerianElements.Observer.NaifId, keplerianElements.Epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds, keplerianElements.PerigeeRadius(),
            keplerianElements.E, keplerianElements.I, keplerianElements.RAAN, keplerianElements.AOP, keplerianElements.M, keplerianElements.Frame.Name);
    }

    /// <summary>
    /// The elements <see cref="SpiceAPI.ConvertStateVectorToConicOrbitalElement(OrbitalParameters.StateVector, double)"/>
    /// returns, about the <paramref name="observer"/> and in the <paramref name="frame"/> of its state (#725, see
    /// <see cref="Convert(DTO.StateVector, ILocalizable, Frame)"/>).
    /// </summary>
    internal static OrbitalParameters.KeplerianElements Convert(this DTO.KeplerianElements keplerianElements, ILocalizable observer, Frame frame)
    {

        // Within 1e-6 of e = 1 the elements need the perigee radius, with the conic's semi-major axis, oscltx's −μ/(2ξ),
        // +Infinity where oscltx gives none (0: e = 1, or a zero energy) (#459; before, +Infinity in the whole band)
        if (System.Math.Abs(keplerianElements.Eccentricity - 1.0) < 1E-06)
        {
            double semiMajorAxis = keplerianElements.SemiMajorAxis == 0.0 ? double.PositiveInfinity : keplerianElements.SemiMajorAxis;
            return new KeplerianElements(semiMajorAxis, keplerianElements.Eccentricity, keplerianElements.Inclination,
                keplerianElements.AscendingNodeLongitude, keplerianElements.PeriapsisArgument, keplerianElements.MeanAnomaly, observer,
                Time.Create(keplerianElements.Epoch, TimeScale.TDB), frame, keplerianElements.TrueAnomaly, null,
                keplerianElements.PerifocalDistance, OrbitalElementsType.Osculating);
        }

        // oscltx's period is 0 for an open orbit, which has none (#663), and can exceed TimeSpan.MaxValue on an ellipse
        // (above 29 227 years, e within about 5e-6 of 1): no period is given then, Period() computing its own or throwing
        double tau = keplerianElements.OrbitalPeriod;
        TimeSpan? period = tau > 0.0 && tau < Time.MaximumSpanSeconds ? Time.SecondsToSpan(tau) : null;
        return new KeplerianElements(keplerianElements.SemiMajorAxis, keplerianElements.Eccentricity, keplerianElements.Inclination, keplerianElements.AscendingNodeLongitude,
            keplerianElements.PeriapsisArgument, keplerianElements.MeanAnomaly, observer, Time.Create(keplerianElements.Epoch, TimeScale.TDB),
            frame, keplerianElements.TrueAnomaly, period, null, OrbitalElementsType.Osculating);
    }

    /// <summary>
    /// The elements EQNCPV takes (eqncpv.c): a, h = e sin ϖ (G), k = e cos ϖ (F), p (K), q (H), the mean longitude
    /// λ = Ω + ω + M at the epoch, and a pole at (−π/2, π/2), which applies no rotation. λ is
    /// <see cref="OrbitalParameters.OrbitalParameters.MeanLongitude"/>, right for every set since #469, circular and
    /// equatorial ones included (#450: before, the true longitude L0, the state of another point of the orbit).
    /// </summary>
    internal static DTO.EquinoctialElements Convert(this OrbitalParameters.EquinoctialElements equinoctialElements)
    {
        return new EquinoctialElements(equinoctialElements.Epoch.ToTDB().TimeSpanFromJ2000().TotalSeconds, equinoctialElements.Observer.NaifId, equinoctialElements.Frame.Name,
            equinoctialElements.SemiMajorAxis(), equinoctialElements.G, equinoctialElements.F,
            equinoctialElements.K, equinoctialElements.H, equinoctialElements.MeanLongitude(), 0.0, -Constants.HalfPi, Constants.HalfPi, 0.0);
    }
}