using System;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The two-body trajectory of the <c>eclipse</c> (spec.md section 12.2), <c>ground_station</c> (14.1) and
/// <c>attitude</c> (14.3) categories: the Kepler motion of an initial state about a body with the case's GM
/// (<c>gm_km3_s2</c>), integrated by RK7(8) about the Earth over the TT seconds of the window (#501).
/// </summary>
/// <remarks>
/// The kernels give the body the GM of DE440; a case GM that differs from it is applied as the GM of the whole field of a
/// geopotential of degree 0 (<see cref="GeopotentialCentralTerm.ModelGravitationalParameter"/>), which is the point mass
/// of that GM (decision 19 of #457). The field file of the suite's <c>data/</c> gives no coefficient at degree 0; it is
/// read for its header only.
/// </remarks>
internal sealed class TwoBodyTrajectory
{
    // m³ per km³
    private const double CubicMetresPerCubicKilometre = 1e9;

    private readonly string _conformanceTestsPath;

    /// <param name="conformanceTestsPath">The root of the suite, whose <c>data/</c> holds the field file that carries a
    /// case GM.</param>
    public TwoBodyTrajectory(string conformanceTestsPath)
    {
        _conformanceTestsPath = conformanceTestsPath;
    }

    /// <summary>The GM of a case, m³/s², from <c>gm_km3_s2</c>; the kernel GM of <paramref name="body"/> when the case
    /// gives none.</summary>
    public static double Gm(double? gmKm3S2, CelestialBody body)
    {
        return gmKm3S2.HasValue ? gmKm3S2.Value * CubicMetresPerCubicKilometre : body.GM;
    }

    /// <summary>
    /// The body about which the trajectory is integrated: <paramref name="body"/>, a point mass of its kernel GM, or of
    /// <paramref name="gm"/> (m³/s²) when it differs.
    /// </summary>
    public CelestialBody CentralBody(CelestialBody body, double gm, Time epoch)
    {
        if (gm == body.GM)
        {
            return new CelestialBody(body.NaifId, Frame.ICRF, epoch);
        }

        var fieldPath = Path.Combine(_conformanceTestsPath, "data", "EGM2008_to70_TideFree");
        SuiteDataFile.EnsureContent(fieldPath);
        return new CelestialBody(body.NaifId, Frame.ICRF, epoch,
            new GeopotentialModelParameters(fieldPath, 0, gm, body.EquatorialRadius)
            {
                CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter
            });
    }

    /// <summary>The RK7(8) integrator of the trajectory: tolerances <paramref name="tolerance"/>, steps from 1 ms to 300 s.</summary>
    public static RK78Integrator Integrator(double tolerance) => new(
        absoluteTolerance: tolerance, relativeTolerance: tolerance, initialStepSize: 60.0, minStepSize: 1e-3, maxStepSize: 300.0);

    /// <summary>
    /// A spacecraft at <paramref name="initialState"/>, propagated over <paramref name="window"/> about the central
    /// body of the state alone, its states stored every <paramref name="outputStep"/>.
    /// </summary>
    /// <param name="spacecraft">The spacecraft, built at the initial state about the central body.</param>
    /// <param name="window">The window, from the epoch of the state.</param>
    /// <param name="tolerance">The tolerance of RK7(8).</param>
    /// <param name="outputStep">The step of the stored states.</param>
    public static void Propagate(Spacecraft spacecraft, Window window, double tolerance, TimeSpan outputStep)
    {
        var central = (CelestialBody)spacecraft.InitialOrbitalParameters.Observer;
        spacecraft.Propagate(window, [central], Integrator(tolerance), false, false, outputStep);
    }

    /// <summary>
    /// The state of Keplerian elements about <paramref name="centralBody"/> with <paramref name="gm"/> (m³/s²): the library
    /// converts them with the kernel GM of <paramref name="body"/>; at the position of the elements, which does not depend
    /// on GM, the velocity scales as √GM.
    /// </summary>
    public static StateVector KeplerianState(KeplerianOrbit kep, CelestialBody body, CelestialBody centralBody, double gm, Time epoch)
    {
        var state = new KeplerianElements(UnitConversion.KmToM(kep.AKm), kep.E, UnitConversion.DegToRad(kep.IDeg),
            UnitConversion.DegToRad(kep.RaanDeg), UnitConversion.DegToRad(kep.ArgpDeg), UnitConversion.DegToRad(kep.MaDeg),
            body, epoch, Frame.ICRF).ToStateVector();
        return new StateVector(state.Position, state.Velocity * System.Math.Sqrt(gm / body.GM), centralBody, epoch, Frame.ICRF);
    }
}
