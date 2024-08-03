// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.ComponentModel;

namespace IO.Astrodynamics;

public enum OccultationType
{
    [Description("FULL")] Full,
    [Description("ANNULAR")] Annular,
    [Description("PARTIAL")] Partial,
    [Description("ANY")] Any,
    [Description("NONE")] None
}