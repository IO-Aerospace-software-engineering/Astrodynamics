// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Propagator;

/// <summary>
/// Central-body-centered propagator for missions around any celestial body (LEO, GEO, lunar, interplanetary, etc.).
/// The central body is inferred from the spacecraft's initial orbital parameters observer.
/// When the observer is a Star or Barycenter, the Sun is used as the central body.
/// </summary>
public class CentralBodyPropagator : PropagatorBase
{
    private readonly CelestialItem _centralBody;

    // Orientation cache of the central body frame, active on the propagating thread only (Frame.UseOrientationCache)
    private readonly PropagationFrameOrientationCache _orientationCache;

    /// <summary>
    /// Instantiate central-body propagator with a custom integrator.
    /// Forces (gravity, third-body perturbations, drag, SRP) are built automatically from the
    /// provided celestial bodies and flags.
    /// The central body is inferred from the spacecraft's orbital parameters observer.
    /// </summary>
    public CentralBodyPropagator(in Window window, Spacecraft spacecraft, Integrator integrator,
        IEnumerable<CelestialItem> celestialBodies, bool includeAtmosphericDrag,
        bool includeSolarRadiationPressure, TimeSpan deltaT)
        : this(window, spacecraft, integrator, deltaT)
    {
        var items = (celestialBodies ?? Array.Empty<CelestialItem>()).Distinct().ToArray();
        var forces = BuildCentralBodyForces(_centralBody, items, spacecraft, includeAtmosphericDrag,
            includeSolarRadiationPressure);
        InjectEphemerisCache(Window, forces, items, _centralBody, InitialState.Frame);
        _orientationCache = CreateFrameOrientationCache(Window, _centralBody);
        foreach (var force in forces)
        {
            integrator.AddForce(force);
        }

        integrator.Initialize(InitialState);
    }

    /// <summary>
    /// Instantiate a central-body propagator with a custom integrator and its forces, built as by the overload without
    /// <paramref name="covariancePropagation"/>, which also propagates the state transition matrix and, in
    /// <see cref="CovariancePropagationMode.Covariance"/>, the covariance of the initial orbital parameters.
    /// </summary>
    /// <param name="window">The propagation window.</param>
    /// <param name="spacecraft">The spacecraft.</param>
    /// <param name="integrator">The integrator; the state transition matrix needs an <see cref="RK78Integrator"/>.</param>
    /// <param name="celestialBodies">The bodies of gravity, third-body perturbations, drag and SRP.</param>
    /// <param name="includeAtmosphericDrag">Whether to include atmospheric drag.</param>
    /// <param name="includeSolarRadiationPressure">Whether to include solar radiation pressure.</param>
    /// <param name="deltaT">The step of the output states.</param>
    /// <param name="covariancePropagation">The propagation of the state transition matrix and of the covariance; null
    /// for none, as the overload without it. Without it, a covariance of the initial orbital parameters is not
    /// propagated: the output states carry none, and no partial derivative is evaluated.</param>
    /// <exception cref="NotSupportedException"><paramref name="covariancePropagation"/> is given and the integrator is
    /// not an <see cref="RK78Integrator"/>.</exception>
    /// <exception cref="ArgumentException">Process noise or consider parameters without the covariance, or a consider
    /// parameter without its force.</exception>
    /// <exception cref="InvalidOperationException">The covariance is asked for, and the initial orbital parameters carry
    /// none, carry it at another epoch than the start of the window, or carry one that is not symmetric and positive
    /// semi-definite.</exception>
    public CentralBodyPropagator(in Window window, Spacecraft spacecraft, Integrator integrator,
        IEnumerable<CelestialItem> celestialBodies, bool includeAtmosphericDrag,
        bool includeSolarRadiationPressure, TimeSpan deltaT, CovariancePropagationOptions covariancePropagation)
        : this(window, spacecraft, integrator, celestialBodies, includeAtmosphericDrag, includeSolarRadiationPressure,
            deltaT)
    {
        EnableCovariancePropagation(covariancePropagation);
    }

