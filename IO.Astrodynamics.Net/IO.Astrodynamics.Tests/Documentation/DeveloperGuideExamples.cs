// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The C# examples of DEVELOPER_GUIDE.md, compiled (#668). Each block of the guide is the region "guide:<n>" of one method,
// n its rank among the guide's csharp blocks (the excerpt of Constants, rank 0, is checked by value instead); the lines a
// method has before its region give the inputs of a block that continues an earlier one, as the comment opening the block
// says. DeveloperGuideExamplesTests checks that each block equals its region (the block's using directives aside, which
// are this file's) and runs the blocks that need only Data/SolarSystem. The namespace is outside IO.Astrodynamics, as an
// application's, so a name resolves here as it does for a reader of the guide. Do not edit a region alone: change the
// guide and copy the block.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.OMM;
using IO.Astrodynamics.CCSDS.OPM;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Cosmographia;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Exceptions;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Maneuver.Lambert;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Mission;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Physics;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using TLE = IO.Astrodynamics.OrbitalParameters.TLE.TLE;

namespace DeveloperGuideSamples;

/// <summary>The C# examples of DEVELOPER_GUIDE.md, one method per block (#668).</summary>
internal static class DeveloperGuideExamples
{

    // DEVELOPER_GUIDE.md, "Loading SPICE Kernels"
    internal static void Block01()
    {
        #region guide:1
        // Load kernels from a directory
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo("path/to/kernels"));

        // Or load a specific kernel file
        SpiceAPI.Instance.LoadKernels(new FileInfo("de440.bsp"));
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Basic Example: Compute Earth Position"
    internal static void Block02()
    {
        #region guide:2
        // Load kernels
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo("Data/SolarSystem"));

        // Create celestial bodies
        var earth = PlanetsAndMoons.EARTH_BODY;
        var sun = new CelestialBody(Stars.Sun);

