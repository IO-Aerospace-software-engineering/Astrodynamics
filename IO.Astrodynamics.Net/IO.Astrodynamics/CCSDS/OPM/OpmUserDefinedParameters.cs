// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;

namespace IO.Astrodynamics.CCSDS.OPM;

/// <summary>
/// Represents the optional user-defined parameters section of an OPM message.
/// </summary>
/// <remarks>
/// As defined in CCSDS 502.0-B-3 (ODM Blue Book), the userDefinedParameters section
/// allows arbitrary key-value pairs for mission-specific data.
/// </remarks>
public class OpmUserDefinedParameters
{
    /// <summary>
    /// Gets optional comments associated with the user-defined parameters.
    /// </summary>
    public IReadOnlyList<string> Comments { get; }

    /// <summary>
    /// Gets the user-defined parameters as a dictionary of key-value pairs.
    /// </summary>
    /// <remarks>
    /// Keys correspond to the "parameter" attribute, values correspond to the element content.
    /// </remarks>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpmUserDefinedParameters"/> class.
    /// </summary>
    /// <param name="parameters">Dictionary of parameter name-value pairs.</param>
    /// <param name="comments">Optional comments.</param>
    public OpmUserDefinedParameters(
        IReadOnlyDictionary<string, string> parameters = null,
        IReadOnlyList<string> comments = null)
    {
        Parameters = parameters ?? new Dictionary<string, string>();
        Comments = comments ?? Array.Empty<string>();
    }

    /// <summary>
    /// Creates user-defined parameters from a collection of key-value pairs.
    /// </summary>
    /// <param name="parameters">Enumerable of key-value pairs.</param>
    /// <param name="comments">Optional comments.</param>
    /// <returns>A new OpmUserDefinedParameters instance.</returns>
    public static OpmUserDefinedParameters Create(
        IEnumerable<KeyValuePair<string, string>> parameters,
        IReadOnlyList<string> comments = null)
    {
        var dict = new Dictionary<string, string>();
        if (parameters != null)
        {
            foreach (var kvp in parameters)
            {
                dict[kvp.Key] = kvp.Value;
            }
        }
        return new OpmUserDefinedParameters(dict, comments);
    }

    /// <summary>
    /// Gets the value of a user-defined parameter.
    /// </summary>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The parameter value, or null if not found.</returns>
    public string GetValue(string parameterName)
    {
        return Parameters.TryGetValue(parameterName, out var value) ? value : null;
    }

    /// <summary>
    /// Gets whether any parameters are defined.
    /// </summary>
    public bool HasParameters => Parameters.Count > 0;

    /// <inheritdoc />
    public override string ToString()
    {
        return $"OpmUserDefinedParameters[{Parameters.Count} parameters]";
    }
}
