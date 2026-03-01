using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.CCSDS.Common;
using IO.Astrodynamics.CCSDS.OPM;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using StateOrientation = IO.Astrodynamics.OrbitalParameters.StateOrientation;


namespace IO.Astrodynamics.Body.Spacecraft
{
    public class Spacecraft : CelestialItem, IOrientable<SpacecraftFrame>
    {
        private readonly ConcurrentDictionary<Time, StateVector> _stateVectorsRelativeToICRF = new();
        public ImmutableSortedDictionary<Time, StateVector> StateVectorsRelativeToICRF => _stateVectorsRelativeToICRF.ToImmutableSortedDictionary();

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

        private readonly HashSet<Maneuver.Maneuver> _executedManeuvers = new HashSet<Maneuver.Maneuver>();
        public IReadOnlyCollection<Maneuver.Maneuver> ExecutedManeuvers => _executedManeuvers;
        public Maneuver.Maneuver StandbyManeuver { get; private set; }
        public Maneuver.Maneuver InitialManeuver { get; private set; }
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
        public double MaximumOperatingMass { get; }
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
        /// Typical values range from 1.0 (absorbing surface) to 2.0 (reflecting surface).
        /// Default is 1.0.
        /// </remarks>
        public double SolarRadiationCoeff { get; }

        private bool _isPropagated;
        public bool IsPropagated => _isPropagated;

        /// <summary>
        /// Spacecraft constructor
        /// </summary>
        /// <param name="naifId"></param>
        /// <param name="name"></param>
        /// <param name="mass"></param>
        /// <param name="maximumOperatingMass"></param>
        /// <param name="clock"></param>
        /// <param name="initialOrbitalParameters"></param>
        /// <param name="sectionalArea">Mean sectional area (used for both drag and solar radiation pressure)</param>
        /// <param name="dragCoeff">Drag coefficient (Cd), default 2.2 for satellites in free-molecular flow</param>
        /// <param name="cosparId">COSPAR international designator (e.g., "1998-067A")</param>
        /// <param name="solarRadiationCoeff">Solar radiation pressure coefficient (Cr), default 1.0</param>
        /// <param name="bodyFront">Instance body front axis (default: +Y). Must be orthogonal to bodyRight and bodyUp.</param>
        /// <param name="bodyRight">Instance body right axis (default: +X). Must be orthogonal to bodyFront and bodyUp.</param>
        /// <param name="bodyUp">Instance body up axis (default: +Z). Must be orthogonal to bodyFront and bodyRight.</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException">Thrown when body axes are not orthogonal or not right-handed.</exception>
        public Spacecraft(int naifId, string name, double mass, double maximumOperatingMass, Clock clock, OrbitalParameters.OrbitalParameters initialOrbitalParameters,
            double sectionalArea = 1.0, double dragCoeff = 2.2, string cosparId = null, double solarRadiationCoeff = 1.0,
            Vector3? bodyFront = null, Vector3? bodyRight = null, Vector3? bodyUp = null) : base(
            naifId, name, mass, initialOrbitalParameters)
        {
            if (maximumOperatingMass < mass) throw new ArgumentOutOfRangeException(nameof(maximumOperatingMass));
            if (naifId >= 0) throw new ArgumentOutOfRangeException(nameof(naifId));
            ArgumentOutOfRangeException.ThrowIfNegative(dragCoeff);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sectionalArea);
            ArgumentOutOfRangeException.ThrowIfNegative(solarRadiationCoeff);
            MaximumOperatingMass = maximumOperatingMass;
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Clock.AttachSpacecraft(this);
            Frame = new SpacecraftFrame(this);
            SectionalArea = sectionalArea;
            DragCoefficient = dragCoeff;
            CosparId = cosparId;
            SolarRadiationCoeff = solarRadiationCoeff;

            BodyFront = bodyFront?.Normalize() ?? Vector3.VectorY;
            BodyRight = bodyRight?.Normalize() ?? Vector3.VectorX;
            BodyUp = bodyUp?.Normalize() ?? Vector3.VectorZ;

