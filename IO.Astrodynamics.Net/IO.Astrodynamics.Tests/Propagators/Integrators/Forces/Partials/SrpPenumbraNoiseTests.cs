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
/// The rounding noise of the SRP partials in the penumbra, along the first shadow crossing of R3 (Sun-synchronous 700 km,
/// SRP with Cr): that of the central differences of the acceleration, the cause of the thousands of steps that RK7(8)
/// took there with the variational equations in the step control before #423, and that of the derivative of the shadow
/// function that replaced them.
/// </summary>
/// <remarks>
/// <para>
/// From #379 to #423 the SRP partials in the penumbra were the central differences of the whole acceleration (the default
/// path, <see cref="ForceBase.AccumulateStatePartialsByCentralDifferences"/> with its default steps), whose shadow
/// fraction ν came from the closed form of the two-circle area in <see cref="CelestialItem.ShadowFraction(double,
/// double, double)"/>: its terms of order R_occ² acos(…) cancelled down to the area of the solar disc that is covered, so
/// ν carried a rounding of a few 1e-10 inside the penumbra, up to a few 1e-8 at its contacts, which the stencil
/// (2 h ≈ 42 m) divided. The embedded error estimate of Y is first order in the step for a noisy derivative, as for the
/// density gradient of #420. The derivative of the shadow function in its reduced form (<see cref="SolarRadiationPressure"/>)
/// does not divide the rounding of ν by a step. Since #434 the shadow fraction itself is the sum of two circular segments,
/// without cancellation: the noise of ν falls to a few 1e-16 to a few 1e-14, and with it that of the central
/// differences, which only ever divided it.
/// </para>
/// <para>
/// The noise is the root mean square of the second difference of each element along 2000 points 10 µm apart along the
/// velocity, divided by √6 (the variance of the second difference of a white noise) and by the Frobenius norm of the
/// block, at points where ν is 1e-4 to 1 − 1e-4 and 2 m inside each contact. The partials vary over the width of the
/// penumbra, about 20 km, so over 10 µm their second difference is noise alone. Near a contact the exact partials grow
/// as the square root of the distance d to it, whose second difference over a spacing δ is about δ² / (4 d²) of them:
/// 6e-12 at 2 m for 10 µm, 6e-10 for 0.1 mm. The 2 cm of the points must stay inside the penumbra. The two points
/// near the contacts are placed from the contacts found on the geometry of the discs: the points of the trajectory 1 ms
/// apart (7.5 m) nearest to ν = 1e-6 and 1 − 1e-6, first taken, fell within 0.1 m of the inner contact on Windows and
/// macOS arm64, and the second difference measured the square root (8.0e-3 and 1.9e-2 against 1.3e-3 on Linux).
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
        // Arrange: the first hour of R3, its first shadow crossing, an entry, at about 415 s
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

        // The contacts in time, by bisection on the angles of the discs, as in the partials
        var earth = (CelestialBody)start.Observer;
        var sun = r3.Bodies.OfType<CelestialBody>().First(body => body.NaifId == 10);
        bool InsideTheOuterContact(double t) => Discs(sun, earth, At(t)) is var (a, b, c) && c < a + b;
        bool InsideTheInnerContact(double t) => Discs(sun, earth, At(t)) is var (a, b, c) && c <= b - a;
        // A shadow entry: the outer contact at about 415.7 s, the inner one at about 424.5 s
        double outer = Bisect(t => !InsideTheOuterContact(t), 400.0, 440.0);
        double inner = Bisect(t => !InsideTheInnerContact(t), 400.0, 440.0);
        double twoMetres = 2.0 / start.Velocity.Magnitude();

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
        var times = new[] { 1e-4, 1e-2, 0.5, 1.0 - 1e-2, 1.0 - 1e-4 }
            .Select(target => crossing.OrderBy(sample => System.Math.Abs(sample.Nu - target)).First().T)
            .Prepend(outer + twoMetres).Append(inner - twoMetres);
        foreach (double time in times)
        {
            var state = At(time);
            var (centralNoise, shadowNoise, nu, inside) = Noise(CentralDifferences, state, Shadow);
            var (analyticNoise, _, _, _) = Noise(Analytic, state, Shadow);
            Assert.True(inside, $"the points around ν = {nu:E3} leave the penumbra");
            worstCentral = System.Math.Max(worstCentral, centralNoise);
            worstAnalytic = System.Math.Max(worstAnalytic, analyticNoise);
            worstShadow = System.Math.Max(worstShadow, shadowNoise);
            _output.WriteLine($"MEASURE #423 R3 penumbra ν {nu:E3}: noise of the SRP partials, central differences " +
                              $"{centralNoise:E2}, analytic {analyticNoise:E2} of their block; of ν {shadowNoise:E2}");
        }

        // Assert, .NET 10. Before #434 (CI of aed79f2, 2026-10-22): the central differences, the cause, 5.0e-7, 5.1e-7
        // and 4.9e-7 of their block at ν = 0.5 to 1.6e-3, 1.6e-3 and 1.6e-3 at 2 m from the contacts on Linux, Windows
        // and macOS arm64, ν 3.9e-10 to 3.8e-8; the derivative of the shadow function 1.2e-15 to 1.8e-15 at ν = 0.5,
        // 2.9e-13 to 3.2e-13 at ν = 0.01 and 0.99, 5.7e-12 to 6.6e-12 at 1e-4 and 1 − 1e-4, and 3.2e-10 to 3.7e-10 at 2 m
        // from the contacts, where the rounding of the angular separation (about 1e-16 rad) enters the square root of the
        // half chord, a + b − c being some 3e-7 rad there. With the shadow fraction of #434 (CI of 3340116, 2026-10-22), on
        // Linux, Windows and macOS arm64: the central differences 2.6e-11 to 4.4e-11, 2.8e-11 to 4.6e-11 and 2.6e-11 to
        // 4.4e-11 of their block, the derivative of the shadow function 5.1e-16 to 3.4e-10, 5.2e-16 to 3.5e-10 and
        // 5.1e-16 to 3.4e-10, ν 2.8e-16 to 2.5e-14, 3.0e-16 to 2.6e-14 and 2.8e-16 to 2.5e-14. Bounds at twice the worst
        // over the three platforms (lesson 13 of #364): 4.57e-11, 3.45e-10 and 2.61e-14 (Windows)
        Assert.True(worstCentral < 9.2e-11, $"noise of the central differences {worstCentral:E2}");
        Assert.True(worstAnalytic < 6.9e-10, $"noise of the derivative of the shadow function {worstAnalytic:E2}");
        Assert.True(worstShadow < 5.3e-14, $"noise of ν {worstShadow:E2}");
    }

    private delegate void Partials(StateVector state, double[] dadr);

    // The angular radii of the Sun and of the Earth and their angular separation seen from the state, in rad
    private static (double SunRadius, double BodyRadius, double Separation) Discs(CelestialBody sun, CelestialBody earth,
        StateVector state)
    {
        var toSun = sun.GetEphemeris(state.Epoch, state.Observer, state.Frame, Aberration.None).ToStateVector().Position -
                    state.Position;
        var toEarth = earth.Equals(state.Observer) ? state.Position * -1.0 : earth.GetEphemeris(state.Epoch,
            state.Observer, state.Frame, Aberration.None).ToStateVector().Position - state.Position;
        return (System.Math.Asin(sun.EquatorialRadius / toSun.Magnitude()),
            System.Math.Asin(earth.EquatorialRadius / toEarth.Magnitude()),
            System.Math.Atan2(toSun.Cross(toEarth).Magnitude(), toSun * toEarth));
    }

    // The time in [low, high] where the predicate turns from true to false
    private static double Bisect(Func<double, bool> before, double low, double high)
    {
        for (int k = 0; k < 100 && high - low > 1e-9; k++)
        {
            double middle = 0.5 * (low + high);
            if (before(middle))
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return 0.5 * (low + high);
    }

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
            work.UpdatePosition(state.Position + direction * (1e-5 * (k - count / 2)));
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
