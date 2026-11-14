// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>A theory skipped on Windows, for the reason given: reported as skipped there, not as passed.</summary>
public sealed class NotOnWindowsTheoryAttribute : TheoryAttribute
{
    public NotOnWindowsTheoryAttribute(string reason)
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = reason;
        }
    }
}
