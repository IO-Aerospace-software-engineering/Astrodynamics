using System;
using System.IO;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.SmokeTests;

/// <summary>
/// The example of the covariance propagation reference page, against the packed package: activation through the
/// builder, Φ and P read from the solution.
/// </summary>
public class CovariancePropagationSmokeTests
{
    private static readonly DirectoryInfo SolarSystemKernelPath = new("Data/SolarSystem");

    public CovariancePropagationSmokeTests()
    {
        SpiceAPI.Instance.LoadKernels(SolarSystemKernelPath);
    }

    [Fact]
    public void ReferencePageExample_RunsAndGivesASymmetricPositiveCovariance()
    {
        // The example of docs/reference/covariance-propagation.md
        var epoch = new Time(2021, 3, 20, 12, 0, 0);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch, null,
            new Nrlmsise00Model(SpaceWeather.Nominal));
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, epoch);
        var sun = new CelestialBody(Stars.Sun);

        var p0 = new Matrix(6, 6);
        for (int i = 0; i < 3; i++)
        {
            p0.Set(i, i, 100.0 * 100.0);         // σ = 100 m
            p0.Set(i + 3, i + 3, 0.1 * 0.1);     // σ = 0.1 m/s
        }

        var orbit = new KeplerianElements(6778137.0, 0.0005, 51.6 * Constants.Deg2Rad, 0.0, 0.0, 0.0, earth, epoch,
            Frame.ICRF).ToStateVector();
        var initialState = new StateVector(orbit.Position, orbit.Velocity, earth, epoch, Frame.ICRF, p0);
        var spacecraft = new Spacecraft(-1990, "SMOKE", 100.0, 1000.0, new Clock("smoke", 65536), initialState,
            10.0, 2.2);
        var window = new Window(epoch, epoch.AddHours(2.0));
        var tca = epoch.AddSeconds(5000.5);

        var propagator = new CentralBodyPropagatorBuilder(window, spacecraft, new RK78Integrator(1e-11, 1e-11),
                TimeSpan.FromSeconds(60))
            .WithPerturbingBodies(new CelestialItem[] { earth, moon, sun })
            .IncludeAtmosphericDrag()
            .WithCovariancePropagation(new CovariancePropagationOptions
            {
                Mode = CovariancePropagationMode.Covariance,
                ProcessNoise = new RtnProcessNoise(1e-12, 1e-11, 1e-12),
                ConsiderParameters = new ConsiderParameters(dragCoefficientSigma: 0.1)
            })
            .Build();

        var solution = propagator.Propagate();
        Matrix phi = solution.GetStateTransitionMatrix(tca);  // Φ(tca, t0), 6×6
        Matrix p = solution.GetCovariance(tca);               // 6×6, inertial frame of the propagation

        Assert.Equal(6, phi.Rows);
        Assert.Equal(6, p.Columns);
        Assert.Equal(new[] { ForceParameter.DragCoefficient }, solution.SensitivityParameters);
        Assert.Equal(7, solution.GetExtendedCovariance(tca).Rows);
        for (int i = 0; i < 6; i++)
        {
            Assert.True(p.Get(i, i) > 0.0);
            for (int j = 0; j < 6; j++)
            {
                Assert.Equal(p.Get(i, j), p.Get(j, i));
            }
        }

        Assert.NotNull(solution.StateVectors[^1].Covariance);
    }
}
