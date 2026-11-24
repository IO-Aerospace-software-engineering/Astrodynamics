using System;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Physics;

namespace IO.Astrodynamics.Body.Spacecraft
{
    public class Engine : IEquatable<Engine>
    {
        public string Name { get; }
        public string Model { get; }
        public double ISP { get; }
        public double FuelFlow { get; }
        public double Thrust { get; }
        public FuelTank FuelTank { get; }
        public string SerialNumber { get; }

        /// <summary>
        /// An engine fed by <paramref name="fuelTank"/>.
        /// </summary>
        /// <param name="name">The name of the engine</param>
        /// <param name="model">The model of the engine</param>
        /// <param name="serialNumber">The serial number of the engine</param>
        /// <param name="isp">The specific impulse, s: finite and above 0</param>
        /// <param name="fuelFlow">The fuel flow, kg/s: finite and above 0</param>
        /// <param name="fuelTank">The tank that feeds the engine</param>
        /// <exception cref="ArgumentException">The name, the model or the serial number is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Naming it, <paramref name="isp"/> or <paramref name="fuelFlow"/>
        /// is not finite or not above 0 (#800, decision 75 of #668, decided by S. Guillet, 2026-11-23: a NaN and an infinity
        /// passed, and 0 or less threw <see cref="ArgumentException"/> naming no parameter).</exception>
        /// <exception cref="ArgumentNullException"><paramref name="fuelTank"/> is null.</exception>
        public Engine(string name, string model, string serialNumber, double isp, double fuelFlow, FuelTank fuelTank)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Engine requires a name");
            }

            if (string.IsNullOrEmpty(model))
            {
                throw new ArgumentException("Engine requires a model");
            }

            if (!double.IsFinite(isp) || isp <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(isp), isp, "The specific impulse must be finite and above 0.");
            }

            if (!double.IsFinite(fuelFlow) || fuelFlow <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(fuelFlow), fuelFlow, "The fuel flow must be finite and above 0.");
            }

            if (string.IsNullOrEmpty(serialNumber)) throw new ArgumentException("Value cannot be null or empty.", nameof(serialNumber));

            Name = name;
            Model = model;
            ISP = isp;
            FuelFlow = fuelFlow;
            FuelTank = fuelTank ?? throw new ArgumentNullException(nameof(fuelTank));
            SerialNumber = serialNumber;
            Thrust = isp * fuelFlow * Constants.g0;
        }

        /// <summary>
        /// Burns the fuel of <paramref name="deltaV"/> from the tank, at the total mass of its spacecraft (Tsiolkovsky), and
        /// returns it. Internal since 11.0.0 (decision 49 of #668, decided by S. Guillet, 2026-11-23): public, it took the
        /// fuel of a finished run outside any run, and threw <see cref="NullReferenceException"/> on a tank of no spacecraft
        /// (#802). The fuel of a delta-V is <see cref="Tsiolkovski.DeltaM"/>.
        /// </summary>
        internal double Ignite(in Vector3 deltaV)
        {
            var fuelBurned = Tsiolkovski.DeltaM(ISP, FuelTank.Spacecraft.GetTotalMass(), deltaV.Magnitude());
            FuelTank.Burn(fuelBurned);
            return fuelBurned;
        }
        
        public bool Equals(Engine other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name && Model == other.Model && SerialNumber == other.SerialNumber;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Engine)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Model, SerialNumber);
        }

        public static bool operator ==(Engine left, Engine right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Engine left, Engine right)
        {
            return !Equals(left, right);
        }
    }
}