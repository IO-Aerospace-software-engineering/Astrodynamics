using System.Collections.Generic;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

public class PropagatorSolver : ICategorySolver
{
    private readonly string _dataPath;

    public PropagatorSolver(string dataPath)
    {
        _dataPath = dataPath;
    }

    public string Category => "propagator";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var propagationCase = PropagationCase.Build(caseInput, _dataPath);

        var integrator = new RK78Integrator(
            absoluteTolerance: 1e-11, relativeTolerance: 1e-11,
            initialStepSize: 60.0, minStepSize: 1e-3, maxStepSize: 300.0);

        // Propagate
        var propagator = new CentralBodyPropagator(
            propagationCase.Window, propagationCase.Spacecraft, integrator, propagationCase.Bodies,
            propagationCase.Drag, propagationCase.SolarRadiationPressure, propagationCase.StepSize);

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
