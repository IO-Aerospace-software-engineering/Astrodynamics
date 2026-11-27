using System;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Physics;

namespace IO.Astrodynamics.Body.Spacecraft
{
    /// <summary>
    /// An engine, compared by reference: a spacecraft holds one engine of a name, and a maneuver draws on an engine instance
    /// of its spacecraft (decision 76 of #668, decided by S. Guillet, 2026-11-23; value equality on the name, the model and
    /// the serial number before 11.0.0).
    /// </summary>
    public class Engine
    {
        public string Name { get; }
        public string Model { get; }
        public double Isp { get; }
        public double FuelFlow { get; }
        public double Thrust { get; }
        public FuelTank FuelTank { get; }
        public string SerialNumber { get; }

        /// <summary>
        /// An engine fed by <paramref name="fuelTank"/>.
        /// </summary>
        /// <param name="name">The name of the engine, unique among the engines of its spacecraft</param>
        /// <param name="isp">The specific impulse, s: finite and above 0</param>
        /// <param name="fuelFlow">The fuel flow, kg/s: finite and above 0</param>
        /// <param name="fuelTank">The tank that feeds the engine</param>
        /// <param name="model">The model of the engine, informational; null by default</param>
        /// <param name="serialNumber">The serial number of the engine, informational; null by default</param>
        /// <exception cref="ArgumentException">The name is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Naming it, <paramref name="isp"/> or <paramref name="fuelFlow"/>
        /// is not finite or not above 0 (#800, decision 75 of #668, decided by S. Guillet, 2026-11-23: a NaN and an infinity
        /// passed, and 0 or less threw <see cref="ArgumentException"/> naming no parameter).</exception>
        /// <exception cref="ArgumentNullException"><paramref name="fuelTank"/> is null.</exception>
        /// <remarks>
        /// Since 11.0.0 (R10 and decision 76 of #668, decided by S. Guillet, 2026-11-23) the model and the serial number come
        /// last and are optional; before, <c>(name, model, serialNumber, isp, fuelFlow, fuelTank)</c>, each string required.
        /// </remarks>
        public Engine(string name, double isp, double fuelFlow, FuelTank fuelTank, string model = null, string serialNumber = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Engine requires a name", nameof(name));
            }

            if (!double.IsFinite(isp) || isp <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(isp), isp, "The specific impulse must be finite and above 0.");
            }

            if (!double.IsFinite(fuelFlow) || fuelFlow <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(fuelFlow), fuelFlow, "The fuel flow must be finite and above 0.");
            }

            Name = name;
            Model = model;
            Isp = isp;
            FuelFlow = fuelFlow;
            FuelTank = fuelTank ?? throw new ArgumentNullException(nameof(fuelTank));
            SerialNumber = serialNumber;
            Thrust = isp * fuelFlow * Constants.StandardGravity;
        }

        /// <summary>
        /// Burns the fuel of <paramref name="deltaV"/> from the tank, at the total mass of its spacecraft (Tsiolkovsky), and
        /// returns it. Internal since 11.0.0 (decision 49 of #668, decided by S. Guillet, 2026-11-23): public, it took the
        /// fuel of a finished run outside any run, and threw <see cref="NullReferenceException"/> on a tank of no spacecraft
        /// (#802). The fuel of a delta-V is <see cref="Tsiolkovsky.DeltaM"/>.
        /// </summary>
        internal double Ignite(in Vector3 deltaV)
        {
            var fuelBurned = Tsiolkovsky.DeltaM(Isp, FuelTank.Spacecraft.GetTotalMass(), deltaV.Magnitude());
            FuelTank.Burn(fuelBurned);
            return fuelBurned;
        }
    }
}