using System.Collections.Generic;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Variational;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>stm</c> category: the propagation of a <c>propagator</c> case with its state transition matrix, Φ(t_end,
/// t_start) at the exact end of the window, in the frame of the case.
/// </summary>
/// <remarks>
/// The propagation is the <c>propagator</c> category's (spec.md section 16.4, lot 6 of #457: the cases of
/// conformance-tests#10 regenerated on the suite's Earth): the field of the suite's <c>data/</c>, the files of
/// <c>data/</c> the case lists (<see cref="CaseKernels"/>) and the RK7(8) integrator at 1e-13
/// (<see cref="PropagatorSolver.CreateIntegrator"/>), the tolerance of the validation of the matrix against central
/// differences of whole propagations (F1). Φ comes from the variational equations integrated with the state. Φ and the
/// final state are read at the end of the window as the case tags it, the instant of the end (#501). Φ is the same in km
/// and km/s as in m and m/s (its rv block in s, its vr block in 1/s).
/// </remarks>
public class StmSolver : ICategorySolver
{
    private readonly string _conformanceTestsPath;
    private readonly string _dataPath;

    /// <param name="conformanceTestsPath">The root of the suite, whose <c>data/</c> holds the kernels of the cases.</param>
    /// <param name="dataPath">The directory of the field files; the suite's <c>data/</c> when null.</param>
    public StmSolver(string conformanceTestsPath, string dataPath = null)
    {
        _conformanceTestsPath = conformanceTestsPath;
        _dataPath = dataPath ?? System.IO.Path.Combine(conformanceTestsPath, "data");
    }

    public string Category => "stm";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        using var kernels = new CaseKernels(_conformanceTestsPath, InputParser.ParseKernels(caseInput.Inputs));
        var propagationCase = PropagationCase.Build(caseInput, _dataPath);
        var integrator = PropagatorSolver.CreateIntegrator();
        var propagator = new CentralBodyPropagator(
            propagationCase.Window, propagationCase.Spacecraft, integrator, propagationCase.Bodies,
            propagationCase.Drag, propagationCase.SolarRadiationPressure, propagationCase.OutputStep,
            new CovariancePropagationOptions { Mode = CovariancePropagationMode.StateTransitionOnly });
        var solution = propagator.Propagate();

        var end = propagationCase.Window.EndDate;
        var phi = solution.GetStateTransitionMatrix(end);
        var values = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                values[6 * i + j] = phi.Get(i, j);
            }
        }

        var (finalPos, finalVel) = solution.InterpolateAt(end);
        return new Dictionary<string, object>
        {
            ["final_stm"] = values,
            ["final_x_km"] = UnitConversion.MToKm(finalPos.X),
            ["final_y_km"] = UnitConversion.MToKm(finalPos.Y),
            ["final_z_km"] = UnitConversion.MToKm(finalPos.Z),
            ["final_vx_km_s"] = UnitConversion.MSToKmS(finalVel.X),
            ["final_vy_km_s"] = UnitConversion.MSToKmS(finalVel.Y),
            ["final_vz_km_s"] = UnitConversion.MSToKmS(finalVel.Z)
        };
    }
}
