// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using Xunit;
using Xunit.Abstractions;
using Quaternion = IO.Astrodynamics.Math.Quaternion;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// The density gradient of the drag partials across the altitudes at which NRLMSISE-00 drops the mixing of a species,
/// where its density jumps (#413): on the side of the point where the stencil in altitude straddles one of them, the
/// fourth-order difference in altitude of the gradient in geodetic coordinates everywhere else (#420).
/// </summary>
/// <remarks>
/// The points are placed on the radial line of the LEO states of B7, at the altitude where the density chain of the
/// drag (rotation to the body-fixed frame and planetodetic coordinates) crosses a cutoff, found by bisection on the
/// interval of the model, so that they do not depend on the geodetic conversion of the library.
/// </remarks>
public class DragPartialsAtTheCutoffsTests : IClassFixture<PartialsTestCases>
{
    private readonly PartialsTestCases _cases;
    private readonly ITestOutputHelper _output;

    public DragPartialsAtTheCutoffsTests(PartialsTestCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    private static readonly string[] Geometries = { PartialsTestCases.Leo400, PartialsTestCases.Leo800 };

    // The cutoff of index k of the model on the radial line of a state: the scale of the unit radial vector, in m, at
    // which the density chain leaves interval k for interval k + 1
    private static double CutoffRadius(AtmosphericDrag drag, IDensityDiscontinuities model, in Vector3 direction,
        in Quaternion? rotation, TimeSystem.Time epoch, int k)
    {
        double cutoff = model.DensityDiscontinuities[k];
        double low = 6378137.0 + cutoff - 30000.0;
        double high = 6378137.0 + cutoff + 30000.0;
        for (int i = 0; i < 200 && high - low > 1e-9; i++)
        {
            double middle = 0.5 * (low + high);
            int interval = model.DensityInterval(drag.Geodetic(direction * middle, rotation, epoch).Altitude);
            if (interval <= k)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        Assert.Equal(k, model.DensityInterval(drag.Geodetic(direction * low, rotation, epoch).Altitude));
        Assert.Equal(k + 1, model.DensityInterval(drag.Geodetic(direction * high, rotation, epoch).Altitude));
        return low;
    }

    private (AtmosphericDrag Drag, IDensityDiscontinuities Model, Vector3 Direction, Quaternion? Rotation,
        TimeSystem.Time Epoch) Geometry(string name)
    {
        var state = PartialsTestCases.State(name, _cases.Earth);
        var drag = new AtmosphericDrag(PartialsTestCases.Spacecraft(state), _cases.Earth);
        return (drag, (IDensityDiscontinuities)_cases.Earth.Atmosphere, state.Position.Normalize(),
            drag.BodyFixedRotation(state), state.Epoch);
    }

    // The central difference of the density chain at the step of B5, as the gradient took it before #413
    private static double[] CentralGradient(AtmosphericDrag drag, in Vector3 position, in Quaternion? rotation,
        TimeSystem.Time epoch)
    {
        double step = AtmosphericDrag.DensityGradientRelativeStep * position.Magnitude();
        var gradient = new double[3];
        for (int j = 0; j < 3; j++)
        {
            double component = RiddersDerivative.Component(position, j);
            gradient[j] = (drag.Density(With(position, j, component + step), rotation, epoch) -
                           drag.Density(With(position, j, component - step), rotation, epoch)) /
                          ((component + step) - (component - step));
        }

        return gradient;
    }

    [Fact]
    public void Gradient_OnBothSidesOfEachCutoff_MatchesRiddersOnTheSideOfThePoint()
    {
        double worst = 0.0, worstReference = 0.0, worstCentral = 0.0;
        foreach (string name in Geometries)
        {
            // Arrange
            var (drag, model, direction, rotation, epoch) = Geometry(name);
            for (int k = 0; k < model.DensityDiscontinuities.Length; k++)
            {
                double radius = CutoffRadius(drag, model, direction, rotation, epoch, k);
                foreach (double offset in new[] { -1.5, -0.5, 0.5, 1.5 })
                {
                    var position = direction * (radius + offset);
                    var gradient = new double[3];

                    // Act
                    drag.DensityGradient(position, rotation, epoch, gradient);

                    // Assert: Ridders on each component, its steps below the distance to the cutoff, so on the side of
                    // the point; that reference resolves ∇ρ to 2.5e-8 at 0.5 m (its steps from 0.2 m), well below the
                    // threshold it checks
                    var expected = new double[3];
                    var errors = new double[3];
                    for (int j = 0; j < 3; j++)
                    {
                        int column = j;
                        (expected[j], errors[j]) = RiddersDerivative.Derivative(
                            t => drag.Density(With(position, column, t), rotation, epoch),
                            RiddersDerivative.Component(position, j), 0.4 * System.Math.Abs(offset));
                    }

                    double error = RiddersDerivative.RelativeFrobeniusError(gradient, expected);
                    double referenceError = System.Math.Sqrt(errors.Sum(e => e * e) / expected.Sum(e => e * e));
                    double central = RiddersDerivative.RelativeFrobeniusError(
                        CentralGradient(drag, position, rotation, epoch), expected);
                    worst = System.Math.Max(worst, error);
                    worstReference = System.Math.Max(worstReference, referenceError);
                    worstCentral = System.Math.Max(worstCentral, central);
                    _output.WriteLine($"{name}, {model.DensityDiscontinuities[k] / 1000.0} km {offset:+0.0;-0.0} m: " +
                                      $"∇ρ {error:E2}, reference {referenceError:E2}, central difference {central:E2}");
                    Assert.True(referenceError < 1e-7, $"reference error {referenceError:E2}");
                    Assert.True(error < 1e-6, $"{name}, {model.DensityDiscontinuities[k]} m {offset} m: {error:E2}");
                }
            }
        }

        _output.WriteLine($"worst: ∇ρ {worst:E2}, reference {worstReference:E2}, central difference {worstCentral:E2}");
    }

    [Fact]
    public void Gradient_AcrossEachCutoff_ChangesByTheSlopeOfTheModelOnly()
    {
        // Arrange
        var (drag, model, direction, rotation, epoch) = Geometry(PartialsTestCases.Leo400);
        double worst = 0.0;
        for (int k = 0; k < model.DensityDiscontinuities.Length; k++)
        {
            double radius = CutoffRadius(drag, model, direction, rotation, epoch, k);
            var below = direction * (radius - 0.5);
            var above = direction * (radius + 0.5);
            var gradientBelow = new double[3];
            var gradientAbove = new double[3];

            // Act
            drag.DensityGradient(below, rotation, epoch, gradientBelow);
            drag.DensityGradient(above, rotation, epoch, gradientAbove);

            // Assert: over 1 m across the cutoff, ∇ρ changes as the slope of the model does, that of Ridders' derivative
            // on each side; the central difference straddles the cutoff at both points
            double change = RiddersDerivative.RelativeFrobeniusError(gradientAbove, gradientBelow);
            double modelChange = RiddersDerivative.RelativeFrobeniusError(Ridders(drag, above, rotation, epoch, 0.2),
                Ridders(drag, below, rotation, epoch, 0.2));
            worst = System.Math.Max(worst, System.Math.Abs(change - modelChange));
            _output.WriteLine($"{model.DensityDiscontinuities[k] / 1000.0} km: ∇ρ changes by {change:E2} over 1 m, " +
                              $"the slope of the model by {modelChange:E2}");
        }

        _output.WriteLine($"worst departure from the change of slope of the model {worst:E2}");
        Assert.True(worst < 1e-6, $"worst departure {worst:E2}");
    }

    private static double[] Ridders(AtmosphericDrag drag, Vector3 position, Quaternion? rotation, TimeSystem.Time epoch,
        double initialStep)
    {
        var derivative = new double[3];
        for (int j = 0; j < 3; j++)
        {
            int column = j;
            derivative[j] = RiddersDerivative.Derivative(t => drag.Density(With(position, column, t), rotation, epoch),
                RiddersDerivative.Component(position, j), initialStep).Derivative;
        }

        return derivative;
    }

    // ∂ρ/∂h by the fourth-order central difference of the density of the body at the geodetic coordinates of a position,
    // with the altitude step of the gradient: the rule away from the cutoffs (#420)
    private static double FourthOrderAltitudeDerivative(AtmosphericDrag drag, CelestialBody body, in Vector3 position,
        in Quaternion? rotation, TimeSystem.Time epoch)
    {
        var origin = drag.Geodetic(position, rotation, epoch);
        double step = AtmosphericDrag.AltitudeRelativeStep * position.Magnitude();
        double Rho(double altitude) => body.GetAirDensity(altitude, origin.Latitude, origin.Longitude, epoch);
        double plus = origin.Altitude + step;
        double minus = origin.Altitude - step;
        return (-Rho(origin.Altitude + 2.0 * step) + 8.0 * Rho(plus) - 8.0 * Rho(minus) +
                Rho(origin.Altitude - 2.0 * step)) / (6.0 * (plus - minus));
    }

    // ∂ρ/∂h by the central difference of the density of the body over the step of the cutoffs
    private static double CentralAltitudeDerivative(AtmosphericDrag drag, CelestialBody body, in Vector3 position,
        in Quaternion? rotation, TimeSystem.Time epoch)
    {
        var origin = drag.Geodetic(position, rotation, epoch);
        double step = AtmosphericDrag.DensityGradientRelativeStep * position.Magnitude();
        double plus = origin.Altitude + step;
        double minus = origin.Altitude - step;
        return (body.GetAirDensity(plus, origin.Latitude, origin.Longitude, epoch) -
                body.GetAirDensity(minus, origin.Latitude, origin.Longitude, epoch)) / (plus - minus);
    }

    [Fact]
    public void Gradient_AwayFromTheCutoffs_IsTheFourthOrderDifferenceInAltitude()
    {
        foreach (string name in PartialsTestCases.StateNames.Where(n => n != PartialsTestCases.Geo))
        {
            // Arrange
            var state = PartialsTestCases.State(name, _cases.Earth);
            var drag = new AtmosphericDrag(PartialsTestCases.Spacecraft(state), _cases.Earth);
            var rotation = drag.BodyFixedRotation(state);

            // Act
            double derivative = drag.DensityAltitudeDerivative(state.Position, rotation, state.Epoch);

            // Assert: bit for bit the fourth-order difference of the model in altitude
            Assert.Equal(FourthOrderAltitudeDerivative(drag, _cases.Earth, state.Position, rotation, state.Epoch),
                derivative);
        }
    }

    [Fact]
    public void WithoutTheMixingSwitch_TheAltitudeDerivativeIsTheFourthOrderDifference()
    {
        // Arrange: flag 15 off, the density continuous at the altitudes of the cutoffs
        var flags = NrlmsiseFlags.CreateStandard();
        flags.Switches[15] = 0;
        var earth = new CelestialBody(SolarSystemObjects.PlanetsAndMoons.EARTH, Frames.Frame.ICRF,
            PartialsTestCases.Epoch, null, new Nrlmsise00Model(flags, SpaceWeather.Nominal));
        var (drag, model, direction, rotation, epoch) = Geometry(PartialsTestCases.Leo400);
        double radius = CutoffRadius(drag, model, direction, rotation, epoch, model.DensityDiscontinuities.Length - 1);
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, earth);
        var unmixed = new AtmosphericDrag(PartialsTestCases.Spacecraft(state), earth);
        var position = direction * (radius + 0.5);

        // Act
        double derivative = unmixed.DensityAltitudeDerivative(position, rotation, epoch);

        // Assert
        Assert.True(((IDensityDiscontinuities)earth.Atmosphere).DensityDiscontinuities.IsEmpty);
        Assert.Equal(FourthOrderAltitudeDerivative(unmixed, earth, position, rotation, epoch), derivative);
    }

    [Fact]
    public void Partials_WithTheStencilAcrossACutoff_AllocateNothing()
    {
        // Arrange: a state 0.5 m above 450 km, the one-sided difference taken
        var (drag, model, direction, rotation, epoch) = Geometry(PartialsTestCases.Leo400);
        double radius = CutoffRadius(drag, model, direction, rotation, epoch, model.DensityDiscontinuities.Length - 1);
        var reference = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var state = new StateVector(direction * (radius + 0.5), reference.Velocity, _cases.Earth, epoch,
            reference.Frame);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new AtmosphericDrag(spacecraft, _cases.Earth);
        var cache = new PropagationFrameOrientationCache(new TimeSystem.Window(epoch.AddHours(-1.0), epoch.AddHours(1.0)),
            _cases.Earth.Frame, TimeSpan.FromSeconds(60.0));
        var dadr = new double[9];
        var dadv = new double[9];

        using (Frames.Frame.UseOrientationCache(_cases.Earth.Frame, cache))
        {
            force.AccumulateStatePartials(state, context, dadr, dadv);

            // Act
            long before = GC.GetAllocatedBytesForCurrentThread();
            force.AccumulateStatePartials(state, context, dadr, dadv);
            long partials = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert
            Assert.Equal(0, partials);
        }
    }

    [Fact]
    public void StepControlOfTheVariationalEquations_ThroughTheCutoffs_Completes()
    {
        // Arrange: a near-circular LEO starting on the 450 km cutoff, its geodetic altitude crossing it again with the
        // latitude; drag alone on a point-mass Earth, Φ and Ψ_Cd in the step control at 1e-14 (#412 stopped there after
        // 100 rejections at the minimum step)
        var (drag, model, direction, rotation, epoch) = Geometry(PartialsTestCases.Leo400);
        double radius = CutoffRadius(drag, model, direction, rotation, epoch, model.DensityDiscontinuities.Length - 1);
        var reference = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var position = direction * radius;
        var velocity = reference.Velocity * System.Math.Sqrt(reference.Position.Magnitude() / radius);
        var start = new StateVector(position, velocity, _cases.Earth, epoch, reference.Frame);
        var spacecraft = IO.Astrodynamics.Tests.Propagators.Variational.ReferenceCase.NewSpacecraft(-1962, start);
        var window = new TimeSystem.Window(epoch, epoch.AddHours(1.0));

        // Act
        var solution = spacecraft.PropagateWithVariationalEquations(window, new CelestialItem[] { _cases.Earth },
            new RK78Integrator(1e-14, 1e-14), true, false, TimeSpan.FromSeconds(600.0),
            new VariationalOptions(ForceParameters.DragCoefficient, IncludeInStepControl: true));

        // Assert: the propagation reaches the end of its window, across the cutoffs
        var end = solution.GetStateTransitionMatrix(window.EndDate);
        int crossings = 0;
        int interval = model.DensityInterval(drag.Geodetic(position, rotation, epoch).Altitude);
        for (double t = 10.0; t <= 3600.0; t += 10.0)
        {
            var state = solution.GetStateVector(epoch.AddSeconds(t));
            int next = model.DensityInterval(drag.Geodetic(state.Position, drag.BodyFixedRotation(state), state.Epoch)
                .Altitude);
            crossings += next != interval ? 1 : 0;
            interval = next;
        }

        _output.WriteLine($"crossings of a cutoff along the hour: {crossings}; Φ_rr(0, 0) at the end {end.Get(0, 0):E6}");
        Assert.True(crossings > 0, "the propagation crosses no cutoff");
    }

    [Fact]
    public void TwoDiscontinuitiesWithinTheStencil_TheAltitudeDerivativeIsTheCentralDifference()
    {
        // Arrange: an exponential atmosphere whose density jumps at two altitudes 1 m apart, the point between them, so
        // that neither point of the one-sided stencil (h ≈ 2 m) is in the interval of the point
        var (direction, rotation, epoch) = RadialLine();
        var (earth, drag, model) = SteppedEarth(400000.0, 400001.0);
        double radius = CutoffRadius(drag, model, direction, rotation, epoch, 0) + 0.5;
        var position = direction * radius;

        // Act
        double derivative = drag.DensityAltitudeDerivative(position, rotation, epoch);

        // Assert: the central difference over the step of the cutoffs
        Assert.Equal(1, model.DensityInterval(drag.Geodetic(position, rotation, epoch).Altitude));
        Assert.Equal(CentralAltitudeDerivative(drag, earth, position, rotation, epoch), derivative);
    }

    [Fact]
    public void SecondPointAcrossAnotherDiscontinuity_TheAltitudeDerivativeIsTheCentralDifference()
    {
        // Arrange: jumps at two altitudes 3 m apart, the point 0.5 m above the first: the central stencil (h ≈ 2 m)
        // straddles the first, and the second point of the one-sided difference upwards, 2 h away, crosses the other
        var (direction, rotation, epoch) = RadialLine();
        var (earth, drag, model) = SteppedEarth(400000.0, 400003.0);
        double radius = CutoffRadius(drag, model, direction, rotation, epoch, 0) + 0.5;
        var position = direction * radius;
        double altitude = drag.Geodetic(position, rotation, epoch).Altitude;
        double step = AtmosphericDrag.DensityGradientRelativeStep * position.Magnitude();

        // Act
        double derivative = drag.DensityAltitudeDerivative(position, rotation, epoch);

        // Assert: the geometry of the case, then the central difference
        Assert.Equal(1, model.DensityInterval(altitude + step));
        Assert.Equal(2, model.DensityInterval(altitude + 2.0 * step));
        Assert.Equal(0, model.DensityInterval(altitude - step));
        Assert.Equal(CentralAltitudeDerivative(drag, earth, position, rotation, epoch), derivative);
    }

    [Fact]
    public void AnotherDiscontinuityWithinTheThirdOrderStencil_TheAltitudeDerivativeIsTheSecondOrderOneSided()
    {
        // Arrange: jumps at two altitudes 50 m apart, the point 0.5 m above the first: the third-order one-sided stencil
        // upwards (3 δ_h ≈ 60 m) reaches the second, so the one-sided difference of #413 over 2 h ≈ 4 m is taken
        var (direction, rotation, epoch) = RadialLine();
        var (earth, drag, model) = SteppedEarth(400000.0, 400050.0);
        double radius = CutoffRadius(drag, model, direction, rotation, epoch, 0) + 0.5;
        var position = direction * radius;
        var origin = drag.Geodetic(position, rotation, epoch);
        double step = AtmosphericDrag.DensityGradientRelativeStep * position.Magnitude();
        double x1 = origin.Altitude + step;
        double x2 = origin.Altitude + 2.0 * step;
        double Rho(double altitude) => earth.GetAirDensity(altitude, origin.Latitude, origin.Longitude, epoch);
        double d1 = x1 - origin.Altitude;
        double d2 = x2 - origin.Altitude;
        double expected = -(d1 + d2) / (d1 * d2) * Rho(origin.Altitude) + d2 / (d1 * (d2 - d1)) * Rho(x1) -
                          d1 / (d2 * (d2 - d1)) * Rho(x2);

        // Act
        double derivative = drag.DensityAltitudeDerivative(position, rotation, epoch);

        // Assert
        Assert.Equal(2, model.DensityInterval(origin.Altitude + 3.0 * AtmosphericDrag.AltitudeRelativeStep *
            position.Magnitude()));
        Assert.Equal(expected, derivative);
    }

    [Fact]
    public void BetweenTwoDiscontinuitiesWithinTheFourthOrderStencil_TheAltitudeDerivativeIsTheCentralDifference()
    {
        // Arrange: jumps at two altitudes 50 m apart, the point midway: the fourth-order stencil (± 2 δ_h ≈ 40 m) and the
        // third-order one-sided ones (3 δ_h ≈ 60 m) reach a jump on each side, the central difference over 2 h ≈ 4 m none
        var (direction, rotation, epoch) = RadialLine();
        var (earth, drag, model) = SteppedEarth(400000.0, 400050.0);
        double radius = CutoffRadius(drag, model, direction, rotation, epoch, 0) + 25.0;
        var position = direction * radius;

        // Act
        double derivative = drag.DensityAltitudeDerivative(position, rotation, epoch);

        // Assert
        Assert.Equal(1, model.DensityInterval(drag.Geodetic(position, rotation, epoch).Altitude));
        Assert.Equal(CentralAltitudeDerivative(drag, earth, position, rotation, epoch), derivative);
    }

    [Fact]
    public void NearThePolarAxis_TheGradientIsTheCartesianDifference()
    {
        // Arrange: a position 100 m from the polar axis, within the horizontal step, where the east step is not defined
        var (_, _, _, rotation, epoch) = Geometry(PartialsTestCases.Leo400);
        var reference = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var drag = new AtmosphericDrag(PartialsTestCases.Spacecraft(reference), _cases.Earth);
        var axis = rotation.HasValue ? new Vector3(0.0, 0.0, 1.0).Rotate(rotation.Value.Conjugate()) : new Vector3(0.0, 0.0, 1.0);
        var across = rotation.HasValue ? new Vector3(1.0, 0.0, 0.0).Rotate(rotation.Value.Conjugate()) : new Vector3(1.0, 0.0, 0.0);
        var position = axis * (6356752.0 + 400000.0) + across * 100.0;
        var gradient = new double[3];
        var cartesian = new double[3];

        // Act
        drag.DensityGradient(position, rotation, epoch, gradient);
        drag.CartesianDensityGradient(position, rotation, epoch, cartesian);

        // Assert
        Assert.Equal(cartesian, gradient);
    }

    [Theory]
    [InlineData(1000.0, 0.0)]
    [InlineData(10000.0, 0.0)]
    [InlineData(100000.0, 0.0)]
    [InlineData(0.0, 89.0)]
    public void NearThePolarAxis_TheGradientMatchesRidders(double axisDistance, double latitude)
    {
        // Arrange: 400 km above the north pole, at a distance from the polar axis or at a geodetic latitude, longitude
        // 40°, where the horizontal step spans up to 0.2 rad of the parallel and the longitude step is capped
        var (_, _, _, rotation, epoch) = Geometry(PartialsTestCases.Leo400);
        var reference = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var drag = new AtmosphericDrag(PartialsTestCases.Spacecraft(reference), _cases.Earth);
        double longitude = 40.0 * IO.Astrodynamics.Constants.Deg2Rad;
        double parallel, polar;
        if (axisDistance > 0.0)
        {
            parallel = axisDistance;
            polar = 6356752.0 + 400000.0;
        }
        else
        {
            double phi = latitude * IO.Astrodynamics.Constants.Deg2Rad;
            double e2 = _cases.Earth.Flattening * (2.0 - _cases.Earth.Flattening);
            double n = _cases.Earth.EquatorialRadius / System.Math.Sqrt(1.0 - e2 * System.Math.Sin(phi) * System.Math.Sin(phi));
            parallel = (n + 400000.0) * System.Math.Cos(phi);
            polar = (n * (1.0 - e2) + 400000.0) * System.Math.Sin(phi);
        }

        var bodyFixed = new Vector3(parallel * System.Math.Cos(longitude), parallel * System.Math.Sin(longitude), polar);
        var position = rotation.HasValue ? bodyFixed.Rotate(rotation.Value.Conjugate()) : bodyFixed;
        var gradient = new double[3];
        var cartesian = new double[3];
        var expected = new double[3];
        var errors = new double[3];
        for (int j = 0; j < 3; j++)
        {
            int column = j;
            (expected[j], errors[j]) = RiddersDerivative.Derivative(
                t => drag.Density(With(position, column, t), rotation, epoch),
                RiddersDerivative.Component(position, j), System.Math.Min(0.1 * parallel, 200.0));
        }

        // Act
        drag.DensityGradient(position, rotation, epoch, gradient);
        drag.CartesianDensityGradient(position, rotation, epoch, cartesian);

        // Assert: Ridders on each component, from steps of a tenth of the distance to the axis. Measured 2026-10-21:
        // 7.6e-11 at 1 km, 5.5e-12 at 10 km, 4.6e-11 at 100 km and 4.8e-11 at 89°, the resolution of that reference
        // (1.5e-11 to 4.0e-11); 1.1e-6, 1.2e-8, 2.5e-10 and 1.9e-10 with the arc of the parallel, 8.1e-9, 8.2e-10,
        // 1.1e-10 and 8.7e-11 with the chord without the cap of the longitude step
        double error = RiddersDerivative.RelativeFrobeniusError(gradient, expected);
        double referenceError = System.Math.Sqrt(errors.Sum(e => e * e) / expected.Sum(e => e * e));
        double former = RiddersDerivative.RelativeFrobeniusError(cartesian, expected);
        _output.WriteLine($"{parallel / 1000.0:F1} km from the polar axis: ∇ρ {error:E2}, reference " +
                          $"{referenceError:E2}, Cartesian differences {former:E2}");
        Assert.True(referenceError < 1e-10, $"reference {referenceError:E2}");
        Assert.True(error < 1.6e-10, $"{error:E2}");
    }

    [Fact]
    public void AltitudeDerivative_OfAModelOfTheAltitudeAlone_IsItsClosedForm()
    {
        // Arrange: the US 1976 atmosphere gives dρ/dh in closed form
        var earth = new CelestialBody(SolarSystemObjects.PlanetsAndMoons.EARTH, Frames.Frame.ICRF,
            PartialsTestCases.Epoch, null, new EarthStandardAtmosphere());
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, earth);
        var drag = new AtmosphericDrag(PartialsTestCases.Spacecraft(state), earth);
        var rotation = drag.BodyFixedRotation(state);
        double altitude = drag.Geodetic(state.Position, rotation, state.Epoch).Altitude;

        // Act
        double derivative = drag.DensityAltitudeDerivative(state.Position, rotation, state.Epoch);

        // Assert
        Assert.Equal(((IAltitudeDensityDerivative)earth.Atmosphere).GetDensityAltitudeDerivative(altitude), derivative);
    }

