// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// The plateau study that sets the relative steps of the default central differences
/// (<see cref="ForceBase.DefaultPositionRelativeStep"/>, <see cref="ForceBase.DefaultVelocityRelativeStep"/>): the
/// error of the default path against a reference, as a function of the relative step, for every built-in force at the
/// four reference states.
/// </summary>
/// <remarks>
/// <para>
/// The reference is analytic for the point mass (B2) and the third bodies (B4), and Ridders' extrapolation for the
/// forces without analytic partials yet (geopotential, drag, SRP, albedo, thermal).
/// </para>
/// <para>
/// Two errors are measured. The relative error ‖ΔJ‖_F / ‖J‖_F of the block of the force itself, and, for ∂a/∂r, the
/// error scaled by the point-mass block of the central body ‖ΔJ‖_F / ‖J_pm‖_F, which is the error the state transition
/// matrix sees, since the blocks of all the forces are summed. The two differ for SRP: its position derivative is about
/// |a| / d_sun, so a step scaled by the geocentric |r| leaves it with a rounding error of about 1e-6 relative, but near
/// 1e-17 scaled (9.1e-17 measured at the default step).
/// </para>
/// <para>
/// Bounds at the default steps: relative error below 1e-4, the threshold of the specification for the default path
/// (B7); scaled error of ∂a/∂r, and relative error of ∂a/∂v (drag only, whose block is the whole velocity block), below
/// 1e-9, so that the partials stay two orders of magnitude below the 1e-7 required of the state transition matrix on
/// the Keplerian reference case (F1); Ridders' error estimate below 1e-7 relative, so that the reference resolves the
/// errors measured. The bounds are checked on the four states that chose the step, so the check is in-sample: it
/// guards the plateau against a change of a force, not the choice of the step itself.
/// </para>
/// <para>
/// To write the curves, set the environment variable <c>IO_ASTRODYNAMICS_STUDY_OUTPUT</c> to a directory: the test then
/// writes <c>fd_step_study.csv</c> there (force, state, block, relative step, relative error, scaled error, Ridders'
/// error estimate).
/// </para>
/// </remarks>
public class FiniteDifferenceStepStudyTests : IClassFixture<PartialsTestCases>
{
    private const string OutputVariable = "IO_ASTRODYNAMICS_STUDY_OUTPUT";

    // Half-decade grid from 1e-12 to 1e-2, plus the default steps.
    private static readonly double[] RelativeSteps = Enumerable.Range(0, 21)
        .Select(k => System.Math.Pow(10.0, -12.0 + 0.5 * k))
        .Append(ForceBase.DefaultPositionRelativeStep)
        .Append(ForceBase.DefaultVelocityRelativeStep)
        .Distinct()
        .Order()
        .ToArray();

    private readonly PartialsTestCases _cases;

    public FiniteDifferenceStepStudyTests(PartialsTestCases cases)
    {
        _cases = cases;
    }

    [Fact]
    public void DefaultSteps_LieOnThePlateauOfEveryBuiltInForce()
    {
        // Arrange
        var studyCases = StudyCases().ToList();

        // Act
        var rows = new List<StudyRow>();
        foreach (var studyCase in studyCases)
        {
            rows.AddRange(Study(studyCase));
        }

        // Assert
        WriteCsvIfRequested(rows);
        foreach (var row in rows.Where(r => r.RelativeStep == DefaultStep(r.Block)))
        {
            string what = $"{row.Force}, {row.State}, {row.Block}";
            Assert.True(row.Error < 1e-4, $"{what}: relative error {row.Error:E2} at the default step");
            Assert.True(row.ScaledError < 1e-9, $"{what}: scaled error {row.ScaledError:E2} at the default step");
            Assert.True(row.ReferenceError < 1e-7, $"{what}: reference error estimate {row.ReferenceError:E2}");
        }
    }

    private static double DefaultStep(string block)
    {
        return block == "dadr" ? ForceBase.DefaultPositionRelativeStep : ForceBase.DefaultVelocityRelativeStep;
    }

