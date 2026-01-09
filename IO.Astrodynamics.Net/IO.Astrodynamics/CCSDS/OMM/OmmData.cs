// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using IO.Astrodynamics.CCSDS.Common;

namespace IO.Astrodynamics.CCSDS.OMM;

/// <summary>
/// Represents the data section of an OMM message.
/// </summary>
/// <remarks>
/// Contains mean orbital elements (required) and optional TLE parameters,
/// spacecraft parameters, covariance matrix, and user-defined parameters.
/// </remarks>
public class OmmData
{
    /// <summary>
    /// Gets the mean Keplerian orbital elements (required).
    /// </summary>
    public MeanElements MeanElements { get; }

    /// <summary>
    /// Gets the spacecraft parameters (optional).
    /// </summary>
    public SpacecraftParameters SpacecraftParameters { get; }

    /// <summary>
    /// Gets the TLE-specific parameters (optional, required for SGP4 propagation).
    /// </summary>
    public TleParameters TleParameters { get; }

    /// <summary>
    /// Gets the covariance matrix (optional).
    /// </summary>
    public CovarianceMatrix CovarianceMatrix { get; }

    /// <summary>
    /// Gets the user-defined parameters (optional).
    /// </summary>
    public UserDefinedParameters UserDefinedParameters { get; }

    /// <summary>
    /// Gets optional comments associated with the data section.
    /// </summary>
    public IReadOnlyList<string> Comments { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmmData"/> class.
    /// </summary>
    /// <param name="meanElements">The mean orbital elements (required).</param>
    /// <param name="spacecraftParameters">Optional spacecraft parameters.</param>
    /// <param name="tleParameters">Optional TLE-specific parameters.</param>
    /// <param name="covarianceMatrix">Optional covariance matrix.</param>
    /// <param name="userDefinedParameters">Optional user-defined parameters.</param>
    /// <param name="comments">Optional comments.</param>
    /// <exception cref="ArgumentNullException">Thrown when meanElements is null.</exception>
    public OmmData(
        MeanElements meanElements,
        SpacecraftParameters spacecraftParameters = null,
        TleParameters tleParameters = null,
        CovarianceMatrix covarianceMatrix = null,
        UserDefinedParameters userDefinedParameters = null,
        IReadOnlyList<string> comments = null)
    {
        MeanElements = meanElements ?? throw new ArgumentNullException(nameof(meanElements));
        SpacecraftParameters = spacecraftParameters;
        TleParameters = tleParameters;
        CovarianceMatrix = covarianceMatrix;
        UserDefinedParameters = userDefinedParameters;
        Comments = comments ?? Array.Empty<string>();
    }

    /// <summary>
    /// Gets a value indicating whether this OMM has spacecraft parameters.
    /// </summary>
    public bool HasSpacecraftParameters => SpacecraftParameters != null;

    /// <summary>
    /// Gets a value indicating whether this OMM has TLE parameters.
    /// </summary>
    public bool HasTleParameters => TleParameters != null;

    /// <summary>
    /// Gets a value indicating whether this OMM has a covariance matrix.
    /// </summary>
    public bool HasCovarianceMatrix => CovarianceMatrix != null;

    /// <summary>
    /// Gets a value indicating whether this OMM has user-defined parameters.
    /// </summary>
    public bool HasUserDefinedParameters => UserDefinedParameters != null && UserDefinedParameters.HasParameters;

    /// <summary>
    /// Creates a minimal OmmData with only mean elements.
    /// </summary>
    public static OmmData CreateMinimal(MeanElements meanElements)
    {
        return new OmmData(meanElements);
    }

    /// <summary>
    /// Creates an OmmData suitable for TLE/SGP4 propagation.
    /// </summary>
    public static OmmData CreateForTle(
        MeanElements meanElements,
        TleParameters tleParameters,
        SpacecraftParameters spacecraftParameters = null,
        CovarianceMatrix covarianceMatrix = null,
        UserDefinedParameters userDefinedParameters = null,
        IReadOnlyList<string> comments = null)
    {
        if (tleParameters == null)
            throw new ArgumentNullException(nameof(tleParameters), "TLE parameters are required for TLE/SGP4 OMM.");

        return new OmmData(meanElements, spacecraftParameters, tleParameters, covarianceMatrix, userDefinedParameters, comments);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var parts = new List<string> { "MeanElements" };
        if (HasSpacecraftParameters) parts.Add("SC");
        if (HasTleParameters) parts.Add("TLE");
        if (HasCovarianceMatrix) parts.Add("Cov");
        if (HasUserDefinedParameters) parts.Add("UDP");

        return $"OmmData[{string.Join("+", parts)}]";
    }
}
