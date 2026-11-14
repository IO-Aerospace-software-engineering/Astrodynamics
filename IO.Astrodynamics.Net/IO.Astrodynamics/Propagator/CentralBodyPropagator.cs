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
    /// <remarks>
    /// The central body takes part whether or not it is listed, with the models of the observer: inserted first when no
    /// listed body has its NAIF identifier, so that a run that lists it is unchanged. Listed bodies of one NAIF identifier,
    /// the observer's included, must have the same models (GM, radii, gravitational field, atmospheric model). Drag about a
    /// central body is that of its atmosphere: its model, or NRLMSISE-00 at nominal space weather for an Earth without one;
    /// each other listed body with an atmosphere adds its drag, an Earth always having one, and about the Sun (a star or
    /// barycentre observer) the drag is that of those bodies. The integrator must be a new instance: the propagator adds
    /// the forces to it (#674).
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaT"/> is not positive.</exception>
    /// <exception cref="ArgumentException">The integrator already holds forces or was already used by a propagation that
    /// builds its forces; or two listed bodies of one NAIF identifier, or a listed body and the observer, have different
    /// models.</exception>
    /// <exception cref="InvalidOperationException">Drag is asked about a central body other than the Earth and the Sun
    /// without an atmospheric model, or about the Sun with no listed Earth and no listed body with a model.</exception>
    public CentralBodyPropagator(in Window window, Spacecraft spacecraft, Integrator integrator,
        IEnumerable<CelestialItem> celestialBodies, bool includeAtmosphericDrag,
        bool includeSolarRadiationPressure, TimeSpan deltaT)
        : this(window, spacecraft, integrator, deltaT,
            PlanForces(spacecraft, celestialBodies, includeAtmosphericDrag, includeSolarRadiationPressure, deltaT,
                integrator))
    {
    }

    // The force-building constructors once their inputs are checked and the integrator claimed (PlanForces)
    private CentralBodyPropagator(in Window window, Spacecraft spacecraft, Integrator integrator, TimeSpan deltaT,
        ForceModelPlan plan)
        : this(window, spacecraft, integrator, deltaT,
            PropagationTimeArgument.For(spacecraft?.InitialOrbitalParameters?.Observer))
    {
        InjectEphemerisCache(Window, plan.Forces, plan.Bodies, _centralBody, InitialState.Frame);
        _orientationCache = CreateFrameOrientationCache(Window, _centralBody);
        foreach (var force in plan.Forces)
        {
            integrator.AddForce(force);
        }

        integrator.Initialize(InitialState);
    }

    /// <summary>The bodies and forces of a force-building constructor, checked before anything changes.</summary>
    private sealed record ForceModelPlan(CelestialItem[] Bodies, List<ForceBase> Forces);

    /// <summary>
    /// Checks the step, the bodies and the drag request, builds the forces, then claims <paramref name="integrator"/>
    /// (null for the default integrator, which is new): a rejected input leaves the integrator and the spacecraft as they
    /// were (#674).
    /// </summary>
    private static ForceModelPlan PlanForces(Spacecraft spacecraft, IEnumerable<CelestialItem> celestialBodies,
        bool includeAtmosphericDrag, bool includeSolarRadiationPressure, TimeSpan deltaT, Integrator integrator)
    {
        RequirePositiveStep(deltaT, nameof(deltaT));
        ArgumentNullException.ThrowIfNull(spacecraft);
        var centralBody = ResolveCentralBody(spacecraft);
        var bodies = ForceModelBodies(centralBody, celestialBodies);
        var forces = BuildCentralBodyForces(centralBody, bodies, spacecraft, includeAtmosphericDrag,
            includeSolarRadiationPressure);
        integrator?.ClaimForForces();
        return new ForceModelPlan(bodies, forces);
    }

    /// <summary>
    /// The bodies of the force model: the listed ones, each NAIF identifier once, the central body among them with the
    /// models of <paramref name="centralBody"/>, inserted first when none is listed (#674).
    /// </summary>
    /// <exception cref="ArgumentException">Two listed bodies of one NAIF identifier, or a listed body and the central
    /// body, have different models.</exception>
    private static CelestialItem[] ForceModelBodies(CelestialItem centralBody, IEnumerable<CelestialItem> celestialBodies)
    {
        var listed = (celestialBodies ?? Array.Empty<CelestialItem>()).ToArray();
        CelestialItem.RequireOneModelPerBody(centralBody, listed, nameof(celestialBodies));
        var bodies = new List<CelestialItem>(listed.Length + 1);
        if (!listed.Any(body => body?.NaifId == centralBody.NaifId))
        {
            bodies.Add(centralBody);
        }

        foreach (var body in listed.Distinct())
        {
            bodies.Add(body != null && body.NaifId == centralBody.NaifId ? centralBody : body);
        }

        return bodies.ToArray();
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
        : this(window, spacecraft, integrator, deltaT,
            PropagationTimeArgument.For(spacecraft?.InitialOrbitalParameters?.Observer))
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
    /// <remarks>
    /// The time argument follows the central body (#501): about the Earth the propagation integrates over the TT seconds
    /// between the instants of the window, the forces at t being those of TT₀ + t; about any other central body, the Sun
    /// of a star or barycentre observer included, over TDB seconds. The epochs of the segments, the steps, the events and
    /// the output states are TDB labels in both cases, and the velocities are per second of the time argument.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaT"/> is not positive.</exception>
    public CentralBodyPropagator(in Window window, Spacecraft spacecraft, IIntegrator integrator, TimeSpan deltaT)
        : this(window, spacecraft, integrator, RequirePositiveStep(deltaT, nameof(deltaT)),
            PropagationTimeArgument.For(spacecraft?.InitialOrbitalParameters?.Observer))
    {
    }

    // Every constructor ends here; the internal one of the avoidance options and of the non-linearity index directly,
    // with the length of its window as its step
    private CentralBodyPropagator(in Window window, Spacecraft spacecraft, IIntegrator integrator, TimeSpan deltaT,
        PropagationTimeArgument timeArgument)
        : base(window, spacecraft, integrator, deltaT, timeArgument)
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
    /// The two epochs are compared as TDB labels: a UTC epoch compared with its own TDB label through TAI can be a tick
    /// off it (#510).
    /// </remarks>
    private static StateVector StartState(OrbitalParameters.OrbitalParameters orbitalParameters, in Time start)
    {
        if (orbitalParameters is StateVector state && state.Epoch.ToTDB() == start.ToTDB())
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
    /// <remarks>The forces are built as by the constructor with an integrator.</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaT"/> is not positive.</exception>
    /// <exception cref="ArgumentException">Two listed bodies of one NAIF identifier, or a listed body and the observer,
    /// have different models.</exception>
    /// <exception cref="InvalidOperationException">Drag is asked about a central body other than the Earth and the Sun
    /// without an atmospheric model, or about the Sun with no listed Earth and no listed body with a model.</exception>
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
        var plan = PlanForces(spacecraft, celestialItems, includeAtmosphericDrag, includeSolarRadiationPressure, deltaT,
            null);
        var centralBody = ResolveCentralBody(spacecraft);

        var tdbStartDate = window.StartDate.ToTDB();
        var initialState = StartState(spacecraft.InitialOrbitalParameters, tdbStartDate)
            .RelativeTo(centralBody, Aberration.None).ToStateVector();
        var tdbWindow = new Window(window.StartDate.ToTDB(), window.EndDate.ToTDB());
        InjectEphemerisCache(tdbWindow, plan.Forces, plan.Bodies, centralBody, initialState.Frame);
        return new VVIntegrator(plan.Forces, deltaT, initialState);
    }

    /// <summary>
    /// The forces of a propagation about <paramref name="centralBody"/>: its gravity, the third-body perturbations of the
    /// other bodies of <paramref name="items"/>, drag and solar radiation pressure, the bodies of
    /// <paramref name="items"/> occulting the Sun.
    /// </summary>
    /// <remarks>
    /// Drag about a central body other than the Sun is that of its atmosphere: its atmospheric model, or NRLMSISE-00 at
    /// nominal space weather for an Earth without one; about the Sun (a star or barycentre observer, SSB mode), that of the
    /// listed bodies with an atmosphere. The other bodies with an atmosphere add their drag in both cases, an Earth without
    /// a model with NRLMSISE-00 at nominal space weather: an Earth always has an atmosphere (#674, amendment of
    /// 2026-11-14).
    /// </remarks>
    /// <exception cref="InvalidOperationException">Drag is asked about a central body other than the Sun without an
    /// atmosphere, or about the Sun with no listed body that has one (no Earth, and no body with a model).</exception>
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
            AddDrag(forces, centralBody, items, spacecraft);
        }

        if (includeSolarRadiationPressure)
        {
            forces.Add(new SolarRadiationPressure(spacecraft, items.OfType<CelestialBody>()));
        }

        return forces;
    }

    // The drag forces, in the order of the bodies, the central body's where it is listed, first otherwise; each body with
    // an atmosphere, an Earth without a model taking NRLMSISE-00 at nominal space weather (amendment of 2026-11-14)
    private static void AddDrag(List<ForceBase> forces, CelestialItem centralBody, CelestialItem[] items,
        Spacecraft spacecraft)
    {
        bool aboutTheSun = centralBody.NaifId == Stars.Sun.NaifId;
        CelestialBody centralAtmosphere = null;
        if (!aboutTheSun)
        {
            centralAtmosphere = centralBody is CelestialBody { Atmosphere: not null } body
                ? body
                : throw new InvalidOperationException(
                    $"Atmospheric drag is asked about the central body {centralBody.Name} (NAIF {centralBody.NaifId}), " +
                    "which has no atmospheric model: give its CelestialBody an IAtmosphericModel (an Earth without one " +
                    "takes NRLMSISE-00 at nominal space weather), or do not include atmospheric drag.");
        }

        int first = forces.Count;
        bool centralAdded = false;
        foreach (var body in items.OfType<CelestialBody>())
        {
            if (body.NaifId == centralBody.NaifId)
            {
                if (centralAtmosphere != null && !centralAdded)
                {
                    forces.Add(new AtmosphericDrag(spacecraft, centralAtmosphere));
                    centralAdded = true;
                }
            }
            else if (body.Atmosphere != null)
            {
                forces.Add(new AtmosphericDrag(spacecraft, body));
            }
        }

        if (centralAtmosphere != null && !centralAdded)
        {
            forces.Insert(first, new AtmosphericDrag(spacecraft, centralAtmosphere));
        }

        if (forces.Count == first)
        {
            throw new InvalidOperationException(
                "Atmospheric drag is asked about the Sun (the central body of a star or barycentre observer), and no " +
                "listed body has an atmosphere: list an Earth (without a model it takes NRLMSISE-00 at nominal space " +
                "weather) or a CelestialBody built with an IAtmosphericModel, or do not include atmospheric drag.");
        }
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
        // Store CB-relative states in ICRF (#535), the frame every reader of the spacecraft assumes; the solution keeps
        // the frame of the propagation. Conversion to SSB happens on demand. The states replace those stored before, split
        // into arcs at each impulse with the states on either side at its epoch, the short arcs filled from the solution
        // with the covariance in covariance mode (#540)
        // An impulse that no segment of the solution carries (located at the end of the propagation, §9 item 7 of #554)
        // opens no arc: the solution ends with the state before it, and so do the stored states
        var solution = CurrentSolution;
        var impulses = Impulses.Select(i => new ImpulseBoundary(i.Epoch, ToIcrf(i.Before),
            solution.Segments.Any(s => s.BaseEpoch == i.Epoch) ? ToIcrf(i.After) : null)).ToArray();
        Spacecraft.SetPropagatedStates(outputStates.Select(ToIcrf).ToArray(), impulses,
            impulses.Length == 0 ? null : epoch => ToIcrf(solution.GetStateVector(epoch)));
    }
}
