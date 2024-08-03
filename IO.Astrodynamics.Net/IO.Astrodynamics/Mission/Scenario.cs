using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.Time;

namespace IO.Astrodynamics.Mission
{
    public class Scenario : IEquatable<Scenario>
    {
        public string Name { get; }
        public Window Window { get; }
        public Mission Mission { get; }
        private readonly HashSet<Body.CelestialItem> _additionalCelestialBodies = new();
        public IReadOnlyCollection<Body.CelestialItem> AdditionalCelstialBodies => _additionalCelestialBodies;

        private readonly HashSet<Body.Spacecraft.Spacecraft> _spacecrafts = new();
        public IReadOnlyCollection<Body.Spacecraft.Spacecraft> Spacecrafts => _spacecrafts;
        private readonly HashSet<Site> _sites = new();
        public IReadOnlyCollection<Site> Sites => _sites;

        private readonly HashSet<Star> _stars = new();
        public IReadOnlyCollection<Star> Stars => _stars;
        public DirectoryInfo RootDirectory { get; private set; }
        public DirectoryInfo SpacecraftDirectory { get; private set; }
        public DirectoryInfo SiteDirectory { get; private set; }
        public DirectoryInfo StarDirectory { get; private set; }

        public bool IsSimulated => RootDirectory?.Exists == true && (SpacecraftDirectory?.Exists == true || SiteDirectory?.Exists == true || StarDirectory?.Exists == true);

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
            _additionalCelestialBodies.Add(celestialItem);
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
        /// Propagate this scenario
        /// </summary>
        /// <param name="outputDirectory">Output folder used to write files</param>
        /// <param name="includeAtmosphericDrag">The drag will be computed relatively to initial spacecraft's center of motion</param>
        /// <param name="includeSolarRadiationPressure">Radiation pressure will be computed from drag coefficient defined in spacecraft</param>
        /// <param name="propagatorStepSize"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<ScenarioSummary> SimulateAsync(DirectoryInfo outputDirectory, bool includeAtmosphericDrag, bool includeSolarRadiationPressure,
            TimeSpan propagatorStepSize)
        {
            if (_spacecrafts.Count == 0 && _sites.Count == 0 && _stars.Count == 0)
            {
                throw new InvalidOperationException("There is nothing to simulate");
            }

            InitializeDirectories(outputDirectory);
            if (_sites.Count > 0)
            {
                //step size 1s up to 1000.0s window length
                //step size variable from 1000.0s up to 60000s window length
                //step size 60s after 60000s window length
                var siteStepSize = Window.Length / 1000.0;
                if (siteStepSize < propagatorStepSize)
                {
                    siteStepSize = propagatorStepSize;
                }
                else if (siteStepSize > TimeSpan.FromMinutes(1.0))
                {
                    siteStepSize = TimeSpan.FromMinutes(1.0);
                }

                foreach (var site in _sites)
                {
                    await site.PropagateAsync(Window, siteStepSize, SiteDirectory);
                }
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
                    await star.PropagateAsync(Window, starStepSize, StarDirectory);
                }
            }


            foreach (var spacecraft in _spacecrafts)
            {
                await spacecraft.PropagateAsync(Window, _additionalCelestialBodies, includeAtmosphericDrag, includeSolarRadiationPressure, propagatorStepSize, SpacecraftDirectory);
            }

            ScenarioSummary scenarioSummary = new ScenarioSummary(this.Window, SiteDirectory, SpacecraftDirectory);
            foreach (var spacecraft in _spacecrafts)
            {
                scenarioSummary.AddSpacecraftSummary(spacecraft.GetSummary());
            }

            return scenarioSummary;
        }

        private void InitializeDirectories(DirectoryInfo outputDirectory)
        {
            RootDirectory = null;
            SpacecraftDirectory = null;
            SiteDirectory = null;
            StarDirectory = null;


            RootDirectory = outputDirectory.CreateSubdirectory(Mission.Name).CreateSubdirectory(this.Name);
            SpacecraftDirectory = RootDirectory.CreateSubdirectory("Spacecrafts");
            SiteDirectory = RootDirectory.CreateSubdirectory("Sites");
            StarDirectory = RootDirectory.CreateSubdirectory("Stars");
        }

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
            return (Name != null ? Name.GetHashCode() : 0);
        }

        public static bool operator ==(Scenario left, Scenario right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Scenario left, Scenario right)
        {
            return !Equals(left, right);
        }
    }
}