// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Body;

namespace IO.Astrodynamics.SolarSystemObjects;

/// <summary>
/// An object of the NAIF catalogues (<see cref="PlanetsAndMoons"/>, <see cref="Barycenters"/>, <see cref="Stars"/>,
/// <see cref="GroundStations"/>, ...): its NAIF identifier and its SPICE name, data that no kernel read builds.
/// </summary>
/// <remarks>
/// Its <c>Frame</c> is removed in 11.0.0 (R02 of #668): the library read it nowhere, and it was wrong for the Moon
/// (<c>IAU_MOON</c> against <c>MOON_ME</c> with the usual frame kernels) and for NEW NORCIA. The frame of a body is that of
/// the <see cref="CelestialBody"/> built from it (its <c>Frame</c>), which the loaded kernels decide.
/// </remarks>
/// <param name="NaifId">The NAIF identifier.</param>
/// <param name="Name">The SPICE name.</param>
public readonly record struct NaifObject(int NaifId, string Name) : INaifObject;
