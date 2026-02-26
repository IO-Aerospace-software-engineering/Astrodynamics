// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.TimeSystem;
using Quaternion = IO.Astrodynamics.Math.Quaternion;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Propagator;

public abstract class PropagatorBase : IPropagator
{
    protected readonly CelestialItem OriginalObserver;
    public Window Window { get; }
    public Spacecraft Spacecraft { get; }
    public IIntegrator Integrator { get; }
    public TimeSpan DeltaT { get; }

    protected uint SvCacheSize;
    protected StateVector[] SvCache;

    protected PropagatorBase(in Window window, Spacecraft spacecraft, IIntegrator integrator, TimeSpan deltaT)
    {
        Spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        Integrator = integrator ?? throw new ArgumentNullException(nameof(integrator));
        ValidateInertialFrame(spacecraft);

        OriginalObserver = spacecraft.InitialOrbitalParameters.Observer as CelestialItem;
        Window = new Window(window.StartDate.ToTDB(), window.EndDate.ToTDB());
        DeltaT = deltaT;
    }

    protected void InitializeCache(StateVector initialState)
    {
        SvCacheSize = (uint)System.Math.Round(Window.Length.TotalSeconds / DeltaT.TotalSeconds, MidpointRounding.AwayFromZero) + 1;
        SvCache = new StateVector[SvCacheSize];
        SvCache[0] = initialState;
        for (int i = 1; i < SvCacheSize; i++)
        {
            SvCache[i] = new StateVector(Vector3.Zero, Vector3.Zero, initialState.Observer, Window.StartDate + (i * DeltaT), initialState.Frame);
        }
    }

    public void Propagate()
    {
        Spacecraft.Frame.AddStateOrientationToICRF(new StateOrientation(Quaternion.Zero, Vector3.Zero, Window.StartDate, Spacecraft.InitialOrbitalParameters.Frame));
        for (int i = 0; i < SvCacheSize - 1; i++)
        {
            var prvSv = SvCache[i];
            if (Spacecraft.StandbyManeuver?.CanExecute(prvSv) == true)
            {
                var res = Spacecraft.StandbyManeuver.TryExecute(prvSv);
                Spacecraft.Frame.AddStateOrientationToICRF(res.so);
            }

            Integrator.Integrate(SvCache, i + 1);
        }

        var latestOrientation = Spacecraft.Frame.GetLatestStateOrientationToICRF();
        Spacecraft.Frame.AddStateOrientationToICRF(new StateOrientation(latestOrientation.Rotation, latestOrientation.AngularVelocity, Window.EndDate,
            latestOrientation.ReferenceFrame));

        StorePropagatedStates();
    }

    protected abstract void StorePropagatedStates();

    private static void ValidateInertialFrame(Spacecraft spacecraft)
    {
        if (spacecraft.InitialOrbitalParameters.Frame != Frame.ICRF
            && spacecraft.InitialOrbitalParameters.Frame != Frame.B1950
            && spacecraft.InitialOrbitalParameters.Frame != Frame.FK4
            && spacecraft.InitialOrbitalParameters.Frame != Frame.ECLIPTIC_J2000
            && spacecraft.InitialOrbitalParameters.Frame != Frame.ECLIPTIC_B1950
            && spacecraft.InitialOrbitalParameters.Frame != Frame.GALACTIC_SYSTEM2)
        {
            throw new ArgumentException("Spacecraft initial orbital parameters must be defined in inertial frame", nameof(spacecraft));
        }
    }

    public void Dispose()
    {
    }
}
