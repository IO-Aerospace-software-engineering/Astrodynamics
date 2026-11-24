using System;

namespace IO.Astrodynamics.Body.Spacecraft
{
    public class Payload : IEquatable<Payload>
    {
        
        public string Name { get; }
        public string SerialNumber { get; }
        public double Mass { get; }

        /// <summary>
        /// A payload.
        /// </summary>
        /// <param name="name">The name of the payload</param>
        /// <param name="mass">The mass, kg: finite and above 0</param>
        /// <param name="serialNumber">The serial number of the payload</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="mass"/> is not finite or not above 0 (#800,
        /// decision 75 of #668, decided by S. Guillet, 2026-11-23: a NaN and an infinity passed, and 0 or less threw
        /// <see cref="ArgumentException"/> naming no parameter).</exception>
        /// <exception cref="ArgumentException">The name or the serial number is empty.</exception>
        public Payload(string name, double mass, string serialNumber)
        {
            if (!double.IsFinite(mass) || mass <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(mass), mass, "The mass of a payload must be finite and above 0.");
            }


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