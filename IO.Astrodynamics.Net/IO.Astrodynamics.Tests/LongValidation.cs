// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The switch of the <c>LongValidation</c> category: validations and studies of more than a few minutes each, run only
/// when the variable <see cref="Variable"/> is 1, as the manual workflow <c>long-validation.yml</c> sets it. A plain
/// <c>dotnet test</c>, the CI, the CD and an IDE skip them (decision of S. Guillet, 2026-10-22).
/// </summary>
internal static class LongValidation
{
    /// <summary>The environment variable that enables the category.</summary>
    internal const string Variable = "IO_ASTRODYNAMICS_LONG_VALIDATION";

    /// <summary>Null when the category is enabled, otherwise the reason the tests are skipped.</summary>
    internal static string SkipReason => Environment.GetEnvironmentVariable(Variable) == "1"
        ? null
        : $"LongValidation: set {Variable}=1 to run it (manual workflow long-validation.yml).";
}

/// <summary>A fact of the <c>LongValidation</c> category, skipped unless <see cref="LongValidation.Variable"/> is 1.</summary>
public sealed class LongValidationFactAttribute : FactAttribute
{
    public LongValidationFactAttribute()
    {
        Skip = LongValidation.SkipReason;
    }
}

/// <summary>A theory of the <c>LongValidation</c> category, skipped unless <see cref="LongValidation.Variable"/> is 1.</summary>
public sealed class LongValidationTheoryAttribute : TheoryAttribute
{
    public LongValidationTheoryAttribute()
    {
        Skip = LongValidation.SkipReason;
    }
}
