// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

namespace IO.Astrodynamics.SolarSystemObjects;

public readonly record struct NaifObject(int NaifId, string Name, string Frame);