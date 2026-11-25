using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.OPM;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using StateOrientation = IO.Astrodynamics.OrbitalParameters.StateOrientation;


namespace IO.Astrodynamics.Body.Spacecraft
{
    public class Spacecraft : CelestialItem, IOrientable<SpacecraftFrame>
    {
        // The stored states, in arcs split at each impulse (#540)
        private readonly PropagatedEphemeris _propagatedStates = new();

        public static readonly Vector3 Front = Vector3.VectorY;
        public static readonly Vector3 Back = Front.Inverse();
        public static readonly Vector3 Right = Vector3.VectorX;
        public static readonly Vector3 Left = Right.Inverse();
        public static readonly Vector3 Up = Vector3.VectorZ;
        public static readonly Vector3 Down = Up.Inverse();

        /// <summary>
        /// Instance-level body front axis. Defaults to +Y (same as static Front).
        /// Override via constructor to support non-standard body frame conventions (e.g., +X forward).
        /// </summary>
        public Vector3 BodyFront { get; }

        /// <summary>
        /// Instance-level body right axis. Defaults to +X (same as static Right).
        /// </summary>
        public Vector3 BodyRight { get; }

        /// <summary>
        /// Instance-level body up axis. Defaults to +Z (same as static Up).
        /// </summary>
        public Vector3 BodyUp { get; }

        public Vector3 BodyBack => BodyFront.Inverse();
        public Vector3 BodyLeft => BodyRight.Inverse();
        public Vector3 BodyDown => BodyUp.Inverse();

        private static readonly IReadOnlyList<Maneuver.Maneuver> NoManeuver = Array.AsReadOnly(Array.Empty<Maneuver.Maneuver>());

        // The maneuvers the last run executed, in execution order: never changed once published, replaced as a whole
        private IReadOnlyList<Maneuver.Maneuver> _executedManeuvers = NoManeuver;

        /// <summary>
        /// The maneuvers the last run executed, attitudes included, in execution order: a new read-only list at each run
        /// (decision 58 of #668, decided by S. Guillet, 2026-11-23). The re-arm of a run swaps in an empty list, and each
        /// maneuver the run executes swaps in a new list that adds it, so a list once read never changes: a list held from
        /// an earlier run keeps its members, a reader on another thread during a run sees the maneuvers executed so far, and
        /// a stopped run leaves those executed up to the stop (C2 of lot L4). It lists the definitions, whose values
        /// (<c>DeltaV</c>, <c>FuelBurned</c>, the windows) are those of their last run, reset by the next one (decision 52):
        /// the stable values of a run are its records, <see cref="PropagationSolution.Maneuvers"/>. Before 11.0.0 it was a
        /// live set that the next run emptied. Until the fix of #794 (decision 44 of #668, decided by S. Guillet, 2026-11-23,
        /// after lot L5), a stopped run also lists an attitude it scheduled before the stop (at its start, or at the event
        /// of the impulse before it) whose activation it never reached, that attitude's <c>ThrustWindow</c> and
        /// <c>ManeuverWindow</c> set and the chain advanced past it; a completed run reaches every attitude it schedules.
        /// </summary>
        public IReadOnlyList<Maneuver.Maneuver> ExecutedManeuvers => Volatile.Read(ref _executedManeuvers);
        /// <summary>
        /// The maneuver of the chain in standby during a run: the head of the plan before it, the next one to fire as it
        /// runs. Internal since 11.0.0 (decision 45 of #668, decided by S. Guillet, 2026-11-23): it is run state; the plan
        /// is <see cref="Maneuvers"/>, the maneuvers of the last run <see cref="ExecutedManeuvers"/>.
        /// </summary>
        internal Maneuver.Maneuver StandbyManeuver { get; private set; }

        // The plan, in order (SetManeuvers); empty for none
        private IReadOnlyList<Maneuver.Maneuver> _maneuvers = Array.Empty<Maneuver.Maneuver>();

        /// <summary>
        /// The maneuver plan of the spacecraft, in the order of <see cref="SetManeuvers"/>, each linked to the next
        /// (<see cref="Maneuver.Maneuver.NextManeuver"/>): every propagation runs it from its first maneuver. Empty for no
        /// plan. It is the plan as it was set, whatever a run executed (decision 45 of #668, decided by S. Guillet,
        /// 2026-11-23).
        /// </summary>
        public IReadOnlyList<Maneuver.Maneuver> Maneuvers => _maneuvers;

        /// <summary>The first maneuver of the plan, the head of the chain every run starts from; null for no plan.</summary>
        internal Maneuver.Maneuver PlanHead => _maneuvers.Count > 0 ? _maneuvers[0] : null;

        // The claims of the plans of every spacecraft, taken atomically (decision 46 of #668)
        private static readonly object PlanSync = new();
        public Spacecraft Parent { get; private set; }
        public Spacecraft Child { get; private set; }
        public Clock Clock { get; }

        private readonly HashSet<Instrument> _instruments = new();
        public IReadOnlyCollection<Instrument> Instruments => _instruments;

        private readonly HashSet<FuelTank> _fuelTanks = new();
        public IReadOnlyCollection<FuelTank> FuelTanks => _fuelTanks;

        private readonly HashSet<Engine> _engines = new();
        public IReadOnlyCollection<Engine> Engines => _engines;

        private readonly HashSet<Payload> _payloads = new();
        public IReadOnlyCollection<Payload> Payloads => _payloads;
        public double DryOperatingMass => Mass;
        /// <summary>
        /// The maximum operating mass, kg: informational, checked against the dry mass only, never against the fuel and the
        /// payloads (L5-37 of #668); double.MaxValue unless given. <c>MaximumOperatingMass</c> before 11.0.0 (decision 70
        /// of #668, decided by S. Guillet, 2026-11-23).
        /// </summary>
        public double MaxOperatingMass { get; }
        public SpacecraftFrame Frame { get; }
        public double SectionalArea { get; }
        public double DragCoefficient { get; }

        /// <summary>
        /// Gets the COSPAR international designator (e.g., "1998-067A").
        /// </summary>
        /// <remarks>
        /// This is used for CCSDS OPM OBJECT_ID field.
        /// Format: YYYY-NNNP where YYYY is launch year, NNN is launch number, P is piece.
        /// </remarks>
        public string CosparId { get; }

        /// <summary>
        /// Gets the solar radiation pressure coefficient (Cr).
        /// </summary>
        /// <remarks>
        /// Typically 1.0 (absorbing surface) to 2.0 (reflecting surface); finite and not negative (#800). Default is 1.0.
        /// </remarks>
        public double SolarRadiationCoeff { get; }

        /// <summary>
        /// Gets the hard-body radius in meters used for conjunction assessment collision probability.
        /// </summary>
        /// <remarks>
        /// Represents the bounding sphere radius for the spacecraft. Combined with the secondary
        /// object's hard-body radius to compute the collision cross-section.
        /// Default is 0.0 (point object).
        /// </remarks>
        public double HardBodyRadius { get; }

        private readonly bool _isFromKernel;
        public override bool IsSpiceBacked => _isFromKernel;

        private bool _isPropagated;

        /// <summary>
        /// Whether a numerical propagation ran on the spacecraft, by any path: <c>Propagate</c>, <c>Scenario.Simulate</c>,
        /// <see cref="BatchPropagator"/> or a <see cref="CentralBodyPropagator"/> built directly (#583: the builder of
        /// 10.x and a propagator built directly left it false before 11.0.0).
        /// </summary>
        public bool IsPropagated => _isPropagated;

        // Whether the stored states are those of a numerical propagation, by any path: a TLE spacecraft then reads them, SGP4
        // otherwise (#675).
        // Set by SetPropagatedStates, cleared with the stored states; AddPropagatedStates (TLEPropagator's samples
        // included) leaves it
        private bool _holdsPropagatedStates;

        /// <summary>
        /// A spacecraft propagated by the library from <paramref name="initialOrbitalParameters"/>. The NAIF id, the name, the
        /// dry mass and the initial orbit are required; the other values are optional (decisions 70 and 71 of #668, decided by
        /// S. Guillet, 2026-11-23; R10 of the ergonomics review).
        /// </summary>
        /// <param name="naifId">The NAIF id, in [-2147483, -1] (#676): it names the kernels of the spacecraft.</param>
        /// <param name="name">The name of the spacecraft.</param>
        /// <param name="mass">The dry mass, kg: finite and not negative.</param>
        /// <param name="initialOrbitalParameters">The initial orbit, which the propagations start from.</param>
        /// <param name="maxOperatingMass">The maximum operating mass, kg, double.MaxValue by default: informational, finite
        /// and at least <paramref name="mass"/>, checked against the dry mass only (L5-37 of #668), never against the fuel
        /// and the payloads.</param>
        /// <param name="clock">The onboard clock, by default a new clock named Name + "_CLK" at 65536 ticks per second (the
        /// resolution of the CK, <see cref="SpacecraftFrame"/>); not attached to a spacecraft of another NAIF id (#676).
        /// Before 11.0.0 it was required, a null clock throwing <see cref="ArgumentNullException"/>.</param>
        /// <param name="sectionalArea">Mean sectional area (used for both drag and solar radiation pressure), m²</param>
        /// <param name="dragCoefficient">Drag coefficient (Cd), default 2.2 for satellites in free-molecular flow</param>
        /// <param name="cosparId">COSPAR international designator (e.g., "1998-067A")</param>
        /// <param name="solarRadiationCoefficient">Solar radiation pressure coefficient (Cr), default 1.0: typically 1.0 to
        /// 2.0; finite and not negative</param>
        /// <param name="bodyFront">Instance body front axis (default: +Y). Finite and non-zero; must be orthogonal to bodyRight and bodyUp.</param>
        /// <param name="bodyRight">Instance body right axis (default: +X). Finite and non-zero; must be orthogonal to bodyFront and bodyUp.</param>
        /// <param name="bodyUp">Instance body up axis (default: +Z). Finite and non-zero; must be orthogonal to bodyFront and bodyRight.</param>
        /// <param name="hardBodyRadius">Hard-body radius in meters for conjunction assessment, finite and not negative (default 0.0, none): the radius of the spacecraft as a secondary object too (#683)</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="naifId"/> is not in [-2147483, -1]
        /// (#676), or, naming it, a value is not finite or out of its range (#800, decision 75 of #668, decided by
        /// S. Guillet, 2026-11-23): <paramref name="mass"/> not negative, <paramref name="maxOperatingMass"/> at least
        /// <paramref name="mass"/>, <paramref name="sectionalArea"/> above 0, <paramref name="dragCoefficient"/>,
        /// <paramref name="solarRadiationCoefficient"/> and <paramref name="hardBodyRadius"/> not negative.</exception>
        /// <exception cref="ArgumentException">Thrown when body axes are not orthogonal or not right-handed, when a body axis
        /// has a magnitude of 0 (the exception names it, #680) or a component that is not finite (the exception names it,
        /// the first of front, right and up; fourth round of #668), or when <paramref name="clock"/> is attached to a spacecraft
        /// of another NAIF id (#676: an SCLK identifies one spacecraft id).</exception>
        public Spacecraft(int naifId, string name, double mass, OrbitalParameters.OrbitalParameters initialOrbitalParameters,
            double maxOperatingMass = double.MaxValue, Clock clock = null, double sectionalArea = 1.0, double dragCoefficient = 2.2,
            string cosparId = null, double solarRadiationCoefficient = 1.0, Vector3? bodyFront = null, Vector3? bodyRight = null,
            Vector3? bodyUp = null, double hardBodyRadius = 0.0) : base(
            naifId, name, CheckedPhysicalProperties(mass, maxOperatingMass, sectionalArea, dragCoefficient, solarRadiationCoefficient, hardBodyRadius),
            initialOrbitalParameters)
        {
            if (naifId >= 0) throw new ArgumentOutOfRangeException(nameof(naifId));
            CheckFrameIdRange(naifId);
            MaxOperatingMass = maxOperatingMass;
            Clock = clock ?? new Clock(name + "_CLK", DefaultClockResolution);
            Clock.CheckAttachableTo(naifId, name);
            Frame = new SpacecraftFrame(this);
            SectionalArea = sectionalArea;
            DragCoefficient = dragCoefficient;
            CosparId = cosparId;
            SolarRadiationCoeff = solarRadiationCoefficient;
            HardBodyRadius = hardBodyRadius;

            BodyFront = CheckedBodyAxis(bodyFront, Vector3.VectorY, nameof(bodyFront));
            BodyRight = CheckedBodyAxis(bodyRight, Vector3.VectorX, nameof(bodyRight));
            BodyUp = CheckedBodyAxis(bodyUp, Vector3.VectorZ, nameof(bodyUp));

            if (bodyFront.HasValue || bodyRight.HasValue || bodyUp.HasValue)
            {
                ValidateBodyAxes();
            }

            // After every check, so that a constructor that throws leaves the clock free and lists nothing (#676)
            Clock.AttachSpacecraft(this);
            RegisterWithObserver();
        }