    /// <summary>
    /// Instantiate a central-body propagator with a custom integrator that already has its forces, as by the overload
    /// without <paramref name="covariancePropagation"/>, which also propagates the state transition matrix and, in
    /// <see cref="CovariancePropagationMode.Covariance"/>, the covariance of the initial orbital parameters. The columns
    /// of Ψ follow the forces of the integrator at construction.
    /// </summary>
    /// <param name="window">The propagation window.</param>
    /// <param name="spacecraft">The spacecraft.</param>
    /// <param name="integrator">The integrator and its forces; the state transition matrix needs an
    /// <see cref="RK78Integrator"/>.</param>
    /// <param name="deltaT">The step of the output states.</param>
    /// <param name="covariancePropagation">The propagation of the state transition matrix and of the covariance; null
    /// for none.</param>
    /// <exception cref="NotSupportedException"><paramref name="covariancePropagation"/> is given and the integrator is
    /// not an <see cref="RK78Integrator"/>.</exception>
    /// <exception cref="ArgumentException">Process noise or consider parameters without the covariance, or a consider
    /// parameter without its force.</exception>
    /// <exception cref="InvalidOperationException">The covariance is asked for, and the initial orbital parameters carry
    /// none, carry it at another epoch than the start of the window, or carry one that is not symmetric and positive
    /// semi-definite.</exception>
    public CentralBodyPropagator(in Window window, Spacecraft spacecraft, IIntegrator integrator, TimeSpan deltaT,
        CovariancePropagationOptions covariancePropagation)
        : this(window, spacecraft, integrator, deltaT)
    {
        EnableCovariancePropagation(covariancePropagation);
    }

    /// <summary>
    /// A central-body propagator with an integrator that already has its forces, the forces of another propagation, and
    /// the orientation cache of that propagation, which must cover <paramref name="window"/>: the propagation of an
    /// avoidance option from the state at its burn, with the force model of the trajectory it leaves (step 10, E4;
    /// #402, in RK7(8) or by Velocity-Verlet).
    /// The solution evaluates those forces under <paramref name="syncRoot"/>, the lock of that propagation.
    /// </summary>
    internal CentralBodyPropagator(in Window window, Spacecraft spacecraft, Integrator integrator, TimeSpan deltaT,
        CovariancePropagationOptions covariancePropagation, PropagationFrameOrientationCache orientationCache,
        object syncRoot)
        : this(window, spacecraft, integrator, deltaT)
    {
        _orientationCache = orientationCache;
        DynamicsSyncRoot = syncRoot ?? throw new ArgumentNullException(nameof(syncRoot));
        integrator.Initialize(InitialState);
        EnableCovariancePropagation(covariancePropagation);
    }

    /// <summary>
    /// Instantiate central-body propagator with a custom integrator that already has forces configured.
    /// No forces are added; the integrator is used as-is.
    /// The central body is inferred from the spacecraft's orbital parameters observer.
    /// When the observer is a Star or Barycenter, the Sun is used as the central body.
    /// </summary>
    public CentralBodyPropagator(in Window window, Spacecraft spacecraft, IIntegrator integrator, TimeSpan deltaT)
        : base(window, spacecraft, integrator, deltaT)
    {
        _centralBody = ResolveCentralBody(spacecraft);

        InitialState = StartState(Spacecraft.InitialOrbitalParameters, Window.StartDate)
            .RelativeTo(_centralBody, Aberration.None).ToStateVector();
    }

    /// <summary>
    /// The state of <paramref name="orbitalParameters"/> at <paramref name="start"/>: a state vector given at that instant
    /// as it is, any other orbital parameters through <see cref="OrbitalParameters.AtEpoch"/>.
    /// </summary>
    /// <remarks>
    /// A state vector at the start of the window is not taken through the Keplerian elements: that round trip returns
    /// it only to its rounding (about 1e-8 m), which would move the start and the steps of the propagation, and its
    /// derivative would not be exactly the identity (#398). The covariance of the state is not carried, as through the
    /// Keplerian elements before: the initial covariance is read from the initial orbital parameters of the spacecraft.
    /// </remarks>
    private static StateVector StartState(OrbitalParameters.OrbitalParameters orbitalParameters, in Time start)
    {
        if (orbitalParameters is StateVector state && state.Epoch.CompareTo(start) == 0)
        {
            return new StateVector(state.Position, state.Velocity, state.Observer, start, state.Frame);
        }

        return orbitalParameters.AtEpoch(start).ToStateVector();
    }

    /// <summary>
    /// Instantiate central-body propagator with default Velocity-Verlet integrator.
    /// Forces (gravity, third-body perturbations, drag, SRP) are built automatically from the
    /// provided celestial bodies and flags.
    /// The central body is inferred from the spacecraft's orbital parameters observer.
    /// </summary>
    public CentralBodyPropagator(in Window window, Spacecraft spacecraft,
        IEnumerable<CelestialItem> celestialBodies, bool includeAtmosphericDrag,
        bool includeSolarRadiationPressure, TimeSpan deltaT)
        : this(window, spacecraft,
            CreateDefaultIntegrator(window, spacecraft, celestialBodies,
                includeAtmosphericDrag, includeSolarRadiationPressure, deltaT),
            deltaT)
    {
        _orientationCache = CreateFrameOrientationCache(Window, _centralBody);
    }

