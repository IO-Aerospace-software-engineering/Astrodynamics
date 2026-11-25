using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.CLI.Cli;

/// <summary>
/// The epochs a command writes, in the scale of <c>--time-scale</c>, the scale of the input by default (R29 of #668,
/// decided by S. Guillet, 2026-11-19). A label converted to another scale is the nearest tick of the instant, which can sit
/// one tick off it; a label already in the scale is written as it is, bit for bit.
/// </summary>
internal static class CliTime
{
    internal static Time Relabel(in Time epoch, TimeScale scale) => epoch.Scale == scale ? epoch : epoch.ConvertTo(scale);

    internal static Window Relabel(in Window window, TimeScale scale) =>
        window.StartDate.Scale == scale && window.EndDate.Scale == scale
            ? window
            : new Window(Relabel(window.StartDate, scale), Relabel(window.EndDate, scale));

    internal static StateOrientation Relabel(StateOrientation orientation, TimeScale scale) =>
        orientation.Epoch.Scale == scale
            ? orientation
            : new StateOrientation(orientation.Rotation, orientation.AngularVelocity, Relabel(orientation.Epoch, scale),
                orientation.ReferenceFrame);

    /// <summary>
    /// Orbital parameters labelled in <paramref name="scale"/>: those given when their epoch is in it; otherwise their state
    /// at the same instant, labelled in the scale, in the kind given (state vector, Keplerian or equinoctial elements).
    /// </summary>
    internal static OrbitalParameters.OrbitalParameters Relabel(OrbitalParameters.OrbitalParameters orbit, TimeScale scale)
    {
        if (orbit.Epoch.Scale == scale)
        {
            return orbit;
        }

        var sv = orbit.ToStateVector();
        var relabelled = new StateVector(sv.Position, sv.Velocity, sv.Observer, Relabel(sv.Epoch, scale), sv.Frame);
        return orbit switch
        {
            KeplerianElements => relabelled.ToKeplerianElements(),
            EquinoctialElements => relabelled.ToEquinoctial(),
            _ => relabelled
        };
    }
}
