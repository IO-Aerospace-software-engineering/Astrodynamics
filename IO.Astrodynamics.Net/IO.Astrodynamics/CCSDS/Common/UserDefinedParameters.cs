// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;

namespace IO.Astrodynamics.CCSDS.Common;

/// <summary>
/// Represents a single user-defined parameter in a CCSDS Navigation Data Message.
/// </summary>
public readonly struct UserDefinedParameter : IEquatable<UserDefinedParameter>
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the parameter value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserDefinedParameter"/> struct.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    /// <exception cref="ArgumentNullException">Thrown when name or value is null.</exception>
    /// <exception cref="ArgumentException">Thrown when name is empty or whitespace.</exception>
    public UserDefinedParameter(string name, string value)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Parameter name cannot be empty or whitespace.", nameof(name));

        Name = name;
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public bool Equals(UserDefinedParameter other)
    {
        return Name == other.Name && Value == other.Value;
    }

    public override bool Equals(object obj)
    {
        return obj is UserDefinedParameter other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Value);
    }

    public static bool operator ==(UserDefinedParameter left, UserDefinedParameter right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(UserDefinedParameter left, UserDefinedParameter right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"{Name}={Value}";
    }
}

/// <summary>
/// Represents a collection of user-defined parameters in a CCSDS Navigation Data Message.
/// </summary>
/// <remarks>
/// As defined in CCSDS 505.0-B-3 (NDM/XML Blue Book).
/// Allows extensibility for mission-specific or custom parameters.
/// </remarks>
public class UserDefinedParameters
{
    /// <summary>
    /// Gets the comments associated with the user-defined parameters.
    /// </summary>
    public IReadOnlyList<string> Comments { get; }

    /// <summary>
    /// Gets the user-defined parameters.
    /// </summary>
    public IReadOnlyList<UserDefinedParameter> Parameters { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserDefinedParameters"/> class.
    /// </summary>
    /// <param name="parameters">The collection of user-defined parameters.</param>
    /// <param name="comments">Comments associated with the parameters.</param>
    public UserDefinedParameters(
        IEnumerable<UserDefinedParameter> parameters = null,
        IReadOnlyList<string> comments = null)
    {
        Parameters = parameters?.ToList() ?? new List<UserDefinedParameter>();
        Comments = comments ?? Array.Empty<string>();
    }

    /// <summary>
    /// Gets a parameter value by name.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>The parameter value, or null if not found.</returns>
    public string GetValue(string name)
    {
        return Parameters.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
    }

    /// <summary>
    /// Returns true if any parameters are defined.
    /// </summary>
    public bool HasParameters => Parameters.Count > 0;

    public override string ToString()
    {
        return $"UserDefinedParameters[Count={Parameters.Count}]";
    }
}