        // Define epoch
        var epoch = new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB);

        // Get Earth's state vector relative to the Sun
        var stateVector = earth.GetEphemeris(epoch, sun, Frame.ICRF, Aberration.None);

        Console.WriteLine($"Position: {stateVector.Position}");
        Console.WriteLine($"Velocity: {stateVector.Velocity}");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Basic Example: Create an Orbit"
    internal static void Block03()
    {
        #region guide:3
        var earth = PlanetsAndMoons.EARTH_BODY;
        var epoch = new Time(2024, 1, 1, 0, 0, 0, TimeScale.TDB);

        // Create from Keplerian elements
        var orbit = new KeplerianElements(
            7000000.0,                           // Semi-major axis (m)
            0.001,                               // Eccentricity
            51.6 * Constants.Deg2Rad,            // Inclination (rad)
            100.0 * Constants.Deg2Rad,           // RAAN (rad)
            90.0 * Constants.Deg2Rad,            // Argument of periapsis (rad)
            0.0,                                 // Mean anomaly (rad)
            earth,                               // Observer: the center of motion
            epoch,
            Frame.ICRF
        );

        // Convert to state vector
        var sv = orbit.ToStateVector();
        Console.WriteLine($"Orbital Period: {orbit.Period().TotalHours:F2} hours");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Basic Example: Parse a TLE"
    internal static void Block04()
    {
        #region guide:4
        var tle = new TLE("ISS (ZARYA)",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        // Propagate to a specific time
        var epoch = new Time(2021, 1, 21, 12, 0, 0, TimeScale.TDB);
        var sv = tle.ToStateVector(epoch);

        Console.WriteLine($"ISS Position: {sv.Position}");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "SpiceAPI"
    internal static void Block05()
    {
        #region guide:5
        // Load all kernels from a directory
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo("kernels/"));

        // Check loaded kernels
        foreach (var kernel in SpiceAPI.Instance.GetLoadedKernels())
            Console.WriteLine(kernel);
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Configuration"
    internal static void Block06()
    {
        #region guide:6
        // Use memory data provider for testing
        Configuration.Instance.SetDataProvider(new MemoryDataProvider());
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Time"
    internal static void Block07()
    {
        #region guide:7
        // Create times
        var t1 = new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB);
        var t2 = Time.Parse("2024-06-21T12:00:00.000 UTC");
        var t3 = Time.CreateTDB(788400000.0);  // Seconds from J2000

        // Convert between time scales
        var tdb = t1.ToTDB();
        var utc = tdb.ToUTC();

        // Arithmetic
        var t4 = t1.AddDays(1.0);
        var duration = t4 - t1;  // TimeSpan
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Window"
    internal static void Block08()
    {
        #region guide:8
        var window = new Window(
            new Time(2024, 1, 1, TimeScale.TDB),
            new Time(2024, 1, 31, TimeScale.TDB)
        );

        Console.WriteLine($"Duration: {window.Length.TotalDays} days");

        // Check intersection
        var t = new Time(2024, 1, 15, TimeScale.TDB);
        if (window.Intersects(t))
            Console.WriteLine("Time is within window");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "CelestialBody"
    internal static void Block09()
    {
        #region guide:9
        // Use predefined bodies
        var earth = PlanetsAndMoons.EARTH_BODY;
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var sun = new CelestialBody(Stars.Sun);

        // Get ephemeris
        var state = earth.GetEphemeris(Time.J2000TDB, sun, Frame.ICRF, Aberration.None);
        var sv = state.ToStateVector();

        // Compute geosynchronous orbit
        var geoOrbit = earth.GeosynchronousOrbit(0.0, 0.0, new Time(2024, 1, 1, TimeScale.TDB));

        // Find solar eclipses: the Sun (this item) behind the Moon, seen from the Earth's center
        var eclipses = sun.FindWindowsOnOccultationConstraint(
            new Window(new Time(2024, 1, 1, TimeScale.TDB), new Time(2024, 12, 31, TimeScale.TDB)),
            earth, ShapeType.Ellipsoid, moon, ShapeType.Ellipsoid,
            OccultationType.Any, Aberration.None, TimeSpan.FromHours(1));
        #endregion
    }

    // DEVELOPER_GUIDE.md, "StateVector"
    internal static void Block10()
    {
        #region guide:10
        var earth = PlanetsAndMoons.EARTH_BODY;
        var epoch = new Time(2024, 1, 1, TimeScale.TDB);

        // Create state vector
        var sv = new StateVector(
            new Vector3(6800000.0, 0.0, 0.0),      // Position (m)
            new Vector3(0.0, 8000.0, 0.0),         // Velocity (m/s)
            earth, epoch, Frame.ICRF
        );

        // Compute orbital elements
        Console.WriteLine($"Semi-major axis: {sv.SemiMajorAxis():F0} m");
        Console.WriteLine($"Eccentricity: {sv.Eccentricity():F6}");
        Console.WriteLine($"Inclination: {sv.Inclination() * Constants.Rad2Deg:F2}°");
        Console.WriteLine($"Period: {sv.Period().TotalMinutes:F2} min");

        // Convert to Keplerian elements
        var kep = sv.ToKeplerianElements();

        // Transform to different frame
        var svEcliptic = sv.ToFrame(Frame.ECLIPTIC_J2000);
        #endregion
    }

    // DEVELOPER_GUIDE.md, "KeplerianElements"
    internal static void Block11()
    {
        #region guide:11
        var earth = PlanetsAndMoons.EARTH_BODY;
        var epoch = new Time(2024, 1, 1, TimeScale.TDB);

        // Create osculating Keplerian elements (ISS-like orbit)
        var kep = new KeplerianElements(
            6800000.0,                           // Semi-major axis (m)
            0.001,                               // Eccentricity
            51.6 * Constants.Deg2Rad,            // Inclination
            100.0 * Constants.Deg2Rad,           // RAAN
            90.0 * Constants.Deg2Rad,            // Argument of periapsis
            0.0,                                 // Mean anomaly (at perigee)
            earth,                               // Observer: the center of motion
            epoch,
            Frame.ICRF
        );

        // Get state at later time
        var futureState = kep.ToStateVector(epoch.AddHours(1.5));

        // Propagate using 2-body mechanics
        var laterKep = kep.AtEpoch(epoch.AddDays(1));

        // Create mean Keplerian elements from OMM data
        // FromOMM accepts OMM native units: rev/day for mean motion, degrees for angles
        var meanKep = KeplerianElements.FromOMM(
            meanMotion: 15.49309423,             // rev/day
            eccentricity: 0.0000493,
            inclination: 51.6423,                // degrees
            raan: 353.0312,                      // degrees
            argumentOfPeriapsis: 320.8755,       // degrees
            meanAnomaly: 39.2360,                // degrees
            observer: earth,
            epoch: epoch,
            frame: Frame.TEME
        );

        Console.WriteLine($"Elements type: {meanKep.ElementsType}"); // Mean
        // Note: Mean elements cannot be directly converted to StateVector
        // Use TLE.ToOsculating() or TLE.ToStateVector() for SGP4-based conversion
        #endregion
    }

    // DEVELOPER_GUIDE.md, "EquinoctialElements"
    internal static void Block12()
    {
        #region guide:12
        var sv = new StateVector(
            new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2, 1000.0),
            PlanetsAndMoons.EARTH_BODY, new Time(2024, 1, 1, TimeScale.TDB), Frame.ICRF);
        var equinoctial = sv.ToEquinoctial();

        // Access equinoctial components
        Console.WriteLine($"Ex: {equinoctial.EquinoctialEx()}");
        Console.WriteLine($"Ey: {equinoctial.EquinoctialEy()}");
        Console.WriteLine($"Lv: {equinoctial.Lv() * Constants.Rad2Deg}°");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "TLE"
    internal static void Block13()
    {
        #region guide:13
        // Parse TLE
        var tle = new TLE("ISS (ZARYA)",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        // TLE always stores mean elements
        Console.WriteLine($"Elements type: {tle.ElementsType}"); // Mean

        // Get osculating state at TLE epoch (SGP4 propagation)
        var sv = tle.ToOsculating();

        // Or propagate to specific time
        var now = new Time(2021, 1, 21, 12, 0, 0, TimeScale.TDB);
        var svAtTime = tle.ToStateVector(now);

        // Access TLE mean parameters
        Console.WriteLine($"NORAD ID: {tle.NoradCatalogId}");
        Console.WriteLine($"Inclination: {tle.MeanInclination * Constants.Rad2Deg:F4}°");
        Console.WriteLine($"Eccentricity: {tle.MeanEccentricity:F7}");

        // Get mean Keplerian elements (preserves mean motion precision)
        var meanKep = tle.ToMeanKeplerianElements();
        Console.WriteLine($"Mean motion: {meanKep.MeanMotion()} rad/s");

        // Create TLE from state vector
        var epoch = new Time(2024, 1, 1, TimeScale.TDB);
        var state = new StateVector(
            new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 5413.8, 5413.8),
            PlanetsAndMoons.EARTH_BODY, epoch, Frame.ICRF);
        // The Configuration of IO.Astrodynamics.OrbitalParameters.TLE, not IO.Astrodynamics.Configuration
        var config = new IO.Astrodynamics.OrbitalParameters.TLE.Configuration(99999, "MY_SAT", "24001A");
        var newTle = state.ToTLE(config);

        // NORAD catalog numbers above 99999 use the Alpha-5 convention (up to 339999)
        var alpha5 = new IO.Astrodynamics.OrbitalParameters.TLE.Configuration(275544, "MY_SAT", "24001A");
        var alpha5Tle = state.ToTLE(alpha5);
        Console.WriteLine(alpha5Tle.Line1.Substring(2, 5)); // T5544
        Console.WriteLine(alpha5Tle.NoradCatalogId);        // 275544
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Omm"
    internal static void Block14()
    {
        #region guide:14
        // Load OMM from file
        var omm = Omm.LoadFromFile("satellite.omm", validateSchema: true, validateContent: true);

        Console.WriteLine($"Object: {omm.ObjectName}");
        Console.WriteLine($"COSPAR ID: {omm.ObjectId}");
        Console.WriteLine($"Epoch: {omm.Data.MeanElements.Epoch}");
        Console.WriteLine($"Mean Motion: {omm.Data.MeanElements.MeanMotion} rev/day");

        // Convert OMM to TLE for propagation
        if (omm.IsTleCompatible)
        {
            var tle = omm.ToTle();
            var stateVector = tle.ToStateVector();  // Propagate with SGP4
            Console.WriteLine($"Position: {stateVector.Position}");
        }

        // Create OMM programmatically
        var newOmm = Omm.CreateForTle(
            objectName: "MY_SATELLITE",
            objectId: "2024-001A",
            epoch: DateTime.UtcNow,
            meanMotion: 15.5,           // rev/day
            eccentricity: 0.001,
            inclination: 51.6,          // degrees
            raan: 100.0,                // degrees
            argOfPericenter: 90.0,      // degrees
            meanAnomaly: 0.0,           // degrees
            bstar: 0.0001,
            meanMotionDot: 0.00001,
            meanMotionDDot: 0.0
        );

        // Save to file
        newOmm.SaveToFile("output.omm", validateBeforeSave: true);
        #endregion
    }

    // DEVELOPER_GUIDE.md, "TLE.ToOmm() - Convert TLE to OMM"
    internal static void Block15()
    {
        #region guide:15
        // Parse TLE
        var tle = new TLE("ISS (ZARYA)",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");

        // Convert to OMM
        var omm = tle.ToOmm(originator: "My Organization");

        // Save to file for sharing/archiving
        omm.SaveToFile("iss.omm");

        // Or get as XML string
        var xml = omm.ToXml();
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Round-Trip Conversion"
    internal static void Block16()
    {
        #region guide:16
        // Load OMM
        var originalOmm = Omm.LoadFromFile("satellite.omm", validateSchema: false);

        // Convert to TLE
        var tle = originalOmm.ToTle();

        // Use TLE for propagation
        var sv = tle.ToStateVector(new Time(2024, 6, 15, 12, 0, 0, TimeScale.TDB));

        // Convert back to OMM
        var newOmm = tle.ToOmm();

        // Save new OMM
        newOmm.SaveToFile("satellite_updated.omm");

        // Note: Due to TLE precision limitations, some values may differ slightly
        // from the original OMM (typically 4-6 decimal places for angles)
        #endregion
    }

    // DEVELOPER_GUIDE.md, "OmmWriter / OmmReader"
    internal static void Block17()
    {
        #region guide:17
        // OmmReader for parsing
        var reader = new OmmReader();
        var omm = reader.ReadFromFile("input.omm");

        // OmmWriter for custom output options
        var writer = new OmmWriter
        {
            WrapInNdmContainer = true,  // Wrap in <ndm> element
            IndentOutput = true         // Pretty-print XML
        };

        var xml = writer.WriteToString(omm);
        writer.WriteToFile(omm, "output.omm");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "OmmValidator"
    internal static void Block18()
    {
        var omm = new TLE("ISS (ZARYA)",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703").ToOmm();
        #region guide:18
        // omm: an Omm, loaded or converted as above (tle.ToOmm())
        var validator = new OmmValidator();
        var result = validator.Validate(omm);

        if (result.IsValid)
        {
            Console.WriteLine("OMM is valid");
        }
        else
        {
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"Error at {error.Path}: {error.Message}");
            }
        }

        // Warnings are also available
        foreach (var warning in result.Warnings)
        {
            Console.WriteLine($"Warning: {warning.Message}");
        }
        #endregion
    }

    // DEVELOPER_GUIDE.md, "COSPAR ID Format Conversion"
    internal static void Block19()
    {
        #region guide:19
        // OMM automatically converts COSPAR ID format during ToTle()
        var omm = Omm.LoadFromFile("iss.omm");  // ObjectId: "1998-067A"
        var tle = omm.ToTle();                   // Uses "98067A" internally

        // TLE.ToOmm() converts back to full format
        var newOmm = tle.ToOmm();                // ObjectId: "1998-067A"
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Opm"
    internal static void Block20()
    {
        #region guide:20
        // Load OPM from file
        var opm = Opm.LoadFromFile("spacecraft.opm", validateSchema: true, validateContent: true);

        Console.WriteLine($"Object: {opm.ObjectName}");
        Console.WriteLine($"COSPAR ID: {opm.ObjectId}");
        Console.WriteLine($"Epoch: {opm.Data.StateVector.Epoch}");
        Console.WriteLine($"Position: {opm.Data.StateVector.X}, {opm.Data.StateVector.Y}, {opm.Data.StateVector.Z} km");

        // Convert to framework StateVector
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var stateVector = opm.ToStateVector(earth);  // Units: m, m/s

        // Convert to Spacecraft for mission operations
        var spacecraft = opm.ToSpacecraft(
            naifId: -1000,
            maxOperatingMass: 500000.0,
            clock: new Clock("OnboardClock", 256),
            observer: earth);
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Spacecraft.ToOpm() - Convert Spacecraft to OPM"
    internal static void Block21(Clock clock, StateVector stateVector)
    {
        #region guide:21
        // clock: a Clock; stateVector: the initial state (an ISS-like orbit)
        // Create spacecraft and propagate with maneuvers
        var spacecraft = new Spacecraft(-1000, "ISS", 420000.0, stateVector, 500000.0,
            clock, 1600.0, 2.2, "1998-067A", 1.5);

        // After propagation with executed maneuvers...
        var opm = spacecraft.ToOpm(
            originator: "Mission Control",
            epoch: null,                      // The initial epoch (or one within the propagation, #658)
            includeKeplerianElements: true,   // Include optional Keplerian elements
            includeSpacecraftParameters: true, // Include mass, drag, SRP
            includeManeuvers: true);          // Include the maneuvers ignited by the epoch

        // Save for archival/sharing
        opm.SaveToFile("iss_state.opm");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "OpmData Components"
    internal static void Block22(DateTime epoch, double x, double y, double z, double vx, double vy, double vz)
    {
        #region guide:22
        // epoch: the epoch of the state; x, y, z (km) and vx, vy, vz (km/s): its components
        // Create OPM with all components
        var header = CcsdsHeader.Create("My Organization");
        var metadata = new OpmMetadata("SAT-1", "2024-001A", "EARTH", "ICRF", "UTC");

        // State vector (required) - units: km, km/s
        var stateVector = new OpmStateVector(epoch, x, y, z, vx, vy, vz);

        // Keplerian elements (optional) - units: km, degrees
        var keplerianElements = OpmKeplerianElements.CreateWithTrueAnomaly(
            semiMajorAxis: 6878.137,   // km
            eccentricity: 0.001,
            inclination: 51.6,         // degrees
            raan: 120.0,               // degrees
            aop: 90.0,                 // degrees
            trueAnomaly: 45.0,         // degrees
            gm: 398600.4418);          // km³/s²

        // Covariance matrix (optional) - units: km², km²/s, km²/s²
        var covariance = new CovarianceMatrix(
            cxX: 1.0e-6, cyX: 0.0, cyY: 1.0e-6, czX: 0.0, czY: 0.0, czZ: 1.0e-6,
            cxDotX: 0.0, cxDotY: 0.0, cxDotZ: 0.0, cxDotXDot: 1.0e-9,
            cyDotX: 0.0, cyDotY: 0.0, cyDotZ: 0.0, cyDotXDot: 0.0, cyDotYDot: 1.0e-9,
            czDotX: 0.0, czDotY: 0.0, czDotZ: 0.0, czDotXDot: 0.0, czDotYDot: 0.0, czDotZDot: 1.0e-9,
            referenceFrame: "ICRF");

        // Maneuver parameters (optional) - units: s, kg, km/s
        var maneuver = new OpmManeuverParameters(
            manEpochIgnition: epoch.AddHours(2),
            manDuration: 120.0,        // seconds
            manDeltaMass: -30.0,       // kg (negative = expelled)
            manRefFrame: "RSW",
            manDv1: 0.05,              // km/s (radial)
            manDv2: 0.0,               // km/s (along-track)
            manDv3: 0.01);             // km/s (cross-track)

        // User-defined parameters (optional)
        var userParams = new OpmUserDefinedParameters(
            new Dictionary<string, string>
            {
                ["MISSION_ID"] = "MISSION-001",
                ["OPERATOR"] = "Space Agency"
            });

        // Assemble OPM data
        var data = new OpmData(
            stateVector: stateVector,
            keplerianElements: keplerianElements,
            mass: 2000.0,                         // kg
            dragArea: 30.0,                       // m²
            dragCoefficient: 2.2,
            solarRadiationPressureArea: 35.0,     // m²
            solarRadiationCoefficient: 1.5,
            covariance: covariance,
            maneuvers: new[] { maneuver },
            userDefinedParameters: userParams);

        var opm = new Opm(header, metadata, data);
        opm.SaveToFile("complete_opm.xml");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "ImpulseManeuver.ToOpmManeuverParameters()"
    internal static void Block23(Spacecraft spacecraft)
    {
        #region guide:23
        // spacecraft: a Spacecraft propagated with impulse maneuvers
        // After spacecraft propagation with executed maneuvers
        foreach (var maneuver in spacecraft.ExecutedManeuvers)
        {
            if (maneuver is ImpulseManeuver impulseManeuver && impulseManeuver.ThrustWindow.HasValue)
            {
                var opmManeuver = impulseManeuver.ToOpmManeuverParameters(
                    referenceFrame: "EME2000",
                    comments: new[] { "Orbit raising maneuver" });

                Console.WriteLine($"Epoch: {opmManeuver.ManEpochIgnition}");
                Console.WriteLine($"Duration: {opmManeuver.ManDuration} s");
                Console.WriteLine($"Delta-V: {opmManeuver.DeltaVMagnitude:F6} km/s");
                Console.WriteLine($"Delta-Mass: {opmManeuver.ManDeltaMass} kg");
            }
        }
        #endregion
    }

    // DEVELOPER_GUIDE.md, "OpmValidator"
    internal static void Block24(Opm opm)
    {
        #region guide:24
        // opm: an Opm, loaded or built as above
        var validator = new OpmValidator();
        var result = validator.Validate(opm);

        if (result.IsValid)
        {
            Console.WriteLine("OPM is valid");
        }
        else
        {
            foreach (var error in result.Errors)
            {
                Console.WriteLine($"Error at {error.Path}: {error.Message}");
            }
        }
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Frame"
    internal static void Block25()
    {
        #region guide:25
        // Use predefined frames
        var icrf = Frame.ICRF;
        var ecliptic = Frame.ECLIPTIC_J2000;

        // Create body-fixed frame
        var earthFrame = new Frame("IAU_EARTH");

        // Transform state vector
        var svICRF = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2, 0.0),
            PlanetsAndMoons.EARTH_BODY, Time.J2000TDB, icrf);
        var svEcliptic = svICRF.ToFrame(ecliptic);

        // Get frame transformation
        var rotation = icrf.ToFrame(ecliptic, Time.J2000TDB);
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Site"
    internal static void Block26()
    {
        #region guide:26
        var earth = PlanetsAndMoons.EARTH_BODY;

        // Create site from SPICE data (DSS-13 Goldstone)
        var goldstone = new Site(13, "DSS-13", earth);

        // Create site with explicit coordinates
        var mySite = new Site(100, "MySite", earth,
            new Planetodetic(
                longitude: -117.0 * Constants.Deg2Rad,
                latitude: 34.0 * Constants.Deg2Rad,
                altitude: 1000.0  // meters
            ));

        // Get horizontal coordinates to Moon
        var epoch = new Time(2024, 1, 1, 12, 0, 0, TimeScale.TDB);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var horizontal = mySite.GetHorizontalCoordinates(epoch, moon, Aberration.LT);

        Console.WriteLine($"Azimuth: {horizontal.Azimuth * Constants.Rad2Deg:F2}°");
        Console.WriteLine($"Elevation: {horizontal.Elevation * Constants.Rad2Deg:F2}°");
        Console.WriteLine($"Range: {horizontal.Range / 1000:F0} km");

        // Find visibility windows
        var windows = mySite.FindWindowsOnDistanceConstraint(
            new Window(new Time(2024, 1, 1, TimeScale.TDB), new Time(2024, 1, 2, TimeScale.TDB)),
            moon, RelationnalOperator.Lower, 400000000, Aberration.LT, TimeSpan.FromMinutes(10));
        // One window, 2024-01-01 09:42:24.997 to 14:20:43.029 TDB, as CSPICE's spkcpt_c corrects the site (#489)
        #endregion
    }

    // DEVELOPER_GUIDE.md, "LaunchSite"
    internal static void Block27()
    {
        #region guide:27
        var earth = PlanetsAndMoons.EARTH_BODY;
        var launchSite = new LaunchSite(100, "MyLaunchSite", earth,
            new Planetodetic(-81.0 * Constants.Deg2Rad, 28.5 * Constants.Deg2Rad, 0.0),
            new AzimuthRange(45 * Constants.Deg2Rad, 135 * Constants.Deg2Rad));

        if (launchSite.IsAzimuthAllowed(90 * Constants.Deg2Rad))
            Console.WriteLine("East launch is allowed");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Planetodetic"
    internal static void Block28()
    {
        #region guide:28
        // Create geodetic coordinates
        var coords = new Planetodetic(
            longitude: -122.0 * Constants.Deg2Rad,
            latitude: 37.0 * Constants.Deg2Rad,
            altitude: 100.0
        );
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Equatorial"
    internal static void Block29()
    {
        #region guide:29
        // Get equatorial coordinates
        var earth = PlanetsAndMoons.EARTH_BODY;
        var epoch = new Time(2024, 1, 1, 12, 0, 0, TimeScale.TDB);
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var sv = moon.GetEphemeris(epoch, earth, Frame.ICRF, Aberration.None);
        var eq = sv.ToEquatorial();

        Console.WriteLine($"RA: {eq.RightAscension * Constants.Rad2Deg:F4}°");
        Console.WriteLine($"Dec: {eq.Declination * Constants.Rad2Deg:F4}°");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Spacecraft"
    internal static (Spacecraft Spacecraft, PropagationSolution Solution, KeplerianElements Orbit, Time Epoch) Block30()
    {
        #region guide:30
        var earth = PlanetsAndMoons.EARTH_BODY;
        var epoch = new Time(2024, 1, 1, TimeScale.TDB);

        // Define initial orbit
        var orbit = new KeplerianElements(
            7000000, 0.001, 51.6 * Constants.Deg2Rad, 0, 0, 0,
            earth, epoch, Frame.ICRF
        );

        // Create spacecraft
        var clock = new Clock("MainClock", 256);
        var spacecraft = new Spacecraft(-1001, "MySat", 500.0, orbit, 1000.0, clock);

        // Add components: the tank first, then the engine that draws on it (AddEngine requires its tank on the spacecraft);
        // each Add returns its argument, the model and the serial number optional
        var tank = spacecraft.AddFuelTank(new FuelTank("MainTank", 200.0, 100.0));  // capacity, initial quantity (kg)
        var engine = spacecraft.AddEngine(new Engine("MainEngine", 300.0, 0.05, tank));  // ISP (s), fuel flow (kg/s)

        // Add instrument: its NAIF id by default the first free NaifId × 1000 − n, -1001001, and no model
        spacecraft.AddCircularInstrument("Camera",
            10 * Constants.Deg2Rad,              // FOV
            new Vector3(1, 0, 0),                // Boresight
            new Vector3(0, 1, 0));               // Reference vector; the orientation zero by default

        // Propagate (returns PropagationSolution for dense output). Velocity-Verlet is of the second order: its error grows as
        // the square of the step (about 120 m after 12 h in this LEO at 1 s, 450 km at 60 s); without an integrator the
        // options run RK7(8) at 1e-9
        var solution = spacecraft.Propagate(
            new Window(epoch, epoch.AddDays(1)),
            new PropagationOptions
            {
                Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1) },
                OutputStep = TimeSpan.FromSeconds(1)
            }
        );

        // The state at any epoch within the propagation window, in its frame about its central body: position (m) and velocity (m/s)
        var halfDay = solution.GetStateVector(epoch.AddHours(12));

        // Read the stored states relative to an observer, in ICRF without correction: every minute over the first four
        var states = spacecraft.GetEphemeris(new Window(epoch, epoch.AddMinutes(4)), earth, TimeSpan.FromMinutes(1));
        foreach (var sv in states)
        {
            Console.WriteLine($"{sv.Epoch}: {sv.Position.Magnitude():F0} m");
        }
        #endregion
        return (spacecraft, solution, orbit, epoch);
    }

    // DEVELOPER_GUIDE.md, "Attitude maneuvers"
    internal static void Block31(CelestialBody earth, CelestialBody moon, CelestialBody sun, Instrument camera, Engine engine, Time minimumEpoch, Time epoch, TimeSpan holdDuration, TimeSpan duration)
    {
        #region guide:31
        // earth, moon, sun: CelestialBody; camera: an Instrument and engine an Engine of the spacecraft;
        // minimumEpoch, epoch: Time; holdDuration, duration: TimeSpan
        // Point spacecraft Front at Moon, keep Up toward Sun
        var attitude = new TriadAttitude(
            minimumEpoch, TimeSpan.FromMinutes(30),
            Spacecraft.Front, moon,    // Primary: Front points at Moon
            Spacecraft.Up, sun);

        // Using instruments
        var cameraAttitude = new TriadAttitude(
            minimumEpoch, holdDuration,
            camera,                    // Uses camera boresight (primary) and refVector (secondary)
            earth, sun);

        // Using IAttitudeTarget (orbital directions and/or celestial bodies)
        var targetAttitude = new TriadAttitude(
            earth, epoch, duration,
            Spacecraft.Front, OrbitalDirectionTarget.Prograde,       // Primary: prograde pointing
            Spacecraft.Up, new CelestialAttitudeTarget(sun));

        // Factory methods for common operational attitudes, on the axes of the plan's spacecraft
        var lvlh = TriadAttitude.CreateLVLH(earth, epoch, duration);
        var sunTrack = TriadAttitude.CreateProgradeWithSunTracking(earth, epoch, duration, sun);
        #endregion
    }

    // DEVELOPER_GUIDE.md, "LambertSolver"
    internal static void Block32()
    {
        #region guide:32
        var earth = PlanetsAndMoons.EARTH_BODY;
        var epoch = new Time(2024, 1, 1, TimeScale.TDB);

        // Define departure and arrival states: a LEO, and a point of a higher circular orbit 45 minutes later
        var departure = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2, 0.0),
            earth, epoch, Frame.ICRF);
        var arrival = new StateVector(new Vector3(-6000000.0, 4000000.0, 0.0), new Vector3(-4124.0, -6187.0, 0.0),
            earth, epoch.AddMinutes(45.0), Frame.ICRF);

        // Solve Lambert problem
        var solver = new LambertSolver();
        var result = solver.Solve(false, departure, arrival, earth, 0);

        // Get zero-revolution solution
        var solution = result.GetZeroRevolutionSolution();
        Console.WriteLine($"Delta-V at departure: {solution.DeltaV1.Magnitude():F1} m/s");
        Console.WriteLine($"Delta-V at arrival: {solution.DeltaV2.Magnitude():F1} m/s");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "CentralBodyPropagator"
    internal static void Block33()
    {
        #region guide:33
        var epoch = new Time(2024, 1, 1, TimeScale.TDB);
        var clock = new Clock("Clock", 256);

        // Create Earth with degree-10 EGM2008 geopotential
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

        // Initial state in ICRF
        var orbit = new StateVector(
            new Vector3(6800000.0, 0, 0),
            new Vector3(0, 7500.0, 0),
            earth, epoch, Frame.ICRF);

        var spacecraft = new Spacecraft(-1001, "Sat", 100.0, orbit, 10000.0, clock);

        // Propagate with geopotential, Moon, and Sun perturbations
        var propagator = new CentralBodyPropagator(
            new Window(epoch, epoch.AddDays(1)),
            spacecraft,
            new PropagationOptions
            {
                Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) },
                OutputStep = TimeSpan.FromSeconds(1.0),
                ThirdBodies = [PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY]
            });
        var solution = propagator.Propagate();

        // Use dense output for sub-step reads
        var middle = solution.GetStateVector(epoch.AddHours(12));
        #endregion
    }

    // DEVELOPER_GUIDE.md, "GeopotentialGravitationalField"
    internal static void Block34()
    {
        #region guide:34
        var epoch = new Time(2024, 1, 1, TimeScale.TDB);

        // Degree-10 geopotential (good balance of accuracy and speed)
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

        // Degree-2 (J2-only) for quick analysis
        var earthJ2 = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 2));

        // Point-mass only (no geopotential)
        var earthPointMass = new CelestialBody(PlanetsAndMoons.EARTH);
        #endregion
    }

    // DEVELOPER_GUIDE.md, "EarthStandardAtmosphere"
    internal static void Block35()
    {
        #region guide:35
        var model = new EarthStandardAtmosphere();
        var context = AtmosphericContext.FromAltitude(10000);  // 10 km

        // Get individual values
        var temp = model.GetTemperature(context);     // °C
        var pressure = model.GetPressure(context);    // kPa
        var density = model.GetDensity(context);      // kg/m³

        // Or get complete atmospheric data
        var atmosphere = model.GetAtmosphere(context);
        Console.WriteLine($"T={atmosphere.Temperature:F1}°C, P={atmosphere.Pressure:F3} kPa, ρ={atmosphere.Density:E3} kg/m³");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Nrlmsise00Model"
    internal static void Block36()
    {
        #region guide:36
        // Use nominal conditions
        var model = new Nrlmsise00Model();

        // Or specify space weather
        var spaceWeather = new SpaceWeather { F107 = 150, F107A = 150, Ap = 4 };
        var weatherModel = new Nrlmsise00Model(spaceWeather);

        // Or use preset conditions
        var solarMinModel = new Nrlmsise00Model(SpaceWeather.SolarMinimum);
        var solarMaxModel = new Nrlmsise00Model(SpaceWeather.SolarMaximum);

        // Full context required
        var context = AtmosphericContext.FromPlanetodetic(
            new Planetodetic(0, 0, 400000),  // prime meridian, equator, 400 km
            new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB));

        var atmosphere = model.GetAtmosphere(context);
        Console.WriteLine($"Density at 400 km: {atmosphere.Density:E3} kg/m³");

        // Access model-specific details via pattern matching
        if (atmosphere.Details is NrlmsiseDetails details)
        {
            Console.WriteLine($"Atomic Oxygen: {details.AtomicOxygenDensity:E3} m⁻³");
            Console.WriteLine($"Molecular Nitrogen: {details.NitrogenDensity:E3} m⁻³");
            Console.WriteLine($"Exospheric Temp: {details.ExosphericTemperature:F0} K");
        }
        #endregion
    }

    // DEVELOPER_GUIDE.md, "MarsStandardAtmosphere"
    internal static void Block37()
    {
        #region guide:37
        var model = new MarsStandardAtmosphere();
        var context = AtmosphericContext.FromAltitude(50000);  // 50 km
        var atmosphere = model.GetAtmosphere(context);
        Console.WriteLine($"Mars atmosphere: ρ={atmosphere.Density:E3} kg/m³");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "CelestialBody Integration"
    internal static void Block38()
    {
        #region guide:38
        var earth = PlanetsAndMoons.EARTH_BODY;

        // Simple: altitude-only; EARTH_BODY has no model, so NaN, 0 and 0 (give the body an EarthStandardAtmosphere for US 1976)
        var atm1 = earth.GetAtmosphere(10000);  // 10 km altitude

        // Full context: NRLMSISE-00 at nominal space weather for an Earth without a model
        var context = AtmosphericContext.FromPlanetodetic(
            new Planetodetic(-75 * Constants.Deg2Rad, 45 * Constants.Deg2Rad, 400000),  // longitude, latitude (rad), altitude (m)
            new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB));
        var atm2 = earth.GetAtmosphere(context);

        // Access NRLMSISE-00 specific data
        if (atm2.Details is NrlmsiseDetails details)
        {
            Console.WriteLine($"O₂ density: {details.MolecularOxygenDensity:E3} m⁻³");
        }
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Vector3"
    internal static void Block39()
    {
        #region guide:39
        var v1 = new Vector3(1, 0, 0);
        var v2 = new Vector3(0, 1, 0);

        var cross = v1.Cross(v2);           // (0, 0, 1)
        var dot = v1.Dot(v2);               // 0
        var angle = v1.Angle(v2);           // π/2
        var sum = v1 + v2;                  // (1, 1, 0)
        var scaled = v1 * 2.0;              // (2, 0, 0)
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Tsiolkovski"
    internal static void Block40()
    {
        #region guide:40
        double isp = 300;          // seconds
        double m0 = 1000;          // kg initial
        double mf = 800;           // kg final

        double deltaV = Tsiolkovski.DeltaV(isp, m0, mf);
        Console.WriteLine($"Delta-V: {deltaV:F1} m/s");

        double fuelNeeded = Tsiolkovski.DeltaM(isp, m0, 500);  // 500 m/s delta-V
        Console.WriteLine($"Fuel needed: {fuelNeeded:F1} kg");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "MemoryDataProvider"
    internal static void Block41(Time epoch, StateVector earthState)
    {
        #region guide:41
        // epoch: Time; earthState: a StateVector of the Earth
        // For testing without SPICE kernels
        var memoryProvider = new MemoryDataProvider();
        Configuration.Instance.SetDataProvider(memoryProvider);

        memoryProvider.AddCelestialBodyInfo(new CelestialBodyInfo(NaifId: 399, Name: "EARTH", CenterOfMotionId: 10,
            BarycenterOfMotionId: 3, Radii: new Vector3(6378137.0, 6378137.0, 6356752.0), GM: 3.986004418e14,
            FrameName: "ITRF93", FrameId: 13000));
        memoryProvider.AddStateVector(399, epoch, earthState);
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Kernel Management"
    internal static void Block42()
    {
        #region guide:42
        // Load all required kernels at startup
        SpiceAPI.Instance.LoadKernels(new DirectoryInfo("kernels"));

        // Required kernel types:
        // - LSK (leap seconds): naif0012.tls
        // - SPK (ephemeris): de440.bsp, mar097.bsp
        // - PCK (planetary constants): pck00010.tpc
        // - FK (frames): earth_assoc_itrf93.tf

        // Clean up when done
        SpiceAPI.Instance.ClearKernels();
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Time Handling"
    internal static void Block43()
    {
        #region guide:43
        // The scale is a required argument: no calendar is TDB by default
        var tdbNoon = new Time(2024, 1, 1, 12, 0, 0, TimeScale.TDB);
        var utc = new Time(new DateTime(2024, 1, 1, 12, 0, 0), TimeScale.UTC);

        // Convert as needed
        var tdb = utc.ToTDB();
        var tai = utc.ToTAI();

        // Use TDB for dynamical calculations
        var earth = PlanetsAndMoons.EARTH_BODY;
        var sun = new CelestialBody(Stars.Sun);
        var state = earth.GetEphemeris(utc.ToTDB(), sun, Frame.ICRF, Aberration.None);
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Reference Frame Awareness"
    internal static void Block44()
    {
        #region guide:44
        // ICRF/J2000: Inertial frame for most calculations
        var sv = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2, 0.0),
            PlanetsAndMoons.EARTH_BODY, new Time(2024, 1, 1, TimeScale.TDB), Frame.ICRF);

        // Body-fixed frames for surface operations
        var earthFrame = new Frame("IAU_EARTH");
        var bodyFixed = sv.ToFrame(earthFrame);

        // TEME for TLE propagation (handled automatically)
        var tle = new TLE("ISS (ZARYA)",
            "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        var tleSv = tle.ToStateVector();  // Returns in TEME
        var icrfSv = tleSv.ToFrame(Frame.ICRF);
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Error Handling"
    internal static void Block45(CelestialItem body, Time epoch, ILocalizable observer, Frame frame)
    {
        #region guide:45
        // body: a CelestialItem; epoch: Time; observer: an ILocalizable; frame: a Frame
        try
        {
            var state = body.GetEphemeris(epoch, observer, frame, Aberration.None);
        }
        catch (SpiceException ex)
        {
            // An error SPICE signals (an InvalidOperationException): a kernel not loaded,
            // an epoch out of coverage; ShortMessage is SPICE's code, such as
            // SPICE(SPKINSUFFDATA), LongMessage its long message.
            Console.WriteLine($"Ephemeris error {ex.ShortMessage}: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            // Another failure of the native layer or of the library
            Console.WriteLine($"Ephemeris error: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            // Invalid parameters
            Console.WriteLine($"Parameter error: {ex.Message}");
        }
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Mission and Scenario Management"
    internal static void Block46()
    {
        #region guide:46
        // Create a mission
        var mission = new Mission("LunarExplorer");

        // Create a scenario with a time window
        var start = new Time(2024, 1, 1, TimeScale.TDB);
        var end = start.AddDays(7);
        var scenario = new Scenario("Phase1", mission, new Window(start, end));

        // Add celestial bodies to the scenario
        var earth = PlanetsAndMoons.EARTH_BODY;
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var sun = new CelestialBody(Stars.Sun);

        scenario.AddCelestialItem(earth);
        scenario.AddCelestialItem(moon);
        scenario.AddCelestialItem(sun);

        // Add ground stations
        var goldstone = new Site(13, "DSS-13", earth);
        var madrid = new Site(65, "DSS-65", earth);
        scenario.AddSite(goldstone);
        scenario.AddSite(madrid);
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Spacecraft Rendezvous Mission"
    internal static async Task Block47()
    {
        #region guide:47
        var earth = PlanetsAndMoons.EARTH_BODY;
        var start = new Time(2024, 3, 1, TimeScale.TDB);
        var end = start.AddHours(12);

        // Create mission and scenario
        var mission = new Mission("Rendezvous");
        var scenario = new Scenario("RendezvousPhase", mission, new Window(start, end));
        scenario.AddCelestialItem(earth);

        // Define target spacecraft orbit
        var targetOrbit = new StateVector(
            new Vector3(4390853.7, 5110607.0, 917659.9),
            new Vector3(-4979.5, 3033.3, 6933.2),
            earth, start, Frame.ICRF
        );

        // Define chaser spacecraft initial orbit (parking orbit)
        var chaserOrbit = new StateVector(
            new Vector3(5056554.2, 4395595.5, 0.0),
            new Vector3(-3708.6, 4266.3, 6736.9),
            earth, start, Frame.ICRF
        );

        // Create target spacecraft
        var targetClock = new Clock("TargetClock", 65536);
        var target = new Spacecraft(-1001, "Target", 500.0, targetOrbit, 1000.0, targetClock);
        scenario.AddSpacecraft(target);

        // Create chaser spacecraft with propulsion
        var chaserClock = new Clock("ChaserClock", 65536);
        var chaser = new Spacecraft(-1002, "Chaser", 1000.0, chaserOrbit, 10000.0, chaserClock);

        // Add fuel tank and engine
        var fuelTank = new FuelTank("MainTank", 9000.0, 9000.0, "Model1", "SN001");
        var engine = new Engine("MainEngine", 450.0, 50.0, fuelTank, "Model1", "SN001");
        chaser.AddFuelTank(fuelTank);
        chaser.AddEngine(engine);

        // The plan: plane alignment -> apsidal alignment -> phasing -> circularization
        var maneuver1 = new PlaneAlignmentManeuver(
            targetOrbit,
            engine,
            new Time(DateTime.MinValue, TimeScale.TDB),
            TimeSpan.Zero
        );

        chaser.SetManeuvers(maneuver1,
            new ApsidalAlignmentManeuver(
                targetOrbit,
                engine,
                new Time(DateTime.MinValue, TimeScale.TDB),
                TimeSpan.Zero),
            new PhasingManeuver(
                targetOrbit,
                1,
                engine,
                new Time(DateTime.MinValue, TimeScale.TDB)),
            new ApogeeHeightManeuver(
                targetOrbit.SemiMajorAxis(),
                engine,
                new Time(DateTime.MinValue, TimeScale.TDB),
                TimeSpan.Zero,
                earth));

        scenario.AddSpacecraft(chaser);

        // Run the simulation
        var summary = scenario.Simulate(new PropagationOptions
        {
            Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) },
            OutputStep = TimeSpan.FromSeconds(1.0)
        });

        // Read maneuver results
        var executedManeuver = chaser.Maneuvers[0];  // the first maneuver of the plan, its values those of the last run
        Console.WriteLine($"Maneuver 1 start: {executedManeuver.ManeuverWindow?.StartDate}");
        Console.WriteLine($"Maneuver 1 delta-V: {((ImpulseManeuver)executedManeuver).DeltaV.Magnitude():F1} m/s");
        Console.WriteLine($"Fuel burned: {executedManeuver.FuelBurned:F1} kg");
        Console.WriteLine($"Total fuel consumption: {summary.SpacecraftSummaries.Single(x => x.Spacecraft == chaser).FuelConsumption:F1} kg");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Lambert Transfer (Interplanetary or Lunar)"
    internal static void Block48()
    {
        #region guide:48
        var earth = PlanetsAndMoons.EARTH_BODY;
        var moon = new CelestialBody(PlanetsAndMoons.MOON);

        // Define departure state (LEO)
        var departureEpoch = Time.J2000TDB;
        var departureState = new StateVector(
            new Vector3(7000000.0, 0.0, 0.0),
            new Vector3(0.0, 8000.0, 0.0),
            earth, departureEpoch, Frame.ICRF
        );

        // Define arrival (Moon position in 3 days)
        var arrivalEpoch = departureEpoch.AddDays(3);
        var moonState = moon.GetEphemeris(arrivalEpoch, earth, Frame.ICRF, Aberration.None);

        // Solve Lambert's problem
        var solver = new LambertSolver();
        var result = solver.Solve(
            isRetrograde: false,
            departureState,
            moonState,
            earth,
            maxRevolution: 0
        );

        // Get the zero-revolution solution
        var solution = result.GetZeroRevolutionSolution();

        Console.WriteLine($"Departure velocity: {solution.V1.Magnitude():F1} m/s");
        Console.WriteLine($"Arrival velocity: {solution.V2.Magnitude():F1} m/s");
        Console.WriteLine($"Departure delta-V: {solution.DeltaV1.Magnitude():F1} m/s");
        Console.WriteLine($"Arrival delta-V: {solution.DeltaV2.Magnitude():F1} m/s");

        // Create the transfer trajectory
        var transferOrbit = new StateVector(
            departureState.Position,
            solution.V1,
            earth, departureEpoch, Frame.ICRF
        );

        // Verify arrival position
        var finalState = transferOrbit.AtEpoch(arrivalEpoch).ToStateVector();
        Console.WriteLine($"Position error: {(finalState.Position - moonState.Position).Magnitude():F1} m");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Launch Window Computation"
    internal static void Block49()
    {
        #region guide:49
        var earth = PlanetsAndMoons.EARTH_BODY;
        var searchStart = new Time(2024, 6, 2, TimeScale.TDB);

        // Create launch site (Kennedy Space Center)
        var launchSite = new LaunchSite(
            33,
            "KSC",
            earth,
            new Planetodetic(
                -81.0 * Constants.Deg2Rad,   // Longitude
                28.5 * Constants.Deg2Rad,    // Latitude
                0.0                          // Altitude
            ),
            new AzimuthRange(0.0, 2 * Math.PI)  // All azimuths allowed
        );

        // Create recovery site
        var recoverySite = new Site(34, "Recovery", earth,
            new Planetodetic(-81.0 * Constants.Deg2Rad, 28.5 * Constants.Deg2Rad, 0.0));

        // Define target orbit (ISS-like)
        var targetOrbit = new StateVector(
            new Vector3(-1.144E+06, 4.905E+06, 4.553E+06),
            new Vector3(-5.588E+03, -4.213E+03, 3.126E+03),
            earth, searchStart, Frame.ICRF
        );

        // Create launch scenario
        var launch = new Launch(
            launchSite,
            recoverySite,
            targetOrbit,
            twilight: Constants.CivilTwilight,  // Only launch during daylight
            launchByDay: true
        );

        // Search for launch windows over 24 hours
        var searchWindow = new Window(searchStart, TimeSpan.FromDays(1.0));
        var windows = launch.FindLaunchWindows(searchWindow).ToArray();

        foreach (var lw in windows)
        {
            Console.WriteLine($"Launch window: {lw.Window.StartDate}");
            Console.WriteLine($"  Inertial azimuth: {lw.InertialAzimuth * Constants.Rad2Deg:F2}°");
            Console.WriteLine($"  Non-inertial azimuth: {lw.NonInertialAzimuth * Constants.Rad2Deg:F2}°");
            Console.WriteLine($"  Insertion velocity: {lw.InertialInsertionVelocity:F1} m/s");
        }
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Geometry Finder: Occultations and Eclipses"
    internal static void Block50()
    {
        #region guide:50
        var earth = PlanetsAndMoons.EARTH_BODY;
        var moon = new CelestialBody(PlanetsAndMoons.MOON);
        var sun = new CelestialBody(Stars.Sun);

        // Search window: year 2024
        var searchWindow = new Window(
            new Time(2024, 1, 1, TimeScale.TDB),
            new Time(2024, 12, 31, TimeScale.TDB)
        );

        // Find lunar eclipses: the Moon (this item) behind the Earth, seen from the Sun
        var lunarEclipses = moon.FindWindowsOnOccultationConstraint(
            searchWindow,
            observer: sun,
            targetShape: ShapeType.Ellipsoid,
            frontBody: earth,
            frontShape: ShapeType.Ellipsoid,
            occultationType: OccultationType.Any,
            aberration: Aberration.None,
            stepSize: TimeSpan.FromHours(1)
        ).ToArray();

        Console.WriteLine($"Found {lunarEclipses.Length} lunar eclipses in 2024:");
        foreach (var eclipse in lunarEclipses)
        {
            Console.WriteLine($"  {eclipse.StartDate} to {eclipse.EndDate}");
            Console.WriteLine($"  Duration: {eclipse.Length.TotalMinutes:F1} minutes");
        }

        // Find distance constraint windows (Moon farther than 400,000 km from Earth)
        var farMoonWindows = earth.FindWindowsOnDistanceConstraint(
            searchWindow,
            moon,
            RelationnalOperator.Greater,
            400000000,  // 400,000 km in meters
            Aberration.None,
            TimeSpan.FromHours(24)
        ).ToArray();

        Console.WriteLine($"\nMoon > 400,000 km from Earth:");
        foreach (var w in farMoonWindows.Take(5))
        {
            Console.WriteLine($"  {w.StartDate.ToUTC()} to {w.EndDate.ToUTC()}");
        }
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Instrument Field of View Analysis"
    internal static async Task Block51()
    {
        #region guide:51
        var earth = PlanetsAndMoons.EARTH_BODY;
        var start = new Time(2024, 6, 10, TimeScale.TDB);
        var end = start.AddHours(2);

        // Create mission and scenario
        var mission = new Mission("EarthObservation");
        var scenario = new Scenario("Observation", mission, new Window(start, end));
        scenario.AddCelestialItem(earth);

        // Define spacecraft orbit
        var orbit = new StateVector(
            new Vector3(6800000.0, 0.0, 0.0),
            new Vector3(0.0, 7656.2, 0.0),
            earth, start, Frame.ICRF
        );

        // Create spacecraft with camera
        var clock = new Clock("Clock1", 65536);
        var spacecraft = new Spacecraft(-179, "Observer", 1000.0, orbit, 3000.0, clock);

        // Add camera instrument (circular FOV, 43° half-angle, pointing nadir)
        spacecraft.AddCircularInstrument(
            naifId: -179100,
            name: "MainCamera",
            model: "HighRes",
            fieldOfView: 0.75,                    // ~43° half-angle
            boresight: Vector3.VectorZ,           // Boresight along Z
            refVector: Vector3.VectorY,           // Reference vector
            orientation: new Vector3(0, Math.PI / 2, 0)  // Point toward nadir
        );

        scenario.AddSpacecraft(spacecraft);

        // Simulate
        scenario.Simulate(new PropagationOptions
        {
            Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) },
            OutputStep = TimeSpan.FromSeconds(1.0)
        });

        // Find windows when Earth is in the camera's field of view: any part of its ellipsoid, oriented by its frame, with the light time.
        // The spacecraft is propagated, so the library searches; the windows are those of CSPICE's gftfov, in TDB, to 1 µs
        var visibilityWindows = spacecraft.Instruments.First()
            .FindWindowsInFieldOfViewConstraint(
                new Window(start, end),
                spacecraft,
                earth,
                earth.Frame,
                ShapeType.Ellipsoid,
                Aberration.LT,
                TimeSpan.FromSeconds(60)
            ).ToArray();

        Console.WriteLine($"Earth visibility windows:");
        foreach (var window in visibilityWindows)
        {
            Console.WriteLine($"  {window.StartDate} to {window.EndDate}");
            Console.WriteLine($"  Duration: {window.Length.TotalSeconds:F0} seconds");
        }

        // Check if a specific target is in FOV at a given time
        bool isVisible = spacecraft.Instruments.First()
            .IsInFOV(start.AddMinutes(30), earth, Aberration.LT);
        Console.WriteLine($"Earth in FOV at T+30min: {isVisible}");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Export to Cosmographia"
    internal static async Task Block52(Time start, Time end)
    {
        #region guide:52
        // start, end: the window of the scenario
        // After running a scenario simulation...
        var mission = new Mission("Visualization");
        var scenario = new Scenario("Demo", mission, new Window(start, end));
        // ... add celestial items, spacecraft, run simulation ...
        scenario.Simulate(new PropagationOptions
        {
            Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) },
            OutputStep = TimeSpan.FromSeconds(1.0)
        });

        // Export to Cosmographia
        var exporter = new CosmographiaExporter();
        await exporter.ExportAsync(scenario, new DirectoryInfo("CosmographiaExport"));

        // This creates:
        // - SPK files for spacecraft trajectories
        // - CK files for spacecraft orientations
        // - FK files for reference frames
        // - JSON catalog for Cosmographia import
        #endregion
    }

    // DEVELOPER_GUIDE.md, "TLE-Based Spacecraft Tracking"
    internal static void Block53()
    {
        #region guide:53
        var earth = PlanetsAndMoons.EARTH_BODY;

        // Parse ISS TLE
        var issTLE = new TLE(
            "ISS (ZARYA)",
            "1 25544U 98067A   24153.17509025  .00020162  00000+0  35104-3 0  9990",
            "2 25544  51.6393  34.6631 0005642 260.2910 238.1766 15.50732314456064"
        );

        // Create spacecraft from TLE
        var clock = new Clock("ISSClock", 65536);
        var iss = new Spacecraft(-25544, "ISS", 420000.0, issTLE, 450000.0, clock);

        // Track from a ground station
        var goldstone = new Site(13, "DSS-13", earth);

        var trackStart = new Time(2024, 6, 3, TimeScale.TDB);
        var trackEnd = trackStart.AddDays(1);

        // Get ISS position relative to ground station over time
        Console.WriteLine("ISS tracking from Goldstone:");
        for (var t = trackStart; t < trackEnd; t = t.AddMinutes(10))
        {
            var horizontal = goldstone.GetHorizontalCoordinates(t, iss, Aberration.LT);

            if (horizontal.Elevation > 0)  // Above horizon
            {
                Console.WriteLine($"{t.ToUTC()}: " +
                    $"Az={horizontal.Azimuth * Constants.Rad2Deg:F1}° " +
                    $"El={horizontal.Elevation * Constants.Rad2Deg:F1}° " +
                    $"Range={horizontal.Range / 1000:F0} km");
            }
        }

        // Find visibility windows from ground station
        var visibilityWindows = goldstone.FindWindowsOnDistanceConstraint(
            new Window(trackStart, trackEnd),
            iss,
            RelationnalOperator.Lower,
            2000000,  // Within 2000 km
            Aberration.LT,
            TimeSpan.FromMinutes(1)
        );
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Deep Space Propagation with Multiple Perturbations"
    internal static async Task<(Spacecraft Spacecraft, StateVector MoonState, Time Start, Time End)> Block54()
    {
        #region guide:54
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, Time.J2000TDB);
        var sun = new CelestialBody(Stars.Sun);

        // The Moon moves about the Earth under the attraction of both masses, so the Earth's central term is GM_Earth + GM_Moon:
        // a field of degree 0 with the GM of the model is a point mass of that GM. With the Earth's GM alone, a massless probe
        // started at the Moon's state would end about 54 310 km from the Moon after 25 days
        double gmEarthMoon = PlanetsAndMoons.EARTH_BODY.GM + moon.GM;
        var earthMoonField = new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 0, gmEarthMoon, 6378136.3)
        {
            CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter
        };
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, Time.J2000TDB, earthMoonField);

        var start = Time.J2000TDB;
        var end = start.AddDays(25);

        var mission = new Mission("DeepSpaceTest");
        var scenario = new Scenario("LunarOrbit", mission, new Window(start, end));

        // Start at the Moon's position and velocity
        var moonState = moon.GetEphemeris(start, earth, Frame.ICRF, Aberration.None);

        var clock = new Clock("Clock", 256);
        var spacecraft = new Spacecraft(-1001, "Probe", 100.0, moonState, 10000.0, clock);

        scenario.AddSpacecraft(spacecraft);

        // Add gravitational perturbations from multiple bodies
        scenario.AddCelestialItem(sun);
        scenario.AddCelestialItem(earth);
        scenario.AddCelestialItem(new Barycenter(5));  // Jupiter barycenter
        scenario.AddCelestialItem(new Barycenter(6));  // Saturn barycenter

        // Propagate at a 5-minute step. Velocity-Verlet is of the second order: it ends about 470 m from an exact integration of
        // these forces after 25 days
        var summary = scenario.Simulate(new PropagationOptions
        {
            Integrator = new VVIntegratorOptions { Step = TimeSpan.FromMinutes(5) },
            OutputStep = TimeSpan.FromMinutes(5)
        });

        // Compare the propagated Moon with the Moon of the ephemeris (DE440)
        var propagatedMoon = spacecraft.GetEphemeris(end, earth, Frame.ICRF, Aberration.None);
        var ephemerisMoon = moon.GetEphemeris(end, earth, Frame.ICRF, Aberration.None);

        var positionDifference = (propagatedMoon.Position - ephemerisMoon.Position).Magnitude();
        var velocityDifference = (propagatedMoon.Velocity - ephemerisMoon.Velocity).Magnitude();

        Console.WriteLine($"Propagated Moon - DE440 Moon after 25 days:");
        Console.WriteLine($"  Position difference: {positionDifference:F1} m");
        Console.WriteLine($"  Velocity difference: {velocityDifference:F4} m/s");
        #endregion
        return (spacecraft, moonState, start, end);
    }

    // DEVELOPER_GUIDE.md, "High-Fidelity LEO Propagation with Geopotential"
    internal static void Block55()
    {
        #region guide:55
        var epoch = new Time(2025, 8, 25, 11, 55, 44, TimeScale.UTC);

        // Earth with degree-10 EGM2008 geopotential
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10));

        // LEO orbit (~400 km altitude)
        var orbit = new StateVector(
            new Vector3(5442162.59, -4068949.85, -13456.85),   // Position (m)
            new Vector3(2858.20, 3809.79, 6002.13),             // Velocity (m/s)
            earth, epoch, Frame.ICRF);

        var clock = new Clock("Clock", 256);
        var spacecraft = new Spacecraft(-1001, "LEOSat", 100.0, orbit, 10000.0, clock);

        var propagator = new CentralBodyPropagator(
            new Window(epoch, epoch.AddDays(1)),
            spacecraft,
            new PropagationOptions
            {
                Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) },
                OutputStep = TimeSpan.FromSeconds(1.0),
                ThirdBodies = [PlanetsAndMoons.MOON_BODY, Stars.SUN_BODY]
            });

        var solution = propagator.Propagate();

        // Get the final position and velocity from the dense output
        var final = solution.GetStateVector(epoch.AddDays(1));

        Console.WriteLine($"Position after 24h: ({final.Position.X/1000:F3}, " +
            $"{final.Position.Y/1000:F3}, {final.Position.Z/1000:F3}) km");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Earth Observation with Attitude Maneuvers"
    internal static async Task Block56()
    {
        #region guide:56
        var earth = PlanetsAndMoons.EARTH_BODY;
        var start = new Time(2024, 1, 1, TimeScale.TDB);
        var end = start.AddDays(1);

        var mission = new Mission("EarthObs");
        var scenario = new Scenario("TargetPointing", mission, new Window(start, end));
        scenario.AddCelestialItem(earth);

        // Define orbit
        var orbit = new KeplerianElements(
            11800000.0, 0.3, 1.0, 0.0, 0.0, 0.0,
            earth, start, Frame.ICRF
        );

        // Create spacecraft
        var clock = new Clock("Clock", 256);
        var spacecraft = new Spacecraft(-334, "Observer", 1000.0, orbit, 2000.0, clock);

        // Add antenna instrument
        spacecraft.AddCircularInstrument("Antenna",
            0.2, Vector3.VectorZ, Vector3.VectorY, Vector3.Zero, naifId: -334100, model: "HighGain");

        // Add propulsion
        var tank = new FuelTank("Tank1", 2000.0, 2000.0, "Model", "SN1");
        var engine = new Engine("Engine1", 450, 50.0, tank, "Model", "SN1");
        spacecraft.AddFuelTank(tank);
        spacecraft.AddEngine(engine);

        // Create ground target
        var groundStation = new Site(14, "DSS-14", earth);
        scenario.AddSite(groundStation);
        scenario.AddSpacecraft(spacecraft);

        // Configure attitude maneuver to point antenna at ground station
        var pointingManeuver = new InstrumentPointingToAttitude(
            start.AddHours(7.25),           // Start time
            TimeSpan.FromHours(0.5),        // Duration
            spacecraft.Instruments.First(), // Instrument to point
            groundStation
        );                                  // The direction is light-time corrected (Aberration.LT, the default, #549);
                                            // pass Aberration.None as a sixth argument for the geometric direction

        spacecraft.SetManeuvers(pointingManeuver);

        // Simulate
        scenario.Simulate(new PropagationOptions
        {
            Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) },
            OutputStep = TimeSpan.FromSeconds(1.0)
        });

        Console.WriteLine($"Pointing maneuver executed at: {pointingManeuver.ManeuverWindow?.StartDate}");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "TRIAD Attitude for Multi-Target Pointing"
    internal static async Task Block57()
    {
        #region guide:57
        var earth = PlanetsAndMoons.EARTH_BODY;
        var sun = new CelestialBody(Stars.Sun);
        var start = new Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB);
        var end = start.AddHours(2);

        var mission = new Mission("ObservationMission");
        var scenario = new Scenario("DualPointing", mission, new Window(start, end));
        scenario.AddCelestialItem(earth);
        scenario.AddCelestialItem(sun);

        // Create spacecraft in LEO
        var orbit = new KeplerianElements(
            6778000.0, 0.001, 51.6 * Constants.Deg2Rad, 0.0, 0.0, 0.0,
            earth, start, Frame.ICRF
        );

        var clock = new Clock("Clock", 256);
        var spacecraft = new Spacecraft(-500, "Observer", 500.0, orbit, 1000.0, clock);

        // Add Earth observation camera (points down via boresight)
        spacecraft.AddCircularInstrument("EarthCam",
            0.5, Vector3.VectorZ.Inverse(), Vector3.VectorY, Vector3.Zero, naifId: -500100, model: "NadirCamera");  // Boresight: -Z (nadir), refVector: +Y

        // Add propulsion
        var tank = new FuelTank("Tank1", 100.0, 100.0, "Model", "SN1");
        var engine = new Engine("Engine1", 220, 10.0, tank, "Model", "SN1");
        spacecraft.AddFuelTank(tank);
        spacecraft.AddEngine(engine);

        scenario.AddSpacecraft(spacecraft);

        // Configure TRIAD attitude:
        // - Primary: Spacecraft Down (-Z) points at Earth (nadir)
        // - Secondary: Spacecraft Right (+X) oriented toward Sun, which sets the roll about nadir (a vector at 5 degrees at
        //   least from the primary: Up, opposite to Down, would be rejected)
        var triadManeuver = new TriadAttitude(
            start,                          // Start time
            TimeSpan.FromHours(1.5),        // Hold duration
            Spacecraft.Down, earth,         // Primary constraint: nadir pointing
            Spacecraft.Right, sun
        );

        spacecraft.SetManeuvers(triadManeuver);

        // Simulate with high time resolution for attitude dynamics
        scenario.Simulate(new PropagationOptions
        {
            Integrator = new VVIntegratorOptions { Step = TimeSpan.FromSeconds(1.0) },
            OutputStep = TimeSpan.FromSeconds(1.0)
        });

        Console.WriteLine($"TRIAD maneuver executed at: {triadManeuver.ManeuverWindow?.StartDate}");
        Console.WriteLine("Spacecraft maintains nadir pointing with roll constrained toward Sun");
        #endregion
    }

    // DEVELOPER_GUIDE.md, "TRIAD Attitude for Multi-Target Pointing"
    internal static void Block58(CelestialBody earth, CelestialBody sun, Engine engine, Time epoch, TimeSpan duration)
    {
        #region guide:58
        // earth: the CelestialBody of the orbit; sun: the Sun; engine: an Engine of the spacecraft; epoch: Time; duration: TimeSpan
        // LVLH attitude using factory method (the spacecraft's BodyDown to nadir, its BodyFront toward prograde)
        var lvlh = TriadAttitude.CreateLVLH(earth, epoch, duration);

        // Prograde with sun tracking using factory method
        var sunTrack = TriadAttitude.CreateProgradeWithSunTracking(earth, epoch, duration, sun);

        // Custom: nadir pointing with normal constraint (using OrbitalDirectionTarget)
        var customLvlh = new TriadAttitude(
            earth, epoch, duration,
            Spacecraft.Down, OrbitalDirectionTarget.Nadir,
            Spacecraft.Front, OrbitalDirectionTarget.Prograde);
        #endregion
    }

    // DEVELOPER_GUIDE.md, "Recommended Validation Approach"
    internal static void Block59(CelestialBody moon, CelestialBody earth, Time epoch)
    {
        #region guide:59
        // moon, earth: CelestialBody; epoch: Time
        // Example: Validate against JPL Horizons for critical calculations
        var computed = moon.GetEphemeris(epoch, earth, Frame.ICRF, Aberration.None);
        // Compare computed.Position with Horizons data
        // Acceptable tolerance depends on your application requirements
        #endregion
    }
}