    /// <summary>
    /// Resolves the central body from the spacecraft's observer.
    /// For Star or Barycenter observers, returns the Sun.
    /// </summary>
    internal static CelestialItem ResolveCentralBody(Spacecraft spacecraft)
    {
        var observer = spacecraft.InitialOrbitalParameters.Observer;

        if (observer is Star or Barycenter)
        {
            return Stars.SUN_BODY;
        }

        return observer as CelestialItem
               ?? throw new ArgumentException(
                   "Spacecraft initial orbital parameters observer must be a CelestialItem",
                   nameof(spacecraft));
    }

    private static IIntegrator CreateDefaultIntegrator(Window window, Spacecraft spacecraft,
        IEnumerable<CelestialItem> celestialItems, bool includeAtmosphericDrag,
        bool includeSolarRadiationPressure, TimeSpan deltaT)
    {
        var centralBody = ResolveCentralBody(spacecraft);

        var tdbStartDate = window.StartDate.ToTDB();
        var initialState = StartState(spacecraft.InitialOrbitalParameters, tdbStartDate)
            .RelativeTo(centralBody, Aberration.None).ToStateVector();
        var items = (celestialItems ?? Array.Empty<CelestialItem>()).Distinct().ToArray();
        var forces = BuildCentralBodyForces(centralBody, items, spacecraft, includeAtmosphericDrag,
            includeSolarRadiationPressure);
        var tdbWindow = new Window(window.StartDate.ToTDB(), window.EndDate.ToTDB());
        InjectEphemerisCache(tdbWindow, forces, items, centralBody, initialState.Frame);
        return new VVIntegrator(forces, deltaT, initialState);
    }

    protected static List<ForceBase> BuildCentralBodyForces(CelestialItem centralBody,
        CelestialItem[] items, Spacecraft spacecraft,
        bool includeAtmosphericDrag, bool includeSolarRadiationPressure)
    {
        List<ForceBase> forces = new List<ForceBase>();

        // Central body gravity (two-body + geopotential if configured)
        forces.Add(new GravitationalAcceleration(centralBody));

        // Third-body perturbations via Battin's formula for all other bodies
        foreach (var body in items)
        {
            if (!body.Equals(centralBody))
            {
                forces.Add(new ThirdBodyPerturbation(body, centralBody));
            }
        }

        if (includeAtmosphericDrag)
        {
            foreach (var celestialBody in items.OfType<CelestialBody>().Where(x => x.HasAtmosphericModel).ToArray())
            {
                forces.Add(new AtmosphericDrag(spacecraft, celestialBody));
            }
        }

        if (includeSolarRadiationPressure)
        {
            forces.Add(new SolarRadiationPressure(spacecraft, items.OfType<CelestialBody>()));
        }

        return forces;
    }

    // The orientation cache of the central body frame over the window, kept by the propagator: it is active only on the
    // thread that propagates (PropagatorBase.Propagate) or evaluates the solution, never set on the shared Frame
    private static PropagationFrameOrientationCache CreateFrameOrientationCache(Window tdbWindow,
        CelestialItem centralBody)
    {
        return centralBody is CelestialBody celestialBody
            ? new PropagationFrameOrientationCache(tdbWindow, celestialBody.Frame, TimeSpan.FromSeconds(60))
            : null;
    }

    /// <summary>
    /// Gives the forces an ephemeris cache of the geometric positions of the bodies relative to
    /// <paramref name="observer"/>, in <paramref name="frame"/>, the frame of the propagated state, so that the forces
    /// combine vectors of one frame. Every force reads geometric positions (<see cref="Aberration.None"/>): the
    /// radiation forces no longer apply the light time from the observer (#360).
    /// </summary>
    private static void InjectEphemerisCache(Window tdbWindow, List<ForceBase> forces,
        CelestialItem[] celestialBodies, ILocalizable observer, Frame frame)
    {
        if (celestialBodies.Length == 0) return;

        var entries = new List<(CelestialItem, Aberration)>();
        foreach (var body in celestialBodies)
        {
            entries.Add((body, Aberration.None));
        }

        var cache = new PropagationEphemerisCache(tdbWindow, entries, observer, frame, TimeSpan.FromSeconds(60));
        foreach (var force in forces)
        {
            force.EphemerisCache = cache;
        }
    }

    private protected override (Frame Frame, PropagationFrameOrientationCache Cache) OrientationCache =>
        _centralBody is CelestialBody celestialBody ? (celestialBody.Frame, _orientationCache) : (null, null);

    protected override void StorePropagatedStates(StateVector[] outputStates)
    {
        // Store CB-relative states directly; conversion to SSB happens on demand
        Spacecraft.AddPropagatedStates(outputStates);
    }
}
