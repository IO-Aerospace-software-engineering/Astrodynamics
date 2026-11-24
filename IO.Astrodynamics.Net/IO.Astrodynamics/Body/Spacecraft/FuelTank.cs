using System;
using System.Threading;
using IO.Astrodynamics.Exceptions;

namespace IO.Astrodynamics.Body.Spacecraft
{
    /// <summary>
    /// A fuel tank, compared by reference: a tank is an entity whose quantity a run changes, and a spacecraft holds one
    /// tank of a name (decision 76 of #668, decided by S. Guillet, 2026-11-23; value equality on the name, the model and
    /// the serial number before 11.0.0).
    /// </summary>
    public class FuelTank
    {
        private Spacecraft _spacecraft;

        /// <summary>
        /// The spacecraft that holds the tank, the first one it was added to: a tank belongs to one spacecraft (#676, #586),
        /// attached atomically, so that one spacecraft takes it when several add it on several threads.
        /// </summary>
        public Spacecraft Spacecraft => Volatile.Read(ref _spacecraft);
        public string Name { get; }
        public string Model { get; }
        public double Capacity { get; }
        public double InitialQuantity { get; }

        public double Quantity { get; private set; }
        public string SerialNumber { get; }

        /// <summary>
        /// A fuel tank.
        /// </summary>
        /// <param name="name">The name of the tank, unique among the tanks of its spacecraft</param>
        /// <param name="capacity">The capacity, kg: finite and above 0</param>
        /// <param name="initialQuantity">The fuel at the start of a run, kg: finite, from 0 (an empty tank, whose first burn
        /// throws <see cref="InsufficientFuelException"/>) to <paramref name="capacity"/></param>
        /// <param name="model">The model of the tank, informational; null by default</param>
        /// <param name="serialNumber">The serial number of the tank, informational; null by default</param>
        /// <exception cref="ArgumentException">The name is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Naming it, <paramref name="capacity"/> (checked first) or
        /// <paramref name="initialQuantity"/> is not finite or out of its range (#800, decision 75 of #668, decided by
        /// S. Guillet, 2026-11-23: a NaN passed the comparisons, and so did an infinite capacity).</exception>
        /// <remarks>
        /// Since 11.0.0 (R10 and decision 76 of #668, decided by S. Guillet, 2026-11-23) the model and the serial number come
        /// last and are optional, and a tank may start empty; before, <c>(name, model, serialNumber, capacity,
        /// initialQuantity)</c>, each string required, the initial quantity above 0.
        /// </remarks>
        public FuelTank(string name, double capacity, double initialQuantity, string model = null, string serialNumber = null)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Value cannot be null or empty.", nameof(name));
            if (!double.IsFinite(capacity) || capacity <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "The capacity of a tank must be finite and above 0.");
            }

            if (!double.IsFinite(initialQuantity) || initialQuantity < 0.0 || initialQuantity > capacity)
            {
                throw new ArgumentOutOfRangeException(nameof(initialQuantity), initialQuantity,
                    $"The initial quantity of a tank must be finite, not negative and at most its capacity, {capacity} kg.");
            }

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
                throw new InsufficientFuelException(
                    $"The tank {Name} holds {Quantity} kg of fuel, the burn needs {requiredMass} kg.", requiredMass, Quantity);
            }

            Quantity -= requiredMass;
        }

        /// <summary>
        /// Attaches the tank to <paramref name="spacecraft"/> unless it is held already, atomically (#676).
        /// </summary>
        /// <returns>The spacecraft that holds the tank after the call: <paramref name="spacecraft"/>, or the one that held it.</returns>
        internal Spacecraft AttachSpacecraft(Spacecraft spacecraft)
        {
            return Interlocked.CompareExchange(ref _spacecraft, spacecraft, null) ?? spacecraft;
        }

        internal void Refuel()
        {
            Quantity = InitialQuantity;
        }
    }
}