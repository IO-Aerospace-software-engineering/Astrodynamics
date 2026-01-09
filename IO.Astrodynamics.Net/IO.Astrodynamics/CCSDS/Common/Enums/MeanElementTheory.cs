// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.ComponentModel;

namespace IO.Astrodynamics.CCSDS.Common.Enums;

/// <summary>
/// Mean element theory used for analytical orbit propagation in OMM.
/// </summary>
/// <remarks>
/// As defined in CCSDS 502.0-B-3 (ODM Blue Book).
/// The theory determines how mean elements are defined and propagated.
/// </remarks>
public enum MeanElementTheory
{
    /// <summary>
    /// Simplified General Perturbations 4 (NORAD/Space-Track standard for near-Earth)
    /// </summary>
    [Description("SGP4")]
    SGP4,

    /// <summary>
    /// Extended SGP4 with improved atmospheric model
    /// </summary>
    [Description("SGP4-XP")]
    SGP4XP,

    /// <summary>
    /// Draper Semi-analytical Satellite Theory
    /// </summary>
    [Description("DSST")]
    DSST,

    /// <summary>
    /// US Space Force propagator
    /// </summary>
    [Description("USM")]
    USM
}
