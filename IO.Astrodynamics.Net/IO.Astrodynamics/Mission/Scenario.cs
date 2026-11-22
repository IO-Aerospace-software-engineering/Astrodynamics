using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
        /// turn, as <see cref="Body.Spacecraft.Spacecraft.Propagate(Window, PropagationOptions, System.Threading.CancellationToken)"/> does, with the bodies of
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
        /// may share it with the others, each propagation building its own field. It replaces the three
        /// <c>SimulateAsync</c> overloads of flags, removed in 11.0.0 (R07 and R28 of #668): the settings of their
        /// integrator, flags and step give each spacecraft their run, bit for bit (the overload without a factory,
        /// <see cref="VVIntegratorOptions"/> with its step).
        /// <para>
        /// Cancellation (R28 of #668): <paramref name="cancellationToken"/> is read once the settings are checked, before
        /// anything changes, so that a token already cancelled changes nothing; then before each star and each
        /// spacecraft, and inside each spacecraft's propagation (see
        /// <see cref="Body.Spacecraft.Spacecraft.Propagate(Window, PropagationOptions, System.Threading.CancellationToken)"/>).
        /// A simulation stopped returns no summary and leaves <see cref="IsSimulated"/> false: the stars and spacecraft
        /// propagated before the stop are complete, the spacecraft it stopped in is left as a stopped propagation leaves it
        /// (C2 of lot L4), the later ones are untouched.
        /// </para>
        /// </remarks>
        /// <param name="options">The settings; null for <see cref="PropagationOptions.Default"/>.</param>
        /// <param name="cancellationToken">The token that stops the simulation; none by default.</param>
        /// <returns>The summary of the simulation.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; the exception
        /// carries it.</exception>
        /// <exception cref="InvalidOperationException">There is nothing to simulate; or a propagation throws it (see
        /// <see cref="Body.Spacecraft.Spacecraft.Propagate(Window, PropagationOptions, System.Threading.CancellationToken)"/>).</exception>
        /// <exception cref="ArgumentException"><see cref="PropagationOptions.ThirdBodies"/> differs from the bodies of the
        /// scenario; or a propagation throws it.</exception>
        public ScenarioSummary Simulate(PropagationOptions options = null, CancellationToken cancellationToken = default)
        {
            options ??= PropagationOptions.Default;
            if (_spacecrafts.Count == 0 && _sites.Count == 0 && _stars.Count == 0)
            {
                throw new InvalidOperationException("There is nothing to simulate");
            }

            // Before any star is propagated (decision 7 of lot L4 of #668)
            if (_spacecrafts.Count > 0)
            {
                Propagator.PropagatorBase.RequireWindowLength(Window);
            }

            if (options.ThirdBodies != null && _celestialBodies.Count > 0)
            {
                foreach (var spacecraft in _spacecrafts)
                {
                    RequireSameThirdBodies(options.ThirdBodies, spacecraft);
                }
            }

            // Before anything changes, so that a token already cancelled changes nothing (decision 6 of lot L4 of #668)
            cancellationToken.ThrowIfCancellationRequested();
            IsSimulated = false;
            foreach (var star in _stars)
            {
                cancellationToken.ThrowIfCancellationRequested();
                star.Propagate(Window, StarStep(options.OutputStep));
            }

            foreach (var spacecraft in _spacecrafts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                spacecraft.Propagate(Window, options, _celestialBodies, cancellationToken);
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