            if (bodyFront.HasValue || bodyRight.HasValue || bodyUp.HasValue)
            {
                ValidateBodyAxes();
            }
        }

        private void ValidateBodyAxes()
        {
            const double tolerance = 1e-10;

            // Check orthogonality
            if (System.Math.Abs(BodyFront * BodyRight) > tolerance)
                throw new ArgumentException("Body axes must be orthogonal: BodyFront and BodyRight are not perpendicular.");
            if (System.Math.Abs(BodyFront * BodyUp) > tolerance)
                throw new ArgumentException("Body axes must be orthogonal: BodyFront and BodyUp are not perpendicular.");
            if (System.Math.Abs(BodyRight * BodyUp) > tolerance)
                throw new ArgumentException("Body axes must be orthogonal: BodyRight and BodyUp are not perpendicular.");

            // Check right-handedness: Right x Front should equal Up
            var cross = BodyRight.Cross(BodyFront);
            if ((cross - BodyUp).Magnitude() > tolerance)
                throw new ArgumentException("Body axes must form a right-handed coordinate system: BodyRight x BodyFront must equal BodyUp.");
        }

        /// <summary>
        /// Add circular instrument to spacecraft
        /// </summary>
        /// <param name="naifId"></param>
        /// <param name="name"></param>
        /// <param name="model"></param>
        /// <param name="fieldOfView"></param>
        /// <param name="boresight"></param>
        /// <param name="refVector"></param>
        /// <param name="orientation">Expressed as Euler angle</param>
        /// <exception cref="ArgumentException"></exception>
        public void AddCircularInstrument(int naifId, string name, string model, double fieldOfView, in Vector3 boresight, in Vector3 refVector, in Vector3 orientation)
        {
            if (!_instruments.Add(new CircularInstrument(this, naifId, name, model, fieldOfView, boresight, refVector, orientation)))
            {
                throw new ArgumentException("Instrument already added to spacecraft");
            }
        }

        /// <summary>
        /// Add rectangular instrument
        /// </summary>
        /// <param name="naifId"></param>
        /// <param name="name"></param>
        /// <param name="model"></param>
        /// <param name="fieldOfView"></param>
        /// <param name="crossAngle"></param>
        /// <param name="boresight"></param>
        /// <param name="refVector"></param>
        /// <param name="orientation">Expressed as Euler angle</param>
        /// <exception cref="ArgumentException"></exception>
        public void AddRectangularInstrument(int naifId, string name, string model, double fieldOfView, double crossAngle, in Vector3 boresight, in Vector3 refVector,
            in Vector3 orientation)
        {
            if (!_instruments.Add(new RectangularInstrument(this, naifId, name, model, fieldOfView, crossAngle, boresight, refVector, orientation)))
            {
                throw new ArgumentException("Instrument already added to spacecraft");
            }
        }

        /// <summary>
        /// Add elliptical instrument
        /// </summary>
        /// <param name="naifId"></param>
        /// <param name="name"></param>
        /// <param name="model"></param>
        /// <param name="fieldOfView"></param>
        /// <param name="crossAngle"></param>
        /// <param name="boresight"></param>
        /// <param name="refVector"></param>
        /// <param name="orientation">Expressed as Euler angle</param>
        /// <exception cref="ArgumentException"></exception>
        public void AddEllipticalInstrument(int naifId, string name, string model, double fieldOfView, double crossAngle, in Vector3 boresight, in Vector3 refVector,
            in Vector3 orientation)
        {
            if (!_instruments.Add(new EllipticalInstrument(this, naifId, name, model, fieldOfView, crossAngle, boresight, refVector, orientation)))
            {
                throw new ArgumentException("Instrument already added to spacecraft");
            }
        }

        /// <summary>
        /// Add engine to spacecraft
        /// </summary>
        /// <param name="engine"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public void AddEngine(Engine engine)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (!FuelTanks.Contains(engine.FuelTank))
            {
                throw new InvalidOperationException(
                    "Unknown fuel tank, you must add fuel tank to spacecraft before add engine");
            }

