// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.DTO;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.TimeSystem.Frames;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using KeplerianElements = IO.Astrodynamics.OrbitalParameters.KeplerianElements;
using Launch = IO.Astrodynamics.Maneuver.Launch;
using Planetodetic = IO.Astrodynamics.Coordinates.Planetodetic;
using Quaternion = IO.Astrodynamics.Math.Quaternion;
using Site = IO.Astrodynamics.Surface.Site;
using StateOrientation = IO.Astrodynamics.OrbitalParameters.StateOrientation;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;
using Window = IO.Astrodynamics.TimeSystem.Window;

namespace IO.Astrodynamics.Converters;

public static class ProfilesConfiguration
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
        return new DTO.StateVector(stateVector.Observer.NaifId, stateVector.Epoch.TimeSpanFromJ2000().TotalSeconds, stateVector.Frame.Name, stateVector.Position.Convert(),
            stateVector.Velocity.Convert());
    }

    internal static StateVector Convert(this DTO.StateVector stateVector)
    {
        return new StateVector(stateVector.Position.Convert(), stateVector.Velocity.Convert(), new CelestialBody(stateVector.CenterOfMotionId),
            Time.Create(stateVector.Epoch, TimeFrame.TDBFrame), new Frame(stateVector.Frame));
    }

    internal static DTO.StateOrientation Convert(this StateOrientation stateOrientation)
    {
        return new DTO.StateOrientation(stateOrientation.Rotation.Convert(), stateOrientation.AngularVelocity.Convert(), stateOrientation.Epoch.TimeSpanFromJ2000().TotalSeconds,
            stateOrientation.ReferenceFrame.Name);
    }

    internal static DTO.Window Convert(this Window window)
    {
        return new DTO.Window(window.StartDate.TimeSpanFromJ2000().TotalSeconds, window.EndDate.TimeSpanFromJ2000().TotalSeconds);
    }

    internal static Window Convert(this in DTO.Window window)
    {
        return new Window(Time.Create(window.Start, TimeFrame.TDBFrame), Time.Create(window.End, TimeFrame.TDBFrame));
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

    internal static OrbitalParameters.KeplerianElements Convert(this DTO.KeplerianElements keplerianElements)
    {
        var observer = new CelestialBody(keplerianElements.CenterOfMotionId);
        return new KeplerianElements(keplerianElements.SemiMajorAxis, keplerianElements.Eccentricity, keplerianElements.Inclination, keplerianElements.AscendingNodeLongitude,
            keplerianElements.PeriapsisArgument, keplerianElements.MeanAnomaly, observer, Time.Create(keplerianElements.Epoch, TimeFrame.TDBFrame),
            new Frame(keplerianElements.Frame), keplerianElements.TrueAnomaly, TimeSpan.FromSeconds(keplerianElements.OrbitalPeriod));
    }

    internal static DTO.EquinoctialElements Convert(this OrbitalParameters.EquinoctialElements equinoctialElements)
    {
        return new EquinoctialElements(equinoctialElements.Epoch.TimeSpanFromJ2000().TotalSeconds, equinoctialElements.Observer.NaifId, equinoctialElements.Frame.Name,
            equinoctialElements.SemiMajorAxis(), equinoctialElements.G, equinoctialElements.F,
            equinoctialElements.K, equinoctialElements.H, equinoctialElements.L0, 0.0, -Constants.PI2, Constants.PI2, 0.0);
    }

    internal static OrbitalParameters.EquinoctialElements Convert(this DTO.EquinoctialElements equinoctialElements)
    {
        var observer = new CelestialBody(equinoctialElements.CenterOfMotionId);
        var e = System.Math.Sqrt(equinoctialElements.H * equinoctialElements.H + equinoctialElements.K * equinoctialElements.K);
        var p = equinoctialElements.SemiMajorAxis / (1 - e * e);
        return new OrbitalParameters.EquinoctialElements(p, equinoctialElements.K, equinoctialElements.H, equinoctialElements.Q, equinoctialElements.P, equinoctialElements.L,
            observer, Time.Create(equinoctialElements.Epoch, TimeFrame.TDBFrame), new Frame(equinoctialElements.Frame));
    }
}