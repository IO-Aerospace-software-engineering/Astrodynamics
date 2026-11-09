using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>conjunction</c> category (spec.md section 16.1): the TCA of two objects in an analysis window, the miss
/// distance, the relative speed and state in the RTN of object 1, each object's covariance at TCA and the 2D Pc; or
/// every TCA of the window.
/// </summary>
/// <remarks>
/// <para>
/// A propagated object is the spacecraft of a <c>propagator</c> case (<see cref="PropagationCase"/>) built from its state
/// and spacecraft and the case's force model, its initial state carrying its covariance rotated from its RTN, propagated
/// in RK7(8) (<see cref="PropagatorSolver.CreateIntegrator"/>) from the epoch to the end of the window, about the Earth
/// over TT seconds; the analysis runs on the two <see cref="PropagationSolution"/>s, and the framework propagates each
/// covariance to TCA by a replay of the variational equations along the steps of its propagation (P = Φ P0 Φᵀ, no
/// process noise). A kernel object is a kernel-backed spacecraft whose orbit is read about the Earth (#620), the SPKs of
/// the case loaded, analyzed through the <see cref="ILocalizable"/> overloads.
/// </para>
/// <para>
/// <c>closest</c> is <see cref="ConjunctionAssessment.Analyze(ProtectedSpacecraftProfile, PropagationSolution, ILocalizable, PropagationSolution, Window, ConjunctionAnalysisOptions)"/>,
/// the interior minimum of least miss; <c>all_minima</c> is <c>AnalyzeAll</c>, its encounters in time order. The
/// combined hard-body radius is object 1's, none on object 2. <c>tca_s</c> counts the TT seconds from the epoch, the TDB
/// seconds for kernel objects (the time argument of their SPKs). A covariance the framework did not propagate (held in
/// RTN, unavailable) and a Pc it did not compute are outputs the solver cannot give, an ERROR when the golden holds them.
/// </para>
/// </remarks>
public class ConjunctionSolver : ICategorySolver
{
    // The output step of the propagations; the analysis reads their dense output
    private static readonly TimeSpan OutputStep = TimeSpan.FromSeconds(60.0);

    // The NAIF id of object 1 of a propagated case; object 2 takes the next lower
    private const int NaifId = -1501;

    // The inputs of a propagation case a conjunction case shares (spec.md section 12.1)
    private static readonly string[] PropagationKeys = ["central_body", "geopotential_degree", "geopotential_model", "perturbation_bodies", "force_model"];

    private readonly string _conformanceTestsPath;
    private readonly string _dataPath;

    /// <param name="conformanceTestsPath">The root of the suite, whose <c>data/</c> holds the kernels of the cases.</param>
    /// <param name="dataPath">The directory of the field files; the suite's <c>data/</c> when null.</param>
    public ConjunctionSolver(string conformanceTestsPath, string dataPath = null)
    {
        _conformanceTestsPath = conformanceTestsPath;
        _dataPath = dataPath ?? Path.Combine(conformanceTestsPath, "data");
    }

    public string Category => "conjunction";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParseConjunctionInputs(caseInput.Inputs);
        using var kernels = new CaseKernels(_conformanceTestsPath, InputParser.ParseKernels(caseInput.Inputs));
        var epoch = EpochParser.Parse(inputs.Epoch);
        var window = new Window(EpochParser.Parse(inputs.AnalysisWindow.Start), EpochParser.Parse(inputs.AnalysisWindow.End));
        double hardBodyRadius = UnitConversion.KmToM(inputs.HardBodyRadiusKm ?? 0.0);
        var options = new ConjunctionAnalysisOptions { SecondaryHardBodyRadiusMeters = 0.0 };
        bool closest = inputs.Report == ConjunctionInputs.Closest;

