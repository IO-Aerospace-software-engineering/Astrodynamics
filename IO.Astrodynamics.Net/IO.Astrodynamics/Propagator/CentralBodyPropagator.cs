// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Atmosphere;
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
/// When the observer is a Star or Barycenter, the Sun is used as the central body; an initial state about a star is
/// converted to the Sun with the star's space velocity (before 11.0.0 a zero velocity, #731).
/// </summary>
public class CentralBodyPropagator : PropagatorBase
{
    private readonly CelestialItem _centralBody;

    // Orientation cache of the central body frame, active on the propagating thread only (Frame.UseOrientationCache)
    private readonly PropagationFrameOrientationCache _orientationCache;

    /// <summary>
    /// A central-body propagator of the settings of <paramref name="options"/> (R06 of #668): the integrator, the output
    /// step, the third bodies, the forces, the geopotential and atmosphere of the central body, and the propagation of
    /// the covariance, each run building its own integrator, forces and geopotential field.
    /// </summary>
    /// <remarks>
    /// The central body is that of the spacecraft's initial orbit (the Sun for a star or barycentre observer), and takes
    /// part with the models of the observer (#674); <see cref="PropagationOptions.ThirdBodies"/> null means none. The
    /// settings are checked here, before the integrator is built and before anything changes; the resolved ones are on
    /// <see cref="PropagationSolution.Options"/>. With the integrator, forces and covariance options the settings name,
    /// the run is bit for bit that of the constructor taking them (<c>CentralBodyPropagatorOptionsTests</c>).
    /// </remarks>
    /// <param name="window">The propagation window.</param>
    /// <param name="spacecraft">The spacecraft.</param>
    /// <param name="options">The settings; null for <see cref="PropagationOptions.Default"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="spacecraft"/> is null.</exception>
    /// <exception cref="ArgumentException">The settings contradict each other (<see cref="RK78IntegratorOptions.FixedStep"/>
    /// with another setting), or the inputs: a geopotential or an atmosphere other than the central body's model, either
    /// on a central body that cannot take it, albedo or thermal radiation pressure on a central body without the
    /// coefficient, two bodies of one NAIF identifier with different models; or the GM of the central body is not above
    /// 0.</exception>
    /// <exception cref="InvalidOperationException">Drag is asked about a central body without an atmosphere; or the
    /// covariance is propagated (Auto or asked for) from a covariance that is not positive semi-definite, or asked for
    /// without a valid covariance at the start of the window.</exception>
    /// <exception cref="NotSupportedException">The covariance propagation is asked for under Velocity-Verlet.</exception>
    public CentralBodyPropagator(in Window window, Spacecraft spacecraft, PropagationOptions options = null)
        : this(window, spacecraft, PlanOptions(window, spacecraft, options, null))
    {
    }

    /// <summary>
    /// The propagator of a scenario's spacecraft (<see cref="Mission.Scenario.Simulate(PropagationOptions, System.Threading.CancellationToken)"/>): the
    /// scenario's bodies when <see cref="PropagationOptions.ThirdBodies"/> is null.
    /// </summary>
    internal CentralBodyPropagator(in Window window, Spacecraft spacecraft, PropagationOptions options,
        IEnumerable<CelestialItem> scenarioBodies)
        : this(window, spacecraft, PlanOptions(window, spacecraft, options, scenarioBodies))
    {
    }

    // The options constructors once the settings are resolved and the integrator claimed (PlanOptions)
    private CentralBodyPropagator(in Window window, Spacecraft spacecraft, OptionsPlan plan)
        : this(window, spacecraft, plan.Integrator, plan.OutputStep, plan.Forces)
    {
        EnableCovariancePropagation(plan.CovariancePropagation);
        ResolvedOptions = plan.Resolved;
        CovarianceAbsence = plan.CovarianceAbsence;
    }

    /// <summary>
    /// The resolved settings of an options constructor: the integrator, built and claimed, its forces, the covariance
    /// options to enable (null for none), why the covariance is not propagated under Auto, and the settings reported on
    /// the solution.
    /// </summary>
    private sealed record OptionsPlan(Integrator Integrator, TimeSpan OutputStep, ForceModelPlan Forces,
        CovariancePropagationOptions CovariancePropagation, string CovarianceAbsence, PropagationOptions Resolved);