    [Fact]
    public void Gradient_IsTheAltitudeDerivativeAlongTheNormal_ForAModelOfTheAltitudeAlone()
    {
        // Arrange: the stepped atmosphere depends on the altitude alone, so its horizontal differences vanish and ∇ρ is
        // ∂ρ/∂h along the normal of the ellipsoid, the gradient of the geodetic altitude
        var (direction, rotation, epoch) = RadialLine();
        var (_, drag, _) = SteppedEarth(500000.0);
        var position = direction * (6378137.0 + 400000.0);
        var gradient = new double[3];
        var normal = new double[3];
        var normalErrors = new double[3];
        double altitude = drag.Geodetic(position, rotation, epoch).Altitude;
        for (int j = 0; j < 3; j++)
        {
            int column = j;
            (normal[j], normalErrors[j]) = RiddersDerivative.Derivative(
                t => drag.Geodetic(With(position, column, t), rotation, epoch).Altitude,
                RiddersDerivative.Component(position, j), 10.0);
        }

        // Act
        drag.DensityGradient(position, rotation, epoch, gradient);
        double derivative = drag.DensityAltitudeDerivative(position, rotation, epoch);

        // Assert: ∇h by Ridders on the geodetic conversion, against the normal of the gradient; agreement 1.2e-10, the
        // resolution of that reference (1.3e-10, measured 2026-10-21)
        var expected = normal.Select(n => n * derivative).ToArray();
        double error = RiddersDerivative.RelativeFrobeniusError(gradient, expected);
        double referenceError = System.Math.Sqrt(normalErrors.Sum(e => e * e));
        _output.WriteLine($"∇ρ against ∂ρ/∂h ∇h (Ridders): {error:E2}, reference {referenceError:E2}; altitude " +
                          $"{altitude:F3} m");
        Assert.True(referenceError < 1e-9, $"reference {referenceError:E2}");
        Assert.True(error < 1e-9, $"{error:E2}");
    }

