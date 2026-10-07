using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Propagator.Integrators;
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
        /// <exception cref="ArgumentNullException"></exception>
        public void AddCelestialItem(Body.CelestialItem celestialItem)
        {
            if (celestialItem == null) throw new ArgumentNullException(nameof(celestialItem));
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
        /// Propagate this scenario with the default Velocity-Verlet integrator, whose step is
        /// <paramref name="propagatorStepSize"/>.
        /// </summary>
        /// <param name="includeAtmosphericDrag">The drag will be computed relatively to initial spacecraft's center of motion</param>
        /// <param name="includeSolarRadiationPressure">Radiation pressure will be computed from drag coefficient defined in spacecraft</param>
        /// <param name="propagatorStepSize"></param>
        /// <exception cref="InvalidOperationException"></exception>
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
        /// <exception cref="InvalidOperationException">There is nothing to simulate, or the factory returns null or an
        /// instance it already returned for another spacecraft.</exception>
        /// <remarks>
        /// The overload without a factory keeps the default Velocity-Verlet integrator. Stars are propagated the same
        /// way by both overloads.
        /// </remarks>
        public async Task<ScenarioSummary> SimulateAsync(bool includeAtmosphericDrag, bool includeSolarRadiationPressure,
            TimeSpan propagatorStepSize, Func<Integrator> integratorFactory)
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

                return Task.Run(() => spacecraft.Propagate(Window, _celestialBodies, integrator, includeAtmosphericDrag,
                    includeSolarRadiationPressure, propagatorStepSize));
            });
        }

        private async Task<ScenarioSummary> SimulateAsync(TimeSpan propagatorStepSize,
            Func<Body.Spacecraft.Spacecraft, Task> propagateSpacecraft)
        {
            IsSimulated = false;
            if (_spacecrafts.Count == 0 && _sites.Count == 0 && _stars.Count == 0)
            {
                throw new InvalidOperationException("There is nothing to simulate");
            }

            if (_stars.Count > 0)
            {
                //step size 1s up to 1000.0s window length
                //step size variable from 1000.0s up to 60000s window length
                //step size 60s after 60000s window length
                var starStepSize = Window.Length / 1000.0;
                if (starStepSize < propagatorStepSize)
                {
                    starStepSize = propagatorStepSize;
                }
                else if (starStepSize > TimeSpan.FromMinutes(1.0))
                {
                    starStepSize = TimeSpan.FromMinutes(1.0);
                }

                foreach (var star in _stars)
                {
                    await star.PropagateAsync(Window, starStepSize);
                }
            }


            foreach (var spacecraft in _spacecrafts)
            {
                await propagateSpacecraft(spacecraft);
            }

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