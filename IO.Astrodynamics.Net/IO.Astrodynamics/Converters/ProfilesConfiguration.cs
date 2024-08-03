// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.DTO;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Time;
using Launch = IO.Astrodynamics.Maneuver.Launch;
using Planetodetic = IO.Astrodynamics.Coordinates.Planetodetic;
using Quaternion = IO.Astrodynamics.Math.Quaternion;
using Site = IO.Astrodynamics.Surface.Site;
using StateOrientation = IO.Astrodynamics.OrbitalParameters.StateOrientation;
using StateVector = IO.Astrodynamics.OrbitalParameters.StateVector;
using Window = IO.Astrodynamics.Time.Window;

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
        return new DTO.StateVector(stateVector.Observer.NaifId, stateVector.Epoch.SecondsFromJ2000TDB(), stateVector.Frame.Name, stateVector.Position.Convert(),
            stateVector.Velocity.Convert());
    }

    internal static DTO.StateOrientation Convert(this StateOrientation stateOrientation)
    {
        return new DTO.StateOrientation(stateOrientation.Rotation.Convert(), stateOrientation.AngularVelocity.Convert(), stateOrientation.Epoch.SecondsFromJ2000TDB(),
            stateOrientation.ReferenceFrame.Name);
    }

    internal static DTO.Window Convert(this Window window)
    {
        return new DTO.Window(window.StartDate.SecondsFromJ2000TDB(), window.EndDate.SecondsFromJ2000TDB());
    }

    internal static Window Convert(this in DTO.Window window)
    {
        return new Window(DateTimeExtension.CreateTDB(window.Start), DateTimeExtension.CreateTDB(window.End));
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
}