using System;

namespace IO.Astrodynamics.Body.Spacecraft
{
    public class Payload : IEquatable<Payload>
    {
        
        public string Name { get; }
        public string SerialNumber { get; }
        public double Mass { get; }

        public Payload(string name, double mass, string serialNumber)
        {
            if (mass <= 0) throw new ArgumentException("Payload must have a mass");


            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Value cannot be null or empty.", nameof(name));
            if (string.IsNullOrEmpty(serialNumber)) throw new ArgumentException("Value cannot be null or empty.", nameof(serialNumber));

            Name = name;
            Mass = mass;
            SerialNumber = serialNumber;
        }
        
        public bool Equals(Payload other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name && SerialNumber == other.SerialNumber && Mass.Equals(other.Mass);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Payload)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, SerialNumber, Mass);
        }

        public static bool operator ==(Payload left, Payload right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Payload left, Payload right)
        {
            return !Equals(left, right);
        }

    }
}