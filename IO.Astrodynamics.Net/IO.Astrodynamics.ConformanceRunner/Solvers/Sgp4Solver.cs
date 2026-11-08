using System;
using System.Collections.Generic;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>sgp4</c> category (spec.md section 15.1): the SGP4/SDP4 states of a TLE in TEME at minutes since its epoch,
/// or in TEME and ICRF at UTC instants, and the TLE epoch.
/// </summary>
/// <remarks>
/// <para>
/// The framework's SGP4 is CSPICE <c>evsgp4_c</c>: Vallado's 2006 code in the AFSPC mode with WGS-72 "old" (KE
/// 7.43669161e-2), the model of <c>gravity_model: wgs72old</c> and <c>opsmode: afspc</c>, its only one. A case of another
/// model or mode is an ERROR that names it, a model or mode the spec does not define another.
/// </para>
/// <para>
/// <see cref="TLE.ToOsculating(Time)"/> takes tsince as the TT seconds from the TLE epoch to its argument (#599): a
/// tsince sample is the TT instant <c>tle.Epoch</c> + 60·tsince, a UTC instant is given as it is. The ICRF state is the
/// TEME state through <see cref="Frame.TEME"/> (#451), quasi-inertial, v′ = R v. The epoch is the UTC calendar date of
/// <see cref="TLE"/>'s epoch, exact to the tick (#603).
/// </para>
/// </remarks>
public class Sgp4Solver : ICategorySolver
{
    // The models and modes of SGP4 a case may declare (Vallado's names; spec.md section 15.1 and the later lots), and the
    // framework's
    private static readonly HashSet<string> GravityModels = [Sgp4Inputs.Wgs72Old, "wgs72", "wgs84"];
    private static readonly HashSet<string> Opsmodes = [Sgp4Inputs.Afspc, "improved"];

    public string Category => "sgp4";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParseSgp4Inputs(caseInput.Inputs);
        CheckModel(inputs.GravityModel, inputs.Opsmode);
        var tle = new TLE(string.IsNullOrEmpty(inputs.Tle.Name) ? "TLE" : inputs.Tle.Name, inputs.Tle.Line1, inputs.Tle.Line2);

        var instants = new List<Time>();
        if (inputs.TsinceMin != null)
        {
            var epoch = tle.Epoch.ToTDT();
            instants.AddRange(inputs.TsinceMin.ConvertAll(tsince => epoch.AddSeconds(60.0 * tsince)));
        }
        else
        {
            instants.AddRange(inputs.UtcEpochs.ConvertAll(EpochParser.Parse));
        }

        bool icrf = inputs.OutputFrames.Contains("ICRF");
        var outputs = new Dictionary<string, object>
        {
            ["epoch"] = tle.Epoch.DateTime,
            ["states"] = instants.Count
        };
        for (int k = 0; k < instants.Count; k++)
        {
            var teme = tle.ToOsculating(instants[k]);
            AddState(outputs, OutputComparer.IndexedOutput("states", k, "teme"), teme);
            if (icrf)
            {
                AddState(outputs, OutputComparer.IndexedOutput("states", k, "icrf"), teme.ToFrame(Frame.ICRF).ToStateVector());
            }
        }

        return outputs;
    }

    /// <summary>
    /// Refuses a gravity model or a mode the framework's SGP4 does not run (<c>wgs72old</c> and <c>afspc</c> only), naming
    /// the reason.
    /// </summary>
    /// <exception cref="ArgumentException">The model or the mode is not the framework's.</exception>
    internal static void CheckModel(string gravityModel, string opsmode)
    {
        if (gravityModel != Sgp4Inputs.Wgs72Old)
        {
            throw new ArgumentException(GravityModels.Contains(gravityModel ?? string.Empty)
                ? $"The gravity model '{gravityModel}' cannot be run: the framework's SGP4 (CSPICE evsgp4_c) takes WGS-72 old (wgs72old) only."
                : $"The gravity model '{gravityModel}' is not a gravity model of SGP4 (spec.md section 15.1).");
        }

        if (opsmode != Sgp4Inputs.Afspc)
        {
            throw new ArgumentException(Opsmodes.Contains(opsmode ?? string.Empty)
                ? $"The mode '{opsmode}' cannot be run: the framework's SGP4 (CSPICE evsgp4_c) runs the AFSPC mode (afspc) only."
                : $"The mode '{opsmode}' is not a mode of SGP4 (spec.md section 15.1).");
        }
    }

    private static void AddState(Dictionary<string, object> outputs, string prefix, StateVector state)
    {
        outputs[$"{prefix}.position_km"] = new[]
        {
            UnitConversion.MToKm(state.Position.X), UnitConversion.MToKm(state.Position.Y), UnitConversion.MToKm(state.Position.Z)
        };
        outputs[$"{prefix}.velocity_km_s"] = new[]
        {
            UnitConversion.MSToKmS(state.Velocity.X), UnitConversion.MSToKmS(state.Velocity.Y), UnitConversion.MSToKmS(state.Velocity.Z)
        };
    }
}
