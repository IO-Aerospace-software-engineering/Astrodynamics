using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Mission
{
    public class Scenario : IEquatable<Scenario>
    {
        public string Name { get; }
        public Window Window { get; }
        public Mission Mission { get; }
        private readonly HashSet<Body.CelestialItem> _celestialBodies = new();
        public IReadOnlyCollection<Body.CelestialItem> CelestialBodies => _celestialBodies;

        private readonly HashSet<Body.Spacecraft.Spacecraft> _spacecrafts = new();
        public IReadOnlyCollection<Body.Spacecraft.Spacecraft> Spacecrafts => _spacecrafts;
        private readonly HashSet<Site> _sites = new();
        public IReadOnlyCollection<Site> Sites => _sites;

        private readonly HashSet<Star> _stars = new();
        public IReadOnlyCollection<Star> Stars => _stars;
        
        public bool IsSimulated { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">Scenario name</param>
        /// <param name="mission">Mission</param>
        /// <param name="window">Time window for propagation</param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public Scenario(string name, Mission mission, in Window window)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException($"'{nameof(name)}' cannot be null or empty.", nameof(name));
            }

            Name = name;
            Mission = mission ?? throw new ArgumentNullException(nameof(mission));
            Window = window;
        }

        /// <summary>
        /// Add celestialItem involved in the simulation
        /// </summary>
        /// <param name="celestialItem"></param>
        /// <remarks>
        /// The scenario holds one body per NAIF identifier: a body of an identifier already added is ignored when its
        /// models (GM, radii, gravitational field, atmospheric model) are those of the body added first, and rejected
        /// otherwise (#674).
        /// </remarks>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException">A body of the same NAIF identifier with other models was added
        /// before.</exception>
        public void AddCelestialItem(Body.CelestialItem celestialItem)
        {
            if (celestialItem == null) throw new ArgumentNullException(nameof(celestialItem));
            if (_celestialBodies.TryGetValue(celestialItem, out var added))
            {
                var difference = added.ModelDifference(celestialItem);
                if (difference != null)
                {
                    throw new ArgumentException(
                        $"The scenario already holds {added.Name} (NAIF {added.NaifId}), and {celestialItem.Name} has " +
                        $"another {difference}: a scenario takes one model per body. Add one of them.",
                        nameof(celestialItem));
                }

                return;
            }

            _celestialBodies.Add(celestialItem);
        }

        /// <summary>
        /// Add spacecraft to scenario
        /// </summary>
        /// <param name="spacecraft"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public void AddSpacecraft(Body.Spacecraft.Spacecraft spacecraft)
        {
            if (spacecraft == null) throw new ArgumentNullException(nameof(spacecraft));
            _spacecrafts.Add(spacecraft);
        }

        /// <summary>
        /// Add site to scenario
        /// </summary>
        /// <param name="site"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public void AddSite(Site site)
        {
            if (site == null) throw new ArgumentNullException(nameof(site));
            _sites.Add(site);
        }

        public void AddStar(Star star)
        {
            if (star == null) throw new ArgumentNullException(nameof(star));
            _stars.Add(star);
        }

        /// <summary>
        /// Propagate this scenario with the settings of <paramref name="options"/> (R06 of #668): each spacecraft, in
        /// turn, as <see cref="Body.Spacecraft.Spacecraft.Propagate(Window, PropagationOptions)"/> does, with the bodies of
        /// the scenario (<see cref="AddCelestialItem"/>) as its third bodies when
        /// <see cref="PropagationOptions.ThirdBodies"/> is null; each star at a step of the window length / 1000, between
        /// <see cref="PropagationOptions.OutputStep"/> and 60 s.
        /// </summary>
        /// <remarks>
        /// <see cref="PropagationOptions.ThirdBodies"/> and the bodies of the scenario are one setting (section A of the
        /// re-baseline of #668, decided by S. Guillet, 2026-11-19): given with a scenario that holds no body, the list
        /// applies; given with a scenario that holds bodies, it must hold the same ones, each spacecraft's central body
        /// aside (the list of <see cref="PropagationSolution.Options"/> omits it), with the same models, or the call throws
        /// before anything is propagated. Synchronous and sequential: a spacecraft whose central body has a geopotential
        /// may share it with the others, each propagation building its own field. With the integrator, forces and
        /// covariance options the settings name, each spacecraft's run is bit for bit that of
        /// <c>SimulateAsync(drag, srp, step, factory, covariance)</c>.
        /// </remarks>
        /// <param name="options">The settings; null for <see cref="PropagationOptions.Default"/>.</param>
        /// <returns>The summary of the simulation.</returns>
        /// <exception cref="InvalidOperationException">There is nothing to simulate; or a propagation throws it (see
        /// <see cref="Body.Spacecraft.Spacecraft.Propagate(Window, PropagationOptions)"/>).</exception>
        /// <exception cref="ArgumentException"><see cref="PropagationOptions.ThirdBodies"/> differs from the bodies of the
        /// scenario; or a propagation throws it.</exception>
        public ScenarioSummary Simulate(PropagationOptions options = null)
        {
            options ??= PropagationOptions.Default;
            if (_spacecrafts.Count == 0 && _sites.Count == 0 && _stars.Count == 0)
            {
                throw new InvalidOperationException("There is nothing to simulate");
            }

            if (options.ThirdBodies != null && _celestialBodies.Count > 0)
            {
                foreach (var spacecraft in _spacecrafts)
                {
                    RequireSameThirdBodies(options.ThirdBodies, spacecraft);
                }
            }

            IsSimulated = false;
            foreach (var star in _stars)
            {
                star.Propagate(Window, StarStep(options.OutputStep));
            }

            foreach (var spacecraft in _spacecrafts)
            {
                spacecraft.Propagate(Window, options, _celestialBodies);
            }

            return Summarize();
        }

        // The list of the options and the bodies of the scenario name the same bodies with the same models, the central
        // body of the spacecraft aside (section A of the re-baseline of #668)
        private void RequireSameThirdBodies(IReadOnlyList<Body.CelestialItem> thirdBodies,
            Body.Spacecraft.Spacecraft spacecraft)
        {
            int central = Propagator.CentralBodyPropagator.ResolveCentralBody(spacecraft).NaifId;
            var given = thirdBodies.Where(body => body.NaifId != central).GroupBy(body => body.NaifId)
                .ToDictionary(group => group.Key, group => group.First());
            var held = _celestialBodies.Where(body => body.NaifId != central).ToDictionary(body => body.NaifId);
            bool same = given.Count == held.Count && given.All(pair =>
                held.TryGetValue(pair.Key, out var body) && body.ModelDifference(pair.Value) == null);
            if (!same)
            {
                throw new ArgumentException(
                    $"PropagationOptions.ThirdBodies ({string.Join(", ", given.Values.Select(body => body.Name))}) differs " +
                    $"from the bodies of the scenario ({string.Join(", ", held.Values.Select(body => body.Name))}), the " +
                    $"central body of {spacecraft.Name} aside: they are one setting. Leave ThirdBodies null to take the " +
                    "bodies of the scenario, or give the same bodies.", "options");
            }
        }

        /// <summary>
        /// Propagate this scenario with the default Velocity-Verlet integrator, whose step is
        /// <paramref name="propagatorStepSize"/>.
        /// </summary>
        /// <param name="includeAtmosphericDrag">The drag will be computed relatively to initial spacecraft's center of motion</param>
        /// <param name="includeSolarRadiationPressure">Radiation pressure will be computed from drag coefficient defined in spacecraft</param>
        /// <param name="propagatorStepSize"></param>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="propagatorStepSize"/> is not positive; checked
        /// before any star is propagated.</exception>
        public async Task<ScenarioSummary> SimulateAsync(bool includeAtmosphericDrag, bool includeSolarRadiationPressure,
            TimeSpan propagatorStepSize)
        {
            return await SimulateAsync(propagatorStepSize, spacecraft => spacecraft.PropagateAsync(Window, _celestialBodies,
                includeAtmosphericDrag, includeSolarRadiationPressure, propagatorStepSize));
        }

        /// <summary>
        /// Propagate this scenario with the integrator that <paramref name="integratorFactory"/> creates for each spacecraft.
        /// </summary>
        /// <param name="includeAtmosphericDrag">The drag will be computed relatively to initial spacecraft's center of motion</param>
        /// <param name="includeSolarRadiationPressure">Radiation pressure will be computed from drag coefficient defined in spacecraft</param>
        /// <param name="propagatorStepSize">Step of the propagated states. It is also the integration step of a
        /// Velocity-Verlet integrator; an adaptive <see cref="RK78Integrator"/> chooses its own steps.</param>
        /// <param name="integratorFactory">Creates the integrator of one spacecraft, for example
        /// <c>() => new RK78Integrator()</c>. It is called once per spacecraft and must return a new instance each
        /// time: the propagator adds the force model of the spacecraft to its integrator.</param>
        /// <returns>The summary of the simulation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="integratorFactory"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="propagatorStepSize"/> is not positive; checked
        /// before any star is propagated.</exception>
        /// <exception cref="InvalidOperationException">There is nothing to simulate, or the factory returns null or an
        /// instance it already returned for another spacecraft.</exception>
        /// <remarks>
        /// The overload without a factory keeps the default Velocity-Verlet integrator. Stars are propagated the same
        /// way by both overloads; the propagation of a star replaces the states it held (<see cref="Star.Propagate"/>), as
        /// that of a spacecraft does.
        /// </remarks>
        public async Task<ScenarioSummary> SimulateAsync(bool includeAtmosphericDrag, bool includeSolarRadiationPressure,
            TimeSpan propagatorStepSize, Func<Integrator> integratorFactory)
        {
            return await SimulateAsync(includeAtmosphericDrag, includeSolarRadiationPressure, propagatorStepSize,
                integratorFactory, null);
        }

        /// <summary>
        /// Propagate this scenario with the integrator that <paramref name="integratorFactory"/> creates for each
        /// spacecraft, and the state transition matrix and the covariance of <paramref name="covariancePropagation"/> for
        /// every spacecraft.
        /// </summary>
        /// <param name="includeAtmosphericDrag">The drag will be computed relatively to initial spacecraft's center of motion</param>
        /// <param name="includeSolarRadiationPressure">Radiation pressure will be computed from drag coefficient defined in spacecraft</param>
        /// <param name="propagatorStepSize">Step of the propagated states. It is also the integration step of a
        /// Velocity-Verlet integrator; an adaptive <see cref="RK78Integrator"/> chooses its own steps.</param>
        /// <param name="integratorFactory">Creates the integrator of one spacecraft, for example
        /// <c>() => new RK78Integrator()</c>, which the state transition matrix needs. It is called once per spacecraft
        /// and must return a new instance each time.</param>
        /// <param name="covariancePropagation">The propagation of the state transition matrix and of the covariance, for
        /// every spacecraft; null for none, as the overload without it. In
        /// <see cref="CovariancePropagationMode.Covariance"/>, every spacecraft needs a covariance on its initial orbital
        /// parameters, at the start of the window; the stored states of each spacecraft then carry their covariance.
        /// </param>
        /// <returns>The summary of the simulation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="integratorFactory"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="propagatorStepSize"/> is not positive; checked
        /// before any star is propagated.</exception>
        /// <exception cref="InvalidOperationException">There is nothing to simulate, or the factory returns null or an
        /// instance it already returned for another spacecraft; or the covariance is asked for and a spacecraft has no
        /// valid initial covariance at the start of the window (the message names the spacecraft).</exception>
        /// <exception cref="NotSupportedException">The covariance propagation is asked for and the factory creates
        /// another integrator than <see cref="RK78Integrator"/>.</exception>
        /// <exception cref="ArgumentException">Process noise or consider parameters without the covariance, or a
        /// consider parameter without its force.</exception>
        public async Task<ScenarioSummary> SimulateAsync(bool includeAtmosphericDrag, bool includeSolarRadiationPressure,
            TimeSpan propagatorStepSize, Func<Integrator> integratorFactory,
            CovariancePropagationOptions covariancePropagation)
        {
            if (integratorFactory == null) throw new ArgumentNullException(nameof(integratorFactory));

            var integrators = new HashSet<Integrator>(ReferenceEqualityComparer.Instance);
            return await SimulateAsync(propagatorStepSize, spacecraft =>
            {
                var integrator = integratorFactory() ??
                                 throw new InvalidOperationException("The integrator factory returned null.");
                if (!integrators.Add(integrator))
                {
                    throw new InvalidOperationException(
                        "The integrator factory must return a new instance for each spacecraft: the propagator adds the forces of each spacecraft to its integrator.");
                }

                // A rejected initial covariance names its spacecraft (PropagatorBase); any other failure keeps its own
                // message
                return Task.Run(() => spacecraft.Propagate(Window, _celestialBodies, integrator, includeAtmosphericDrag,
                    includeSolarRadiationPressure, propagatorStepSize, covariancePropagation));
            });
        }

        private async Task<ScenarioSummary> SimulateAsync(TimeSpan propagatorStepSize,
            Func<Body.Spacecraft.Spacecraft, Task> propagateSpacecraft)
        {
            // Before the stars are propagated (#674)
            Propagator.PropagatorBase.RequirePositiveStep(propagatorStepSize, nameof(propagatorStepSize));
            IsSimulated = false;
            if (_spacecrafts.Count == 0 && _sites.Count == 0 && _stars.Count == 0)
            {
                throw new InvalidOperationException("There is nothing to simulate");
            }

            foreach (var star in _stars)
            {
                await star.PropagateAsync(Window, StarStep(propagatorStepSize));
            }

            foreach (var spacecraft in _spacecrafts)
            {
                await propagateSpacecraft(spacecraft);
            }

            return Summarize();
        }

        // The step of a star: the window length / 1000, at least the output step and at most 60 s
        private TimeSpan StarStep(TimeSpan outputStep)
        {
            var starStepSize = Window.Length / 1000.0;
            if (starStepSize < outputStep)
            {
                starStepSize = outputStep;
            }
            else if (starStepSize > TimeSpan.FromMinutes(1.0))
            {
                starStepSize = TimeSpan.FromMinutes(1.0);
            }

            return starStepSize;
        }

        private ScenarioSummary Summarize()
        {
            ScenarioSummary scenarioSummary = new ScenarioSummary(this.Window);
            foreach (var spacecraft in _spacecrafts)
            {
                scenarioSummary.AddSpacecraftSummary(spacecraft.GetSummary());
            }

            IsSimulated = true;
            return scenarioSummary;
        }
        
        #region Operators
        public bool Equals(Scenario other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name && Mission == other.Mission;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Scenario)obj);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public static bool operator ==(Scenario left, Scenario right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Scenario left, Scenario right)
        {
            return !Equals(left, right);
        }
        #endregion
    }
}