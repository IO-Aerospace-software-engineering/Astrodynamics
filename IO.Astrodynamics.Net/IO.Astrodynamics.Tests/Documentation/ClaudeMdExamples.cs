// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of the two CLAUDE.md files, compiled (#668, third round). Each block of IO.Astrodynamics.Net/CLAUDE.md
// is the region "net:<n>", and each block of the root CLAUDE.md the region "root:<n>", n its rank among the file's csharp
// blocks; the parameters of a method give the inputs of a block that continues an earlier one, and the lines after its
// region return what the tests read. ClaudeMdExamplesTests checks that each block equals its region and runs the blocks
// that need only Data/SolarSystem. The library blocks are in a namespace outside IO.Astrodynamics, as an application's,
// so a name resolves here as it does for a reader; the blocks about the test process are in IO.Astrodynamics.Tests, where
// they apply. Do not edit a region alone: change the CLAUDE.md file and copy the block.

using System;
using System.Collections.Generic;
using IO.Astrodynamics;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.CCSDS.OMM;
using IO.Astrodynamics.CCSDS.OPM;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Events;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using TLE = IO.Astrodynamics.OrbitalParameters.TLE.TLE;

namespace ClaudeMdSamples
{
    /// <summary>The C# examples of IO.Astrodynamics.Net/CLAUDE.md and of the root CLAUDE.md, one method per block.</summary>
    internal static class ClaudeMdExamples
    {
        // IO.Astrodynamics.Net/CLAUDE.md, "Creating Mean Elements from OMM Data"
        internal static KeplerianElements Net01(CelestialBody earth, Time epoch)
        {
            #region net:1
            // FromOMM accepts OMM native units: rev/day for mean motion, degrees for angles
            var meanKep = KeplerianElements.FromOMM(
                meanMotion: 15.49309423,        // rev/day
                eccentricity: 0.0000493,
                inclination: 51.6423,           // degrees
                raan: 353.0312,                 // degrees
                argumentOfPeriapsis: 320.8755,  // degrees
                meanAnomaly: 39.2360,           // degrees
                observer: earth,
                epoch: epoch,
                frame: Frame.TEME);

            // The mean motion is cached internally to preserve precision
            // when converting back to TLE format
            Assert.Equal(OrbitalElementsType.Mean, meanKep.ElementsType);

            // This would throw InvalidOperationException:
            // var sv = meanKep.ToStateVector();  // ERROR!
            #endregion
            return meanKep;
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Working with TLE"
        internal static TLE Net02(Time epoch)
        {
            #region net:2
            // Parse TLE - elements are mean by definition
            var tle = new TLE("ISS",
                "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
                "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

            Assert.Equal(OrbitalElementsType.Mean, tle.ElementsType);

            // Get osculating state vector via SGP4 propagation
            var osculating = tle.ToOsculating();           // At TLE epoch
            var osculatingLater = tle.ToOsculating(epoch); // At specific epoch
            Assert.Equal(OrbitalElementsType.Osculating, osculating.ElementsType);

            // Get mean Keplerian elements (for creating new TLEs)
            var meanKep = tle.ToMeanKeplerianElements();
            Assert.Equal(OrbitalElementsType.Mean, meanKep.ElementsType);

            // NORAD catalog number, decoded from Alpha-5 when applicable
            Assert.Equal(25544, tle.NoradCatalogId);
            #endregion
            return tle;
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Loading and Saving OMM" (a file read and written: compiled only)
        internal static void Net03()
        {
            #region net:3
            // Load OMM from file (with optional validation)
            var omm = Omm.LoadFromFile("satellite.omm", validateSchema: true, validateContent: true);

            // Access data
            Console.WriteLine($"Object: {omm.ObjectName}");
            Console.WriteLine($"COSPAR ID: {omm.ObjectId}");  // Format: "1998-067A"
            Console.WriteLine($"Mean Motion: {omm.Data.MeanElements.MeanMotion} rev/day");

            // Save OMM to file
            omm.SaveToFile("output.omm", validateBeforeSave: true);
            #endregion
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "OMM ↔ TLE Bidirectional Conversion" (files: compiled only)
        internal static void Net04(string line1, string line2)
        {
            #region net:4
            // OMM to TLE (for SGP4 propagation)
            var omm = Omm.LoadFromFile("iss.omm");
            if (omm.IsTleCompatible)
            {
                var tle = omm.ToTle();
                var stateVector = tle.ToStateVector();  // Propagate with SGP4
            }

            // TLE to OMM (for archiving/sharing)
            var issTle = new TLE("ISS", line1, line2);
            var issOmm = issTle.ToOmm(originator: "My Organization");
            issOmm.SaveToFile("iss.omm");
            #endregion
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Loading and Saving OPM" (files: compiled only)
        internal static void Net05()
        {
            #region net:5
            // Load OPM from file (with optional validation)
            var opm = Opm.LoadFromFile("spacecraft.opm", validateSchema: true, validateContent: true);

            // Access data
            Console.WriteLine($"Object: {opm.ObjectName}");
            Console.WriteLine($"COSPAR ID: {opm.ObjectId}");  // Format: "1998-067A"
            Console.WriteLine($"Position: {opm.Data.StateVector.X}, {opm.Data.StateVector.Y}, {opm.Data.StateVector.Z} km");

            // Save OPM to file
            opm.SaveToFile("output.opm", validateBeforeSave: true, wrapInNdm: true, indent: true);
            #endregion
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "OPM ↔ Spacecraft Bidirectional Conversion" (files: compiled only)
        internal static void Net06(Clock clock, StateVector stateVector, CelestialBody earth)
        {
            #region net:6
            // Spacecraft to OPM (for archiving/sharing)
            var spacecraft = new Spacecraft(-1000, "ISS", 420000.0, stateVector, 500000.0, clock,
                1600.0, 2.2, "1998-067A", 1.5);
            var opm = spacecraft.ToOpm(
                originator: "My Organization",
                includeKeplerianElements: true,
                includeSpacecraftParameters: true,
                includeManeuvers: true);  // Includes executed maneuvers
            opm.SaveToFile("iss.opm");

            // OPM to Spacecraft (for mission operations)
            var restoredOpm = Opm.LoadFromFile("iss.opm");
            var restored = restoredOpm.ToSpacecraft(
                naifId: -1000,
                maxOperatingMass: 500000.0,
                clock: new Clock("OnboardClock", 256),
                observer: earth);  // Optional, defaults to the CENTER_NAME centre (an unknown one throws)
            #endregion
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "OPM ↔ StateVector Conversion" (a file read: compiled only)
        internal static void Net07(CelestialBody earth)
        {
            #region net:7
            // OPM to StateVector (simple conversion)
            var opm = Opm.LoadFromFile("spacecraft.opm");
            var stateVector = opm.ToStateVector(observer: earth);
            // Units converted: km → m, km/s → m/s

            // StateVector to OPM
            var newOpm = Opm.CreateFromStateVector("SAT-1", "2024-001A", stateVector,
                originator: "Mission Control");
            #endregion
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Maneuver Integration"
        internal static Opm Net08(Spacecraft spacecraft, Time end)
        {
            #region net:8
            // spacecraft: a propagated spacecraft with a COSPAR id; end: an epoch of its propagation after its burns
            // The OPM carries the state of its epoch, with the mass and the maneuvers ignited by then (#658)
            var opm = spacecraft.ToOpm(epoch: end, includeManeuvers: true);
            // OPM will contain OpmManeuverParameters for each ImpulseManeuver ignited by then

            // Manual maneuver export from ImpulseManeuver
            foreach (var maneuver in spacecraft.ExecutedManeuvers)
            {
                if (maneuver is ImpulseManeuver impulseManeuver && impulseManeuver.ThrustWindow.HasValue)
                {
                    var opmManeuver = impulseManeuver.ToOpmManeuverParameters(referenceFrame: "EME2000");
                    // Contains: epoch, duration, delta-mass, delta-V (km/s)
                }
            }
            #endregion
            return opm;
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "User-Defined Parameters"
        internal static OpmData Net09(StateVector stateVector)
        {
            #region net:9
            // Create OPM with custom parameters
            var userParams = new OpmUserDefinedParameters(
                new Dictionary<string, string>
                {
                    ["MISSION_ID"] = "STS-001",
                    ["OPERATOR"] = "NASA"
                },
                comments: new[] { "Custom mission parameters" });

            var data = new OpmData(OpmStateVector.FromStateVector(stateVector), userDefinedParameters: userParams);
            #endregion
            return data;
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Covariance Matrix Support"
        internal static OpmData Net10(StateVector stateVector)
        {
            #region net:10
            // OPM with covariance (units: km², km²/s, km²/s²)
            var covariance = new CovarianceMatrix(
                cxX: 1.0e-6, cyX: 0.0, cyY: 1.0e-6, czX: 0.0, czY: 0.0, czZ: 1.0e-6,
                cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 1.0e-9,
                cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 1.0e-9,
                czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 1.0e-9,
                referenceFrame: "ICRF");

            var data = new OpmData(OpmStateVector.FromStateVector(stateVector), covariance: covariance);
            #endregion
            return data;
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Atmospheric Modeling", "Usage Pattern"
        internal static (Atmosphere Standard, Atmosphere Thermosphere, Atmosphere EarthFull, Atmosphere EarthLow, AtmosphericContext FullContext) Net11()
        {
            #region net:11
            // Simple case - altitude only (standard atmosphere)
            var context = AtmosphericContext.FromAltitude(10000);  // 10 km
            var model = new EarthStandardAtmosphere();
            var atmosphere = model.GetAtmosphere(context);
            // atmosphere.Temperature, .Pressure, .Density, .Details (null for simple models)

            // Full context with NRLMSISE-00
            var fullContext = AtmosphericContext.FromPlanetodetic(
                new Planetodetic(-75 * Constants.Deg2Rad, 45 * Constants.Deg2Rad, 400000),  // longitude, latitude (rad), altitude (m): 400 km
                new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB));
            var nrlmsise = new Nrlmsise00Model(SpaceWeather.Nominal);
            var thermosphere = nrlmsise.GetAtmosphere(fullContext);

            // Access model-specific details via pattern matching
            if (thermosphere.Details is NrlmsiseDetails details)
            {
                var o2Density = details.MolecularOxygenDensity;  // m⁻³
                var exoTemp = details.ExosphericTemperature;     // K
            }

            // Via CelestialBody: its configured model; an Earth without one takes NRLMSISE-00 at nominal space weather with a
            // full context, the atmosphere of its drag (#674), and the U.S. Standard Atmosphere 1976, its base atmosphere, with an
            // altitude only or a partial context (decided by S. Guillet, 2026-11-14)
            var earth = PlanetsAndMoons.EARTH_BODY;
            var atm = earth.GetAtmosphere(fullContext);  // NRLMSISE-00 nominal: EARTH_BODY has no model
            var low = earth.GetAtmosphere(10000.0);  // U.S. Standard Atmosphere 1976: an altitude only
            #endregion
            return (atmosphere, thermosphere, atm, low, fullContext);
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Geopotential Gravity Model", "Usage"
        internal static CentralBodyPropagator Net12(Time epoch, Window window, Clock clock)
        {
            #region net:12
            // Create Earth with degree-10 geopotential
            var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch,
                new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

            // A propagation takes the models of the orbit's centre, and a listed body of its id with other models throws
            // (#674): the orbit is built on this earth
            var orbit = new StateVector(new Vector3(6800000.0, 0, 0), new Vector3(0, 7500.0, 0), earth, epoch, Frame.ICRF);
            var spacecraft = new Spacecraft(-1000, "Sat", 100.0, orbit, 10000.0, clock);

            // The propagator uses the geopotential model of the orbit's centre; the settings in one PropagationOptions value
            var propagator = new CentralBodyPropagator(window, spacecraft, new PropagationOptions
            {
                Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) },
                OutputStep = TimeSpan.FromSeconds(1.0),
                ThirdBodies = [PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY]
            });
            #endregion
            return propagator;
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Atmospheric Drag"
        internal static AtmosphericDrag Net13(Time epoch, GeopotentialModelParameters geopotentialParams, Spacecraft spacecraft)
        {
            #region net:13
            // Atmospheric drag reads the body's atmosphere: its model, or NRLMSISE-00 at nominal space weather for an Earth without
            // one; another body without a model throws (#674)
            var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch,
                geopotentialParams, new EarthStandardAtmosphere());
            var drag = new AtmosphericDrag(spacecraft, earth);
            #endregion
            return drag;
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Solar Radiation Pressure"
        internal static SolarRadiationPressure Net14(Clock clock, OrbitalParameters orbit, CelestialBody earth)
        {
            #region net:14
            // SRP with Cr = 1.5
            var spacecraft = new Spacecraft(-1001, "Sat", 100.0, orbit, 10000.0, clock,
                sectionalArea: 10.0, solarRadiationCoefficient: 1.5);
            var srp = new SolarRadiationPressure(spacecraft, [earth]);
            #endregion
            return srp;
        }

        // Block 15 is the declaration of IIntegrator: ClaudeMdSamples.Declarations, below

        // IO.Astrodynamics.Net/CLAUDE.md, "Configurable Body Axes"
        internal static Spacecraft Net16(Clock clock, OrbitalParameters orbit, CelestialBody earth, Time epoch, TimeSpan duration, Engine engine)
        {
            #region net:16
            // Spacecraft with +X as front (e.g., matching a SPICE FK convention)
            var spacecraft = new Spacecraft(-1001, "Sat", 1000, orbit, 5000, clock,
                bodyFront: Vector3.VectorX, bodyRight: Vector3.VectorY.Inverse(), bodyUp: Vector3.VectorZ);

            // The single-vector attitudes, the impulse attitude and the TRIAD factories use the instance axes
            var prograde = new ProgradeAttitude(epoch, duration, earth);
            // → orients spacecraft.BodyFront (+X) along velocity vector
            #endregion
            return spacecraft;
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Constructor Options", 1
        internal static TriadAttitude Net17(Time minimumEpoch, TimeSpan holdDuration, Instrument camera, CelestialBody earth, CelestialBody sun, Engine engine)
        {
            #region net:17
            // Point camera boresight at Earth, constrain roll using camera refVector toward Sun
            var attitude = new TriadAttitude(
                camera,
                new CelestialAttitudeTarget(earth),
                new CelestialAttitudeTarget(sun),
                minimumEpoch,
                holdDuration);
            #endregion
            return attitude;
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Constructor Options", 2 (a star of the caller's: compiled only)
        internal static TriadAttitude Net18(Time minimumEpoch, TimeSpan holdDuration, Instrument nadirCamera, CelestialBody earth,
            Instrument starTracker, Star referenceStar, Engine engine)
        {
            #region net:18
            // Point nadir camera at Earth, star tracker at reference star
            var attitude = new TriadAttitude(
                nadirCamera,
                new CelestialAttitudeTarget(earth),
                starTracker,
                new CelestialAttitudeTarget(referenceStar),
                minimumEpoch,
                holdDuration);
            #endregion
            return attitude;
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Constructor Options", 3
        internal static TriadAttitude Net19(Time minimumEpoch, TimeSpan holdDuration, CelestialBody moon, CelestialBody sun, Engine engine)
        {
            #region net:19
            // Point spacecraft Front at Moon, keep Up toward Sun
            var attitude = new TriadAttitude(
                Spacecraft.Front,
                new CelestialAttitudeTarget(moon),
                Spacecraft.Up,
                new CelestialAttitudeTarget(sun),
                minimumEpoch,
                holdDuration);
            #endregion
            return attitude;
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Constructor Options", 4
        internal static (TriadAttitude Attitude, TriadAttitude Lvlh) Net20(CelestialBody earth, Time epoch, TimeSpan duration, CelestialBody sun, Engine engine)
        {
            #region net:20
            // Point prograde with solar panels toward Sun
            var attitude = new TriadAttitude(
                Spacecraft.Front,
                OrbitalDirectionTarget.Prograde,
                Spacecraft.Up,
                new CelestialAttitudeTarget(sun),
                epoch,
                duration,
                earth);

            // Full LVLH attitude (nadir pointing with prograde constraint)
            var lvlh = new TriadAttitude(
                Spacecraft.Down,
                OrbitalDirectionTarget.Nadir,
                Spacecraft.Front,
                OrbitalDirectionTarget.Prograde,
                epoch,
                duration,
                earth);
            #endregion
            return (attitude, lvlh);
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Factory Methods"
        internal static (TriadAttitude Lvlh, TriadAttitude SunTrack) Net21(CelestialBody earth, Time epoch, TimeSpan duration, CelestialBody sun, Engine engine)
        {
            #region net:21
            // LVLH: BodyDown→Nadir, BodyFront→Prograde (the axes of the plan's spacecraft, #677)
            var lvlh = TriadAttitude.CreateLvlh(epoch, duration, earth);

            // Prograde with sun tracking: BodyFront→Prograde, BodyUp→Sun
            var sunTrack = TriadAttitude.CreateProgradeWithSunTracking(sun, epoch, duration, earth);
            #endregion
            return (lvlh, sunTrack);
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Use Case Examples", "Earth Observation with Sun Tracking"
        internal static TriadAttitude Net22(Time epoch, CelestialBody earth, CelestialBody sun, Engine engine)
        {
            #region net:22
            // Keep nadir sensor pointed at Earth, the roll about nadir set by the Sun (Up, opposite to Down, would be
            // rejected: the body vectors must be at least 5 degrees apart)
            var earthObservation = new TriadAttitude(
                Spacecraft.Down, new CelestialAttitudeTarget(earth),  // Primary: nadir pointing
                Spacecraft.Right, new CelestialAttitudeTarget(sun),
                epoch, TimeSpan.FromMinutes(30));
            #endregion
            return earthObservation;
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Use Case Examples", "Instrument Pointing with Roll Constraint" (stars of the
        // caller's: compiled only)
        internal static TriadAttitude Net23(Time epoch, Instrument telescope, Star targetStar, Instrument rollSensor, Star referenceStar, Engine engine)
        {
            #region net:23
            // Point telescope at target star, constrain roll using reference star
            var observation = new TriadAttitude(
                telescope,
                new CelestialAttitudeTarget(targetStar),
                rollSensor,
                new CelestialAttitudeTarget(referenceStar),
                epoch,
                TimeSpan.FromHours(2));
            #endregion
            return observation;
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Use Case Examples", "Plane-Change Attitude (Normal Pointing)"
        internal static NormalAttitude Net24(CelestialBody earth, Time epoch, TimeSpan duration, Engine engine)
        {
            #region net:24
            // Orient for plane-change burn: front along orbital normal
            var normalAttitude = new NormalAttitude(epoch, duration, earth);
            #endregion
            return normalAttitude;
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Matrix Utility"
        internal static Matrix Net25(Vector3 col0, Vector3 col1, Vector3 col2)
        {
            #region net:25
            // Create 3x3 matrix from column vectors (used in TRIAD algorithm)
            var rotationMatrix = Matrix.FromColumnVectors(col0, col1, col2);
            #endregion
            return rotationMatrix;
        }

        // CLAUDE.md, "Data Provider Pattern" (sets the global data provider, which no test of the test process may: compiled
        // only)
        internal static void Root01()
        {
            #region root:1
            // Default: SPICE kernel files
            Configuration.Instance.SetDataProvider(new SpiceDataProvider());

            // In-memory, no kernel files required (in IO.Astrodynamics.Tests, in a child process only)
            Configuration.Instance.SetDataProvider(new MemoryDataProvider());
            #endregion
        }
    }
}

namespace ClaudeMdSamples.Declarations
{
    // IO.Astrodynamics.Net/CLAUDE.md, "Integrator Interface": the declaration of the library's interface, compared with it by
    // reflection
    #region net:15
    public interface IIntegrator
    {
        void Initialize(StateVector initialState);
        IntegrationResult IntegrateSegment(
            Vector3 startPosition, Vector3 startVelocity,
            Time baseEpoch, double duration,
            IReadOnlyList<IEventDetector> eventDetectors = null);
    }
    #endregion
}

namespace IO.Astrodynamics.Tests.Documentation
{
    /// <summary>The blocks of the two CLAUDE.md files about the test process, compiled where they apply.</summary>
    internal static class ClaudeMdTestProcessExamples
    {
        // IO.Astrodynamics.Net/CLAUDE.md, "Testing Approach"
        internal static void Net26()
        {
            #region net:26
            TestKernels.EnsureSolarSystem();
            #endregion
        }

        // IO.Astrodynamics.Net/CLAUDE.md, "Name shadowing in test namespaces"
        internal static (Frames.Frame Frame, TimeSystem.Time Epoch, double Deg2Rad) Net27()
        {
            #region net:27
            var icrf = Frames.Frame.ICRF;                     // not Frame.ICRF
            var j2000 = TimeSystem.Time.J2000TDB;             // not Time.J2000TDB
            var deg2Rad = IO.Astrodynamics.Constants.Deg2Rad; // not Constants.Deg2Rad (that is the test helper)
            #endregion
            return (icrf, j2000, deg2Rad);
        }

        // CLAUDE.md, "Data Provider Pattern", the test classes
        internal static void Root02()
        {
            #region root:2
            TestKernels.EnsureSolarSystem();
            #endregion
        }
    }
}
