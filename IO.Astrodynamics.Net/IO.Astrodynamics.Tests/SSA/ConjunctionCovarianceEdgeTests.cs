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
/// covariance, another inertial frame of the propagation, a last encounter past the propagation, the index of a
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
    public void LastPropagation_IsKeptInRk78Only()
    {
        // Arrange: a spacecraft propagated in RK7(8), then in Velocity-Verlet
        var spacecraft = ConjunctionCovarianceCase.NewSpacecraft(-6951, _case.ProtectedStart);
        var rk78 = _case.Propagate(spacecraft);
        var kept = spacecraft.LastPropagation;

        // Act
        spacecraft.Propagate(ConjunctionCovarianceCase.PropagationWindow, new CelestialItem[] { _case.Earth }, false, false,
            TimeSpan.FromSeconds(10.0));

        // Assert: the RK7(8) solution is kept for the replay; the Velocity-Verlet one cannot be replayed, and is not
        // kept, nor is the previous one (H3 review, B2)
        Assert.Same(rk78, kept);
        Assert.Null(spacecraft.LastPropagation);
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
