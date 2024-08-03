using System;
using IO.Astrodynamics.Exceptions;

namespace IO.Astrodynamics.Body.Spacecraft
{
    public class FuelTank : IEquatable<FuelTank>
    {
        public Spacecraft Spacecraft { get; private set; }
        public string Name { get; }
        public string Model { get; }
        public double Capacity { get; }
        public double InitialQuantity { get; }

        public double Quantity { get; private set; }
        public string SerialNumber { get; }

        public FuelTank(string name, string model, string serialNumber, double capacity, double initialQuantity)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Value cannot be null or empty.", nameof(name));
            if (string.IsNullOrEmpty(model)) throw new ArgumentException("Value cannot be null or empty.", nameof(model));
            if (string.IsNullOrEmpty(serialNumber)) throw new ArgumentException("Value cannot be null or empty.", nameof(serialNumber));
            if (initialQuantity <= 0 || initialQuantity > capacity) throw new ArgumentOutOfRangeException(nameof(initialQuantity));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

            Name = name;
            Model = model;
            Capacity = capacity;
            Quantity = InitialQuantity = initialQuantity;
            SerialNumber = serialNumber;
        }

        internal void Burn(double requiredMass)
        {
            if (requiredMass > Quantity)
            {
                throw new InsufficientFuelException();
            }

            Quantity -= requiredMass;
        }

        internal void SetSpacecraft(Spacecraft spacecraft)
        {
            Spacecraft = spacecraft ?? throw new ArgumentNullException(nameof(spacecraft));
        }

        public bool Equals(FuelTank other)
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
            return Equals((FuelTank)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Model, SerialNumber);
        }

        public static bool operator ==(FuelTank left, FuelTank right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(FuelTank left, FuelTank right)
        {
            return !Equals(left, right);
        }

        internal void Refuel()
        {
            Quantity = InitialQuantity;
        }
    }
}