        IReadOnlyList<EncounterCase> encounters;
        Func<Time, double> seconds;
        if (inputs.Objects[0].FromKernel)
        {
            var objects = inputs.Objects.Select(o => new Spacecraft(o.NaifId!.Value, o.Name, epoch,
                hardBodyRadius: o == inputs.Objects[0] ? hardBodyRadius : 0.0)).ToArray();
            var profile = new ProtectedSpacecraftProfile(objects[0]);
            encounters = closest
                ? [ConjunctionAssessment.Analyze(profile, objects[1], window, options)]
                : ConjunctionAssessment.AnalyzeAll(profile, objects[1], window, options);
            seconds = t => (t.ToTDB() - epoch.ToTDB()).TotalSeconds;
        }
        else
        {
            var propagated = Enumerable.Range(0, 2).Select(k => Propagate(caseInput, inputs, k, window, hardBodyRadius)).ToArray();
            var profile = new ProtectedSpacecraftProfile(propagated[0].Spacecraft);
            encounters = closest
                ? [ConjunctionAssessment.Analyze(profile, propagated[0].Solution, propagated[1].Spacecraft, propagated[1].Solution, window, options)]
                : ConjunctionAssessment.AnalyzeAll(profile, propagated[0].Solution, propagated[1].Spacecraft, propagated[1].Solution, window, options);
            seconds = t => (t.ToTDT() - epoch.ToTDT()).TotalSeconds;
        }

        var outputs = new Dictionary<string, object>();
        if (closest)
        {
            AddClosest(outputs, encounters[0], seconds, inputs);
        }
        else
        {
            outputs[Minima] = encounters.Count;
            for (int k = 0; k < encounters.Count; k++)
            {
                outputs[OutputComparer.IndexedOutput(Minima, k, "tca_s")] = seconds(encounters[k].EncounterState.Epoch);
                outputs[OutputComparer.IndexedOutput(Minima, k, "miss_distance_km")] = UnitConversion.MToKm(encounters[k].EncounterState.MissDistanceMeters);
            }
        }

