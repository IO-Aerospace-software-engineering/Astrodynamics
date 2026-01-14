// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.ComponentModel;

namespace IO.Astrodynamics.CCSDS.Common.Enums;

/// <summary>
/// Object type classification used in CCSDS Navigation Data Messages.
/// </summary>
/// <remarks>
/// As defined in CCSDS common schema (ndmxml-4.0.0-common-4.0.xsd).
/// </remarks>
public enum ObjectType
{
    /// <summary>
    /// Operational payload or satellite
    /// </summary>
    [Description("PAYLOAD")]
    Payload,

    /// <summary>
    /// Rocket body or booster stage
    /// </summary>
    [Description("ROCKET BODY")]
    RocketBody,

    /// <summary>
    /// Space debris or fragment
    /// </summary>
    [Description("DEBRIS")]
    Debris,

    /// <summary>
    /// Unknown object type
    /// </summary>
    [Description("UNKNOWN")]
    Unknown,

    /// <summary>
    /// Other object type not listed
    /// </summary>
    [Description("OTHER")]
    Other
}
