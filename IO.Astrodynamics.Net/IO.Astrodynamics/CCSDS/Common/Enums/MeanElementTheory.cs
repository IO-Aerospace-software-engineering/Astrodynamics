// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.ComponentModel;

namespace IO.Astrodynamics.CCSDS.Common.Enums;

/// <summary>
/// Mean element theory used for analytical orbit propagation in OMM.
/// </summary>
/// <remarks>
/// As defined in CCSDS 502.0-B-3 (ODM Blue Book).
/// The theory determines how mean elements are defined and propagated.
/// This enum is restricted to theories implemented by the IO.Astrodynamics framework.
/// </remarks>
public enum MeanElementTheory
{
    /// <summary>
    /// Simplified General Perturbations 4 (NORAD/Space-Track standard).
    /// Includes SDP4 for deep space objects (automatically selected based on orbital period).
    /// Maps to IO.Astrodynamics SGP4/SDP4 propagator implementation.
    /// </summary>
    [Description("SGP4")]
    SGP4
}
