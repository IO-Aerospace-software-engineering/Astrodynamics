using System.Collections.Generic;
using System.IO;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>propagator</c> category (spec.md section 12.1): the final state of a propagation of the case's state in ICRF
/// about the Earth, the Moon, Mars or the Sun, with the field of the suite's <c>data/</c> (its GM and radius applied to
/// the whole field), the third bodies as point masses, and about the Earth NRLMSISE-00 drag and SRP
/// (<see cref="PropagationCase"/>), computed with the files of <c>data/</c> the case lists (<see cref="CaseKernels"/>):
/// the Earth orientation file, the Mars cut, MOON_PA.
/// </summary>
/// <remarks>
/// The framework integrates over the TDB seconds of the window. The suite propagates about the Earth over its TT seconds
/// (spec.md section 12.1), which moves its goldens by 2.3 cm to 0.52 m; the framework's deviation is #501.
/// </remarks>
public class PropagatorSolver : ICategorySolver
{
    private readonly string _conformanceTestsPath;
    private readonly string _dataPath;

    /// <param name="conformanceTestsPath">The root of the suite, whose <c>data/</c> holds the kernels of the cases.</param>
    /// <param name="dataPath">The directory of the field files; the suite's <c>data/</c> when null.</param>
    public PropagatorSolver(string conformanceTestsPath, string dataPath = null)
    {
        _conformanceTestsPath = conformanceTestsPath;
        _dataPath = dataPath ?? Path.Combine(conformanceTestsPath, "data");
    }

    public string Category => "propagator";

    /// <summary>Tolerance of RK7(8), absolute and relative (design of #500, open choice 18).</summary>
    internal const double Tolerance = 1e-13;

    /// <summary>
    /// The RK7(8) integrator of the category: tolerances 1e-13, steps from 1 ms to 300 s. At 1e-11 the embedded error
    /// estimate misses the jumps of the shadow function's derivative at the contacts: 0.21 m of integration error on
    /// 015, and 011 fails at 1e-10 (design of #500, section 6.2).
    /// </summary>
    internal static RK78Integrator CreateIntegrator() => new(
        absoluteTolerance: Tolerance, relativeTolerance: Tolerance,
        initialStepSize: 60.0, minStepSize: 1e-3, maxStepSize: 300.0);

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        using var kernels = new CaseKernels(_conformanceTestsPath, InputParser.ParseKernels(caseInput.Inputs));
        var propagationCase = PropagationCase.Build(caseInput, _dataPath);

        var integrator = CreateIntegrator();

        // Propagate
        var propagator = new CentralBodyPropagator(
            propagationCase.Window, propagationCase.Spacecraft, integrator, propagationCase.Bodies,
            propagationCase.Drag, propagationCase.SolarRadiationPressure, propagationCase.OutputStep);

        var res = propagator.Propagate();

        // Interpolate at exact window end to avoid DeltaT sampling gap
        var (finalPos, finalVel) = res.InterpolateAt(propagationCase.Window.EndDate.ToTDB());

        // Return position (km) and velocity (km/s)
        return new Dictionary<string, object>
        {
            ["final_x_km"] = UnitConversion.MToKm(finalPos.X),
            ["final_y_km"] = UnitConversion.MToKm(finalPos.Y),
            ["final_z_km"] = UnitConversion.MToKm(finalPos.Z),
            ["final_vx_km_s"] = UnitConversion.MSToKmS(finalVel.X),
            ["final_vy_km_s"] = UnitConversion.MSToKmS(finalVel.Y),
            ["final_vz_km_s"] = UnitConversion.MSToKmS(finalVel.Z)
        };
    }
}
