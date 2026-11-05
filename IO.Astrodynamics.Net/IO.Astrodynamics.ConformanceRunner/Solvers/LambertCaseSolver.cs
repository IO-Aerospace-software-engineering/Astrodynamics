using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Maneuver.Lambert;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>lambert</c> category (spec.md section 13.2): the departure and arrival velocities of the two-body transfer
/// from r1 to r2 in the time of flight, with N complete revolutions, in the direction and branch of the case, by the
/// framework's solver (<see cref="LambertSolver"/>, Izzo's algorithm) with the kernel GM of the case's
/// <c>gm_de440.tpc</c>.
/// </summary>
/// <remarks>
/// The solver takes two states: r1 at the departure epoch and r2 at the epoch <c>time_of_flight_s</c> TT seconds later,
/// the time of flight it counts about the Earth (#508); their velocities do not enter v1 and v2. For N ≥ 1 it gives two
/// solutions, which it calls Left and Right; the case names them by their semi-major axis, from the energy of v1 at r1,
/// whatever the solver calls them.
/// </remarks>
public class LambertCaseSolver : ICategorySolver
{
    private readonly string _conformanceTestsPath;

    /// <summary>The branch of the smaller semi-major axis of the two of N ≥ 1 revolutions.</summary>
    public const string LowerA = "lower_a";

    /// <summary>The branch of the larger semi-major axis of the two of N ≥ 1 revolutions.</summary>
    public const string HigherA = "higher_a";

    /// <param name="conformanceTestsPath">The root of the suite, whose <c>data/</c> holds the kernels of the cases.</param>
    public LambertCaseSolver(string conformanceTestsPath)
    {
        _conformanceTestsPath = conformanceTestsPath;
    }

    public string Category => "lambert";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        using var kernels = new CaseKernels(_conformanceTestsPath, InputParser.ParseKernels(caseInput.Inputs));
        var inputs = InputParser.ParseLambertInputs(caseInput.Inputs);
        bool retrograde = inputs.Direction switch
        {
            "prograde" => false,
            "retrograde" => true,
            _ => throw new ArgumentException($"The direction '{inputs.Direction}' is neither prograde nor retrograde (spec.md section 13.2).")
        };

        var departure = EpochParser.Parse(inputs.DepartureEpoch);
        var center = new CelestialBody(BodyResolver.ResolveNaif(inputs.CentralBody), Frame.ICRF, departure);
        var r1 = Metres(inputs.R1Km);
        var initial = new StateVector(r1, Vector3.Zero, center, departure, Frame.ICRF);
        var target = new StateVector(Metres(inputs.R2Km), Vector3.Zero, center, departure.ToTDT().AddSeconds(inputs.TimeOfFlightS), Frame.ICRF);

        var result = new LambertSolver().Solve(retrograde, initial, target, center, (ushort)inputs.Revolutions);
        var solution = Select(result, inputs, r1, center.GM);
        if (System.Math.Sign(r1.Cross(solution.V1).Z) != (retrograde ? -1 : 1))
        {
            throw new InvalidOperationException($"The solver's transfer is not {inputs.Direction}: r1 × v1 has the z component {r1.Cross(solution.V1).Z}.");
        }

        return new Dictionary<string, object>
        {
            ["v1_km_s"] = KilometresPerSecond(solution.V1),
            ["v2_km_s"] = KilometresPerSecond(solution.V2)
        };
    }

    /// <summary>
    /// The solution of the case: of 0 revolutions, or of N ≥ 1 that of its branch, the smaller or the larger semi-major
    /// axis of the two.
    /// </summary>
    /// <exception cref="ArgumentException">A branch with 0 revolutions, or none, or an unknown one, with N ≥ 1.</exception>
    /// <exception cref="InvalidOperationException">The solver gives no solution of 0 revolutions, or not two of N.</exception>
    internal static LambertSolution Select(LambertResult result, LambertInputs inputs, Vector3 r1, double gm)
    {
        if (inputs.Revolutions == 0)
        {
            return inputs.Branch == null
                ? result.GetZeroRevolutionSolution() ?? throw new InvalidOperationException("The solver gives no solution of 0 revolutions.")
                : throw new ArgumentException($"The case has 0 revolutions and the branch '{inputs.Branch}': spec.md section 13.2 takes none.");
        }

        var solutions = result.GetMultiRevolutionSolutions((uint)inputs.Revolutions).ToList();
        if (solutions.Count != 2)
        {
            throw new InvalidOperationException($"The solver gives {solutions.Count} solution(s) of {inputs.Revolutions} revolution(s), not two.");
        }

        var bySemiMajorAxis = solutions.OrderBy(s => SemiMajorAxis(r1, s.V1, gm)).ToList();
        return inputs.Branch switch
        {
            LowerA => bySemiMajorAxis[0],
            HigherA => bySemiMajorAxis[1],
            _ => throw new ArgumentException($"The branch '{inputs.Branch ?? "null"}' of {inputs.Revolutions} revolution(s) is neither {LowerA} nor {HigherA} (spec.md section 13.2).")
        };
    }

    /// <summary>The semi-major axis of the two-body orbit of velocity <paramref name="v"/> at <paramref name="r"/>, m.</summary>
    internal static double SemiMajorAxis(Vector3 r, Vector3 v, double gm)
    {
        return 1.0 / (2.0 / r.Magnitude() - v.MagnitudeSquared() / gm);
    }

    private static Vector3 Metres(double[] km)
    {
        return new Vector3(UnitConversion.KmToM(km[0]), UnitConversion.KmToM(km[1]), UnitConversion.KmToM(km[2]));
    }

    private static double[] KilometresPerSecond(Vector3 v)
    {
        return new[] { UnitConversion.MSToKmS(v.X), UnitConversion.MSToKmS(v.Y), UnitConversion.MSToKmS(v.Z) };
    }
}