        return outputs;
    }

    /// <summary>The indexed array of the TCAs of an <c>all_minima</c> case.</summary>
    public const string Minima = "minima";

    /// <summary>The indexed array of the covariances of the two objects at TCA.</summary>
    public const string Objects = "objects";

    // The TCA of least miss: its time, the relative state, each object's covariance at TCA and the Pc
    private static void AddClosest(Dictionary<string, object> outputs, EncounterCase encounter, Func<Time, double> seconds,
        ConjunctionInputs inputs)
    {
        var state = encounter.EncounterState;
        var relative = state.RelativeState;
        outputs["tca_s"] = seconds(state.Epoch);
        outputs["miss_distance_km"] = UnitConversion.MToKm(state.MissDistanceMeters);
        outputs["relative_speed_km_s"] = UnitConversion.MSToKmS(relative.RelativeVelocityInertial.Magnitude());
        outputs["relative_position_rtn_km"] = Km(relative.RelativePositionRtn);
        outputs["relative_velocity_rtn_km_s"] = Km(relative.RelativeVelocityRtn);
        outputs[Objects] = 2;
        var states = new[] { encounter.ProtectedState, encounter.SecondaryState };
        var sources = new[] { state.ProtectedCovariance, state.SecondaryCovariance };
        for (int k = 0; k < 2; k++)
        {
            outputs[OutputComparer.IndexedOutput(Objects, k, "sigma_rtn")] = SolverOutput.Of(Covariance(states[k], sources[k], inputs.Objects[k], 0));
            outputs[OutputComparer.IndexedOutput(Objects, k, "correlation_rtn")] = SolverOutput.Of(Covariance(states[k], sources[k], inputs.Objects[k], 1));
        }

        outputs["collision_probability"] = SolverOutput.Of(() => inputs.HardBodyRadiusKm == null
            ? throw new InvalidOperationException("The case gives no hard_body_radius_km, the input of the Pc (spec.md section 16.1).")
            : encounter.CollisionRisk.ProbabilityOfCollision ??
              throw new InvalidOperationException($"The framework gives no Pc for this encounter (flags {state.QualityFlags})."));
    }

    // The sigmas (part 0) or the correlations (part 1) of an object's covariance at TCA in its own RTN, the covariance the
    // framework propagated from its initial covariance
    private static Func<object> Covariance(OrbitalParameters.StateVector state, ParticipantCovariance source, ConjunctionObject input, int part)
    {
        return () =>
        {
            if (input.CovarianceRtn == null)
            {
                throw new InvalidOperationException($"The case gives {input.Name} no initial covariance.");
            }

            if (source.Source != CovarianceSource.Propagated || state.Covariance is not { } covariance)
            {
                throw new InvalidOperationException(
                    $"The framework did not propagate the covariance of {input.Name} to TCA (source {source.Source}): spec.md section 16.1 propagates it linearly.");
            }

            var (sigmas, correlations) = LowerTriangle.SigmasAndCorrelations(state.RotateCovarianceToRtn(covariance), 1e-3);
            return part == 0 ? sigmas : correlations;
        };
    }

    private static double[] Km(Math.Vector3 vector)
    {
        return [UnitConversion.MToKm(vector.X), UnitConversion.MToKm(vector.Y), UnitConversion.MToKm(vector.Z)];
    }

    // An object propagated from the epoch to the end of the window by the case's force model, with its covariance
    private (Spacecraft Spacecraft, PropagationSolution Solution) Propagate(CaseInput caseInput, ConjunctionInputs inputs, int k,
        Window window, double hardBodyRadius)
    {
        var o = inputs.Objects[k];
        var propagationCase = PropagationCase.Build(ObjectCase(caseInput, inputs, o), _dataPath, NaifId - k,
            o.CovarianceRtn == null ? null : LowerTriangle.ToMatrix(o.CovarianceRtn, 1e6), k == 0 ? hardBodyRadius : 0.0);
        var spacecraft = propagationCase.Spacecraft;

        var solution = spacecraft.Propagate(new Window(propagationCase.Window.StartDate, window.EndDate), propagationCase.Bodies,
            PropagatorSolver.CreateIntegrator(), propagationCase.Drag, propagationCase.SolarRadiationPressure, OutputStep);
        return (spacecraft, solution);
    }

    /// <summary>
    /// The <c>propagator</c> case of a propagated object (spec.md section 12.1): its state and spacecraft at the epoch, the
    /// case's force model, the window from the epoch to the end of the analysis window.
    /// </summary>
    /// <exception cref="ArgumentException">The case lacks an input of the force model.</exception>
    internal static CaseInput ObjectCase(CaseInput caseInput, ConjunctionInputs inputs, ConjunctionObject o)
    {
        var objectInputs = new Dictionary<string, object>
        {
            ["epoch"] = inputs.Epoch,
            ["orbit"] = new Dictionary<object, object>
            {
                ["type"] = "state_vector",
                ["position_km"] = o.PositionKm.Cast<object>().ToList(),
                ["velocity_km_s"] = o.VelocityKmS.Cast<object>().ToList()
            },
            ["propagation_window"] = new Dictionary<object, object> { ["start"] = inputs.Epoch, ["end"] = inputs.AnalysisWindow.End },
            ["step_size_s"] = OutputStep.TotalSeconds
        };
        foreach (var key in PropagationKeys)
        {
            objectInputs[key] = caseInput.Inputs.TryGetValue(key, out var value)
                ? value
                : throw new ArgumentException($"The conjunction case propagates its objects and gives no {key} (spec.md sections 12.1 and 16.1).");
        }

        if (o.Spacecraft != null)
        {
            objectInputs["spacecraft"] = o.Spacecraft;
        }

        return new CaseInput
        {
            Id = caseInput.Id,
            Category = caseInput.Category,
            Metadata = caseInput.Metadata,
            Inputs = objectInputs
        };
    }
}