    /// <summary>
    /// Resolves <paramref name="options"/> for <paramref name="spacecraft"/>: checks the settings and the inputs, then
    /// builds the integrator and the forces, the central body's geopotential and atmosphere by D1's fallback rule, and
    /// the covariance by the Auto rule; nothing changes when a check fails.
    /// </summary>
    private static OptionsPlan PlanOptions(in Window window, Spacecraft spacecraft, PropagationOptions options,
        IEnumerable<CelestialItem> scenarioBodies)
    {
        RequireWindowLength(window);
        options ??= PropagationOptions.Default;
        options.Integrator.Validate();
        ArgumentNullException.ThrowIfNull(spacecraft);
        ValidateInertialFrame(spacecraft);
        ValidateOsculatingInitialOrbit(spacecraft);
        var centralBody = ResolveCentralBody(spacecraft);

        var (runGeopotential, geopotential) = ResolveGeopotential(centralBody, options.Geopotential);
        var (runAtmosphere, atmosphere) = ResolveAtmosphere(centralBody, options.Atmosphere);
        var bodies = ForceModelBodies(centralBody, options.ThirdBodies ?? scenarioBodies);
        var forces = BuildCentralBodyForces(centralBody, bodies, spacecraft, options.AtmosphericDrag,
            options.SolarRadiationPressure, centralBody.CreatePropagationField(runGeopotential), runAtmosphere);
        AddRadiationForces(forces, centralBody, spacecraft, options);

        var integrator = options.Integrator.CreateIntegrator();
        var tdbStart = window.StartDate.ToTdb();
        var (covariance, absence) = ResolveCovariance(spacecraft, tdbStart, integrator, options.CovariancePropagation);
        integrator.ClaimForForces();

        var resolved = options with
        {
            ThirdBodies = bodies.Where(body => body.NaifId != centralBody.NaifId).ToArray(),
            Geopotential = geopotential,
            Atmosphere = atmosphere,
            CovariancePropagation = covariance ?? CovariancePropagationOptions.None
        };
        return new OptionsPlan(integrator, options.OutputStep, new ForceModelPlan(bodies, forces), covariance, absence,
            resolved);
    }

    /// <summary>
    /// The geopotential of the central body by D1's fallback rule (R06, R12 of #668): the model the run builds its field
    /// from (null for the body's own), and the model reported (null for a point mass).
    /// </summary>
    /// <exception cref="ArgumentException">The option differs from the body's model, or the central body cannot take a
    /// geopotential.</exception>
    private static (GeopotentialModelParameters Run, GeopotentialModelParameters Reported) ResolveGeopotential(
        CelestialItem centralBody, GeopotentialModelParameters option)
    {
        var own = centralBody.GeopotentialModel;
        if (option == null)
        {
            return (null, own);
        }

        if (centralBody is not CelestialBody)
        {
            throw new ArgumentException(
                $"options.Geopotential is given, and the central body {centralBody.Name} (NAIF {centralBody.NaifId}) is " +
                "not a CelestialBody, which a geopotential needs (its orientation).", "options");
        }

        if (own == null)
        {
            // A body without a model counts as not given: the option fills it (R12 of #668)
            return (option, option);
        }

        if (own.Equals(option) || centralBody.Field.HasSameModel(
                CelestialItem.CreateGravitationalField(centralBody.NaifId, centralBody.GM, option)))
        {
            return (null, own);
        }

        throw new ArgumentException(
            $"The central body {centralBody.Name} (NAIF {centralBody.NaifId}) has the geopotential {own.Description}, and " +
            $"options.Geopotential is {option.Description}: an option of the same quantity is a fallback for a body " +
            "without a model. Remove options.Geopotential, or build the body without a geopotential.", "options");
    }

