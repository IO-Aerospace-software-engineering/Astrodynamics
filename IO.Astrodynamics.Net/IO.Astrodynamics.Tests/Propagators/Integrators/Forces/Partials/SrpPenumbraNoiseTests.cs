// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Tests.Propagators.Variational;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// The rounding noise of the SRP partials in the penumbra, along the first shadow exit of R3 (Sun-synchronous 700 km,
/// SRP with Cr): that of the central differences of the acceleration, the cause of the thousands of steps that RK7(8)
/// took there with the variational equations in the step control before #423, and that of the derivative of the shadow
/// function that replaced them.
/// </summary>
/// <remarks>
/// <para>
/// From #379 to #423 the SRP partials in the penumbra were the central differences of the whole acceleration (the default
/// path, <see cref="ForceBase.AccumulateStatePartialsByCentralDifferences"/> with its default steps), whose shadow
/// fraction ν comes from the two-circle area of <see cref="CelestialItem.ShadowFraction(double, double, double)"/>: its
/// terms of order R_occ² acos(…) cancel down to the area of the solar disc that is covered, so ν carries a rounding of a
/// few 1e-10 inside the penumbra, up to a few 1e-8 at its contacts (#434), which the stencil (2 h ≈ 42 m) divides. The
/// embedded error estimate of Y is first order in the step for a noisy derivative, as for the density gradient of #420.
/// The derivative of the shadow function in its reduced form (<see cref="SolarRadiationPressure"/>) does not divide the
/// rounding of ν by a step.
/// </para>
/// <para>
/// The noise is the root mean square of the second difference of each element along 2000 points 0.1 mm apart along the
/// velocity, divided by √6 (the variance of the second difference of a white noise) and by the Frobenius norm of the
/// block, at points where ν is 1e-6 to 1 − 1e-6. The partials vary over the width of the penumbra, about 20 km, so over
/// 0.1 mm their second difference is noise alone. Near a contact the exact partials grow as the square root of the
/// distance to it, so the 0.2 m of the points must stay inside the penumbra: at ν = 1e-6 the contact is about 2 m away.
/// With 2000 points 1 mm apart the window reached the inner contact on macOS arm64, where the point nearest to
/// 1 − 1e-6 fell closer to it, and the second difference measured the square root (1.9e-2 against 1.3e-3 on Linux).
/// </para>
/// </remarks>
public class SrpPenumbraNoiseTests : IClassFixture<ReferenceCases>
{
    private readonly ReferenceCases _cases;
    private readonly ITestOutputHelper _output;

