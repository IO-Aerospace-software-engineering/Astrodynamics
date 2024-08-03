// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Time;

namespace IO.Astrodynamics.Mission;

public class ScenarioSummary
{
    public DirectoryInfo SiteDirectoryInfo { get; }
    public DirectoryInfo SpacecraftDirectoryInfo { get; }
    public Window Window { get; }

    private readonly HashSet<SpacecraftSummary> _spacecraftSummaries = new HashSet<SpacecraftSummary>();
    public IReadOnlyCollection<SpacecraftSummary> SpacecraftSummaries => _spacecraftSummaries;

    internal ScenarioSummary(Window window, DirectoryInfo siteDirectoryInfo, DirectoryInfo spacecraftDirectoryInfo)
    {
        Window = window;
        SiteDirectoryInfo = siteDirectoryInfo ?? throw new ArgumentNullException(nameof(siteDirectoryInfo));
        SpacecraftDirectoryInfo = spacecraftDirectoryInfo ?? throw new ArgumentNullException(nameof(spacecraftDirectoryInfo));
    }

    internal void AddSpacecraftSummary(SpacecraftSummary summary)
    {
        _spacecraftSummaries.Add(summary);
    }
}