            if (!_engines.Add(engine))
            {
                throw new ArgumentException("Engine already added to spacecraft");
            }
        }

        /// <summary>
        /// Add fuel tank to spacecraft
        /// </summary>
        /// <param name="fuelTank"></param>
        /// <exception cref="ArgumentException"></exception>
        public void AddFuelTank(FuelTank fuelTank)
        {
            if (fuelTank == null) throw new ArgumentNullException(nameof(fuelTank));
            if (!_fuelTanks.Add(fuelTank))
            {
                throw new ArgumentException("Fuel tank already added to spacecraft");
            }

            fuelTank.SetSpacecraft(this);
        }

        /// <summary>
        /// Add payload to spacecraft
        /// </summary>
        /// <param name="payload"></param>
        public void AddPayload(Payload payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (!_payloads.Add(payload))
            {
                throw new ArgumentException("Payload already added to spacecraft");
            }
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
        /// Get total ISP of this spacecraft
        /// </summary>
        /// <returns></returns>
        public double GetTotalISP()
        {
            return (Engines.Where(x => x.FuelTank.InitialQuantity > 0.0).Sum(x => x.Thrust) / Constants.g0) /
                   GetTotalFuelFlow();
        }

        /// <summary>
        /// Get total fuel flow of this spacecraft
        /// </summary>
        /// <returns></returns>
        public double GetTotalFuelFlow()
        {
            return Engines.Where(x => x.FuelTank.InitialQuantity > 0.0).Sum(x => x.FuelFlow);
        }

        /// <summary>
        /// Get total fuel of this spacecraft
        /// </summary>
        /// <returns></returns>
        public double GetTotalFuel()
        {
            return FuelTanks.Sum(x => x.InitialQuantity);
        }

        /// <summary>
        /// Put a maneuver in standby
        /// </summary>
        /// <param name="maneuver"></param>
        /// <param name="minimumEpoch"></param>
        public void SetStandbyManeuver(Maneuver.Maneuver maneuver, Time? minimumEpoch = null)
        {
            if (minimumEpoch > maneuver?.MinimumEpoch)
            {
                maneuver.MinimumEpoch = minimumEpoch.Value;
            }

            if (StandbyManeuver != null)
            {
                _executedManeuvers.Add(StandbyManeuver);
            }

            if (StandbyManeuver == null)
            {
                InitialManeuver = maneuver;
            }

            StandbyManeuver = maneuver;
        }

        /// <summary>
        /// Set the initial orbital parameter
        /// </summary>
        /// <param name="orbitalParameters"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public void SetInitialOrbitalParameters(OrbitalParameters.OrbitalParameters orbitalParameters)
        {
            if (orbitalParameters == null) throw new ArgumentNullException(nameof(orbitalParameters));
            (InitialOrbitalParameters?.Observer as CelestialBody)?.RemoveSatellite(this);

            InitialOrbitalParameters = orbitalParameters;
            (InitialOrbitalParameters?.Observer as CelestialBody)?.AddSatellite(this);
        }


        /// <summary>
        /// Get orientation relative to reference frame
        /// </summary>
        /// <param name="referenceFrame"></param>
        /// <param name="epoch"></param>
        /// <returns></returns>
        public StateOrientation GetOrientation(Frame referenceFrame, in Time epoch)
        {
            return referenceFrame.ToFrame(Frame, epoch);
        }

        /// <summary>
        /// Get spacecraft summary
        /// </summary>
        /// <returns></returns>
        internal SpacecraftSummary GetSummary()
        {
            return new SpacecraftSummary(this, _executedManeuvers);
        }

        /// <summary>
        /// Write frame
        /// </summary>
        /// <param name="outputFile"></param>
        private async Task WriteFrameAsync(FileInfo outputFile)
        {
            await (Frame as SpacecraftFrame)!.WriteAsync(outputFile);
        }

        public void WriteOrientation(FileInfo outputFile)
        {
            Frame.WriteOrientation(outputFile, this);
        }

        /// <summary>
        /// Propagate spacecraft
        /// </summary>
        /// <param name="window"></param>
        /// <param name="additionalCelestialBodies">Celestial bodies involved as perturbation bodies</param>
        /// <param name="includeAtmosphericDrag"></param>
        /// <param name="includeSolarRadiationPressure"></param>
        /// <param name="propagatorStepSize"></param>
        public Task PropagateAsync(Window window, IEnumerable<CelestialItem> additionalCelestialBodies, bool includeAtmosphericDrag,
            bool includeSolarRadiationPressure, TimeSpan propagatorStepSize)
        {
            return Task.Run(() => { Propagate(window, additionalCelestialBodies, includeAtmosphericDrag, includeSolarRadiationPressure, propagatorStepSize); });
        }

        public void Propagate(Window window, IEnumerable<CelestialItem> additionalCelestialBodies, bool includeAtmosphericDrag,
            bool includeSolarRadiationPressure, TimeSpan propagatorStepSize)
        {
            ResetPropagation();
            IPropagator propagator;
            if (InitialOrbitalParameters is TLE)
            {
                propagator = new TLEPropagator(window, this, propagatorStepSize);
            }
            else if (InitialOrbitalParameters.Observer is Star or Barycenter)
            {
                propagator = new SsbPropagator(window, this, additionalCelestialBodies, includeAtmosphericDrag, includeSolarRadiationPressure, propagatorStepSize);
            }
            else
            {
                propagator = new CentralBodyPropagator(window, this, additionalCelestialBodies, includeAtmosphericDrag, includeSolarRadiationPressure, propagatorStepSize);
            }

            propagator.Propagate();
            propagator.Dispose();
            _isPropagated = true;
        }

        /// <summary>
        /// Computes the geometric state relative to a reference body.
        /// Uses cached propagation states with Lagrange interpolation, chaining via SPICE if needed.
        /// </summary>
        public override StateVector GetGeometricStateRelativeTo(in Time epoch, CelestialItem referenceBody)
        {
            // TLE fallback
            if (InitialOrbitalParameters is TLE)
                return InitialOrbitalParameters.ToStateVector(epoch)
                    .ToFrame(Frames.Frame.ICRF)
                    .RelativeTo(referenceBody, Aberration.None).ToStateVector();

            // Not enough cached states
            if (_stateVectorsRelativeToICRF.Count < 2)
                return InitialOrbitalParameters.ToStateVector(epoch)
                    .RelativeTo(referenceBody, Aberration.None).ToStateVector();

            // Interpolate from cache
            var states = _stateVectorsRelativeToICRF.Values.OrderBy(x => x.Epoch).ToArray();
            var cb = (CelestialItem)states[0].Observer;
            var interpolated = Lagrange.Interpolate(states, epoch);

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
        public override OrbitalParameters.OrbitalParameters GetEphemeris(in Time epoch, ILocalizable observer, Frame frame, Aberration aberration)
        {
            // Determine reference body
            CelestialItem cb = (_stateVectorsRelativeToICRF.Count >= 2)
                ? (CelestialItem)_stateVectorsRelativeToICRF.Values.First().Observer
                : InitialOrbitalParameters?.Observer as CelestialItem ?? Barycenters.SOLAR_SYSTEM_BARYCENTER;

            // Short-circuit: observer IS the CB, no aberration, have cached states
            if (aberration == Aberration.None && _stateVectorsRelativeToICRF.Count >= 2
                && cb.NaifId == observer.NaifId && cb.NaifId != 0)
            {
                var states = _stateVectorsRelativeToICRF.Values.OrderBy(x => x.Epoch).ToArray();
                return Lagrange.Interpolate(states, epoch).ToFrame(frame);
            }

            // Reference-body approach
            var spacecraftCB = this.GetGeometricStateRelativeTo(epoch, cb);
            var observerCB = observer.GetGeometricStateRelativeTo(epoch, cb);
            var relative = new StateVector(
                spacecraftCB.Position - observerCB.Position,
                spacecraftCB.Velocity - observerCB.Velocity,
                observer, epoch, Frames.Frame.ICRF);

            if (aberration is Aberration.LT or Aberration.XLT or Aberration.LTS or Aberration.XLTS)
                relative = CorrectFromAberrationCB(this, epoch, observer, aberration, relative, observerCB, cb);

            return relative.ToFrame(frame);
        }

        /// <summary>
        /// Write spacecraft ephemeris from propagated states.
        /// Converts CB-relative states to SSB-relative if needed (SPK files need consistent observer).
        /// </summary>
        public override void WriteEphemeris(FileInfo outputFile)
        {
            var states = _stateVectorsRelativeToICRF.Values.OrderBy(x => x.Epoch).ToArray();

            if (states.Length > 0 && states[0].Observer.NaifId != 0)
            {
                var cb = (CelestialItem)states[0].Observer;
                var ssb = Barycenters.SOLAR_SYSTEM_BARYCENTER;
                var ssbStates = new StateVector[states.Length];
                for (int i = 0; i < states.Length; i++)
                {
                    var cbFromSsb = cb.GetGeometricStateFromICRF(states[i].Epoch).ToStateVector();
                    ssbStates[i] = new StateVector(
                        cbFromSsb.Position + states[i].Position,
                        cbFromSsb.Velocity + states[i].Velocity,
                        ssb, states[i].Epoch, Frames.Frame.ICRF);
                }

                states = ssbStates;
            }

            SpiceAPI.Instance.WriteEphemeris(outputFile, NaifId, states);
        }

        public void AddStateVectorRelativeToICRF(params StateVector[] stateVectors)
        {
            foreach (var sv in stateVectors)
            {
                _stateVectorsRelativeToICRF[sv.Epoch] = sv;
            }
        }

        public override OrbitalParameters.OrbitalParameters GetGeometricStateFromICRF(in Time date)
        {
            // Fast path: exact match in cache
            if (_stateVectorsRelativeToICRF.TryGetValue(date, out var exact))
                return ConvertToSsbIfNeeded(exact, date);

            // TLE fallback: convert to ICRF before RelativeTo to avoid passing TEME to SPICE
            if (InitialOrbitalParameters is TLE)
                return InitialOrbitalParameters.ToStateVector(date)
                    .ToFrame(Frames.Frame.ICRF)
                    .RelativeTo(new Barycenter(0, date), Aberration.None)
                    .ToStateVector();

            // Not enough states for interpolation
            if (_stateVectorsRelativeToICRF.Count < 2)
                return this.InitialOrbitalParameters.ToStateVector(date)
                    .RelativeTo(new Barycenter(0, date), Aberration.None)
                    .ToFrame(Frames.Frame.ICRF).ToStateVector();

            // Interpolate and convert
            var interpolated = Lagrange.Interpolate(
                _stateVectorsRelativeToICRF.Values.OrderBy(x => x.Epoch).ToArray(), date);
            return ConvertToSsbIfNeeded(interpolated, date);
        }

        /// <summary>
        /// Converts a CB-relative state vector to SSB-relative if needed.
        /// Returns as-is if already SSB-relative (NaifId == 0).
        /// </summary>
        private StateVector ConvertToSsbIfNeeded(StateVector sv, in Time date)
        {
            if (sv.Observer.NaifId == 0) return sv; // Already SSB-relative
            var cbFromSsb = ((CelestialItem)sv.Observer).GetGeometricStateFromICRF(date).ToStateVector();
            return new StateVector(
                cbFromSsb.Position + sv.Position,
                cbFromSsb.Velocity + sv.Velocity,
                Barycenters.SOLAR_SYSTEM_BARYCENTER, date, Frames.Frame.ICRF);
        }

        /// <summary>
        /// Creates a CCSDS OPM (Orbit Parameter Message) from this spacecraft's current state.
        /// </summary>
        /// <param name="originator">The message originator. If null, uses "IO.Astrodynamics".</param>
        /// <param name="epoch">The epoch for the state vector. If null, uses the initial orbital parameters epoch.</param>
        /// <param name="includeKeplerianElements">If true, includes optional Keplerian elements in the OPM.</param>
        /// <param name="includeSpacecraftParameters">If true, includes spacecraft parameters (mass, drag, SRP).</param>
        /// <param name="includeManeuvers">If true, includes executed maneuvers in the OPM.</param>
        /// <returns>A new OPM instance representing this spacecraft's state.</returns>
        /// <exception cref="InvalidOperationException">Thrown when CosparId is not set.</exception>
        /// <remarks>
        /// <para>
        /// Maneuver delta-V components are exported in the same inertial reference frame as the state vector
        /// (typically ICRF or EME2000). This is the frame in which the framework stores maneuver data.
        /// </para>
        /// <para>
        /// If you need maneuvers in an orbital frame (RSW, RTN, TNW), you must perform the frame transformation
        /// yourself before or after the OPM export. The OPM standard supports both inertial and orbital frames
        /// for maneuver representation via the MAN_REF_FRAME field.
        /// </para>
        /// </remarks>
        public Opm ToOpm(string originator = null, Time? epoch = null, bool includeKeplerianElements = true, bool includeSpacecraftParameters = true, bool includeManeuvers = true)
        {
            if (string.IsNullOrWhiteSpace(CosparId))
            {
                throw new InvalidOperationException("CosparId must be set to create an OPM. Use the spacecraft constructor's cosparId parameter.");
            }

            // Get the state vector at the specified epoch (or initial epoch)
            var targetEpoch = epoch ?? InitialOrbitalParameters.Epoch;
            var stateVector = InitialOrbitalParameters.ToStateVector(targetEpoch);

            // Create header
            var header = originator != null
                ? CcsdsHeader.Create(originator)
                : CcsdsHeader.Create("IO.Astrodynamics");

            // Create metadata
            var metadata = new OpmMetadata(
                objectName: Name,
                objectId: CosparId,
                centerName: stateVector.Observer.Name,
                referenceFrame: stateVector.Frame.Name,
                timeSystem: "UTC");

            // Create state vector DTO (converts m to km, m/s to km/s)
            var opmStateVector = OpmStateVector.FromStateVector(stateVector);

            // Create optional Keplerian elements
            OpmKeplerianElements keplerianElements = null;
            if (includeKeplerianElements)
            {
                var kep = stateVector.ToKeplerianElements();
                // GM in km³/s² (framework uses m³/s², so divide by 1e9)
                var gmKm3S2 = kep.Observer.GM / 1e9;

                keplerianElements = OpmKeplerianElements.CreateWithTrueAnomaly(
                    semiMajorAxis: kep.SemiMajorAxis() / 1000.0, // m to km
                    eccentricity: kep.Eccentricity(),
                    inclination: kep.Inclination() * Constants.Rad2Deg,
                    raan: kep.AscendingNode() * Constants.Rad2Deg,
                    aop: kep.ArgumentOfPeriapsis() * Constants.Rad2Deg,
                    trueAnomaly: kep.TrueAnomaly() * Constants.Rad2Deg,
                    gm: gmKm3S2);
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
                foreach (var maneuver in ExecutedManeuvers.OrderBy(m => m.ThrustWindow?.StartDate))
                {
                    if (maneuver is Maneuver.ImpulseManeuver impulseManeuver && impulseManeuver.ThrustWindow.HasValue)
                    {
                        maneuverList.Add(impulseManeuver.ToOpmManeuverParameters(stateVector.Frame.Name));
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
                mass: includeSpacecraftParameters ? GetTotalMass() : null,
                solarRadiationPressureArea: includeSpacecraftParameters ? SectionalArea : null,
                solarRadiationCoefficient: includeSpacecraftParameters ? SolarRadiationCoeff : null,
                dragArea: includeSpacecraftParameters ? SectionalArea : null,
                dragCoefficient: includeSpacecraftParameters ? DragCoefficient : null,
                covariance: covariance,
                maneuvers: maneuvers);

            return new Opm(header, metadata, data);
        }

        /// <summary>
        /// Reset propagation elements
        /// </summary>
        private void ResetPropagation()
        {
            if (IsPropagated)
            {
                _isPropagated = false;
                _stateVectorsRelativeToICRF.Clear();
                Frame.ClearStateOrientations();
            }

            _executedManeuvers.Clear();
            foreach (var fuelTank in FuelTanks)
            {
                fuelTank.Refuel();
            }

            InitialManeuver?.Reset();
            StandbyManeuver = InitialManeuver;
        }
    }
}