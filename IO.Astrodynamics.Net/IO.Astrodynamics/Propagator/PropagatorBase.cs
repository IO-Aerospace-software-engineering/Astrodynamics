// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Events;
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

    protected StateVector InitialState { get; set; }

    // Standalone attitude maneuver to apply at each output epoch (null if none)
    private Attitude _continuousAttitude;

    protected PropagatorBase(in Window window, Spacecraft spacecraft, IIntegrator integrator, TimeSpan deltaT)
    {
        Spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        Integrator = integrator ?? throw new ArgumentNullException(nameof(integrator));
        ValidateInertialFrame(spacecraft);

        OriginalObserver = spacecraft.InitialOrbitalParameters.Observer as CelestialItem;
        Window = new Window(window.StartDate.ToTDB(), window.EndDate.ToTDB());
        DeltaT = deltaT;
    }

    public PropagationSolution Propagate()
    {
        // 2. Build event detectors from maneuver chain (also discovers standalone attitude maneuvers)
        _continuousAttitude = null;
        var eventDetectors = BuildEventDetectors();

        // 1. Add initial orientation (skip zero-quaternion sentinel when continuous attitude provides the real one)
        if (_continuousAttitude == null)
        {
            Spacecraft.Frame.AddStateOrientationToICRF(
                new StateOrientation(Quaternion.Zero, Vector3.Zero, Window.StartDate,
                    Spacecraft.InitialOrbitalParameters.Frame));
        }

        // 3. Create solution
        var solution = new PropagationSolution();

        var currentPos = InitialState.Position;
        var currentVel = InitialState.Velocity;
        var currentEpoch = Window.StartDate;

        // Compute required propagation duration (may exceed window for non-fitting step sizes)
        uint outputCount = (uint)System.Math.Round(Window.Length.TotalSeconds / DeltaT.TotalSeconds,
            MidpointRounding.AwayFromZero) + 1;
        double outputDuration = (outputCount - 1) * DeltaT.TotalSeconds;
        double propagationDuration = System.Math.Max(Window.Length.TotalSeconds, outputDuration);
        var propagationEnd = Window.StartDate.AddSeconds(propagationDuration);

        // 4. Segment loop
        while (true)
        {
            double remaining = (propagationEnd - currentEpoch).TotalSeconds;
            if (remaining <= 0.0) break;

            // a. Integrate segment
            var result = Integrator.IntegrateSegment(
                currentPos, currentVel, currentEpoch, remaining, eventDetectors);

            // b. Add segment to solution
            solution.AddSegment(result.Segment);

            // c. Handle event
            if (result.Event.HasValue)
            {
                var evt = result.Event.Value;
                var eventEpoch = currentEpoch.AddSeconds(evt.EventTime);

                // Create StateVector at event epoch
                var eventState = new StateVector(evt.EventPosition, evt.EventVelocity,
                    InitialState.Observer, eventEpoch, InitialState.Frame);

                // Delegate to the event detector
                var detector = eventDetectors[evt.DetectorIndex];
                var eventResult = detector.HandleEvent(eventState, Spacecraft);

                if (eventResult.Orientation != null)
                {
                    Spacecraft.Frame.AddStateOrientationToICRF(eventResult.Orientation);
                }

                if (eventResult.Action == EventAction.StopAndRestart)
                {
                    // Update state from post-maneuver
                    currentPos = eventResult.ModifiedState.Position;
                    currentVel = eventResult.ModifiedState.Velocity;
                    currentEpoch = eventResult.ModifiedState.Epoch;

                    // Update event detectors for next segment
                    eventDetectors = UpdateEventDetectors(eventResult.NextDetector);

                    // Re-initialize integrator for new dynamics
                    var newState = new StateVector(currentPos, currentVel,
                        InitialState.Observer, currentEpoch, InitialState.Frame);
                    Integrator.Initialize(newState);

                    continue;
                }
            }
            else
            {
                // No event — propagation complete
                break;
            }
        }

        // 5. Generate output StateVectors by interpolating solution at DeltaT epochs
        var outputStates = GenerateOutput(solution, outputCount);

        // 6. Add final orientation
        var latestOrientation = Spacecraft.Frame.GetLatestStateOrientationToICRF();
        Spacecraft.Frame.AddStateOrientationToICRF(
            new StateOrientation(latestOrientation.Rotation, latestOrientation.AngularVelocity,
                Window.EndDate, latestOrientation.ReferenceFrame));

        // 7. Store propagated states
        StorePropagatedStates(outputStates);

        // 8. Attach sampled output to solution
        solution.SetOutputStates(outputStates);

        return solution;
    }

    /// <summary>
    /// Build event detectors from the spacecraft's standby maneuver chain.
    /// Processes leading attitude maneuvers immediately (they don't need event detection).
    /// </summary>
    private List<IEventDetector> BuildEventDetectors()
    {
        var detectors = new List<IEventDetector>();

        // Walk the maneuver chain to find the first impulse maneuver
        var maneuver = Spacecraft.StandbyManeuver;
        while (maneuver != null)
        {
            if (maneuver is ImpulseManeuver impulse)
            {
                detectors.Add(new ManeuverEventDetector(impulse));
                break;
            }

            if (maneuver is Attitude attitude)
            {
                // Process initial attitude maneuvers immediately at the initial state
                var (_, so) = attitude.TryExecute(InitialState);
                if (so.Rotation != Quaternion.Zero)
                {
                    Spacecraft.Frame.AddStateOrientationToICRF(so);
                }

                // Remember for continuous orientation computation at each output epoch
                _continuousAttitude = attitude;

                maneuver = attitude.NextManeuver;
            }
            else
            {
                break;
            }
        }

        return detectors;
    }

    /// <summary>
    /// Update event detectors after an event (next detector from event result).
    /// </summary>
    private static List<IEventDetector> UpdateEventDetectors(IEventDetector nextDetector)
    {
        var detectors = new List<IEventDetector>();
        if (nextDetector != null)
        {
            detectors.Add(nextDetector);
        }

        return detectors;
    }

    /// <summary>
    /// Generate output StateVectors by interpolating the solution at DeltaT intervals.
    /// Output may extend slightly past the window end when the step size doesn't evenly divide the window.
    /// Also computes continuous attitude orientations at each output epoch if a standalone attitude maneuver exists.
    /// </summary>
    private StateVector[] GenerateOutput(PropagationSolution solution, uint count)
    {
        var output = new StateVector[count];

        for (int i = 0; i < count; i++)
        {
            var epoch = Window.StartDate + (i * DeltaT);
            var (pos, vel) = solution.InterpolateAt(epoch);
            output[i] = new StateVector(pos, vel, InitialState.Observer, epoch, InitialState.Frame);

            // Apply continuous attitude maneuver at each output epoch
            if (_continuousAttitude != null)
            {
                var localSv = output[i].RelativeTo(_continuousAttitude.ManeuverCenter, Aberration.None).ToStateVector();
                var orientation = _continuousAttitude.ComputeOrientation(localSv);
                if (orientation != Quaternion.Zero)
                {
                    Spacecraft.Frame.AddStateOrientationToICRF(
                        new StateOrientation(orientation, Vector3.Zero, epoch,
                            Spacecraft.InitialOrbitalParameters.Frame));
                }
            }
        }

        return output;
    }

    protected abstract void StorePropagatedStates(StateVector[] outputStates);

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
