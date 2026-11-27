// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.ComponentModel;

namespace IO.Astrodynamics;

/// <summary>
/// The aberration corrections of SPICE, each described by its <c>ABCORR</c> string; the members with stellar aberration
/// are <c>LtS</c>, <c>CnS</c>, <c>XltS</c> and <c>XcnS</c> (<c>LTS</c>, <c>CNS</c>, <c>XLTS</c> and <c>XCNS</c> before
/// 11.0.0, R27 of #668).
/// </summary>
public enum Aberration
{
    [Description("NONE")] None,
    [Description("LT")] LT,
    [Description("LT+S")] LtS,
    [Description("CN")] CN,
    [Description("CN+S")] CnS,
    [Description("XLT")] XLT,
    [Description("XLT+S")] XltS,
    [Description("XCN")] XCN,
    [Description("XCN+S")] XcnS
}