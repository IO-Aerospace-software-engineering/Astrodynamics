// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Time;

namespace IO.Astrodynamics.Body.Spacecraft;

public class SpacecraftSummary
{
    public SpacecraftSummary(Spacecraft spacecraft,IEnumerable<Maneuver.Maneuver> maneuvers)
    {
        var enumeratesManeuver = maneuvers as Maneuver.Maneuver[] ?? maneuvers.ToArray();
        Maneuvers = enumeratesManeuver;
        Spacecraft = spacecraft;

        if (enumeratesManeuver.Length > 0)
        {
            var windows = enumeratesManeuver.Select(x => x.ManeuverWindow.Value);
            var enumerable = windows as Window[] ?? windows.ToArray();
            ManeuverWindow = enumerable.FirstOrDefault();
            FuelConsumption = enumeratesManeuver.Sum(x => x.FuelBurned);
            foreach (var win in enumerable.Skip(1))
            {
                ManeuverWindow = ManeuverWindow.Value.Merge(win);
            }
        }
    }

    public Spacecraft Spacecraft { get; }
    public Window? ManeuverWindow { get; }
    public double FuelConsumption { get; }
    public IEnumerable<Maneuver.Maneuver> Maneuvers { get; }
    
}