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

            if (isp <= 0)
            {
                throw new ArgumentException("ISP must be a positive number");
            }

            if (fuelFlow <= 0)
            {
                throw new ArgumentException("Fuel flow must be a positive number");
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