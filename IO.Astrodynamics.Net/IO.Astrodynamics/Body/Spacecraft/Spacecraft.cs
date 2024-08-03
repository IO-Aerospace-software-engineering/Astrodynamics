using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Time;
using StateOrientation = IO.Astrodynamics.OrbitalParameters.StateOrientation;


namespace IO.Astrodynamics.Body.Spacecraft
{
    public class Spacecraft : CelestialItem, IOrientable, IDisposable
    {
        public static readonly Vector3 Front = Vector3.VectorY;
        public static readonly Vector3 Back = Front.Inverse();
        public static readonly Vector3 Right = Vector3.VectorX;
        public static readonly Vector3 Left = Right.Inverse();
        public static readonly Vector3 Up = Vector3.VectorZ;
        public static readonly Vector3 Down = Up.Inverse();

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
        public Frame Frame { get; }
        public double SectionalArea { get; }
        public double DragCoefficient { get; }
        public DirectoryInfo PropagationOutput { get; private set; }
        public bool IsPropagated => PropagationOutput != null;

        /// <summary>
        /// Spacecraft constructor
        /// </summary>
        /// <param name="naifId"></param>
        /// <param name="name"></param>
        /// <param name="mass"></param>
        /// <param name="maximumOperatingMass"></param>
        /// <param name="clock"></param>
        /// <param name="initialOrbitalParameters"></param>
        /// <param name="sectionalArea">Mean sectional area</param>
        /// <param name="dragCoeff">Drag coefficient</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public Spacecraft(int naifId, string name, double mass, double maximumOperatingMass, Clock clock, OrbitalParameters.OrbitalParameters initialOrbitalParameters,
            double sectionalArea = 1.0, double dragCoeff = 0.3) : base(
            naifId, name, mass, initialOrbitalParameters)
        {
            if (maximumOperatingMass < mass) throw new ArgumentOutOfRangeException(nameof(maximumOperatingMass));
            if (naifId >= 0) throw new ArgumentOutOfRangeException(nameof(naifId));
            ArgumentOutOfRangeException.ThrowIfNegative(dragCoeff);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sectionalArea);
            MaximumOperatingMass = maximumOperatingMass;
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Clock.AttachSpacecraft(this);
            Frame = new SpacecraftFrame($"{name}_FRAME", naifId, name);
            SectionalArea = sectionalArea;
            DragCoefficient = dragCoeff;
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
        public void SetStandbyManeuver(Maneuver.Maneuver maneuver, DateTime? minimumEpoch = null)
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
        public StateOrientation GetOrientation(Frame referenceFrame, in DateTime epoch)
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

        /// <summary>
        /// Propagate spacecraft
        /// </summary>
        /// <param name="window"></param>
        /// <param name="additionalCelestialBodies">Celestial bodies involved as perturbation bodies</param>
        /// <param name="includeAtmosphericDrag"></param>
        /// <param name="includeSolarRadiationPressure"></param>
        /// <param name="propagatorStepSize"></param>
        /// <param name="outputDirectory"></param>
        public async Task PropagateAsync(Window window, IEnumerable<CelestialItem> additionalCelestialBodies, bool includeAtmosphericDrag,
            bool includeSolarRadiationPressure, TimeSpan propagatorStepSize, DirectoryInfo outputDirectory)
        {
            ResetPropagation();
            IPropagator propagator;
            if (this.InitialOrbitalParameters is TLE)
            {
                propagator = new TLEPropagator(window, this, propagatorStepSize);
            }
            else
            {
                propagator = new SpacecraftPropagator(window, this, additionalCelestialBodies, includeAtmosphericDrag, includeSolarRadiationPressure, propagatorStepSize);
            }

            var res = propagator.Propagate();
            PropagationOutput = outputDirectory.CreateSubdirectory(Name);

            //Write frame
            await WriteFrameAsync(new FileInfo(Path.Combine(PropagationOutput.CreateSubdirectory("Frames").FullName, Name + ".tf")));


            //write instrument frame and kernel 
            var instrumentDirectory = PropagationOutput.CreateSubdirectory("Instruments");
            foreach (var instrument in Instruments)
            {
                await instrument.WriteFrameAsync(new FileInfo(Path.Combine(instrumentDirectory.FullName, instrument.Name + ".tf")));
                await instrument.WriteKernelAsync(new FileInfo(Path.Combine(instrumentDirectory.FullName, instrument.Name + ".ti")));
            }

            //Write clock
            var clockFile = new FileInfo(Path.Combine(PropagationOutput.CreateSubdirectory("Clocks").FullName, Name + ".tsc"));
            await Clock.WriteAsync(clockFile);

            //Write Ephemeris
            if (API.Instance.WriteEphemeris(new FileInfo(Path.Combine(PropagationOutput.CreateSubdirectory("Ephemeris").FullName, Name + ".spk")), this,
                    res.stateVectors))
            {
                //Clock is loaded because is needed by orientation writer
                API.Instance.LoadKernels(clockFile);
                //Write Orientation
                if (API.Instance.WriteOrientation(new FileInfo(Path.Combine(PropagationOutput.CreateSubdirectory("Orientation").FullName, Name + ".ck")), this,
                        res.stateOrientations))
                {
                    API.Instance.LoadKernels(PropagationOutput);
                }
            }
        }

        /// <summary>
        /// Reset propagation elements
        /// </summary>
        private void ResetPropagation()
        {
            if (IsPropagated)
            {
                API.Instance.UnloadKernels(PropagationOutput);
                PropagationOutput.Delete(true);
                PropagationOutput = null;
            }

            _executedManeuvers.Clear();
            foreach (var fuelTank in FuelTanks)
            {
                fuelTank.Refuel();
            }

            InitialManeuver?.Reset();
            StandbyManeuver = InitialManeuver;
        }

        private void ReleaseUnmanagedResources()
        {
            if (IsPropagated)
            {
                API.Instance.UnloadKernels(PropagationOutput);
            }
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        ~Spacecraft()
        {
            ReleaseUnmanagedResources();
        }
    }
}