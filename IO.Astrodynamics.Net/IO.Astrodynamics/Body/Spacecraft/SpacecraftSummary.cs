// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections.Generic;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body.Spacecraft;

/// <summary>
/// The summary of one spacecraft of a scenario's run (<see cref="Mission.ScenarioSummary"/>), built by
/// <see cref="Mission.Scenario.Simulate"/> from the records of its solution (decision 59 of #668, decided by S. Guillet,
/// 2026-11-23): its values are those of that run, which a later run does not change. Before 11.0.0 its constructor was
/// public and <see cref="Maneuvers"/> held the definitions, whose values the next run reset.
/// </summary>
public sealed class SpacecraftSummary
{
    /// <summary>The summary of <paramref name="spacecraft"/> from the records of its run.</summary>
    internal SpacecraftSummary(Spacecraft spacecraft, IReadOnlyList<ExecutedManeuver> maneuvers)
    {
        Spacecraft = spacecraft;
        Maneuvers = maneuvers;

        // The hull of the maneuver windows and the sum of the fuel, in execution order: on equal ends the later window's,
        // as Window.Merge took them before 11.0.0, bit for bit
        if (maneuvers.Count > 0)
        {
            var start = maneuvers[0].ManeuverWindow.StartDate;
            var end = maneuvers[0].ManeuverWindow.EndDate;
            double fuel = maneuvers[0].FuelBurned;
            for (int i = 1; i < maneuvers.Count; i++)
            {
                var window = maneuvers[i].ManeuverWindow;
                start = start < window.StartDate ? start : window.StartDate;
                end = end > window.EndDate ? end : window.EndDate;
                fuel += maneuvers[i].FuelBurned;
            }

            ManeuverWindow = new Window(start, end);
            FuelConsumption = fuel;
        }
    }

    /// <summary>The spacecraft.</summary>
    public Spacecraft Spacecraft { get; }

    /// <summary>The hull of the maneuver windows of the run, attitudes included; null when it executed no maneuver.</summary>
    public Window? ManeuverWindow { get; }

    /// <summary>The fuel the run burned, kg: the sum of <see cref="ExecutedManeuver.FuelBurned"/>.</summary>
    public double FuelConsumption { get; }

    /// <summary>The records of the maneuvers the run executed, in execution order (<see cref="PropagationSolution.Maneuvers"/>).</summary>
    public IReadOnlyList<ExecutedManeuver> Maneuvers { get; }
}
