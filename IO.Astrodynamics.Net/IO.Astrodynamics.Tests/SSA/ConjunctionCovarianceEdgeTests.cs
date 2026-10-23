// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.CCSDS.CDM;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.Tests.Propagators.Variational;
using Xunit;
using Matrix = IO.Astrodynamics.Math.Matrix;

namespace IO.Astrodynamics.Tests.SSA;

/// <summary>
/// The edges of the propagated covariance in the conjunction analysis (step 10): the checks of the initial cross
/// covariance, an initial covariance remediated by the analysis or beforehand (#405), the last propagation kept by a spacecraft in RK7(8) only, another inertial frame of the propagation, a last encounter past the propagation, the index of a
/// propagation with maneuvers, and the names of the force model of the CDM.
/// </summary>
public class ConjunctionCovarianceEdgeTests : IClassFixture<ConjunctionCovarianceCase>, IClassFixture<ReferenceCases>
{
    private static readonly CovariancePropagationOptions Covariance = new() { Mode = CovariancePropagationMode.Covariance };

    private readonly ConjunctionCovarianceCase _case;
    private readonly ReferenceCases _cases;

    public ConjunctionCovarianceEdgeTests(ConjunctionCovarianceCase @case, ReferenceCases cases)
    {
        _case = @case;
        _cases = cases;
    }

    [Fact]
    public void InitialCrossCovariance_IsChecked()
    {
        // Arrange: a covariance model with Cd as a consider parameter, and a propagator without the covariance
        var model = new CovarianceModel(Enumerable.Range(0, 36).Select(i => i % 7 == 0 ? 1.0 : 0.0).ToArray(),
            new VariationalOptions(ForceParameters.DragCoefficient, PropagateCovariance: true,
                ConsiderParameters: new ConsiderParameters(0.1)));
        using var propagator = new CentralBodyPropagator(ConjunctionCovarianceCase.PropagationWindow,
            ConjunctionCovarianceCase.NewSpacecraft(-6901, _case.ProtectedStart), new RK78Integrator(1e-10, 1e-10),
            new CelestialItem[] { _case.Earth }, false, false, TimeSpan.FromSeconds(60.0));

        // Act, assert: 6 × 1 values, finite; a propagator without the covariance has no consider covariance to set
        Assert.Throws<ArgumentException>(() => model.SetInitialCrossCovariance(new double[5]));
        Assert.Throws<ArgumentException>(() => model.SetInitialCrossCovariance(new[] { 0.0, 0.0, double.NaN, 0.0, 0.0, 0.0 }));
        model.SetInitialCrossCovariance(new double[6]);
        Assert.Throws<InvalidOperationException>(() => propagator.SetInitialCrossCovariance(new double[6]));
    }

    [Fact]
    public void Analyze_InitialCovarianceThePropagationRejects_IsRemediatedThenPropagated()
    {
        // Arrange: the protected object with a T–Ṙ correlation of −1.0001, as from rounded values: not positive
        // semi-definite, rejected by a propagation of the covariance
        var rejected = WithTrCorrelation(_case.ProtectedStart, -1.0001, 1.0);
        Assert.NotNull(PropagatorBase.InitialCovarianceRejection(rejected.Covariance.Value, out _));
        var protectedCraft = ConjunctionCovarianceCase.NewSpacecraft(-6961, rejected);
        var secondaryCraft = ConjunctionCovarianceCase.NewSpacecraft(-6962, _case.SecondaryStart);
        _case.Propagate(protectedCraft);
        _case.Propagate(secondaryCraft);

        // The reference: P0 remediated independently (eigenvalue clipping of the correlation matrix, Hall et al. 2017),
        // propagated with the covariance from the start
        var remediated = ClipCorrelation(rejected.Covariance.Value);
        var reference = ConjunctionCovarianceCase.NewSpacecraft(-6963, new StateVector(rejected.Position, rejected.Velocity,
            rejected.Observer, rejected.Epoch, rejected.Frame, remediated));
        var referenceSolution = _case.Propagate(reference, Covariance);

        // Act
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondaryCraft,
            ConjunctionCovarianceCase.AnalysisWindow);