        /// <summary>
        /// The resolution of the default clock of a propagated spacecraft, ticks per second: that of the CK the library
        /// writes (<see cref="SpacecraftFrame"/>), the resolution most explicit clocks took before 11.0.0.
        /// </summary>
        internal const uint DefaultClockResolution = 65536;

        /// <summary>
        /// Creates a kernel-backed spacecraft whose ephemeris and orientation come from loaded SPK/CK kernels.
        /// Its initial orbital parameters are its state at <paramref name="epoch"/> relative to
        /// <paramref name="centerOfMotion"/>, the Earth by default (#620: the Solar System barycentre before 11.0.0); SPICE
        /// chains the segments to that centre. The conjunction analysis takes this centre as the observer of a protected
        /// spacecraft, so its RTN axes are those of the orbit about it.
        /// </summary>
        /// <param name="naifId">NAIF ID, in [-2147483, -1] (#676)</param>
        /// <param name="name">Spacecraft name</param>
        /// <param name="epoch">Epoch for initial state lookup (defaults to J2000 TDB)</param>
        /// <param name="mass">Dry mass in kg</param>
        /// <param name="maxOperatingMass">Maximum operating mass in kg, informational: checked against the dry mass only
        /// (<c>maximumOperatingMass</c> before 11.0.0, decision 71 of #668)</param>
        /// <param name="clock">Onboard clock (defaults to a new clock named Name + "_CLK" at 256 ticks per second, as before
        /// 11.0.0); not attached to a spacecraft of another NAIF id (#676)</param>
        /// <param name="sectionalArea">Mean sectional area in m²</param>
        /// <param name="dragCoefficient">Drag coefficient (Cd) (<c>dragCoeff</c> before 11.0.0)</param>
        /// <param name="cosparId">COSPAR international designator</param>
        /// <param name="solarRadiationCoefficient">Solar radiation pressure coefficient (Cr) (<c>solarRadiationCoeff</c>
        /// before 11.0.0)</param>
        /// <param name="hardBodyRadius">Hard-body radius in meters for conjunction assessment, finite and not negative (default 0.0, none): the radius of the spacecraft as a secondary object too (#683)</param>
        /// <param name="centerOfMotion">The centre of the initial orbital parameters, the Earth when null; the Solar System
        /// barycentre, or any body the loaded kernels chain to, may be given.</param>
        /// <exception cref="InvalidOperationException">The loaded kernels give no state of the spacecraft relative to
        /// <paramref name="centerOfMotion"/> at <paramref name="epoch"/> (#454).</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="naifId"/> is not in [-2147483, -1]
        /// (#676), or, naming it, a value is not finite or out of its range, checked before the kernels are read, as by the
        /// other constructor (#800, decision 75 of #668, decided by S. Guillet, 2026-11-23; before, only the hard-body
        /// radius, #683): <paramref name="mass"/> not negative, <paramref name="maxOperatingMass"/> at least
        /// <paramref name="mass"/>, <paramref name="sectionalArea"/> above 0, <paramref name="dragCoefficient"/>,
        /// <paramref name="solarRadiationCoefficient"/> and <paramref name="hardBodyRadius"/> not negative.</exception>
        /// <exception cref="ArgumentException"><paramref name="clock"/> is attached to a spacecraft of another NAIF id
        /// (#676).</exception>
        /// <remarks>
        /// Its frame (<see cref="Frame"/>) is read from SPICE as the frame the loaded frame kernels define for the id
        /// NaifId × 1000, whatever its name; <see cref="Frame.Name"/> stays <c>Name + "_FRAME"</c>, and is handed to SPICE
        /// when no loaded kernel defines that id (#676).
        /// </remarks>
        public Spacecraft(int naifId, string name,
            Time? epoch = null, double mass = 0.0, double maxOperatingMass = double.MaxValue,
            Clock clock = null, double sectionalArea = 1.0, double dragCoefficient = 2.2,
            string cosparId = null, double solarRadiationCoefficient = 1.0, double hardBodyRadius = 0.0,
            CelestialItem centerOfMotion = null)
            : base(naifId, name, CheckedPhysicalProperties(mass, maxOperatingMass, sectionalArea, dragCoefficient, solarRadiationCoefficient, hardBodyRadius),
                ReadKernelInitialState(naifId, name, epoch ?? TimeSystem.Time.J2000TDB, centerOfMotion, clock))
        {
            _isFromKernel = true;
            MaxOperatingMass = maxOperatingMass;
            Clock = clock ?? new Clock(name + "_CLK", 256);
            Frame = new SpacecraftFrame(this);
            SectionalArea = sectionalArea;
            DragCoefficient = dragCoefficient;
            CosparId = cosparId;
            SolarRadiationCoeff = solarRadiationCoefficient;
            HardBodyRadius = hardBodyRadius;
            BodyFront = Vector3.VectorY;
            BodyRight = Vector3.VectorX;
            BodyUp = Vector3.VectorZ;

            // After every check, so that a constructor that throws leaves the clock free and lists nothing (#676)
            Clock.AttachSpacecraft(this);
            RegisterWithObserver();
        }

        /// <summary>
        /// The smallest NAIF id of a spacecraft: its frame id, NaifId × 1000 (<see cref="SpacecraftFrame"/>), is an int
        /// down to -2147483000 (#676).
        /// </summary>
        internal const int MinimumNaifId = -2147483;

        // A NAIF id below MinimumNaifId has no frame id in an int: before #676 NaifId × 1000 wrapped around, a positive id
        // from -2147484 to -4294967
        private static void CheckFrameIdRange(int naifId)
        {
            if (naifId < MinimumNaifId)
            {
                throw new ArgumentOutOfRangeException(nameof(naifId), naifId,
                    $"A spacecraft NAIF id is in [{MinimumNaifId}, -1]: its frame id, NaifId × 1000, must be an int.");
            }
        }

        // The physical properties of both constructors, finite and in their ranges (#800, decision 75 of #668, decided by
        // S. Guillet, 2026-11-23): a NaN passed the comparisons (ThrowIfNegative rejected double.NaN through its sign bit
        // only), an infinity passed them, and the kernel-backed constructor checked none. A base-call argument, so that it
        // runs before the base's own check of the mass and before the kernel-backed constructor reads the kernels. Returns
        // the mass
        private static double CheckedPhysicalProperties(double mass, double maxOperatingMass, double sectionalArea,
            double dragCoefficient, double solarRadiationCoefficient, double hardBodyRadius)
        {
            if (!double.IsFinite(mass) || mass < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(mass), mass, "The dry mass must be finite and not negative.");
            }

            if (!double.IsFinite(maxOperatingMass) || maxOperatingMass < mass)
            {
                throw new ArgumentOutOfRangeException(nameof(maxOperatingMass), maxOperatingMass,
                    $"The maximum operating mass must be finite and at least the dry mass, {mass} kg.");
            }

            if (!double.IsFinite(sectionalArea) || sectionalArea <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(sectionalArea), sectionalArea, "The sectional area must be finite and above 0.");
            }

