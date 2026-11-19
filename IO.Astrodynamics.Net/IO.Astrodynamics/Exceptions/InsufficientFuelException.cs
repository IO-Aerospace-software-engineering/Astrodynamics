using System;

namespace IO.Astrodynamics.Exceptions;

/// <summary>
/// A burn that needs more fuel than its tank holds: the mass it needs and the mass the tank holds, in kg (R03 of #668).
/// </summary>
/// <remarks>
/// It derives from <see cref="InvalidOperationException"/>, the state of the tank not allowing the burn. Before 11.0.0 it
/// derived from <c>ExceptionBase</c>, a <see cref="Exception"/>, which is removed, and carried no mass.
/// </remarks>
public class InsufficientFuelException : InvalidOperationException
{
    /// <summary>The fuel mass the burn needs, in kg; NaN when it was not given.</summary>
    public double RequiredMass { get; } = double.NaN;

    /// <summary>The fuel mass the tank holds, in kg; NaN when it was not given.</summary>
    public double AvailableMass { get; } = double.NaN;

    /// <summary>A burn the fuel does not allow, with no message and no mass.</summary>
    public InsufficientFuelException()
    {
    }

    /// <summary>A burn the fuel does not allow, with <paramref name="message"/> and no mass.</summary>
    /// <param name="message">The message.</param>
    public InsufficientFuelException(string message) : base(message)
    {
    }

    /// <summary>A burn the fuel does not allow, with <paramref name="message"/> and no mass, caused by
    /// <paramref name="innerException"/>.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public InsufficientFuelException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>A burn that needs <paramref name="requiredMass"/> of fuel from a tank that holds
    /// <paramref name="availableMass"/>.</summary>
    /// <param name="message">The message.</param>
    /// <param name="requiredMass">The fuel mass the burn needs, in kg.</param>
    /// <param name="availableMass">The fuel mass the tank holds, in kg.</param>
    public InsufficientFuelException(string message, double requiredMass, double availableMass) : base(message)
    {
        RequiredMass = requiredMass;
        AvailableMass = availableMass;
    }
}
