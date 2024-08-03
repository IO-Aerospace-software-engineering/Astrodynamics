// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.ComponentModel;

namespace IO.Astrodynamics;

public enum RelationnalOperator
{
    [Description(">")] Greater,
    [Description("<")] Lower,
    [Description("=")] Equal,
    [Description("ABSMIN")] AbsoluteMin,
    [Description("ABSMAX")] AbsoluteMax,
    [Description("LOCMIN")] LocalMin,
    [Description("LOCMAX")] LocalMax
}