    private (Vector3 Direction, Quaternion? Rotation, TimeSystem.Time Epoch) RadialLine()
    {
        var (_, _, direction, rotation, epoch) = Geometry(PartialsTestCases.Leo400);
        return (direction, rotation, epoch);
    }

    private static (CelestialBody Earth, AtmosphericDrag Drag, IDensityDiscontinuities Model) SteppedEarth(
        params double[] discontinuities)
    {
        var model = new SteppedAtmosphere(discontinuities);
        var earth = new CelestialBody(SolarSystemObjects.PlanetsAndMoons.EARTH, Frames.Frame.ICRF,
            PartialsTestCases.Epoch, null, model);
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, earth);
        return (earth, new AtmosphericDrag(PartialsTestCases.Spacecraft(state), earth), model);
    }

    // An exponential atmosphere (scale height 60 km) whose density jumps by 1e-4 at each of the given altitudes
    private sealed class SteppedAtmosphere : IAtmosphericModel, IGeodeticDensityModel, IDensityDiscontinuities
    {
        private readonly double[] _discontinuities;

        public SteppedAtmosphere(double[] discontinuities) => _discontinuities = discontinuities;

        public ReadOnlySpan<double> DensityDiscontinuities => _discontinuities;

        public int DensityInterval(double altitude) => _discontinuities.Count(d => altitude > d);

        double IGeodeticDensityModel.GetDensity(double altitude, double geodeticLatitude, double geodeticLongitude,
            in TimeSystem.Time epoch) => Density(altitude);

        private double Density(double altitude) =>
            1e-12 * System.Math.Exp(-(altitude - 400000.0) / 60000.0) * (1.0 + 1e-4 * DensityInterval(altitude));

        public IO.Astrodynamics.Atmosphere.Atmosphere GetAtmosphere(IAtmosphericContext context) => new()
            { Temperature = 0.0, Pressure = 0.0, Density = Density(context.Altitude) };

        public double GetTemperature(IAtmosphericContext context) => 0.0;

        public double GetPressure(IAtmosphericContext context) => 0.0;

        public double GetDensity(IAtmosphericContext context) => Density(context.Altitude);
    }

    private static Vector3 With(in Vector3 vector, int index, double value)
    {
        return index switch
        {
            0 => new Vector3(value, vector.Y, vector.Z),
            1 => new Vector3(vector.X, value, vector.Z),
            _ => new Vector3(vector.X, vector.Y, value)
        };
    }
}
