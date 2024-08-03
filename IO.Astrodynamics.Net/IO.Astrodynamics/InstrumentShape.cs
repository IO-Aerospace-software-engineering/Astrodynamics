// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.ComponentModel;

namespace IO.Astrodynamics;

public enum InstrumentShape
{
    [Description("circular")] Circular,
    [Description("elliptical")] Elliptical,
    [Description("rectangular")] Rectangular
}