    private IEnumerable<StudyRow> Study(StudyCase studyCase)
    {
        double[] referenceDadr;
        double[] referenceDadv;
        double referenceDadrError = 0.0;
        double referenceDadvError = 0.0;
        if (studyCase.AnalyticReference)
        {
            referenceDadr = new double[9];
            referenceDadv = new double[9];
            studyCase.Force.AccumulateStatePartials(studyCase.State, studyCase.Context, referenceDadr, referenceDadv);
        }
        else
        {
            (referenceDadr, referenceDadv, referenceDadrError, referenceDadvError) = RiddersDerivative.StatePartials(
                studyCase.Force, studyCase.State, studyCase.Context, 1e-3 * studyCase.State.Position.Magnitude(),
                1e-3 * studyCase.State.Velocity.Magnitude());
        }

        var pointMass = new double[9];
        new GravitationalAcceleration(_cases.Earth).AccumulateStatePartials(studyCase.State, studyCase.Context,
            pointMass, new double[9]);
        double pointMassNorm = Norm(pointMass);
        double referenceDadrNorm = Norm(referenceDadr);

        string force = studyCase.Force.GetType().Name + studyCase.Suffix;
        foreach (var step in RelativeSteps)
        {
            var dadr = new double[9];
            var dadv = new double[9];
            studyCase.Force.AccumulateStatePartialsByCentralDifferences(studyCase.State, studyCase.Context, dadr, dadv,
                step, step);
            double dadrError = RiddersDerivative.RelativeFrobeniusError(dadr, referenceDadr);
            yield return new StudyRow(force, studyCase.StateName, "dadr", step, dadrError,
                dadrError * referenceDadrNorm / pointMassNorm, referenceDadrError);
            if (studyCase.Force.DependsOnVelocity)
            {
                double dadvError = RiddersDerivative.RelativeFrobeniusError(dadv, referenceDadv);
                yield return new StudyRow(force, studyCase.StateName, "dadv", step, dadvError, dadvError,
                    referenceDadvError);
            }
        }
    }

    private static double Norm(double[] matrix)
    {
        return System.Math.Sqrt(matrix.Sum(x => x * x));
    }

    private IEnumerable<StudyCase> StudyCases()
    {
        foreach (var name in PartialsTestCases.StateNames)
        {
            var state = PartialsTestCases.State(name, _cases.Earth);
            var spacecraft = PartialsTestCases.Spacecraft(state);
            var context = ForceEvaluationContext.FromSpacecraft(spacecraft);

            yield return new StudyCase(new GravitationalAcceleration(_cases.Earth), "", name, state, context, true);
            yield return new StudyCase(new ThirdBodyPerturbation(_cases.Moon, _cases.Earth), " (Moon)", name, state,
                context, true);
            yield return new StudyCase(new ThirdBodyPerturbation(_cases.Sun, _cases.Earth), " (Sun)", name, state,
                context, true);

            var geopotentialState = PartialsTestCases.State(name, _cases.EarthWithGeopotential);
            yield return new StudyCase(new GravitationalAcceleration(_cases.EarthWithGeopotential), " (EGM2008 10x10)",
                name, geopotentialState, context, false);

            if (name != PartialsTestCases.Geo)
            {
                yield return new StudyCase(new AtmosphericDrag(spacecraft, _cases.Earth), "", name, state, context, false);
            }

            yield return new StudyCase(
                new SolarRadiationPressure(spacecraft, new CelestialBody[] { _cases.Earth, _cases.Moon }), "", name,
                state, context, false);
            yield return new StudyCase(new AlbedoRadiationPressure(spacecraft, _cases.Earth), "", name, state, context, false);
            yield return new StudyCase(new ThermalRadiationPressure(spacecraft, _cases.Earth), "", name, state, context, false);
        }
    }

    private static void WriteCsvIfRequested(IReadOnlyList<StudyRow> rows)
    {
        var directory = Environment.GetEnvironmentVariable(OutputVariable);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        var lines = new List<string> { "force,state,block,relative_step,relative_error,scaled_error,reference_error" };
        lines.AddRange(rows.Select(r => string.Join(",", r.Force, r.State, r.Block,
            r.RelativeStep.ToString("R", CultureInfo.InvariantCulture),
            r.Error.ToString("R", CultureInfo.InvariantCulture),
            r.ScaledError.ToString("R", CultureInfo.InvariantCulture),
            r.ReferenceError.ToString("R", CultureInfo.InvariantCulture))));
        File.WriteAllLines(Path.Combine(directory, "fd_step_study.csv"), lines);
    }

    private sealed record StudyCase(
        ForceBase Force,
        string Suffix,
        string StateName,
        StateVector State,
        ForceEvaluationContext Context,
        bool AnalyticReference);

    private sealed record StudyRow(
        string Force,
        string State,
        string Block,
        double RelativeStep,
        double Error,
        double ScaledError,
        double ReferenceError);
}