        // Assert: no exception; the covariance of the protected object propagated from the remediated P0, and
        // CovarianceRemediated raised (decision of S. Guillet, 2026-10-18, H3 review of step 10, N2)
        Assert.Equal(CovarianceSource.Propagated, encounter.EncounterState.ProtectedCovariance.Source);
        Assert.True(encounter.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.CovarianceRemediated));
        Assert.True(encounter.ProtectedCovariance.InitialCovarianceRemediated);
        Assert.False(encounter.SecondaryCovariance.InitialCovarianceRemediated);
        var expected = referenceSolution.GetCovariance(encounter.EncounterState.Epoch);
        var actual = encounter.ProtectedCovariance.CovarianceInertial.Value;
        double difference = 0.0, norm = 0.0;
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                difference += (actual.Get(i, j) - expected.Get(i, j)) * (actual.Get(i, j) - expected.Get(i, j));
                norm += expected.Get(i, j) * expected.Get(i, j);
            }
        }

        Assert.True(System.Math.Sqrt(difference / norm) < 1e-10, $"{System.Math.Sqrt(difference / norm):E2}");
    }

    [Fact]
    public void Analyze_StateRemediatedByWithRemediatedCovariance_GivesTheCovarianceTheAnalysisRemediates()
    {
        // Arrange: the same indefinite P0, remediated inside the analysis for one pair, by
        // StateVector.WithRemediatedCovariance beforehand for the other (#405)
        var rejected = WithTrCorrelation(_case.ProtectedStart, -1.0001, 1.0);
        var remediation = rejected.WithRemediatedCovariance();
        Assert.True(remediation.Remediated);
        var inside = ConjunctionCovarianceCase.NewSpacecraft(-6971, rejected);
        var beforehand = ConjunctionCovarianceCase.NewSpacecraft(-6972, remediation.State);
        var secondary = ConjunctionCovarianceCase.NewSpacecraft(-6973, _case.SecondaryStart);
        _case.Propagate(inside);
        _case.Propagate(beforehand);
        _case.Propagate(secondary);

        // Act
        var remediatedInside = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(inside), secondary,
            ConjunctionCovarianceCase.AnalysisWindow);
        var remediatedBeforehand = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(beforehand), secondary,
            ConjunctionCovarianceCase.AnalysisWindow);

        // Assert: one implementation, the same covariance at TCA bit for bit; the flag raised by the analysis only, the
        // covariance it was given being valid in the second case
        Assert.Equal(remediatedInside.EncounterState.Epoch, remediatedBeforehand.EncounterState.Epoch);
        Assert.Equal(remediatedInside.ProtectedCovariance.CovarianceInertial.Value.ToArray(),
            remediatedBeforehand.ProtectedCovariance.CovarianceInertial.Value.ToArray());
        Assert.True(remediatedInside.ProtectedCovariance.InitialCovarianceRemediated);
        Assert.False(remediatedBeforehand.ProtectedCovariance.InitialCovarianceRemediated);
        Assert.False(remediatedBeforehand.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.CovarianceRemediated));
    }

    [Fact]
    public void Analyze_InitialCovarianceThatRoundingDoesNotExplain_IsHeldInRtn()
    {
        // Arrange: a negative variance, which no remediation of rounding repairs
        var state = _case.ProtectedStart;
        var p0 = state.Covariance.Value;
        var negative = new Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                negative.Set(i, j, i == 4 && j == 4 ? -p0.Get(4, 4) : i == 4 || j == 4 ? 0.0 : p0.Get(i, j));
            }
        }

        var protectedCraft = ConjunctionCovarianceCase.NewSpacecraft(-6966, new StateVector(state.Position, state.Velocity,
            state.Observer, state.Epoch, state.Frame, negative));
        var secondaryCraft = ConjunctionCovarianceCase.NewSpacecraft(-6967, _case.SecondaryStart);
        _case.Propagate(protectedCraft);
        _case.Propagate(secondaryCraft);

        // Act
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondaryCraft,
            ConjunctionCovarianceCase.AnalysisWindow);

        // Assert: no exception, the covariance held in RTN as without the propagation
        Assert.Equal(CovarianceSource.InitialStateHeldInRtn, encounter.EncounterState.ProtectedCovariance.Source);
        Assert.False(encounter.ProtectedCovariance.InitialCovarianceRemediated);
    }

    [Fact]
    public void RemediateByEigenvalueClipping_RepairsRoundingOnly()
    {
        // A positive semi-definite matrix comes back as it is, to rounding
        var p0 = _case.ProtectedStart.Covariance.Value;
        var values = Values(p0);
        var same = CovarianceChecks.RemediateByEigenvalueClipping(values, 6);
        for (int i = 0; i < 36; i++)
        {
            Assert.True(System.Math.Abs(same[i] - values[i]) <= 1e-12 * System.Math.Sqrt(values[7 * (i / 6)] * values[7 * (i % 6)]));
        }

        // A correlation beyond −1: remediated, positive semi-definite, close to the input, and that of the test reference
        var rejected = Values(WithTrCorrelation(_case.ProtectedStart, -1.0001, 1.0).Covariance.Value);
        var remediated = CovarianceChecks.RemediateByEigenvalueClipping(rejected, 6);
        Assert.Null(CovarianceChecks.Validate(remediated, 6, "remediated", out _));
        var reference = Values(ClipCorrelation(WithTrCorrelation(_case.ProtectedStart, -1.0001, 1.0).Covariance.Value));
        for (int i = 0; i < 36; i++)
        {
            double scale = System.Math.Sqrt(rejected[7 * (i / 6)] * rejected[7 * (i % 6)]);
            Assert.True(System.Math.Abs(remediated[i] - rejected[i]) < 1e-4 * scale);
            Assert.True(System.Math.Abs(remediated[i] - reference[i]) < 1e-12 * scale);
        }

        // A zero variance: its covariances set to zero
        var zero = (double[])values.Clone();
        zero[0] = 0.0;
        var cleared = CovarianceChecks.RemediateByEigenvalueClipping(zero, 6);
        Assert.All(Enumerable.Range(0, 6), j => Assert.Equal(0.0, cleared[j]));
        Assert.Null(CovarianceChecks.Validate(cleared, 6, "remediated", out _));

        // Not rounding: wrong size, not finite, a negative variance, asymmetric
        Assert.Null(CovarianceChecks.RemediateByEigenvalueClipping(new double[35], 6));
        var nan = (double[])values.Clone();
        nan[1] = double.NaN;
        Assert.Null(CovarianceChecks.RemediateByEigenvalueClipping(nan, 6));
        var negative = (double[])values.Clone();
        negative[14] = -negative[14];
        Assert.Null(CovarianceChecks.RemediateByEigenvalueClipping(negative, 6));
        var asymmetric = (double[])values.Clone();
        asymmetric[1] += 1e-6 * System.Math.Sqrt(values[0] * values[7]);
        Assert.Null(CovarianceChecks.RemediateByEigenvalueClipping(asymmetric, 6));
    }

    // The RTN covariance of the case (σ 50, 200, 30 m and 0.2, 0.05, 0.03 m/s) with a T–Ṙ correlation ρ, scaled by s²
    private static StateVector WithTrCorrelation(StateVector state, double rho, double s)
    {
        var sigma = new[] { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 };
        var rtn = new Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            rtn.Set(i, i, sigma[i] * sigma[i] * s * s);
        }

        rtn.Set(1, 3, rho * sigma[1] * sigma[3] * s * s);
        rtn.Set(3, 1, rho * sigma[1] * sigma[3] * s * s);
        return new StateVector(state.Position, state.Velocity, state.Observer, state.Epoch, state.Frame,
            state.RotateCovarianceFromRtn(rtn));
    }

    // Eigenvalue clipping of the correlation matrix, independently of the library (the sum of its rank-one terms λ⁺ v vᵀ)
    internal static Matrix ClipCorrelation(Matrix p)
    {
        var sigma = Enumerable.Range(0, 6).Select(i => System.Math.Sqrt(p.Get(i, i))).ToArray();
        var c = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.Dense(6, 6,
            (i, j) => 0.5 * (p.Get(i, j) + p.Get(j, i)) / (sigma[i] * sigma[j]));
        var evd = c.Evd(MathNet.Numerics.LinearAlgebra.Symmetricity.Symmetric);
        var clipped = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.Dense(6, 6);
        for (int k = 0; k < 6; k++)
        {
            double lambda = System.Math.Max(evd.EigenValues[k].Real, 0.0);
            var v = evd.EigenVectors.Column(k);
            clipped += lambda * v.OuterProduct(v);
        }

        var result = new Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                result.Set(i, j, 0.5 * (clipped[i, j] + clipped[j, i]) * sigma[i] * sigma[j]);
            }
        }

        return result;
    }

    private static double[] Values(Matrix m)
    {
        var values = new double[36];
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                values[6 * i + j] = m.Get(i, j);
            }
        }

        return values;
    }

    [Fact]
    public void Spacecraft_KeepsItsLastPropagation_InRk78Only()
    {
        // Arrange: one spacecraft propagated in RK7(8), then by Velocity-Verlet (H3 review of step 10, finding B2)
        var spacecraft = ConjunctionCovarianceCase.NewSpacecraft(-6921, _case.ProtectedStart);

        // Act, assert: the RK7(8) solution is kept, for the replay of the conjunction analysis
        var rk78 = _case.Propagate(spacecraft);
        Assert.Same(rk78, spacecraft.LastPropagation);

        // A Velocity-Verlet solution leaves nothing to replay: none is kept, and the RK7(8) one is released
        var verlet = PropagateWithVelocityVerlet(spacecraft);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Null(spacecraft.LastPropagation);
        Assert.False(verlet.IsAlive, "the spacecraft keeps the Velocity-Verlet solution");
        Assert.True(spacecraft.IsPropagated);
    }

    // The Velocity-Verlet propagation of the spacecraft, its solution discarded; a weak reference to it
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private WeakReference PropagateWithVelocityVerlet(IO.Astrodynamics.Body.Spacecraft.Spacecraft spacecraft)
    {
        var solution = spacecraft.Propagate(ConjunctionCovarianceCase.PropagationWindow, new CelestialItem[] { _case.Earth },
            false, false, TimeSpan.FromSeconds(10.0));
        return new WeakReference(solution);
    }

    [Fact]
    public void Resolver_PropagationInAnotherInertialFrame_RotatesTheCovarianceToTheFrameOfTheEncounter()
    {
        // Arrange: the protected participant in ECLIPJ2000, and its encounter state in ICRF
        var ecliptic = _case.ProtectedStart.ToFrame(Frames.Frame.ECLIPTIC_J2000).ToStateVector();
        var spacecraft = ConjunctionCovarianceCase.NewSpacecraft(-6911, ecliptic);
        var solution = _case.Propagate(spacecraft, Covariance);
        var epoch = ConjunctionCovarianceCase.Tca;
        var encounterState = solution.GetStateVector(epoch).ToFrame(Frames.Frame.ICRF).ToStateVector();
        var resolver = new EncounterCovarianceResolver(spacecraft, solution, Covariance, epoch);

        // Act
        var covariance = resolver.Resolve(encounterState);

        // Assert: the covariance of the solution, rotated to ICRF, as ToFrame rotates it
        Assert.Equal(CovarianceSource.Propagated, covariance.Source);
        Assert.Equal(Frames.Frame.ECLIPTIC_J2000, solution.Frame);
        Assert.Equal(encounterState.Covariance.Value.ToArray(), covariance.CovarianceInertial.Value.ToArray());
    }

    [Fact]
    public void Resolver_LastEncounterPastThePropagation_ReplaysToItsEnd()
    {
        // Arrange
        var spacecraft = ConjunctionCovarianceCase.NewSpacecraft(-6921, _case.ProtectedStart);
        var solution = _case.Propagate(spacecraft);
        var resolver = new EncounterCovarianceResolver(spacecraft, solution, Covariance,
            ConjunctionCovarianceCase.PropagationWindow.EndDate.AddHours(5.0), computeNonlinearityIndex: true);
        var state = solution.GetStateVector(ConjunctionCovarianceCase.Tca);

        // Act
        var covariance = resolver.Resolve(state);

        // Assert: the replay covers the whole propagation; the index is computed up to its end
        Assert.Equal(CovarianceSource.Propagated, covariance.Source);
        Assert.Equal(solution.Segments[0].Steps.Count, resolver.Replayed.Segments[0].Steps.Count);
        Assert.True(covariance.NonlinearityIndex > 0.0);
    }

    [Fact]
    public void NonlinearityIndex_OfAPropagationWithManeuvers_IsNotComputed()
    {
        // Arrange: the eclipsing case and its apogee raise, two segments
        var spacecraft = _cases.EclipsingSpacecraft(-6931, CovariancePropagationTests.WithCovariance(_cases.Eclipsing.InitialState));
        var solution = spacecraft.Propagate(_cases.Eclipsing.Window, _cases.Eclipsing.Bodies, new RK78Integrator(1e-10, 1e-10),
            true, true, TimeSpan.FromSeconds(600.0), Covariance);

        // Act, assert
        Assert.Equal(2, solution.Segments.Count);
        Assert.Null(NonlinearityIndex.Create(solution, spacecraft, _cases.Eclipsing.Window.EndDate));
    }

    [Fact]
    public void CdmForceModel_NamesTheModelsOfTheForces()
    {
        // Arrange: a heliocentric gravity among the forces of a geocentric propagation, and three atmospheres
        var epoch = ConjunctionCovarianceCase.Start;
        var sun = new CelestialBody(Stars.Sun);
        var spacecraft = ConjunctionCovarianceCase.NewSpacecraft(-6941, _case.ProtectedStart);
        string NameOf(IAtmosphericModel model)
        {
            var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, epoch, null, model);
            var forces = new ForceBase[]
            {
                new GravitationalAcceleration(earth), new GravitationalAcceleration(sun), new AtmosphericDrag(spacecraft, earth)
            };
            var dynamics = new PropagationDynamics(forces, earth, Frames.Frame.ICRF, epoch, null, null, null, null);
            var model2 = CdmForceModel.From(dynamics);
            Assert.Equal("POINT MASS", model2.GravityModel);
            Assert.Equal("SUN", model2.NBodyPerturbations);
            Assert.False(model2.SolarRadiationPressure);
            return model2.AtmosphericModel;
        }

        // Act, assert
        Assert.Equal("US STANDARD ATMOSPHERE 1976", NameOf(new EarthStandardAtmosphere()));
        Assert.Equal("MARS STANDARD ATMOSPHERE", NameOf(new MarsStandardAtmosphere()));
        Assert.Equal("TESTATMOSPHERE", NameOf(new TestAtmosphere()));
    }

    private sealed class TestAtmosphere : IAtmosphericModel
    {
        public double GetTemperature(IAtmosphericContext context) => 0.0;
        public double GetPressure(IAtmosphericContext context) => 0.0;
        public double GetDensity(IAtmosphericContext context) => 0.0;
        public IO.Astrodynamics.Atmosphere.Atmosphere GetAtmosphere(IAtmosphericContext context) => null;
    }
}
