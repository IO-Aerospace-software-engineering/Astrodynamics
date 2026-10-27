// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.Body;

/// <summary>
/// The gravitational parameter of the central term −GM r / r³ of a geopotential, whose harmonics always take the
/// constants of the model (<see cref="GeopotentialModelParameters"/>).
/// </summary>
public enum GeopotentialCentralTerm
{
    /// <summary>
    /// The GM of the body (<see cref="CelestialItem.GM"/>, from its kernels), the GM of every other point-mass
    /// computation of the library and, for the Earth, TDB-compatible like the propagation: the default (decision D2-A of
    /// #444). A field of degree 0 is then the point mass of the body.
    /// </summary>
    BodyGravitationalParameter,

    /// <summary>
    /// The GM of the model, so that the whole field takes the constants of the model, as GMAT applies the GM of the
    /// header of a <c>.cof</c> file to its central term and to its harmonics (decision 18 of #457, S. Guillet,
    /// 2026-10-27). For the Earth with EGM2008's constants, the TT-compatible 3.986004415e14 m³/s² rather than the
    /// kernel's 3.98600435507e14 m³/s²: the whole acceleration moves by 1.5e-8 of itself, a LEO by about 20 m a day. A
    /// field of degree 0 is then the point mass of the model's GM. For a field whose harmonics take the GM of the body
    /// (another body without constants of the model), the same as <see cref="BodyGravitationalParameter"/>, bit for
    /// bit.
    /// </summary>
    ModelGravitationalParameter
}
