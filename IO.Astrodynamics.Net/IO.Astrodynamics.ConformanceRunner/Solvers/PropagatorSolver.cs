using System.Collections.Generic;
using System.IO;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>propagator</c> category (spec.md section 12.1): the final state of a propagation of the case's state in ICRF
/// about the Earth, with EGM2008 of the suite's <c>data/</c> (its GM and radius applied to the whole field) and the third
/// bodies as point masses, computed with the files of <c>data/</c> the case lists (<see cref="CaseKernels"/>).
/// </summary>
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

    /// <summary>The RK7(8) integrator of the category: tolerances 1e-11, steps from 1 ms to 300 s.</summary>
    internal static RK78Integrator CreateIntegrator() => new(
        absoluteTolerance: 1e-11, relativeTolerance: 1e-11,
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

        var res=propagator.Propagate();

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