            if (!double.IsFinite(dragCoefficient) || dragCoefficient < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(dragCoefficient), dragCoefficient, "The drag coefficient must be finite and not negative.");
            }

            if (!double.IsFinite(solarRadiationCoefficient) || solarRadiationCoefficient < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(solarRadiationCoefficient), solarRadiationCoefficient,
                    "The solar radiation pressure coefficient must be finite and not negative.");
            }

            ThrowIfInvalidHardBodyRadius(hardBodyRadius, nameof(hardBodyRadius));
            return mass;
        }

        // A hard-body radius is finite and not negative, 0 for none (#683: the conjunction analysis reads the radius of a
        // secondary): the rule of the constructors and of the radius options of the conjunction analysis
        internal static void ThrowIfInvalidHardBodyRadius(double hardBodyRadius, string paramName)
        {
            if (!double.IsFinite(hardBodyRadius) || hardBodyRadius < 0.0)
            {
                throw new ArgumentOutOfRangeException(paramName, hardBodyRadius,
                    "The hard-body radius must be finite and not negative.");
            }
        }

        // The unit vector of a given body axis, or the default; an axis of magnitude 0 has no direction (#680: zero axes were
        // accepted when the dot products of ValidateBodyAxes all vanished), nor has an axis with a component that is not
        // finite (fourth round of #668: it was normalized to NaN components, which every comparison of ValidateBodyAxes let
        // through)
        private static Vector3 CheckedBodyAxis(Vector3? axis, Vector3 defaultAxis, string paramName)
        {
            if (!axis.HasValue)
            {
                return defaultAxis;
            }

            Vector3.ThrowIfNoDirection(axis.Value, "A body axis", paramName);
            return axis.Value.NormalizeOrZero();
        }

        private void ValidateBodyAxes()
        {
            const double tolerance = 1e-10;

            // Check orthogonality
            if (System.Math.Abs(BodyFront.Dot(BodyRight)) > tolerance)
                throw new ArgumentException("Body axes must be orthogonal: BodyFront and BodyRight are not perpendicular.");
            if (System.Math.Abs(BodyFront.Dot(BodyUp)) > tolerance)
                throw new ArgumentException("Body axes must be orthogonal: BodyFront and BodyUp are not perpendicular.");
            if (System.Math.Abs(BodyRight.Dot(BodyUp)) > tolerance)
                throw new ArgumentException("Body axes must be orthogonal: BodyRight and BodyUp are not perpendicular.");

            // Check right-handedness: Right x Front should equal Up
            var cross = BodyRight.Cross(BodyFront);
            if ((cross - BodyUp).Magnitude() > tolerance)
                throw new ArgumentException("Body axes must form a right-handed coordinate system: BodyRight x BodyFront must equal BodyUp.");
        }

        /// <summary>
        /// Adds a circular instrument to the spacecraft and returns it (decision 72 of #668, decided by S. Guillet,
        /// 2026-11-23; before 11.0.0 the method returned nothing and took the id and the model first).
        /// </summary>
        /// <param name="name">The name of the instrument, unique on the spacecraft on its invariant upper-cased form</param>
        /// <param name="fieldOfView">The half-angle of the circle, rad: finite and positive</param>
        /// <param name="boresight">The boresight, in the instrument frame: finite components, a non-zero magnitude</param>
        /// <param name="refVector">The reference vector, in the instrument frame: finite components, a non-zero magnitude,
        /// not parallel to the boresight</param>
        /// <param name="orientation">The Euler angles (x, y, z) in radians about the axes 1, 2, 3 of the frame kernel, finite:
        /// <see cref="Instrument.Orientation"/>; zero by default, the instrument frame the spacecraft frame</param>
        /// <param name="naifId">The NAIF id of the instrument; by default the smallest free NaifId × 1000 − n, n in
        /// [1, 999] (NAIF IDs Required Reading, "Instruments"). Required on a kernel-backed spacecraft</param>
        /// <param name="model">The model of the instrument, informational; null by default</param>
        /// <returns>The instrument added.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="naifId"/> is null on a kernel-backed spacecraft
        /// (decision 73 of #668): <c>gftfov</c> reads the instrument kernel loaded under the id, and a default id could
        /// read another instrument of the mission's kernel.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="naifId"/> is null and no id NaifId × 1000 − n,
        /// n in [1, 999], is free (or an int): pass <paramref name="naifId"/>.</exception>
        /// <exception cref="ArgumentException">Naming the parameter: <paramref name="naifId"/> is the id of another
        /// instrument of the spacecraft; <paramref name="name"/> has the invariant upper-cased form of the name of another
        /// of its instruments (decision 74 of #668: both kernels would assign FRAME_&lt;SPACECRAFT&gt;_&lt;INSTRUMENT&gt;,
        /// and write the same file on a case-insensitive file system), or is empty; the field of view is not a finite
        /// positive number, the boresight or the reference vector has a component that is not finite or a magnitude of 0,
        /// or the reference vector is parallel to the boresight: the fields CSPICE's <c>getfov</c> rejects (#707); or an
        /// Euler angle of <paramref name="orientation"/> is not finite (#715).</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="naifId"/> is not negative, or is the id of the
        /// spacecraft frame, NaifId × 1000, which the instrument frame kernel would define relative to itself
        /// (#676).</exception>
        public CircularInstrument AddCircularInstrument(string name, double fieldOfView, in Vector3 boresight, in Vector3 refVector,
            in Vector3 orientation = default, int? naifId = null, string model = null)
        {
            int id = InstrumentIdToAdd(naifId, name);
            return AddInstrument(new CircularInstrument(this, id, name, model, fieldOfView, boresight, refVector, orientation));
        }

        /// <summary>
        /// Adds a rectangular instrument to the spacecraft and returns it (decision 72 of #668, decided by S. Guillet,
        /// 2026-11-23; before 11.0.0 the method returned nothing and took the id and the model first).
        /// </summary>
        /// <param name="name">The name of the instrument, unique on the spacecraft on its invariant upper-cased form</param>
        /// <param name="fieldOfView">The half-angle in the plane of the boresight and the reference vector
        /// (<c>FOV_REF_ANGLE</c>), rad: finite and positive</param>
        /// <param name="crossAngle">The half-angle across that plane (<c>FOV_CROSS_ANGLE</c>), rad: finite and positive</param>
        /// <param name="boresight">The boresight, in the instrument frame: finite components, a non-zero magnitude</param>
        /// <param name="refVector">The reference vector, in the instrument frame: finite components, a non-zero magnitude,
        /// not parallel to the boresight</param>
        /// <param name="orientation">The Euler angles (x, y, z) in radians about the axes 1, 2, 3 of the frame kernel, finite:
        /// <see cref="Instrument.Orientation"/>; zero by default, the instrument frame the spacecraft frame</param>
        /// <param name="naifId">The NAIF id of the instrument; by default the smallest free NaifId × 1000 − n, n in
        /// [1, 999] (NAIF IDs Required Reading, "Instruments"). Required on a kernel-backed spacecraft</param>
        /// <param name="model">The model of the instrument, informational; null by default</param>
        /// <returns>The instrument added.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="naifId"/> is null on a kernel-backed spacecraft
        /// (decision 73 of #668): <c>gftfov</c> reads the instrument kernel loaded under the id, and a default id could
        /// read another instrument of the mission's kernel.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="naifId"/> is null and no id NaifId × 1000 − n,
        /// n in [1, 999], is free (or an int): pass <paramref name="naifId"/>.</exception>
        /// <exception cref="ArgumentException">Naming the parameter: <paramref name="naifId"/> is the id of another
        /// instrument of the spacecraft; <paramref name="name"/> has the invariant upper-cased form of the name of another
        /// of its instruments (decision 74 of #668: both kernels would assign FRAME_&lt;SPACECRAFT&gt;_&lt;INSTRUMENT&gt;,
        /// and write the same file on a case-insensitive file system), or is empty; the field of view is not a finite
        /// positive number, the boresight or the reference vector has a component that is not finite or a magnitude of 0,
        /// or the reference vector is parallel to the boresight: the fields CSPICE's <c>getfov</c> rejects (#707); or an
        /// Euler angle of <paramref name="orientation"/> is not finite (#715).</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="naifId"/> is not negative, or is the id of the
        /// spacecraft frame, NaifId × 1000, which the instrument frame kernel would define relative to itself (#676); or
        /// <paramref name="crossAngle"/> is not a finite positive number: SPICE cannot read a NaN or an infinity in the
        /// kernel (#707), and a cross angle of 0 or −0 is a field of no width (#715).</exception>
        public RectangularInstrument AddRectangularInstrument(string name, double fieldOfView, double crossAngle, in Vector3 boresight,
            in Vector3 refVector, in Vector3 orientation = default, int? naifId = null, string model = null)
        {
            int id = InstrumentIdToAdd(naifId, name);
            return AddInstrument(new RectangularInstrument(this, id, name, model, fieldOfView, crossAngle, boresight, refVector, orientation));
        }

        /// <summary>
        /// Adds a elliptical instrument to the spacecraft and returns it (decision 72 of #668, decided by S. Guillet,
        /// 2026-11-23; before 11.0.0 the method returned nothing and took the id and the model first).
        /// </summary>
        /// <param name="name">The name of the instrument, unique on the spacecraft on its invariant upper-cased form</param>
        /// <param name="fieldOfView">The half-angle in the plane of the boresight and the reference vector
        /// (<c>FOV_REF_ANGLE</c>), rad: finite and positive</param>
        /// <param name="crossAngle">The half-angle across that plane (<c>FOV_CROSS_ANGLE</c>), rad: finite and positive</param>
        /// <param name="boresight">The boresight, in the instrument frame: finite components, a non-zero magnitude</param>
        /// <param name="refVector">The reference vector, in the instrument frame: finite components, a non-zero magnitude,
        /// not parallel to the boresight</param>
        /// <param name="orientation">The Euler angles (x, y, z) in radians about the axes 1, 2, 3 of the frame kernel, finite:
        /// <see cref="Instrument.Orientation"/>; zero by default, the instrument frame the spacecraft frame</param>
        /// <param name="naifId">The NAIF id of the instrument; by default the smallest free NaifId × 1000 − n, n in
        /// [1, 999] (NAIF IDs Required Reading, "Instruments"). Required on a kernel-backed spacecraft</param>
        /// <param name="model">The model of the instrument, informational; null by default</param>
        /// <returns>The instrument added.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="naifId"/> is null on a kernel-backed spacecraft
        /// (decision 73 of #668): <c>gftfov</c> reads the instrument kernel loaded under the id, and a default id could
        /// read another instrument of the mission's kernel.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="naifId"/> is null and no id NaifId × 1000 − n,
        /// n in [1, 999], is free (or an int): pass <paramref name="naifId"/>.</exception>
        /// <exception cref="ArgumentException">Naming the parameter: <paramref name="naifId"/> is the id of another
        /// instrument of the spacecraft; <paramref name="name"/> has the invariant upper-cased form of the name of another
        /// of its instruments (decision 74 of #668: both kernels would assign FRAME_&lt;SPACECRAFT&gt;_&lt;INSTRUMENT&gt;,
        /// and write the same file on a case-insensitive file system), or is empty; the field of view is not a finite
        /// positive number, the boresight or the reference vector has a component that is not finite or a magnitude of 0,
        /// or the reference vector is parallel to the boresight: the fields CSPICE's <c>getfov</c> rejects (#707); or an
        /// Euler angle of <paramref name="orientation"/> is not finite (#715).</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="naifId"/> is not negative, or is the id of the
        /// spacecraft frame, NaifId × 1000, which the instrument frame kernel would define relative to itself (#676); or
        /// <paramref name="crossAngle"/> is not a finite positive number: SPICE cannot read a NaN or an infinity in the
        /// kernel (#707), and a cross angle of 0 or −0 is a field of no width (#715).</exception>
        public EllipticalInstrument AddEllipticalInstrument(string name, double fieldOfView, double crossAngle, in Vector3 boresight,
            in Vector3 refVector, in Vector3 orientation = default, int? naifId = null, string model = null)
        {
            int id = InstrumentIdToAdd(naifId, name);
            return AddInstrument(new EllipticalInstrument(this, id, name, model, fieldOfView, crossAngle, boresight, refVector, orientation));
        }

        // The checks of Add*Instrument, before the instrument is built, in the order of lot L5b (decisions 72 to 74 of
        // #668, decided by S. Guillet, 2026-11-23): a kernel-backed spacecraft requires the id, then the id (given, or the
        // smallest free NaifId × 1000 − n), then a duplicate id, then a name of the invariant upper-cased form of another's.
        // The value checks stay in the constructor of Instrument. Returns the id of the instrument to build
        private int InstrumentIdToAdd(int? naifId, string name)
        {
            if (naifId == null && IsSpiceBacked)
            {
                throw new ArgumentNullException(nameof(naifId),
                    $"An instrument of the kernel-backed spacecraft {Name} ({NaifId}) takes its NAIF id: the field-of-view search (gftfov) reads the instrument kernel loaded under it, and a default id could read another instrument of the mission's kernel.");
            }

            int id = naifId ?? DefaultInstrumentId();
            if (_instruments.Any(x => x.NaifId == id))
            {
                throw new ArgumentException(
                    $"The NAIF id {id} is the id of the instrument {_instruments.First(x => x.NaifId == id).Name} of {Name} ({NaifId}): an instrument id names one instrument.",
                    nameof(naifId));
            }

            // Two names of one upper-cased form write the same kernel variable FRAME_<SPACECRAFT>_<INSTRUMENT> and the same
            // file on a case-insensitive file system (decision 74 of #668); the writers upper-case invariantly too (#798)
            string upper = string.IsNullOrEmpty(name) ? null : name.ToUpperInvariant();
            var sameName = upper == null ? null : _instruments.FirstOrDefault(x => x.Name.ToUpperInvariant() == upper);
            if (sameName != null)
            {
                throw new ArgumentException(
                    $"The instrument name {name} has the upper-cased form of the name of the instrument {sameName.Name} ({sameName.NaifId}) of {Name}: both frame kernels would assign FRAME_{Name.ToUpperInvariant()}_{upper}, and both kernels would be the same file on a case-insensitive file system. Give the instrument another name.",
                    nameof(name));
            }

            return id;
        }

        // The smallest free NaifId × 1000 − n, n in [1, 999], that is an int (NAIF IDs Required Reading, "Instruments";
        // n ≤ 648 at -2147483), computed in long (decision 72 of #668)
        private int DefaultInstrumentId()
        {
            long frameId = (long)NaifId * 1000L;
            for (int n = 1; n <= 999; n++)
            {
                long candidate = frameId - n;
                if (candidate < int.MinValue)
                {
                    break;
                }

                if (_instruments.All(x => x.NaifId != candidate))
                {
                    return (int)candidate;
                }
            }

            throw new InvalidOperationException(
                $"No NAIF id NaifId × 1000 − n, n in [1, 999], is free on {Name} ({NaifId}): pass the instrument's naifId.");
        }

        private T AddInstrument<T>(T instrument) where T : Instrument
        {
            // The id was checked free before the instrument was built
            _instruments.Add(instrument);
            return instrument;
        }

        /// <summary>
        /// Adds an engine to the spacecraft and returns it (decision 76 of #668, decided by S. Guillet, 2026-11-23: it
        /// returned nothing before 11.0.0).
        /// </summary>
        /// <param name="engine">The engine, on a tank added to the spacecraft</param>
        /// <returns><paramref name="engine"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="engine"/> is null.</exception>
        /// <exception cref="ArgumentException">Naming <paramref name="engine"/>: its fuel tank is not one of the tanks added
        /// to the spacecraft (#676, #586; an unknown tank threw <see cref="InvalidOperationException"/> before 11.0.0), the
        /// engine is already added, or another engine of the spacecraft has its name, compared ordinally (decision 76 of
        /// #668).</exception>
        public Engine AddEngine(Engine engine)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (!HoldsFuelTank(engine.FuelTank))
            {
                // ArgumentException, the call receiving the faulty engine (#676, decided by S. Guillet, 2026-11-14)
                throw new ArgumentException(
                    $"Unknown fuel tank, you must add fuel tank to spacecraft before add engine: the fuel tank {engine.FuelTank.Name}{SerialOf(engine.FuelTank.SerialNumber)} of the engine {engine.Name} is not a tank of {Name}.",
                    nameof(engine));
            }

            if (HoldsEngine(engine))
            {
                throw new ArgumentException($"The engine {engine.Name} is already added to {Name}.", nameof(engine));
            }

            if (_engines.Any(x => x.Name == engine.Name))
            {
                throw new ArgumentException(
                    $"{Name} already holds an engine named {engine.Name}: the engines of a spacecraft have one name each (decision 76 of #668).",
                    nameof(engine));
            }

            _engines.Add(engine);
            return engine;
        }

        /// <summary>
        /// Adds a fuel tank to the spacecraft and returns it (decision 76 of #668, decided by S. Guillet, 2026-11-23: it
        /// returned nothing before 11.0.0).
        /// </summary>
        /// <param name="fuelTank">The tank, held by no other spacecraft</param>
        /// <returns><paramref name="fuelTank"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="fuelTank"/> is null.</exception>
        /// <exception cref="ArgumentException">Naming <paramref name="fuelTank"/>: the tank is already added, another tank of
        /// the spacecraft has its name, compared ordinally (decision 76 of #668; checked before the tank is attached, so a
        /// refused tank stays free for another spacecraft), or the tank is held by another spacecraft: a tank belongs to one
        /// spacecraft (#676, #586; before, it moved to this one without an error).</exception>
        public FuelTank AddFuelTank(FuelTank fuelTank)
        {
            if (fuelTank == null) throw new ArgumentNullException(nameof(fuelTank));
            if (HoldsFuelTank(fuelTank))
            {
                throw new ArgumentException($"The fuel tank {fuelTank.Name} is already added to {Name}.", nameof(fuelTank));
            }

            // Before the attach, which is never released: a tank refused after it would be held by no list (IC-16 of lot L5b)
            if (_fuelTanks.Any(x => x.Name == fuelTank.Name))
            {
                throw new ArgumentException(
                    $"{Name} already holds a fuel tank named {fuelTank.Name}: the tanks of a spacecraft have one name each (decision 76 of #668).",
                    nameof(fuelTank));
            }

            // Attached atomically, so that one spacecraft takes a tank added to several at once (#676)
            var holder = fuelTank.AttachSpacecraft(this);
            if (!ReferenceEquals(holder, this))
            {
                throw new ArgumentException(
                    $"The fuel tank {fuelTank.Name}{SerialOf(fuelTank.SerialNumber)} belongs to the spacecraft {holder.Name} ({holder.NaifId}): a tank belongs to one spacecraft.",
                    nameof(fuelTank));
            }

            _fuelTanks.Add(fuelTank);
            return fuelTank;
        }

        // " (serial number s)" for a component given a serial number, nothing otherwise (decision 76 of #668: optional)
        private static string SerialOf(string serialNumber) =>
            string.IsNullOrEmpty(serialNumber) ? string.Empty : $" (serial number {serialNumber})";

        // Whether the tank is one of the tanks of the spacecraft: FuelTank compares by reference since 11.0.0 (decision 76 of
        // #668; by value before, so the check compared references, #676)
        private bool HoldsFuelTank(FuelTank fuelTank) => _fuelTanks.Contains(fuelTank);

        // Whether the engine is one of the engines of the spacecraft: Engine compares by reference since 11.0.0 (decisions 76
        // and 92 of #668)
        private bool HoldsEngine(Engine engine) => _engines.Contains(engine);

        /// <summary>
        /// Checks, before a propagation, that each impulse of the chain given an engine draws on an engine of this spacecraft: an engine of
        /// <see cref="Engines"/>, added with <see cref="AddEngine"/>, compared by reference (decision 92 of #668, decided by
        /// S. Guillet, 2026-11-23: before, the tank alone was checked, so an engine built on the spacecraft's tank and never
        /// added ran), whose fuel tank is one of the tank instances of the spacecraft and is held by this instance (#676,
        /// #586). Without the tank check, an impulse on a tank of no spacecraft threw a <see cref="NullReferenceException"/>
        /// during the run, one on another spacecraft's tank burnt that spacecraft's fuel, and an attitude took the body axes
        /// of the other spacecraft, or the static ones. And that each is in the plan of this spacecraft
        /// (<see cref="SetManeuvers"/>): the run's re-check of the claim (decision 46 of #668, decided by S. Guillet,
        /// 2026-11-23). Then it resolves, writing nothing, the engine and the centre the run gives each maneuver of the plan
        /// (decisions 61, 63 and 64 of #668, decided by S. Guillet, 2026-11-23): see <see cref="ResolveManeuver"/>.
        /// </summary>
        /// <returns>The maneuvers of the plan, from its head, each with the engine (null for an attitude) and the centre
        /// (null for the central body of the run) the run writes on it right after its re-arm.</returns>
        /// <exception cref="InvalidOperationException">A maneuver draws on an engine that is not one of this spacecraft's
        /// engines or on a tank that is not this spacecraft's, or points an instrument of another spacecraft (#797, decision 66
        /// of #668, decided by S. Guillet, 2026-11-23), or is not in its plan, or its engine or its centre cannot be inferred
        /// (decision 64); nothing changes.</exception>
        internal IReadOnlyList<(Maneuver.Maneuver Maneuver, Engine Engine, CelestialItem Center)> CheckManeuverEngines()
        {
            foreach (var maneuver in Maneuver.Maneuver.Chain(PlanHead).Concat(Maneuver.Maneuver.Chain(StandbyManeuver)))
            {
                if (EngineError(maneuver) is { } engineError)
                {
                    throw new InvalidOperationException(engineError);
                }

                if (InstrumentError(maneuver) is { } instrumentError)
                {
                    throw new InvalidOperationException(instrumentError);
                }

                if (!ReferenceEquals(maneuver.PlanOwner, this))
                {
                    throw new InvalidOperationException(
                        $"The {maneuver.GetType().Name} in the maneuver chain of {Name} ({NaifId}) is not in its plan: a plan is linked " +
                        "by SetManeuvers only. Set the plan with SetManeuvers.");
                }
            }

            var resolved = new List<(Maneuver.Maneuver, Engine, CelestialItem)>();
            foreach (var maneuver in Maneuver.Maneuver.Chain(PlanHead))
            {
                var (engine, center) = ResolveManeuver(maneuver);
                resolved.Add((maneuver, engine, center));
            }

            return resolved;
        }

        /// <summary>
        /// The engine and the centre a run gives <paramref name="maneuver"/>, resolved from the values it was given,
        /// never from those a run wrote (IC-1 of lot L5b of #668), writing nothing (decisions 61, 63 and 64 of #668, decided
        /// by S. Guillet, 2026-11-23): for an impulse, the engine it was given, else the only engine of
        /// <see cref="Engines"/> (decision 92); for every maneuver, the centre it was given, else the observer of the
        /// initial orbit of this spacecraft when it is a celestial body other than a star, else, for a law that does not
        /// read its centre (all its targets library <see cref="Maneuver.CelestialAttitudeTarget"/>s), null, the central body of the
        /// run (decision 99 of #668, decided by S. Guillet, 2026-11-24).
        /// </summary>
        /// <exception cref="InvalidOperationException">An impulse given no engine on a spacecraft of no engine or of
        /// several, or a law that reads its centre given none where it cannot be inferred (SSB mode: the observer is a
        /// star or a barycentre), or an impulse whose inferred centre breaks its rule (an apogee below the equatorial radius
        /// of the centre, decision 68 of #668).</exception>
        internal (Engine Engine, CelestialItem Center) ResolveManeuver(Maneuver.Maneuver maneuver)
        {
            Engine engine = null;
            if (maneuver is Maneuver.ImpulseManeuver impulse)
            {
                engine = impulse.GivenEngine;
                if (engine == null)
                {
                    if (_engines.Count != 1)
                    {
                        string held = _engines.Count == 0
                            ? "holds no engine"
                            : $"holds {_engines.Count} engines ({string.Join(", ", _engines.Select(x => x.Name))})";
                        throw new InvalidOperationException(
                            $"The {maneuver.GetType().Name} of {Name} ({NaifId}) was given no engine, and {Name} {held}: the engine is inferred only " +
                            $"when the spacecraft holds one (decisions 61 and 64 of #668). Pass engine: to the constructor of the maneuver, an engine of {Name}.");
                    }

                    engine = _engines.First();
                }
            }

            var center = maneuver.GivenCenter ?? InferredCenter;
            if (center == null && maneuver.ReadsCenter)
            {
                string observer = InitialOrbitalParameters?.Observer is INaifObject named ? named.Name : "none";
                throw new InvalidOperationException(
                    $"The {maneuver.GetType().Name} of {Name} ({NaifId}) was given no maneuverCenter, and the centre cannot be inferred: the observer " +
                    $"of the initial orbit of {Name}, {observer}, is not a celestial body other than a star (SSB mode), and the law reads its centre " +
                    "(an impulse, an orbital direction, a user target or a user attitude; decisions 61, 64 and 99 of #668). Give maneuverCenter to the " +
                    "constructor of the maneuver.");
            }

            // The rule of the centre of an impulse (the apogee of an ApogeeHeightManeuver above the surface, decision 68 of
            // #668): checked by the constructor for a given centre, here for an inferred one, with the exception of the
            // chain checks (L5-27 as amended)
            if (maneuver.GivenCenter == null && center != null && maneuver is Maneuver.ImpulseManeuver checkedImpulse &&
                checkedImpulse.CenterError(center) is { } centerError)
            {
                throw new InvalidOperationException(
                    $"The {maneuver.GetType().Name} of {Name} ({NaifId}) cannot take the centre the run infers, {(center as INaifObject)?.Name}: {centerError}");
            }

            return (engine, center);
        }

        // The centre a maneuver given none takes: the observer of the initial orbit as given when it is a celestial body
        // other than a star (decision 61 of #668, L5-05 as amended: a star or a barycentre is the SSB mode, whose run is
        // about the Sun, and a state read from an SSB-mode solution is about the Sun, so it infers the Sun); null otherwise
        private CelestialItem InferredCenter =>
            InitialOrbitalParameters?.Observer is CelestialBody body and not Star ? body : null;

        // Why a maneuver cannot run on this spacecraft: its engine's tank is not one of this spacecraft's (#676, #586), or
        // the engine is not one of its engines, by reference (decision 92 of #668); null otherwise. An impulse only (an
        // attitude has no engine since lot L5b, decision 48 of #668), on the engine it was given (IC-1 of lot L5b): an
        // inferred engine is one of Engines, whose tanks AddEngine checks
        private string EngineError(Maneuver.Maneuver maneuver)
        {
            if (maneuver is not Maneuver.ImpulseManeuver { GivenEngine: { } engine })
            {
                return null;
            }

            var tank = engine.FuelTank;
            if (ReferenceEquals(tank.Spacecraft, this) && HoldsFuelTank(tank))
            {
                if (HoldsEngine(engine))
                {
                    return null;
                }

                string notOneOf = $"The {maneuver.GetType().Name} of {Name} ({NaifId}) draws on the engine {engine.Name}{SerialOf(engine.SerialNumber)}, which is not one of the engines of {Name}: " +
                                  $"an engine of a plan is an engine instance added with AddEngine. ";
                // An engine of the name of an added one cannot be added (AddEngine refuses a second engine of a name): the
                // advice is the instance of Engines (second review of lot L5a; a twin by value before decision 76 of #668)
                return _engines.Any(x => x.Name == engine.Name)
                    ? notOneOf + $"An engine of the same name is one of the engines of {Name}: build the maneuver on that engine instance (Engines of {Name})."
                    : notOneOf + $"Add the engine to {Name} with AddEngine before the plan, or build the maneuver on an engine of {Name}.";
            }

            string holder = tank.Spacecraft == null
                ? "is on no spacecraft"
                : $"belongs to the spacecraft {tank.Spacecraft.Name} ({tank.Spacecraft.NaifId})";
            return $"The {maneuver.GetType().Name} of {Name} ({NaifId}) draws on the engine {engine.Name}{SerialOf(engine.SerialNumber)}, whose fuel tank {tank.Name}{SerialOf(tank.SerialNumber)} {holder}, not on the spacecraft propagated: " +
                   $"add the tank to {Name} with AddFuelTank and the engine with AddEngine, or build the maneuver on an engine of {Name}.";
        }

        // Why an attitude cannot run on this spacecraft: it points an instrument of another spacecraft (#797, decision 66 of
        // #668, decided by S. Guillet, 2026-11-23), compared by reference (an instrument equal by NAIF id is another
        // spacecraft's twin); null otherwise. Before, the run pointed that instrument's boresight, whose orientation is in
        // the frame of its own spacecraft (0.577 rad off in the probe of L5-26)
        private string InstrumentError(Maneuver.Maneuver maneuver)
        {
            foreach (var instrument in maneuver.PointedInstruments)
            {
                if (!ReferenceEquals(instrument.Spacecraft, this))
                {
                    return $"The {maneuver.GetType().Name} of {Name} ({NaifId}) points the instrument {instrument.Name} ({instrument.NaifId}) of the spacecraft {instrument.Spacecraft.Name} ({instrument.Spacecraft.NaifId}), not an instrument of the spacecraft propagated: " +
                           $"an attitude points the instruments of its own spacecraft. Build the attitude on an instrument of {Name} (Instruments of {Name}).";
                }
            }

            return null;
        }

        /// <summary>
        /// Adds a payload to the spacecraft and returns it (decision 76 of #668, decided by S. Guillet, 2026-11-23: it
        /// returned nothing before 11.0.0).
        /// </summary>
        /// <param name="payload">The payload</param>
        /// <returns><paramref name="payload"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="payload"/> is null.</exception>
        /// <exception cref="ArgumentException">Naming <paramref name="payload"/>: the payload is already added, or another
        /// payload of the spacecraft has its name, compared ordinally. The rule of every component, for uniformity: a payload
        /// name reaches no file (decision 76 of #668).</exception>
        public Payload AddPayload(Payload payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (_payloads.Contains(payload))
            {
                throw new ArgumentException($"The payload {payload.Name} is already added to {Name}.", nameof(payload));
            }

            if (_payloads.Any(x => x.Name == payload.Name))
            {
                throw new ArgumentException(
                    $"{Name} already holds a payload named {payload.Name}: the payloads of a spacecraft have one name each (decision 76 of #668).",
                    nameof(payload));
            }

            _payloads.Add(payload);
            return payload;
        }

        /// <summary>
        /// Set child spacecraft
        /// </summary>
        /// <param name="spacecraft"></param>
        public void SetChild(Spacecraft spacecraft)
        {
            Child = spacecraft;

            if (spacecraft != null)
            {
                spacecraft.Parent = this;
            }
        }

        /// <summary>
        /// Set parent spacecraft
        /// </summary>
        /// <param name="spacecraft"></param>
        public void SetParent(Spacecraft spacecraft)
        {
            Parent = spacecraft;
            if (spacecraft != null)
            {
                spacecraft.Child = this;
            }
        }

        /// <summary>
        /// Get Dry operating mass + fuel+ payloads + children
        /// </summary>
        /// <returns></returns>
        public double GetTotalMass()
        {
            return DryOperatingMass + FuelTanks.Sum(x => x.Quantity) + Payloads.Sum(x => x.Mass) +
                   (Child != null ? Child.GetTotalMass() : 0.0);
        }

        /// <summary>
        /// The specific impulse of the engines of the spacecraft fired together, s: the sum of the thrusts of every engine of
        /// <see cref="Engines"/> over g0 and over their fuel flows (<see cref="GetTotalFuelFlow"/>), whatever their tanks
        /// hold (decision 60 of #668 with L5-20 as amended, decided by S. Guillet, 2026-11-23; #801 part 1), so an engine on
        /// an empty tank counts: engines of 300 s at 10 kg/s and 200 s at 30 kg/s give 225 s whatever the second tank holds.
        /// Before lot L5b it counted only the engines whose tank held fuel at the start of a run (an initial quantity above
        /// 0), which left out the engine of a tank of NaN quantity (300 s in that case), a quantity the tanks refuse since
        /// #800. NaN (0 / 0) for a spacecraft without an engine.
        /// </summary>
        public double GetTotalISP()
        {
            return (Engines.Sum(x => x.Thrust) / Constants.g0) / GetTotalFuelFlow();
        }

        /// <summary>
        /// The sum of the fuel flows of every engine of the spacecraft, kg/s, whatever their tanks hold (decision 60 of
        /// #668, #801 part 1; before lot L5b, the engines whose tank held fuel at the start of a run); 0 without an engine.
        /// </summary>
        public double GetTotalFuelFlow()
        {
            return Engines.Sum(x => x.FuelFlow);
        }

        /// <summary>
        /// The fuel of the spacecraft's own tanks at the start of a run, kg: the sum of their initial quantities, the child
        /// spacecraft's tanks aside (decision 60 of #668, decided by S. Guillet, 2026-11-23; <c>GetTotalFuel</c> before
        /// 11.0.0, the same value).
        /// </summary>
        public double GetInitialFuel()
        {
            return FuelTanks.Sum(x => x.InitialQuantity);
        }

        /// <summary>
        /// The fuel left in the spacecraft's own tanks, kg: the sum of their quantities, what the last run left (the initial
        /// fuel before any run and after the re-arm of the next), the child spacecraft's tanks aside (decision 60 of #668).
        /// </summary>
        public double GetRemainingFuel()
        {
            return FuelTanks.Sum(x => x.Quantity);
        }

        /// <summary>
        /// Sets the maneuver plan of the spacecraft: <paramref name="maneuvers"/> in order, each linked to the next and the
        /// last to none (<see cref="Maneuver.Maneuver.NextManeuver"/>), read back by <see cref="Maneuvers"/>; no maneuver
        /// clears the plan, so the next propagation fires nothing. It is the one way to link a plan (decision 45 of #668,
        /// decided by S. Guillet, 2026-11-23: before 11.0.0 <c>SetStandbyManeuver</c> set the head of a chain that
        /// <c>Maneuver.SetNextManeuver</c> linked). Every later propagation runs the plan from its first maneuver, whatever
        /// an earlier one executed; nothing is added to <see cref="ExecutedManeuvers"/>, which lists the maneuvers of the
        /// last propagation (#584).
        /// </summary>
        /// <param name="maneuvers">The maneuvers of the plan, in order; none to clear it.</param>
        /// <remarks>
        /// <para>
        /// The plan claims each of its maneuvers for this spacecraft (decision 46 of #668, decided by S. Guillet,
        /// 2026-11-23): a maneuver is in one plan at a time, and the claim is released, its link cleared, when this
        /// spacecraft sets another plan without it, after which another spacecraft may take it. A maneuver keeps the
        /// minimum epoch of its constructor: a later one is a new maneuver in a new plan (decision 47; before 11.0.0
        /// <c>SetStandbyManeuver(maneuver, minimumEpoch)</c> raised it).
        /// </para>
        /// <para>
        /// The checks run here, where the call receives the maneuvers, and again for what changed since (another initial
        /// orbit, whose frame the targets no longer share): a propagator built after the change throws
        /// <see cref="ArgumentException"/> (<c>spacecraft</c>), the case of <see cref="Propagate(Window, PropagationOptions, CancellationToken)"/>, a scenario and a batch,
        /// which build theirs (decision 94 of #668, decided by S. Guillet, 2026-11-23); a propagator built before it throws
        /// <see cref="InvalidOperationException"/> at its <c>Propagate</c>, through the initial-orbit guard (#783) or the
        /// re-check of the chain. So the tanks of the engines are added (<see cref="AddFuelTank"/>, and the engines with
        /// <see cref="AddEngine"/>) and the initial orbit set before the plan.
        /// </para>
        /// <para>
        /// The plan of a spacecraft is not changed during a run on it (decision 96 of #668, decided by S. Guillet,
        /// 2026-11-24): a run reads the plan as it goes (its head at the re-arm, each next maneuver when the previous one
        /// ends), so a call from another thread, or from code the run calls (a force, a custom integrator), mixes the two
        /// plans; in the second review of L5a a call that set the plan [m1, m2] again during a run of [m1, m2] left
        /// <see cref="ExecutedManeuvers"/> reading m1, m1. Set the plan before the run, or after it has returned. Nothing
        /// checks the rule yet: the run guard of #793, after lot L5, will make this method throw
        /// <see cref="InvalidOperationException"/> while a run holds the spacecraft.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="maneuvers"/> is null.</exception>
        /// <exception cref="ArgumentException">Naming <c>maneuvers</c>: a maneuver is null; or appears twice, which would
        /// make the chain a cycle (a repeated burn is a new maneuver, #585); or is in the plan of another spacecraft; or
        /// draws on an engine that is not one of this spacecraft's <see cref="Engines"/>, added with <see cref="AddEngine"/>
        /// and compared by reference (decision 92 of #668), or whose fuel tank is not one of this spacecraft's (#676); or, for an
        /// attitude, points an instrument of another spacecraft, compared by reference (#797, decision 66 of #668, decided by
        /// S. Guillet, 2026-11-23: the run pointed that instrument's boresight); or, for
        /// an impulse, has its target
        /// orbit in another frame than the one the spacecraft is propagated in (GCRF and ICRF counting as one, #685). Nothing
        /// changes.</exception>
        public void SetManeuvers(params Maneuver.Maneuver[] maneuvers)
        {
            ArgumentNullException.ThrowIfNull(maneuvers);
            var plan = (Maneuver.Maneuver[])maneuvers.Clone();
            var listed = new HashSet<Maneuver.Maneuver>(ReferenceEqualityComparer.Instance);
            lock (PlanSync)
            {
                for (int i = 0; i < plan.Length; i++)
                {
                    var maneuver = plan[i] ?? throw new ArgumentException($"The maneuver at index {i} of the plan of {Name} is null.", nameof(maneuvers));
                    if (!listed.Add(maneuver))
                    {
                        throw new ArgumentException(
                            $"The {maneuver.GetType().Name} at index {i} of the plan of {Name} appears in it twice, which would make the maneuver chain a cycle: " +
                            "a maneuver appears in a plan once; a repeated burn is a new maneuver.", nameof(maneuvers));
                    }

                    if (maneuver.PlanOwner != null && !ReferenceEquals(maneuver.PlanOwner, this))
                    {
                        throw new ArgumentException(
                            $"The {maneuver.GetType().Name} at index {i} of the plan of {Name} is in the plan of the spacecraft {maneuver.PlanOwner.Name} ({maneuver.PlanOwner.NaifId}): " +
                            "a maneuver is in one plan at a time. Build a new maneuver for this plan, or set the other spacecraft's plan without it first.",
                            nameof(maneuvers));
                    }

                    if (EngineError(maneuver) is { } engineError)
                    {
                        throw new ArgumentException(engineError, nameof(maneuvers));
                    }

                    if (InstrumentError(maneuver) is { } instrumentError)
                    {
                        throw new ArgumentException(instrumentError, nameof(maneuvers));
                    }
                }

                if (InitialOrbitalParameters != null &&
                    PropagatorBase.TargetOrbitFrameError(this, plan, PropagatorBase.PropagationFrame(InitialOrbitalParameters)) is { } frameError)
                {
                    throw new ArgumentException(frameError, nameof(maneuvers));
                }

                // The claims of the former plan are released for the maneuvers this one does not hold
                foreach (var former in _maneuvers)
                {
                    if (!listed.Contains(former))
                    {
                        former.PlanOwner = null;
                        former.NextManeuver = null;
                    }
                }

                for (int i = 0; i < plan.Length; i++)
                {
                    plan[i].PlanOwner = this;
                    plan[i].LastPlanSpacecraft = this;
                    plan[i].NextManeuver = i + 1 < plan.Length ? plan[i + 1] : null;
                }

                _maneuvers = Array.AsReadOnly(plan);
                StandbyManeuver = PlanHead;
            }
        }

        /// <summary>
        /// Put the next maneuver of the chain in standby during a propagation, gated at <paramref name="gate"/>, the end of
        /// the window of the maneuver that precedes it, for this propagation only: the gate is raised on
        /// <see cref="Maneuver.Maneuver.PropagationGate"/>, which the next <c>Propagate</c> clears, never on the minimum epoch
        /// the maneuver was given (#573). The maneuver in standby until then is added to <see cref="ExecutedManeuvers"/>.
        /// The library's advances of a chain go through it, never through <see cref="SetManeuvers"/>, which sets the plan
        /// (#584).
        /// </summary>
        /// <param name="maneuver">The next maneuver of the chain; null at its end.</param>
        /// <param name="gate">The end of the window of the maneuver before it; null for none.</param>
        internal void ChainStandbyManeuver(Maneuver.Maneuver maneuver, Time? gate)
        {
            if (gate.HasValue)
            {
                maneuver?.RaisePropagationGate(gate.Value);
            }

            if (StandbyManeuver != null)
            {
                // A new list that adds it, swapped in: a list once read never changes (decision 58 of #668)
                var executed = Volatile.Read(ref _executedManeuvers);
                var next = new Maneuver.Maneuver[executed.Count + 1];
                for (int i = 0; i < executed.Count; i++)
                {
                    next[i] = executed[i];
                }

                next[^1] = StandbyManeuver;
                Volatile.Write(ref _executedManeuvers, Array.AsReadOnly(next));
            }

            StandbyManeuver = maneuver;
        }

        /// <summary>
        /// Set the initial orbital parameter. The spacecraft is then about its observer: <see cref="CelestialItem.CenterOfMotionId"/>
        /// and <see cref="CelestialItem.BarycenterOfMotionId"/> become those of the new orbit, as the constructor gives them
        /// (#747).
        /// </summary>
        /// <param name="orbitalParameters"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public void SetInitialOrbitalParameters(OrbitalParameters.OrbitalParameters orbitalParameters)
        {
            if (orbitalParameters == null) throw new ArgumentNullException(nameof(orbitalParameters));
            (InitialOrbitalParameters?.Observer as CelestialBody)?.RemoveSatellite(this);

            InitialOrbitalParameters = orbitalParameters;
            SetCustomCenterOfMotion(orbitalParameters);
            (InitialOrbitalParameters?.Observer as CelestialBody)?.AddSatellite(this);
        }


        /// <summary>
        /// The orientation of the spacecraft relative to <paramref name="referenceFrame"/>:
        /// <c>referenceFrame.ToFrame(Frame, epoch)</c>, the rotation from <paramref name="referenceFrame"/> to the spacecraft
        /// frame (the C-matrix of a CK), labelled with <paramref name="referenceFrame"/>, its source, with the angular
        /// velocity of the spacecraft relative to <paramref name="referenceFrame"/>, expressed in it.
        /// </summary>
        /// <param name="referenceFrame">The frame the orientation is relative to.</param>
        /// <param name="epoch">The epoch.</param>
        /// <returns>The rotation <paramref name="referenceFrame"/> to the spacecraft frame.</returns>
        public StateOrientation GetOrientation(Frame referenceFrame, in Time epoch)
        {
            return referenceFrame.ToFrame(Frame, epoch);
        }

        /// <summary>
        /// Write frame
        /// </summary>
        /// <param name="outputFile"></param>
        private async Task WriteFrameAsync(FileInfo outputFile)
        {
            await (Frame as SpacecraftFrame)!.WriteAsync(outputFile);
        }

        /// <summary>
        /// Writes the attitude of the last propagation as a type-3 CK (<see cref="SpacecraftFrame"/>'s remarks): the
        /// C-matrix, J2000 to the spacecraft frame, with the angular velocity of the spacecraft relative to J2000 in J2000,
        /// at each entry of the attitude record, and the earlier attitude repeated 1 µs before each discrete change. The
        /// spacecraft's clock kernel must be loaded.
        /// </summary>
        /// <param name="outputFile">The CK to write.</param>
        /// <exception cref="InvalidOperationException">The spacecraft is kernel-backed, or it has not been propagated.</exception>
        public void WriteOrientation(FileInfo outputFile)
        {
            Frame.WriteOrientation(outputFile, this);
        }

        /// <summary>
        /// Propagate the spacecraft over <paramref name="window"/> with the settings of <paramref name="options"/> (R06 of
        /// #668): by default RK7(8) at 1e-9, an output state every 60 s, the central body of the initial orbit alone with
        /// its models, and the covariance of the initial orbital parameters propagated when it can be (Auto).
        /// </summary>
        /// <remarks>
        /// A spacecraft whose initial orbit is a TLE or a TEME state starts from its SGP4 (or given) state at the window
        /// start, rotated once to ICRF (#675); its Earth is a point mass, which <see cref="PropagationOptions.Geopotential"/>
        /// fills (R12 of #668: a body without a model counts as not given). The propagation replaces the stored states of
        /// the spacecraft once the settings are checked, so that a rejected input leaves the previous propagation as it was
        /// (#583). The resolved settings are on <see cref="PropagationSolution.Options"/>.
        /// </remarks>
        /// <param name="window">The propagation window.</param>
        /// <param name="options">The settings; null for <see cref="PropagationOptions.Default"/>.</param>
        /// <param name="cancellationToken">Stops the run (R28 of #668): read before the spacecraft is re-armed, so that a
        /// token already cancelled changes nothing, then at each accepted step of the integrator, never after the last
        /// segment; see <see cref="PropagatorBase.Propagate"/> for the spacecraft a stopped run leaves. Synchronous:
        /// a caller that wants a task runs this method in one (<c>Task.Run</c>); <c>PropagateAsync</c>, which took no
        /// token, is removed in 11.0.0.</param>
        /// <returns>The solution.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled; the exception
        /// carries it.</exception>
        /// <exception cref="InvalidOperationException">The spacecraft comes from a kernel; drag is asked about a central
        /// body without an atmosphere; or the covariance cannot be propagated (see
        /// <see cref="PropagationOptions.CovariancePropagation"/>).</exception>
        /// <exception cref="ArgumentException">The settings contradict each other or the inputs (see
        /// <see cref="CentralBodyPropagator(in Window, Spacecraft, PropagationOptions)"/>), or the target orbit of an
        /// impulse of the maneuver chain is not in the frame the spacecraft is propagated in (#685).</exception>
        /// <exception cref="NotSupportedException">The covariance propagation is asked for under Velocity-Verlet.</exception>
        public PropagationSolution Propagate(Window window, PropagationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (_isFromKernel) throw new InvalidOperationException("Cannot propagate a kernel-backed spacecraft.");
            var propagator = new CentralBodyPropagator(window, this, options);
            return propagator.Propagate(cancellationToken);
        }

        /// <summary>
        /// The propagation of a scenario (<see cref="Mission.Scenario.Simulate(PropagationOptions, CancellationToken)"/>):
        /// the scenario's bodies when <see cref="PropagationOptions.ThirdBodies"/> is null.
        /// </summary>
        internal PropagationSolution Propagate(Window window, PropagationOptions options,
            IEnumerable<CelestialItem> scenarioBodies, CancellationToken cancellationToken)
        {
            if (_isFromKernel) throw new InvalidOperationException("Cannot propagate a kernel-backed spacecraft.");
            var propagator = new CentralBodyPropagator(window, this, options, scenarioBodies);
            return propagator.Propagate(cancellationToken);
        }

        /// <summary>
        /// Computes the geometric state relative to a reference body.
        /// Uses cached propagation states with Lagrange interpolation, chaining via SPICE if needed.
        /// </summary>
        /// <remarks>
        /// A spacecraft that holds propagated states (<see cref="InterpolatedStatesWindow"/>) is read within them only: an
        /// epoch before the first or after the last throws, where the 8-point polynomial was extrapolated before 11.0.0
        /// without an error, 567 km off 6 min before the first state and 8 282 km 6 min after the last in LEO (#527, decided
        /// by S. Guillet, 2026-11-17). The epoch a light-time correction reads, the observation epoch shifted by the light
        /// time, may lie up to that light time outside them (see <see cref="CelestialItem.GetEphemeris(in IO.Astrodynamics.TimeSystem.Window, IO.Astrodynamics.Body.ILocalizable, IO.Astrodynamics.Frames.Frame, IO.Astrodynamics.Aberration, in System.TimeSpan)"/>), the observation
        /// epoch itself being read within them.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The spacecraft holds propagated states and <paramref name="epoch"/>
        /// lies outside them, as a read past the coverage of SPICE, or in a gap between the windows given to
        /// <see cref="AddPropagatedStates"/> (#792), or beside an arc of one state.</exception>
        public override StateVector GetGeometricStateRelativeTo(in Time epoch, CelestialItem referenceBody)
        {
            return GetGeometricStateRelativeTo(epoch, referenceBody, 0.0);
        }

        /// <summary>
        /// The geometric state relative to <paramref name="referenceBody"/>, the propagated states read up to
        /// <paramref name="margin"/> seconds outside them: the light time of a corrected read (#527).
        /// </summary>
        internal StateVector GetGeometricStateRelativeTo(in Time epoch, CelestialItem referenceBody, double margin)
        {
            // Kernel-backed: delegate to base which uses SPICE directly
            if (_isFromKernel)
                return base.GetGeometricStateRelativeTo(epoch, referenceBody);

            // TLE fallback: SGP4, unless a numerical propagation stored its states (#675)
            if (InitialOrbitalParameters is TLE && !_holdsPropagatedStates)
                return InitialOrbitalParameters.ToStateVector(epoch)
                    .ToFrame(Frames.Frame.ICRF)
                    .RelativeTo(referenceBody, Aberration.None).ToStateVector();

            // No stored state: the two-body orbit, in ICRF as every reader assumes (#535); a store of one state is read
            // through the arcs, at its epoch only (decision 89 of #668, decided by S. Guillet, 2026-11-23)
            if (_propagatedStates.Count == 0)
                return InitialOrbitalParameters.ToStateVector(epoch)
                    .ToFrame(Frames.Frame.ICRF)
                    .RelativeTo(referenceBody, Aberration.None).ToStateVector();

            // Interpolate the arc of the epoch (#540), within the propagated states (#527), never in a gap between added
            // windows (#792)
            var interpolated = _propagatedStates.InterpolateWithin(epoch, margin) ?? throw OutsidePropagatedStates(epoch, margin);
            var cb = (CelestialItem)_propagatedStates.First.Observer;

            if (cb.NaifId == referenceBody.NaifId)
                return new StateVector(interpolated.Position, interpolated.Velocity,
                    referenceBody, epoch, Frames.Frame.ICRF);

            // Chain: spacecraft_CB + SPICE(CB, referenceBody)
            var cbFromRef = cb.GetGeometricStateRelativeTo(epoch, referenceBody);
            return new StateVector(
                cbFromRef.Position + interpolated.Position,
                cbFromRef.Velocity + interpolated.Velocity,
                referenceBody, epoch, Frames.Frame.ICRF);
        }

        /// <summary>
        /// Spacecraft ephemeris using reference-body approach.
        /// Short-circuits when observer IS the central body with no aberration.
        /// </summary>
        public override StateVector GetEphemeris(in Time epoch, ILocalizable observer, Frame frame, Aberration aberration)
        {
            // Kernel-backed: use SPICE directly when observer is also SPICE-backed
            if (_isFromKernel && observer.IsSpiceBacked)
                return _dataProvider.GetEphemeris(epoch, this, observer, frame, aberration);

            if (_isFromKernel)
            {
                // Non-SPICE observer: reference-body approach, but GetGeometricStateRelativeTo delegates to SPICE
                return ApparentStateFromReferenceBody(this, epoch, observer, frame, aberration, GetReferenceBodyFor(observer));
            }

            // Determine reference body
            CelestialItem cb = (_propagatedStates.Count > 0)
                ? (CelestialItem)_propagatedStates.First.Observer
                : InitialOrbitalParameters?.Observer as CelestialItem ?? Barycenters.SOLAR_SYSTEM_BARYCENTER;

            // Short-circuit: observer IS the CB, no aberration, have cached states, the epoch within them (#527: outside,
            // or in a gap between added windows (#792), the reference-body path throws for propagated states, and a TLE
            // spacecraft sampled by TLEPropagator reads SGP4)
            if (aberration == Aberration.None && _propagatedStates.Count > 0
                                              && cb.NaifId == observer.NaifId && cb.NaifId != 0
                                              && _propagatedStates.InterpolateWithin(epoch, 0.0) is { } read)
            {
                return read.ToFrame(frame);
            }

            // Reference-body approach, a non-inertial frame at the epoch of its centre (#593)
            return ApparentStateFromReferenceBody(this, epoch, observer, frame, aberration, cb);
        }

        /// <summary>
        /// Writes the stored states as an SPK: one segment of type 9 (Lagrange, unequal steps) per arc between impulses, of
        /// degree min(7, N - 1) for an arc of N states, in time order, so that at an impulse the later segment, the state
        /// after it, is the one SPICE reads (#540). The states are relative to the central body, its NAIF identifier the
        /// center of the segments, so SPICE chains the ephemerides; they are stored in ICRF whatever the frame of the initial
        /// state, so the SPK is in J2000 (#535). The file is written by the library's managed writer
        /// (<see cref="Kernels.SpkWriter"/>), in SSB mode and about any central body alike: an existing file is unloaded and
        /// replaced, and the new one is left unloaded, as the native writer did. A file <see cref="SpiceAPI.LoadKernels(FileSystemInfo)"/>
        /// loaded is unloaded as <see cref="SpiceAPI.UnloadKernels(FileSystemInfo)"/> does, so that <see cref="SpiceAPI.GetLoadedKernels"/>
        /// no longer lists it (#706).
        /// </summary>
        /// <remarks>
        /// The SPK holds positions and velocities only: the covariance of the propagated states, when the propagation
        /// carried one (<see cref="CovariancePropagationMode.Covariance"/>), is not written. It is read from the
        /// <see cref="PropagationSolution"/> (<see cref="PropagationSolution.GetCovariance"/>) or from the output states.
        /// Before 11.0.0 the native writer wrote one segment of degree 15 across every impulse, of type 8 for equal steps.
        /// An impulse at the end of a propagation opens an arc of one state, the state after it (#575), touching the arc before
        /// it, which a segment cannot hold: the SPK leaves it out, whether it is the last arc or a window added after a gap by
        /// <see cref="AddPropagatedStates"/> follows it (decision 90 of #668, decided by S. Guillet, 2026-11-23), so that its run
        /// of arcs ends with the arc before it, and SPICE reads the state before the impulse at its epoch, where
        /// <c>GetEphemeris</c> reads the state after it.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The spacecraft is kernel-backed.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="outputFile"/> is null.</exception>
        /// <exception cref="ArgumentException">No state is stored, or an arc holds a single state: a state given alone to
        /// <see cref="AddPropagatedStates"/> apart from every arc (decision 87 of #668), the arc of one state after an impulse
        /// at the end of a propagation, which touches the arc before it, aside (decision 90).</exception>
        /// <exception cref="InvalidOperationException">SPICE failed to unload the file it replaces (the reopening of the other
        /// loaded SPKs, on a deleted one SPICE holds under a relative name): the message is SPICE's, and the file is left on
        /// disk as it was, nothing written (#706: before, the failure was ignored and the file replaced).</exception>
        public override void WriteEphemeris(FileInfo outputFile)
        {
            if (_isFromKernel) throw new InvalidOperationException("Cannot write ephemeris for a kernel-backed spacecraft.");
            SpiceAPI.Instance.WriteEphemerisArcs(outputFile, NaifId, SpkArcs(_propagatedStates.Arcs));
        }

        // The arcs an SPK holds: an impulse at the end of a propagation opens an arc of one state at the epoch where the arc
        // before it ends (#575), which no segment can hold (type 9 needs two epochs at least): it is not written, whether it
        // is the last arc or a window added after a gap follows it (decision 90 of #668), and SPICE reads the state before
        // the impulse there; an arc of one state apart from the others is passed on, and refused (decision 87)
        private static IReadOnlyList<StateVector[]> SpkArcs(IReadOnlyList<StateVector[]> arcs)
        {
            var written = new List<StateVector[]>(arcs.Count);
            for (int i = 0; i < arcs.Count; i++)
            {
                if (i > 0 && arcs[i].Length == 1 && arcs[i][0].Epoch == arcs[i - 1][^1].Epoch)
                {
                    continue;
                }

                written.Add(arcs[i]);
            }

            return written.Count == arcs.Count ? arcs : written;
        }

        /// <summary>
        /// Every stored state, in epoch order: those of the last propagation, with their covariance when it propagated the
        /// covariance, both states of each impulse at its epoch (the state before first) and the states that fill a short
        /// arc (#540), or the states added by <see cref="AddPropagatedStates"/>. For the tests.
        /// </summary>
        internal StateVector[] PropagatedStates => _propagatedStates.All;

        /// <summary>The stored states in arcs, split at each impulse of the last propagation (#540).</summary>
        internal IReadOnlyList<StateVector[]> PropagatedArcs => _propagatedStates.Arcs;

        /// <summary>
        /// Replaces the stored states by the output states of a propagation, in ICRF, split at its impulses, the short arcs
        /// filled by <paramref name="fill"/> (#540).
        /// </summary>
        internal void SetPropagatedStates(IReadOnlyList<StateVector> outputs, IReadOnlyList<ImpulseBoundary> impulses,
            Func<Time, StateVector> fill)
        {
            if (_isFromKernel) throw new InvalidOperationException("Cannot add states to a kernel-backed spacecraft.");
            _propagatedStates.Set(outputs, impulses, fill);
            _holdsPropagatedStates = true;
        }

        /// <summary>
        /// The window of the propagated states that <see cref="GetGeometricStateRelativeTo(in IO.Astrodynamics.TimeSystem.Time, IO.Astrodynamics.Body.CelestialItem)"/> interpolates, from the first
        /// to the last, the gaps between the windows given to <see cref="AddPropagatedStates"/> included (#792); null when it
        /// reads no such states: a kernel-backed spacecraft, a TLE that no numerical propagation
        /// stored states for (unpropagated, or sampled by <see cref="TLEPropagator"/>), or no stored state (the initial
        /// orbital parameters are then propagated in two-body motion to the epoch). A store of one state is read through the
        /// arcs, at its epoch only, its window of zero length (decision 89 of #668, decided by S. Guillet, 2026-11-23: before,
        /// fewer than two states read the two-body orbit).
        /// </summary>
        internal Window? InterpolatedStatesWindow
        {
            get
            {
                if (_isFromKernel || (InitialOrbitalParameters is TLE && !_holdsPropagatedStates) || _propagatedStates.Count == 0)
                {
                    return null;
                }

                return _propagatedStates.Span;
            }
        }

        // The exception of a read outside the propagated states the spacecraft interpolates, widened by margin seconds (#527),
        // or in a gap between them (#792)
        private InvalidOperationException OutsidePropagatedStates(in Time epoch, double margin)
        {
            return new InvalidOperationException(
                $"The epoch {epoch.ToTDB()} lies outside the propagated states of {Name}, {DescribeStoredWindows()}" +
                (margin > 0.0 ? $" widened by the light time, {margin:R} s" : "") +
                ": they would be extrapolated. Read the spacecraft within its propagation window, or propagate it over the epochs read.");
        }

        // The windows of the stored states, as [start, end] in TDB, separated by commas
        private string DescribeStoredWindows() =>
            string.Join(", ", _propagatedStates.Windows.Select(w => $"[{w.StartDate.ToTDB()}, {w.EndDate.ToTDB()}]"));

        // The initial state of a kernel-backed spacecraft relative to its centre of motion, the Earth by default (#620); the
        // identifier and the clock checked before the read (#676)
        private static StateVector ReadKernelInitialState(int naifId, string name, in Time epoch, CelestialItem centerOfMotion, Clock clock)
        {
            if (naifId >= 0) throw new ArgumentOutOfRangeException(nameof(naifId), "Kernel-backed spacecraft NAIF ID must be negative.");
            CheckFrameIdRange(naifId);
            clock?.CheckAttachableTo(naifId, name);
            return SpiceAPI.Instance.ReadEphemerisRaw(epoch, centerOfMotion ?? new CelestialBody(PlanetsAndMoons.EARTH), naifId,
                Frames.Frame.ICRF, Aberration.None);
        }

        /// <summary>
        /// Throws when a window leaves the propagated states that <see cref="GetGeometricStateRelativeTo(in IO.Astrodynamics.TimeSystem.Time, IO.Astrodynamics.Body.CelestialItem)"/> interpolates,
        /// which would be extrapolated (#527), or meets a gap between the windows given to <see cref="AddPropagatedStates"/>
        /// (decision 86 of #668, #792, decided by S. Guillet, 2026-11-23): the window must lie within one run of arcs that
        /// touch; nothing to check without such states (<see cref="InterpolatedStatesWindow"/>).
        /// </summary>
        /// <param name="window">The window read.</param>
        /// <param name="description">What the window is, for the message.</param>
        /// <param name="paramName">The parameter of the window.</param>
        /// <exception cref="ArgumentException">The window leaves the propagated states or meets a gap between them.</exception>
        internal void CheckInterpolatedStatesCover(in Window window, string description, string paramName)
        {
            if (InterpolatedStatesWindow is null)
            {
                return;
            }

            if (!_propagatedStates.Covers(window))
            {
                throw new ArgumentException(
                    $"The {description} [{window.StartDate.ToTDB()}, {window.EndDate.ToTDB()}] leaves the propagated states of {Name}, {DescribeStoredWindows()}: they would be extrapolated.",
                    paramName);
            }
        }

        /// <summary>
        /// The solution of the last propagation, whose output states the spacecraft stores, when it was an RK7(8)
        /// propagation; null before any propagation, after a reset and after a propagation by another integrator, which
        /// leaves nothing to replay. The conjunction analysis reads the covariance at the TCA from it, or replays its
        /// variational equations (step 10, E2). It keeps the accepted steps, the forces of the propagation and their
        /// ephemeris cache alive until the next propagation (with <see cref="LastForceModel"/> 0.58 MB for one day in LEO
        /// with EGM2008 10×10, the Moon, the Sun and drag, RK7(8) at 1e-9, and at 1e-11 on Linux and Windows (1.11 MB on
        /// macOS arm64), and 1.04 to 1.07 MB with an initial covariance propagated in the run, a covariance per output
        /// state; measured alone in the CI of #789 at aefd6c2b on Linux x64, Windows x64 and macOS arm64, lot L4 of #668,
        /// <c>DefaultIntegratorFiguresTests</c>, no threshold).
        /// </summary>
        internal PropagationSolution LastPropagation { get; set; }

        /// <summary>
        /// The force model of the last propagation by RK7(8) or Velocity-Verlet (forces with their ephemeris cache,
        /// integrator settings, orientation cache, mass and coefficients of each segment), without its steps, for the
        /// avoidance options of the conjunction assessment (#402); null after another integrator or none. About 0.34 MB for
        /// one day in LEO with EGM2008 10×10, NRLMSISE-00, the Moon and the Sun, kept until the next propagation (0.35 MB
        /// with a covariance propagated; measured alone in the CI of #789 at aefd6c2b on Linux x64, Windows x64 and macOS
        /// arm64, lot L4 of #668, <c>DefaultIntegratorFiguresTests</c>).
        /// </summary>
        internal PropagationForceModel LastForceModel { get; set; }

        /// <summary>
        /// Adds the states of one window to the stored states: one window per call (decisions 42, 83 to 87 and 89 to 91 of
        /// #668, #792, decided by S. Guillet, 2026-11-23). When the hull of their epochs, closed, meets no stored arc (touching counts as
        /// meeting), they make a new arc, inserted in time order; otherwise the arcs it meets are merged where a gap separates
        /// them, so a call over the gap between two windows joins them (arcs that touch at an impulse stay apart), and each
        /// state joins the arc of its epoch among them (the later one at an impulse), replacing a stored state of that arc at
        /// the same epoch. A gap inside the states of one call is not detected: give each window its own call. A read in a
        /// gap between two windows throws as a read outside the stored states (<see cref="GetGeometricStateRelativeTo(in IO.Astrodynamics.TimeSystem.Time, IO.Astrodynamics.Body.CelestialItem)"/>),
        /// the light time of a corrected read allowed outside each arc, and a window argument that meets a gap is refused;
        /// an arc of one state reads at its exact epoch only, and <see cref="WriteEphemeris"/> refuses it, a spacecraft that
        /// stores one state only included (the two-body orbit of the initial orbital parameters reads only a spacecraft that
        /// stores none). An epoch inside an arc is read by that arc; one in a gap within the light time of two arcs by the
        /// nearer, the later one on a tie. Before 11.0.0
        /// every state joined the arc of its epoch, so two windows made one arc read across the gap between them.
        /// They are kept in ICRF, the frame of the readers (#535). Every propagation replaces every stored state, those
        /// added before it included, on every path (11.0.0, #540: they were merged with the propagated ones before), and so
        /// does a <see cref="TLEPropagator"/> run, which re-arms the spacecraft and stores its samples alone (decision 38 of
        /// #668, decided by S. Guillet, 2026-11-23: before 11.0.0 it added them to the stored states).
        /// </summary>
        /// <param name="stateVectors">The states, relative to one central body.</param>
        /// <exception cref="InvalidOperationException">The spacecraft is kernel-backed.</exception>
        public void AddPropagatedStates(params StateVector[] stateVectors)
        {
            if (_isFromKernel) throw new InvalidOperationException("Cannot add states to a kernel-backed spacecraft.");
            ArgumentNullException.ThrowIfNull(stateVectors);
            _propagatedStates.Add(stateVectors.Select(sv => sv.Frame == Frames.Frame.ICRF ? sv : sv.ToFrame(Frames.Frame.ICRF).ToStateVector()).ToArray());
        }

        internal override OrbitalParameters.OrbitalParameters GetGeometricStateFromICRF(in Time date)
        {
            return GetGeometricStateRelativeTo(date, Barycenters.SOLAR_SYSTEM_BARYCENTER);
        }

        /// <summary>
        /// Creates a CCSDS OPM (Orbit Parameter Message) of this spacecraft at <paramref name="epoch"/>: its state, its mass
        /// and its maneuvers at that instant (#658, decided by S. Guillet, 2026-11-17). A spacecraft that holds propagated
        /// states (by <c>Propagate</c>, <c>Scenario.Simulate</c>, a propagator built directly or
        /// <see cref="AddPropagatedStates"/>) writes the state read from them, <see cref="GetEphemeris"/> relative to the
        /// observer and in the frame of its initial orbital parameters without aberration, with the mass and the impulses
        /// ignited by then; one without them, the motion of its initial orbital parameters (two-body for
        /// osculating elements, SGP4 for a TLE, also when <see cref="TLEPropagator"/> sampled it) with its mass now and the
        /// maneuvers of its last propagation, none for a spacecraft never propagated, as before 11.0.0.
        /// </summary>
        /// <param name="originator">The message originator. If null, uses "IO.Astrodynamics".</param>
        /// <param name="epoch">The epoch of the state. If null, the epoch of the initial orbital parameters.</param>
        /// <param name="includeKeplerianElements">If true, includes optional Keplerian elements in the OPM.</param>
        /// <param name="includeSpacecraftParameters">If true, includes spacecraft parameters (mass, drag, SRP).</param>
        /// <param name="includeManeuvers">If true, includes the maneuvers ignited by the epoch (all those executed for a
        /// spacecraft without propagated states).</param>
        /// <returns>A new OPM instance representing this spacecraft's state.</returns>
        /// <exception cref="InvalidOperationException">CosparId is not set; or the spacecraft holds propagated states and
        /// the epoch, given or by default, lies outside them (#527).</exception>
        /// <remarks>
        /// <para>
        /// The mass of a propagated spacecraft at the epoch is its mass after the propagation plus the fuel of the impulses
        /// ignited after the epoch; an impulse counts from its ignition, the start of its thrust window, the
        /// <c>MAN_EPOCH_IGNITION</c> written (#658, decided by S. Guillet, 2026-11-17: "the mass and the maneuvers ignited by
        /// then"). The propagated state receives the delta-V of an impulse at its event, the centroid of the acceleration of
        /// the burn, about half of the burn after the ignition (<see cref="Maneuver.Maneuver.ThrustWindow"/>, #335): at an
        /// epoch between the two (2.6 s for a 5.2 s burn) the state written is the state before the impulse, with the burn
        /// listed and its fuel spent. Before 11.0.0 the state
        /// was the motion of the initial orbit whatever the propagation, with the mass and every maneuver after the
        /// propagation: 1 212 km and 1 137 m/s off the propagated state one burn later.
        /// </para>
        /// <para>
        /// Maneuver delta-V components are exported in the same inertial reference frame as the state vector
        /// (typically ICRF or EME2000). This is the frame in which the framework stores maneuver data. <c>REF_FRAME</c> and
        /// each <c>MAN_REF_FRAME</c> carry the CCSDS name of that frame (#339): <c>ICRF</c> for <see cref="Frame.ICRF"/>
        /// (the SPICE frame <c>J2000</c>, written before 11.0.0), the frame's own name otherwise.
        /// </para>
        /// <para>
        /// If you need maneuvers in an orbital frame (RSW, RTN, TNW), you must perform the frame transformation
        /// yourself before or after the OPM export. The OPM standard supports both inertial and orbital frames
        /// for maneuver representation via the MAN_REF_FRAME field.
        /// </para>
        /// <para>
        /// The covariance of the initial state (<see cref="StateVector.Covariance"/>, m², m²/s, m²/s²) is exported, in km²,
        /// km²/s, km²/s², at the instant of the initial epoch (the default) (#551). At any other epoch the OPM carries no
        /// covariance: moving it there would take the state transition matrix of the motion, which this export does not
        /// compute (<see cref="StateVector.ToStateVector(Time)"/>).
        /// </para>
        /// </remarks>
        public Opm ToOpm(string originator = null, Time? epoch = null, bool includeKeplerianElements = true, bool includeSpacecraftParameters = true, bool includeManeuvers = true)
        {
            if (string.IsNullOrWhiteSpace(CosparId))
            {
                throw new InvalidOperationException("CosparId must be set to create an OPM. Use the spacecraft constructor's cosparId parameter.");
            }

            // The state at the epoch (or the initial epoch): the propagated states when the spacecraft holds them (#658),
            // relative to the observer and in the frame of the initial orbital parameters, within them (#527); the motion
            // of the initial orbit otherwise
            var targetEpoch = epoch ?? InitialOrbitalParameters.Epoch;
            var initialState = InitialOrbitalParameters.ToStateVector(targetEpoch);
            bool propagated = InterpolatedStatesWindow.HasValue;
            var stateVector = initialState;
            if (propagated)
            {
                var read = GetEphemeris(targetEpoch, InitialOrbitalParameters.Observer, InitialOrbitalParameters.Frame, Aberration.None);
                stateVector = new StateVector(read.Position, read.Velocity, InitialOrbitalParameters.Observer, targetEpoch, InitialOrbitalParameters.Frame,
                    initialState.Covariance);
            }

            // The impulses of the last propagation ignited by the epoch (#658, decided by S. Guillet, 2026-11-17), whose
            // fuel the mass has spent; all of them for a spacecraft without propagated states, as before
            static bool IgnitedBy(Maneuver.Maneuver m, Time t) => m is Maneuver.ImpulseManeuver { ThrustWindow: { } thrust } && thrust.StartDate <= t;
            bool ExecutedBy(Maneuver.Maneuver m) => !propagated || IgnitedBy(m, targetEpoch);
            double laterFuel = propagated
                ? ExecutedManeuvers.OfType<Maneuver.ImpulseManeuver>().Where(m => m.ThrustWindow.HasValue && !IgnitedBy(m, targetEpoch)).Sum(m => m.FuelBurned)
                : 0.0;

            // Create header
            var header = originator != null
                ? CcsdsHeader.Create(originator)
                : CcsdsHeader.Create("IO.Astrodynamics");

            // Create metadata
            var metadata = new OpmMetadata(
                objectName: Name,
                objectId: CosparId,
                centerName: stateVector.Observer.Name,
                referenceFrame: CcsdsFrameNames.ToCcsdsName(stateVector.Frame),
                timeSystem: "UTC");

            // Create state vector DTO (converts m to km, m/s to km/s)
            var opmStateVector = OpmStateVector.FromStateVector(stateVector);

            // Create optional Keplerian elements
            OpmKeplerianElements keplerianElements = null;
            if (includeKeplerianElements)
            {
                // km, degrees (each angle in [0, 360), angleRange of NDM/XML 4.0.0, #708) and km³/s², with the true anomaly
                keplerianElements = OpmKeplerianElements.FromKeplerianElements(stateVector.ToKeplerianElements());
            }

            // Convert covariance from framework units (m²) to CCSDS units (km²)
            CovarianceMatrix covariance = null;
            if (stateVector.Covariance.HasValue)
            {
                covariance = CovarianceMatrix.FromMatrixWithUnitConversion(stateVector.Covariance.Value);
            }

            // Convert executed maneuvers if requested
            IReadOnlyList<OpmManeuverParameters> maneuvers = null;
            if (includeManeuvers && ExecutedManeuvers.Count > 0)
            {
                var maneuverList = new List<OpmManeuverParameters>();
                foreach (var maneuver in ExecutedManeuvers.Where(ExecutedBy).OrderBy(m => m.ThrustWindow?.StartDate))
                {
                    if (maneuver is Maneuver.ImpulseManeuver impulseManeuver && impulseManeuver.ThrustWindow.HasValue)
                    {
                        maneuverList.Add(impulseManeuver.ToOpmManeuverParameters(CcsdsFrameNames.ToCcsdsName(stateVector.Frame)));
                    }
                }

                if (maneuverList.Count > 0)
                {
                    maneuvers = maneuverList;
                }
            }

            // Build OPM data with all optional components
            var data = new OpmData(
                stateVector: opmStateVector,
                keplerianElements: keplerianElements,
                mass: includeSpacecraftParameters ? GetTotalMass() + laterFuel : null,
                solarRadiationPressureArea: includeSpacecraftParameters ? SectionalArea : null,
                solarRadiationCoefficient: includeSpacecraftParameters ? SolarRadiationCoeff : null,
                dragArea: includeSpacecraftParameters ? SectionalArea : null,
                dragCoefficient: includeSpacecraftParameters ? DragCoefficient : null,
                covariance: covariance,
                maneuvers: maneuvers);

            return new Opm(header, metadata, data);
        }

        /// <summary>
        /// Re-arms the spacecraft for a propagation (#583): the solution, the force model, the stored states and the attitude
        /// record are dropped, whatever filled them and whatever <see cref="IsPropagated"/> says (decision 38 of #668,
        /// decided by S. Guillet, 2026-11-23: before 11.0.0 they were dropped only after a numerical propagation, so a first
        /// run kept the attitudes added by hand), <see cref="ExecutedManeuvers"/> cleared, the tanks refuelled, and
        /// the chain from the first maneuver of <see cref="Maneuvers"/> reset (its windows, the gates and every other value
        /// of the last propagation, decision 52 of #668) and put back
        /// in standby. <see cref="PropagatorBase.Propagate"/> calls it once its checks pass, on every path: before 11.0.0
        /// only <c>Spacecraft.Propagate</c> did, so a second run through the builder of 10.x or a
        /// <see cref="CentralBodyPropagator"/> built directly fired no burn and started with the fuel left by the first.
        /// <see cref="TLEPropagator"/> calls it too, once every sample is computed, before it stores them.
        /// </summary>
        internal void ResetPropagation()
        {
            LastPropagation = null;
            LastForceModel = null;
            DropStoredStatesAndAttitudes();

            // A new list for the run, empty until it executes a maneuver (decision 58 of #668)
            Volatile.Write(ref _executedManeuvers, NoManeuver);
            foreach (var fuelTank in FuelTanks)
            {
                fuelTank.Refuel();
            }

            Maneuver.Maneuver.ResetChain(PlanHead);
            StandbyManeuver = PlanHead;
        }

        /// <summary>
        /// Drops the stored states and the attitude record, whatever filled them (a propagation, <see cref="AddPropagatedStates"/>,
        /// <see cref="SpacecraftFrame.AddStateOrientationToICRF"/>), and marks the spacecraft not propagated: the re-arm of
        /// every run (decision 38 of #668), and a run stopped after the re-arm (C2 of lot L4 of #668), so that a TLE
        /// spacecraft reads SGP4 again and any other its initial orbit.
        /// </summary>
        internal void DropStoredStatesAndAttitudes()
        {
            _isPropagated = false;
            _propagatedStates.Clear();
            _holdsPropagatedStates = false;
            Frame.ClearStateOrientations();
        }

        /// <summary>
        /// Marks the spacecraft as propagated, at the end of a numerical propagation by any path (#583): the next
        /// propagation then drops its stored states and attitude record.
        /// </summary>
        internal void MarkPropagated()
        {
            _isPropagated = true;
        }
    }
}