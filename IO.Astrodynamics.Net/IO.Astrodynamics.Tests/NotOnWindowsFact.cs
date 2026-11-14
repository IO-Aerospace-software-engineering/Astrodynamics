// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>A fact skipped on Windows, for the reason given: reported as skipped there, not as passed.</summary>
public sealed class NotOnWindowsFactAttribute : FactAttribute
{
    public NotOnWindowsFactAttribute(string reason)
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = reason;
        }
    }
}
