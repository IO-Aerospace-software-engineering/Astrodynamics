using System.Collections.Generic;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>stm</c> category: the propagation of a <c>propagator</c> case with its state transition matrix, Φ(t_end,
/// t_start) at the exact end of the window, in the frame of the case.
/// </summary>
/// <remarks>
/// Φ comes from the variational equations integrated with the state by RK7(8) at 1e-13, the tolerance of the
/// validation of the matrix against central differences of whole propagations (F1), rather than the 1e-11 of the
/// <c>propagator</c> cases. The end of the window, a UTC epoch, is converted to TDB, as for the final state. Φ is the
/// same in km and km/s as in m and m/s (its rv block in s, its vr block in 1/s).
/// </remarks>
public class StmSolver : ICategorySolver
{
    /// <summary>Tolerance of RK7(8), absolute and relative.</summary>
    internal const double Tolerance = 1e-13;

    private readonly string _dataPath;

    public StmSolver(string dataPath)
    {
        _dataPath = dataPath;
    }

    public string Category => "stm";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var propagationCase = PropagationCase.Build(caseInput, _dataPath);
        var integrator = new RK78Integrator(Tolerance, Tolerance);
        var propagator = new CentralBodyPropagator(
            propagationCase.Window, propagationCase.Spacecraft, integrator, propagationCase.Bodies,
            propagationCase.Drag, propagationCase.SolarRadiationPressure, propagationCase.OutputStep,
            new CovariancePropagationOptions { Mode = CovariancePropagationMode.StateTransitionOnly });
        var solution = propagator.Propagate();

        var end = propagationCase.Window.EndDate.ToTDB();
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
