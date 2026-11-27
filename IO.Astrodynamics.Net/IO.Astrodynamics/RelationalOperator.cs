// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.ComponentModel;

namespace IO.Astrodynamics;

/// <summary>
/// The relation of a window search; the comparisons take the names of
/// <see cref="System.Linq.Expressions.ExpressionType"/> (<c>RelationnalOperator</c> with <c>Greater</c>, <c>Lower</c>,
/// <c>GreaterOrEqual</c> and <c>LowerOrEqual</c> before 11.0.0, D2 of #668), the descriptions are the relations of CSPICE.
/// </summary>
public enum RelationalOperator
{
    [Description(">")] GreaterThan,
    [Description("<")] LessThan,
    [Description("=")] Equal,
    [Description("!")] NotEqual,
    [Description(">=")] GreaterThanOrEqual,
    [Description("<=")] LessThanOrEqual,
    [Description("ABSMIN")] AbsoluteMin,
    [Description("ABSMAX")] AbsoluteMax,
    [Description("LOCMIN")] LocalMin,
    [Description("LOCMAX")] LocalMax
}