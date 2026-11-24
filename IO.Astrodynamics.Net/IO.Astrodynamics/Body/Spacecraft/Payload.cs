using System;

namespace IO.Astrodynamics.Body.Spacecraft
{
    /// <summary>
    /// A payload, compared by reference, one of a name on a spacecraft: the rule of every component, for uniformity, since a
    /// payload name reaches no file (decision 76 of #668, decided by S. Guillet, 2026-11-23; value equality on the name, the
    /// serial number and the mass before 11.0.0).
    /// </summary>
    public class Payload
    {
        
        public string Name { get; }
        public string SerialNumber { get; }
        public double Mass { get; }

        /// <summary>
        /// A payload.
        /// </summary>
        /// <param name="name">The name of the payload, unique among the payloads of its spacecraft</param>
        /// <param name="mass">The mass, kg: finite and above 0</param>
        /// <param name="serialNumber">The serial number of the payload, informational; null by default (required before
        /// 11.0.0, decision 76 of #668)</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="mass"/> is not finite or not above 0 (#800,
        /// decision 75 of #668, decided by S. Guillet, 2026-11-23: a NaN and an infinity passed, and 0 or less threw
        /// <see cref="ArgumentException"/> naming no parameter).</exception>
        /// <exception cref="ArgumentException">The name is empty.</exception>
        public Payload(string name, double mass, string serialNumber = null)
        {
            if (!double.IsFinite(mass) || mass <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(mass), mass, "The mass of a payload must be finite and above 0.");
            }

            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Value cannot be null or empty.", nameof(name));

            Name = name;
            Mass = mass;
            SerialNumber = serialNumber;
        }
    }
}