    /// <summary>
    /// The atmosphere of the central body's drag by D1's fallback rule (R06 of #668): the atmosphere the drag takes in
    /// place of the body's (null for the body's), and the atmosphere reported (the body's, which an Earth without a model
    /// has, or null).
    /// </summary>
    /// <exception cref="ArgumentException">The option differs from the body's model, or the central body cannot take an
    /// atmosphere.</exception>
    private static (IAtmosphericModel Run, IAtmosphericModel Reported) ResolveAtmosphere(CelestialItem centralBody,
        IAtmosphericModel option)
    {
        var body = centralBody as CelestialBody;
        if (option == null)
        {
            return (null, body?.Atmosphere);
        }

        if (body == null || centralBody.NaifId == Stars.Sun.NaifId)
        {
            throw new ArgumentException(
                $"options.Atmosphere is given, and the central body {centralBody.Name} (NAIF {centralBody.NaifId}) cannot " +
                "take one: about the Sun the drag is that of the third bodies with an atmosphere.", "options");
        }

        if (!body.HasAtmosphericModel)
        {
            // A body without a model counts as not given: the option replaces it, an Earth's NRLMSISE-00 at nominal
            // space weather included
            return (option, option);
        }

        if (AtmosphericModelComparer.AreEquivalent(body.Atmosphere, option))
        {
            return (null, body.Atmosphere);
        }

        throw new ArgumentException(
            $"The central body {body.Name} (NAIF {body.NaifId}) has the atmosphere {body.Atmosphere.GetType().Name}, and " +
            $"options.Atmosphere is another {option.GetType().Name}: an option of the same quantity is a fallback for a " +
            "body without a model. Remove options.Atmosphere, or build the body without an atmospheric model.", "options");
    }

    // The albedo and thermal radiation pressure of the central body, after the other forces, as the builder of 10.x added them
    private static void AddRadiationForces(List<ForceBase> forces, CelestialItem centralBody, Spacecraft spacecraft,
        PropagationOptions options)
    {
        if (!options.AlbedoRadiationPressure && !options.ThermalRadiationPressure)
        {
            return;
        }

        var body = centralBody as CelestialBody ?? throw new ArgumentException(
            $"Albedo or thermal radiation pressure is asked, and the central body {centralBody.Name} (NAIF " +
            $"{centralBody.NaifId}) is not a CelestialBody, which has the coefficients.", "options");
        if (options.AlbedoRadiationPressure)
        {
            forces.Add(new AlbedoRadiationPressure(spacecraft, body));
        }

        if (options.ThermalRadiationPressure)
        {
            forces.Add(new ThermalRadiationPressure(spacecraft, body));
        }
    }