    public SrpPenumbraNoiseTests(ReferenceCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    [Fact]
    public void Penumbra_OfR3_TheDerivativeOfTheShadowFunctionIsFreeOfTheNoiseOfTheCentralDifferences()
    {
        // Arrange: the first hour of R3, its first shadow exit at about 415 s
        var r3 = _cases.R3;
        var start = r3.InitialState;
        var spacecraft = new Spacecraft(-1423, "PENUMBRA", 100.0, 1000.0, new Clock("penumbra", 65536), start, 10.0,
            ReferenceCases.DragCoefficient, null, ReferenceCases.ReflectivityCoefficient);
        var solution = new CentralBodyPropagator(new TimeSystem.Window(start.Epoch, start.Epoch.AddSeconds(3600.0)),
            spacecraft, new RK78Integrator(ReferenceCases.Tolerance, ReferenceCases.Tolerance), r3.Bodies, false, true,
            TimeSpan.FromSeconds(600.0)).Propagate();
        var srp = new SolarRadiationPressure(spacecraft, r3.Bodies.OfType<CelestialBody>());
        var unshadowed = new SolarRadiationPressure(spacecraft, Array.Empty<CelestialBody>());
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);

        StateVector At(double t)
        {
            var epoch = start.Epoch.AddSeconds(t);
            var (position, velocity) = solution.InterpolateAt(epoch);
            return new StateVector(position, velocity, start.Observer, epoch, start.Frame);
        }

        double Shadow(StateVector state) => 1.0 - srp.Apply(state).Magnitude() / unshadowed.Apply(state).Magnitude();

        var crossing = new List<(double T, double Nu)>();
        for (double t = 400.0; t < 440.0; t += 1e-3)
        {
            double nu = Shadow(At(t));
            if (nu > 0.0 && nu < 1.0)
            {
                crossing.Add((t, nu));
            }
        }

        void CentralDifferences(StateVector state, double[] dadr) => srp.AccumulateStatePartialsByCentralDifferences(state,
            context, dadr, new double[9], ForceBase.DefaultPositionRelativeStep, ForceBase.DefaultVelocityRelativeStep);
        void Analytic(StateVector state, double[] dadr) => srp.AccumulateStatePartials(state, context, dadr,
            new double[9]);

        // Act
        double worstCentral = 0.0;
        double worstAnalytic = 0.0;
        double worstShadow = 0.0;
        foreach (double target in new[] { 1e-6, 1e-4, 1e-2, 0.5, 1.0 - 1e-2, 1.0 - 1e-4, 1.0 - 1e-6 })
        {
            var state = At(crossing.OrderBy(sample => System.Math.Abs(sample.Nu - target)).First().T);
            var (centralNoise, shadowNoise, nu, inside) = Noise(CentralDifferences, state, Shadow);
            var (analyticNoise, _, _, _) = Noise(Analytic, state, Shadow);
            Assert.True(inside, $"the points around ν = {nu:E3} leave the penumbra");
            worstCentral = System.Math.Max(worstCentral, centralNoise);
            worstAnalytic = System.Math.Max(worstAnalytic, analyticNoise);
            worstShadow = System.Math.Max(worstShadow, shadowNoise);
            _output.WriteLine($"MEASURE #423 R3 penumbra ν {nu:E3}: noise of the SRP partials, central differences " +
                              $"{centralNoise:E2}, analytic {analyticNoise:E2} of their block; of ν {shadowNoise:E2}");
        }

        // Assert, Linux .NET 10 (2026-10-22): the central differences, the cause, 5.3e-7 of their block at ν = 0.5 to
        // 1.2e-3 within a few metres of the contacts, ν 4.3e-10 to 2.2e-8 (with 1 mm between the points, Windows and
        // macOS arm64 gave the same to 3 % at the six points that stayed inside the penumbra); the derivative of the
        // shadow function, 2.9e-15 of its block at ν = 0.5 to 1.2e-10 within a few metres of the contacts, where the
        // exact partials grow as the square root of the distance to them. Bounds at twice the worst over Linux, Windows
        // and macOS arm64 (lesson 13 of #364)
        Assert.True(worstCentral < 2.6e-3, $"noise of the central differences {worstCentral:E2}");
        Assert.True(worstAnalytic < 2.4e-10, $"noise of the derivative of the shadow function {worstAnalytic:E2}");
        Assert.True(worstShadow < 4.9e-8, $"noise of ν {worstShadow:E2}");
    }

    private delegate void Partials(StateVector state, double[] dadr);

    private static (double Partials, double Shadow, double Nu, bool Inside) Noise(Partials partials, StateVector state,
        Func<StateVector, double> shadow)
    {
        const int count = 2000;
        var direction = state.Velocity.Normalize();
        var work = new StateVector(state.Position, state.Velocity, state.Observer, state.Epoch, state.Frame);
        var blocks = new double[count][];
        var nus = new double[count];
        for (int k = 0; k < count; k++)
        {
            work.UpdatePosition(state.Position + direction * (1e-4 * (k - count / 2)));
            blocks[k] = new double[9];
            partials(work, blocks[k]);
            nus[k] = shadow(work);
        }

        double blockSum = 0.0;
        double shadows = 0.0;
        for (int k = 1; k < count - 1; k++)
        {
            for (int j = 0; j < 9; j++)
            {
                double second = blocks[k + 1][j] - 2.0 * blocks[k][j] + blocks[k - 1][j];
                blockSum += second * second;
            }

            double secondNu = nus[k + 1] - 2.0 * nus[k] + nus[k - 1];
            shadows += secondNu * secondNu;
        }

        double norm = System.Math.Sqrt(blocks[count / 2].Sum(value => value * value));
        return (System.Math.Sqrt(blockSum / ((count - 2) * 6.0)) / norm, System.Math.Sqrt(shadows / ((count - 2) * 6.0)),
            nus[count / 2], nus.All(nu => nu > 0.0 && nu < 1.0));
    }
}
