// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections.Generic;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Mission;

/// <summary>
/// The summary of a scenario's run, returned by <see cref="Scenario.Simulate"/>.
/// </summary>
public class ScenarioSummary
{
    /// <summary>The window of the scenario.</summary>
    public Window Window { get; }

    /// <summary>
    /// The summary of each spacecraft of the scenario, in scenario order (the order <see cref="Scenario.Spacecrafts"/>
    /// lists them in), a read-only list (decision 59 of #668, decided by S. Guillet, 2026-11-23: a collection of no
    /// order before 11.0.0).
    /// </summary>
    public IReadOnlyList<SpacecraftSummary> SpacecraftSummaries { get; }

    internal ScenarioSummary(Window window, IReadOnlyList<SpacecraftSummary> spacecraftSummaries)
    {
        Window = window;
        SpacecraftSummaries = spacecraftSummaries;
    }
}