    /// <summary>
    /// The covariance options to enable (null for none) and, when none, why: the options given, or under Auto (null) the
    /// covariance of the initial orbital parameters when the integrator is RK7(8) and the covariance is at the start of
    /// the window (R06 of #668, D4.3).
    /// </summary>
    /// <exception cref="InvalidOperationException">Under Auto, the covariance at the start of the window is not 6×6,
    /// symmetric and positive semi-definite.</exception>
    private static (CovariancePropagationOptions Options, string Absence) ResolveCovariance(Spacecraft spacecraft,
        in Time tdbStart, Integrator integrator, CovariancePropagationOptions requested)
    {
        if (requested != null)
        {
            return requested.IsActive
                ? (requested, null)
                : (null, "PropagationOptions.CovariancePropagation is CovariancePropagationOptions.None.");
        }

        var initial = InertialInitialOrbit(spacecraft.InitialOrbitalParameters, tdbStart);
        var covariance = initial.ToStateVector().Covariance;
        if (!covariance.HasValue)
        {
            return (null, "the initial orbital parameters of the spacecraft carry no covariance.");
        }

        if (integrator is not RK78Integrator)
        {
            return (null, "under Auto the covariance is propagated by the RK7(8) integrator only, and the propagation " +
                          "used Velocity-Verlet.");
        }

        if (initial.Epoch.ToTdb() != tdbStart)
        {
            return (null, $"the covariance of the initial orbital parameters is given at {initial.Epoch.ToTdb()}, and the " +
                          $"propagation starts at {tdbStart}: the propagator does not carry a covariance from another " +
                          "epoch.");
        }

        if (InitialCovarianceRejection(covariance.Value, out _) is { } reason)
        {
            throw new InvalidOperationException(
                $"The covariance of spacecraft '{spacecraft.Name}' is propagated under Auto " +
                $"(PropagationOptions.CovariancePropagation null), and it cannot be: {reason} Remediate it with " +
                "StateVector.WithRemediatedCovariance(), or set CovariancePropagation to " +
                "CovariancePropagationOptions.None.");
        }

        return (new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance }, null);
    }

    // The options constructors once their forces are planned and the integrator claimed (PlanOptions)
    private CentralBodyPropagator(in Window window, Spacecraft spacecraft, Integrator integrator, TimeSpan deltaT,
        ForceModelPlan plan)
        : this(window, spacecraft, integrator, deltaT,
            PropagationTimeArgument.For(spacecraft?.InitialOrbitalParameters?.Observer))
    {
        // The caches cover the span the propagation integrates, the window since choice 6 of #668 (#626)
        var cacheWindow = Span.CacheWindow(Window);
        InjectEphemerisCache(cacheWindow, plan.Forces, plan.Bodies, _centralBody, InitialState.Frame);
        _orientationCache = CreateFrameOrientationCache(cacheWindow, _centralBody);
        foreach (var force in plan.Forces)
        {
            integrator.AddForce(force);
        }

        integrator.Initialize(InitialState);
    }

    /// <summary>The bodies and forces of a force-building constructor, checked before anything changes.</summary>
    private sealed record ForceModelPlan(CelestialItem[] Bodies, List<ForceBase> Forces);

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
    /// A central-body propagator with an integrator that already has its forces, the forces of another propagation, and
    /// the orientation cache of that propagation, which must cover <paramref name="window"/>: the propagation of an
    /// avoidance option from the state at its burn, with the force model of the trajectory it leaves (step 10, E4;
    /// #402, in RK7(8) or by Velocity-Verlet).
    /// The solution evaluates those forces under <paramref name="syncRoot"/>, the lock of that propagation. The integrator
    /// is claimed as by the public constructors, and stays claimed when a later check rejects the constructor (decision 27
    /// of lot L4 of #668).
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
    /// No forces are added; the integrator is initialized with the initial state of the propagation.
    /// The central body is inferred from the spacecraft's orbital parameters observer.
    /// When the observer is a Star or Barycenter, the Sun is used as the central body. With <paramref name="options"/>, it
    /// also propagates the state transition matrix and, in <see cref="CovariancePropagationMode.Covariance"/>, the
    /// covariance of the initial orbital parameters; the columns of Ψ follow the forces of the integrator at construction
    /// (one constructor since 11.0.0, D1 of #668, lot L9: the overload with a <c>covariancePropagation</c> parameter
    /// before).
    /// </summary>
    /// <param name="window">The propagation window.</param>
    /// <param name="spacecraft">The spacecraft.</param>
    /// <param name="integrator">The integrator and its forces, given to no other propagator; the state transition matrix
    /// needs an <see cref="RK78Integrator"/>.</param>
    /// <param name="deltaT">The step of the output states.</param>
    /// <param name="options">The propagation of the state transition matrix and of the covariance; null for none.</param>
    /// <remarks>
    /// The time argument follows the central body (#501): about the Earth the propagation integrates over the TT seconds
    /// between the instants of the window, the forces at t being those of TT₀ + t; about any other central body, the Sun
    /// of a star or barycentre observer included, over TDB seconds. The epochs of the segments, the steps, the events and
    /// the output states are TDB labels in both cases, and the velocities are per second of the time argument.
    /// <para>
    /// The propagator initializes the integrator with its initial state (<see cref="IIntegrator.Initialize"/>): about the
    /// central body, the Sun in SSB mode, in the inertial frame of the propagation (ICRF for a TLE or a TEME state), at
    /// the TDB start of the window; at construction, at the start of every run and after each event, so an
    /// <see cref="IIntegrator.Initialize"/> by the caller between construction and <see cref="PropagatorBase.Propagate"/>
    /// has no effect on the run (#782, decision 23 of lot L4 of #668, decided by S. Guillet, 2026-11-22: before, a later
    /// one by the caller replaced it for the first segment, and with another observer or frame changed the run, under
    /// both integrators). Before 11.0.0 the integrator was used as given, and one its caller had not initialized threw
    /// <see cref="NullReferenceException"/> at the first step.
    /// </para>
    /// <para>
    /// The integrator belongs to this propagator (#782, decision 24 of lot L4 of #668): give each propagator a new one. It
    /// is claimed once the checks of the window, the spacecraft and its maneuvers pass, before anything changes it, and the
    /// claim is never released: a constructor rejected by a later check (the GM of the central body) leaves it claimed, and
    /// the caller builds a new one (decision 27 of lot L4 of #668, decided by S. Guillet, 2026-11-22).
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaT"/> is not positive.</exception>
    /// <exception cref="ArgumentException">The integrator was already given to a propagator, or to a propagator
    /// constructor that threw after claiming it, naming <c>integrator</c> (#782; before 11.0.0 the last propagator built
    /// set its observer, frame and time argument for all).</exception>
    /// <exception cref="ArgumentException"><paramref name="spacecraft"/> orbits a central body whose GM is not above 0: a body
    /// of the kernels whose <c>BODYnnn_GM</c> the kernel pool lacks (no GM kernel such as <c>gm_de440.tpc</c>), refused before
    /// anything runs (R03 of #668: before 11.0.0 the propagation ran without central gravity).</exception>
    /// <exception cref="NotSupportedException"><paramref name="options"/> asks for the state transition matrix and the
    /// integrator is not an <see cref="RK78Integrator"/>.</exception>
    /// <exception cref="ArgumentException">Process noise or consider parameters without the covariance, or a consider
    /// parameter without its force.</exception>
    /// <exception cref="InvalidOperationException">The covariance is asked for, and the initial orbital parameters carry
    /// none, carry it at another epoch than the start of the window, or carry one that is not symmetric and positive
    /// semi-definite.</exception>
    public CentralBodyPropagator(in Window window, Spacecraft spacecraft, IIntegrator integrator, TimeSpan deltaT,
        CovariancePropagationOptions options = null)
        : this(window, spacecraft, integrator, RequirePositiveStep(deltaT, nameof(deltaT)),
            PropagationTimeArgument.For(spacecraft?.InitialOrbitalParameters?.Observer))
    {
        integrator.Initialize(InitialState);
        EnableCovariancePropagation(options);
    }

    // Every constructor ends here; the internal one of the avoidance options and of the non-linearity index directly,
    // with the length of its window as its step
    private CentralBodyPropagator(in Window window, Spacecraft spacecraft, IIntegrator integrator, TimeSpan deltaT,
        PropagationTimeArgument timeArgument)
        : base(window, spacecraft, integrator, deltaT, timeArgument)
    {
        _centralBody = ResolveCentralBody(spacecraft);

        InitialState = StartState(InertialInitialOrbit(Spacecraft.InitialOrbitalParameters, Window.StartDate), Window.StartDate)
            .RelativeTo(_centralBody, Aberration.None).ToStateVector();
    }

    /// <summary>
    /// The state of <paramref name="orbitalParameters"/>, the initial orbit in the frame of the propagation
    /// (<see cref="PropagatorBase.InertialInitialOrbit"/>: a TLE or a TEME state rotated to ICRF, #675), at
    /// <paramref name="start"/>: a state vector given at that instant
    /// as it is, any other orbital parameters through <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.AtEpoch(Time)"/>.
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
        if (orbitalParameters is StateVector state && state.Epoch.ToTdb() == start.ToTdb())
        {
            return new StateVector(state.Position, state.Velocity, state.Observer, start, state.Frame);
        }

        return orbitalParameters.AtEpoch(start).ToStateVector();
    }

    /// <summary>
    /// Resolves the central body from the spacecraft's observer.
    /// For Star or Barycenter observers, returns the Sun.
    /// </summary>
    /// <exception cref="ArgumentException">The observer is not a <see cref="CelestialItem"/>, or the central body has no GM
    /// (R03 of #668).</exception>
    internal static CelestialItem ResolveCentralBody(Spacecraft spacecraft)
    {
        var observer = spacecraft.InitialOrbitalParameters.Observer;

        if (observer is Star or Barycenter)
        {
            return RequireGravitationalParameter(Stars.SUN_BODY, nameof(spacecraft));
        }

        return RequireGravitationalParameter(observer as CelestialItem
               ?? throw new ArgumentException(
                   "Spacecraft initial orbital parameters observer must be a CelestialItem",
                   nameof(spacecraft)), nameof(spacecraft));
    }

    /// <summary>
    /// <paramref name="centralBody"/>, when its GM is finite and above 0: the central body of a propagation (R03 of #668,
    /// structuring choice 10, decided by S. Guillet, 2026-11-19). A body read from the kernels has a GM of 0 when the kernel
    /// pool holds no <c>BODYnnn_GM</c>, which its constructor accepts; before 11.0.0 a propagation about it ran without
    /// central gravity, silently.
    /// </summary>
    /// <exception cref="ArgumentException">The GM is 0, negative or not finite, naming <paramref name="paramName"/>.</exception>
    internal static CelestialItem RequireGravitationalParameter(CelestialItem centralBody, string paramName)
    {
        if (double.IsFinite(centralBody.GM) && centralBody.GM > 0.0)
        {
            return centralBody;
        }

        throw new ArgumentException(
            $"The central body {centralBody.Name} ({centralBody.NaifId}) has a GM of {centralBody.GM} m^3/s^2, where a propagation " +
            $"needs one above 0: for a body read from the kernels, the kernel pool holds no BODY{centralBody.NaifId}_GM. Load a GM " +
            "kernel, such as NAIF's gm_de440.tpc, before building the body.", paramName);
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
    private protected static List<ForceBase> BuildCentralBodyForces(CelestialItem centralBody,
        CelestialItem[] items, Spacecraft spacecraft,
        bool includeAtmosphericDrag, bool includeSolarRadiationPressure)
    {
        return BuildCentralBodyForces(centralBody, items, spacecraft, includeAtmosphericDrag,
            includeSolarRadiationPressure, centralBody.CreatePropagationField(), null);
    }

    /// <summary>
    /// The forces of <see cref="BuildCentralBodyForces(CelestialItem, CelestialItem[], Spacecraft, bool, bool)"/>, the
    /// central gravity evaluated with <paramref name="centralField"/>, the field of this run (R06 of #668: one field per
    /// run over the shared coefficients), and the drag of the central body with <paramref name="centralAtmosphere"/> in
    /// place of its own when given.
    /// </summary>
    private static List<ForceBase> BuildCentralBodyForces(CelestialItem centralBody,
        CelestialItem[] items, Spacecraft spacecraft,
        bool includeAtmosphericDrag, bool includeSolarRadiationPressure, GravitationalField centralField,
        IAtmosphericModel centralAtmosphere)
    {
        List<ForceBase> forces = new List<ForceBase>();

        // Central body gravity (two-body + geopotential if configured), with the field of this run
        forces.Add(new GravitationalAcceleration(centralBody, centralField));

        // Third-body perturbations for all other bodies: Battin's form far from the perturbing body, the direct
        // difference where |r - d| < |d|/2 (ThirdBodyPerturbation, #576)
        foreach (var body in items)
        {
            if (!body.Equals(centralBody))
            {
                forces.Add(new ThirdBodyPerturbation(body, centralBody));
            }
        }

        if (includeAtmosphericDrag)
        {
            AddDrag(forces, centralBody, items, spacecraft, centralAtmosphere);
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
        Spacecraft spacecraft, IAtmosphericModel atmosphereOverride)
    {
        bool aboutTheSun = centralBody.NaifId == Stars.Sun.NaifId;
        CelestialBody centralAtmosphere = null;
        if (!aboutTheSun)
        {
            centralAtmosphere = centralBody is CelestialBody body && (body.Atmosphere != null || atmosphereOverride != null)
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
                    forces.Add(new AtmosphericDrag(spacecraft, centralAtmosphere, atmosphereOverride));
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
            forces.Insert(first, new AtmosphericDrag(spacecraft, centralAtmosphere, atmosphereOverride));
        }

        if (forces.Count == first)
        {
            throw new InvalidOperationException(
                "Atmospheric drag is asked about the Sun (the central body of a star or barycentre observer), and no " +
                "listed body has an atmosphere: list an Earth (without a model it takes NRLMSISE-00 at nominal space " +
                "weather) or a CelestialBody built with an IAtmosphericModel, or do not include atmospheric drag.");
        }
    }

    // The orientation cache of the central body frame over the span of the propagation (#626), kept by the propagator: it
    // is active only on the thread that propagates (PropagatorBase.Propagate) or evaluates the solution, never set on the
    // shared Frame
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
        // An impulse at the end of the propagation is carried by a last segment of zero length (#575), which opens a last
        // arc of one state, the state after it; an impulse that no segment carries opens no arc
        var solution = CurrentSolution;
        var impulses = Impulses.Select(i => new ImpulseBoundary(i.Epoch, ToIcrf(i.Before),
            solution.Segments.Any(s => s.BaseEpoch == i.Epoch) ? ToIcrf(i.After) : null)).ToArray();
        Spacecraft.SetPropagatedStates(outputStates.Select(ToIcrf).ToArray(), impulses,
            impulses.Length == 0 ? null : epoch => ToIcrf(solution.GetStateVector(epoch)));
    }
}
