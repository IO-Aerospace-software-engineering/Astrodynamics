// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Numerics;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using Xunit.Abstractions;
using CelestialBody = IO.Astrodynamics.Body.CelestialBody;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces;

/// <summary>
/// The acceleration of <see cref="ThirdBodyPerturbation"/> near the perturbing body (#576, #544): Battin's 1 + q is
/// |r − d|² / |d|², which a cancellation of terms of order 1 gives to about ε |d|² / |r − d|² only, so in SSB mode about
/// the Earth (the Sun the central body, the Earth the third body, |r − d| / |d| = 4.6e-5) the Earth's attraction carried a
/// relative noise of about 1e-7. Against the exact acceleration −μ ((r − d) / |r − d|³ + d / |d|³) of the same doubles r,
/// d and μ, evaluated in integers of 2200 bits (the reference is exact to its last 2^-1000; no rounding of the library
/// enters it).
/// </summary>
public class ThirdBodyNearThePerturberTests
{
    private static readonly TimeSystem.Time Epoch = new(new DateTime(2024, 3, 1, 12, 0, 0), TimeSystem.TimeScale.TDB);
    private readonly ITestOutputHelper _output;

    public ThirdBodyNearThePerturberTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    // Directions of r − d, none along an axis
    private static readonly Vector3[] Directions =
    {
        new Vector3(0.6, -0.48, 0.64), new Vector3(-0.31, 0.77, 0.557), new Vector3(0.123, 0.456, -0.881),
        new Vector3(-0.9, -0.3, 0.316), new Vector3(0.05, -0.999, 0.02)
    };

    /// <summary>
    /// The relative error of the acceleration against the exact one, |r − d| / |d| from 1e-6 to 1.5, about the Sun with the
    /// Earth as third body (SSB mode) and about the Earth with the Moon (a spacecraft near the Moon propagated about the
    /// Earth).
    /// </summary>
    /// <remarks>
    /// Measured on a85098d (Linux x64, Release, 2026-11-17): about the Sun 6.3e-4 at |r − d| / |d| = 1e-6, 2.7e-7 at the
    /// LEO's 4.6e-5, 5.4e-10 at 1e-3, 8.2e-12 at 1e-2, the noise falling as (|d| / |r − d|)²; about the Earth with the Moon
    /// 1.3e-4, 1.1e-7 and 3.8e-10. With the direct difference below |d| / 2: 4.1e-16 at most there, the worst 2.4e-15 on
    /// Battin's side of the switch (at 0.51), Battin's own rounding where |r| and |d| are of one size.
    /// </remarks>
    [Theory]
    [InlineData(10, 399)]
    [InlineData(399, 301)]
    public void Acceleration_IsTheExactOne_AtEveryDistanceFromThePerturber(int central, int perturber)
    {
        // Arrange
        var centralBody = new CelestialBody(central);
        var perturbingBody = new CelestialBody(perturber);
        var force = new ThirdBodyPerturbation(perturbingBody, centralBody);
        var d = perturbingBody.GetEphemeris(Epoch, centralBody, Frames.Frame.ICRF, Aberration.None).ToStateVector()
            .Position;
        double[] ratios = { 1e-6, 1e-5, 4.6e-5, 1e-4, 1e-3, 1e-2, 0.1, 0.3, 0.49, 0.499999, 0.500001, 0.51, 0.7, 1.0, 1.5 };

        // Act and assert
        double worst = 0.0;
        foreach (double ratio in ratios)
        {
            double worstAtRatio = 0.0;
            foreach (var direction in Directions)
            {
                var r = d + direction.Normalize() * (ratio * d.Magnitude());
                var a = force.Apply(new StateVector(r, Vector3.Zero, centralBody, Epoch, Frames.Frame.ICRF));
                var exact = ExactAcceleration(r, d, perturbingBody.GM);
                worstAtRatio = System.Math.Max(worstAtRatio, (a - exact).Magnitude() / exact.Magnitude());
            }

            _output.WriteLine($"MEASURE {central} / {perturber}, |r - d| / |d| = {ratio:E3}: relative error {worstAtRatio:E3}");
            worst = System.Math.Max(worst, worstAtRatio);
        }

        _output.WriteLine($"MEASURE {central} / {perturber}: worst relative error {worst:E3}");
        Assert.True(worst < RelativeError, $"{worst:E3}");
    }

    /// <summary>Twice the worst of the theories above and of the switch, 2.657e-15 (Battin's side of the switch) on Linux
    /// x64, Windows x64 and macOS arm64 alike (CI run 44225490669), rounded up.</summary>
    private const double RelativeError = 5.4e-15;

    /// <summary>Twice the worst relative step between two neighbours across the switch, 2.339e-15 on Linux x64, Windows x64
    /// and macOS arm64 alike (CI run 44225490669), rounded up.</summary>
    private const double StepAcrossTheSwitch = 4.7e-15;

    /// <summary>
    /// Around the switch from the direct difference to Battin's form, |r − d| = |d| / 2: on both sides, 200 points one
    /// ulp of the distance apart, the acceleration is the exact one to the same few ε, so it is continuous there to the
    /// last bits.
    /// </summary>
    [Fact]
    public void Acceleration_IsContinuousAcrossTheSwitch()
    {
        // Arrange
        var sun = new CelestialBody(10);
        var earth = new CelestialBody(399);
        var force = new ThirdBodyPerturbation(earth, sun);
        var d = earth.GetEphemeris(Epoch, sun, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position;
        var u = Directions[1].Normalize();
        double half = 0.5 * d.Magnitude();

        // Act
        double worst = 0.0;
        double largestStep = 0.0;
        Vector3? previous = null;
        double distance = half;
        for (int k = 0; k < 100; k++)
        {
            distance = System.Math.BitDecrement(distance);
        }

        for (int k = 0; k < 200; k++)
        {
            var r = d + u * distance;
            var a = force.Apply(new StateVector(r, Vector3.Zero, sun, Epoch, Frames.Frame.ICRF));
            var exact = ExactAcceleration(r, d, earth.GM);
            worst = System.Math.Max(worst, (a - exact).Magnitude() / exact.Magnitude());
            if (previous.HasValue)
            {
                largestStep = System.Math.Max(largestStep, (a - previous.Value).Magnitude() / a.Magnitude());
            }

            previous = a;
            distance = System.Math.BitIncrement(distance);
        }

        // Assert
        _output.WriteLine($"MEASURE switch: worst relative error {worst:E3}, largest step between neighbours {largestStep:E3}");
        Assert.True(worst < RelativeError, $"{worst:E3}");
        Assert.True(largestStep < StepAcrossTheSwitch, $"{largestStep:E3}");
    }

    /// <summary>
    /// Far from the perturbing body (|r − d| ≥ |d| / 2: the Moon and the Sun on an Earth orbit), the acceleration is Battin's,
    /// as the library computed it before 11.0.0, bit for bit: a propagation about the Earth with distant third bodies does
    /// not move.
    /// </summary>
    [Theory]
    [InlineData(301, 6778137.0)]
    [InlineData(10, 6778137.0)]
    [InlineData(301, 42164137.0)]
    [InlineData(10, 42164137.0)]
    [InlineData(301, 1.5e8)]
    public void Acceleration_FarFromThePerturber_IsBattinsBitForBit(int perturber, double radius)
    {
        // Arrange
        var earth = new CelestialBody(399);
        var perturbingBody = new CelestialBody(perturber);
        var force = new ThirdBodyPerturbation(perturbingBody, earth);
        var d = perturbingBody.GetEphemeris(Epoch, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector().Position;

        foreach (var direction in Directions)
        {
            var r = direction.Normalize() * radius;

            // Act
            var a = force.Apply(new StateVector(r, Vector3.Zero, earth, Epoch, Frames.Frame.ICRF));

            // Assert: Battin's form as the library writes it (Battin, "An Introduction to the Methods of Astrodynamics", section 8.6)
            double d2 = d.Magnitude() * d.Magnitude();
            double q = r.Dot(r - d * 2.0) / d2;
            double f = ThirdBodyPerturbation.BattinF(q);
            var battin = (r + d * f) * (-perturbingBody.GM / (d2 * d.Magnitude() * System.Math.Pow(1.0 + q, 1.5)));
            Assert.Equal((battin.X, battin.Y, battin.Z), (a.X, a.Y, a.Z));
        }
    }

    // The exact −μ ((r − d) / |r − d|³ + d / |d|³) of the doubles r, d and μ, rounded once to doubles at the end
    private static Vector3 ExactAcceleration(in Vector3 r, in Vector3 d, double gm)
    {
        var rho = new[] { Fixed(r.X) - Fixed(d.X), Fixed(r.Y) - Fixed(d.Y), Fixed(r.Z) - Fixed(d.Z) };
        var dj = new[] { Fixed(d.X), Fixed(d.Y), Fixed(d.Z) };
        var direct = InverseCube(rho);
        var indirect = InverseCube(dj);
        var mu = Fixed(gm);
        var components = Enumerable.Range(0, 3)
            .Select(i => -ToDouble(mu * (direct[i] + indirect[i]), Scale + ResultScale)).ToArray();
        return new Vector3(components[0], components[1], components[2]);
    }

    // Fixed point: a double times 2^Scale is an integer for every double (the smallest exponent of a double is −1074)
    private const int Scale = 1100;

    // The fraction bits of v / |v|³
    private const int ResultScale = 1100;

    private static BigInteger Fixed(double x)
    {
        long bits = BitConverter.DoubleToInt64Bits(x);
        bool negative = bits < 0;
        int exponent = (int)((bits >> 52) & 0x7FF);
        long mantissa = bits & 0xFFFFFFFFFFFFFL;
        if (exponent == 0)
        {
            exponent = 1;
        }
        else
        {
            mantissa |= 1L << 52;
        }

        var value = new BigInteger(mantissa) << (exponent - 1075 + Scale);
        return negative ? -value : value;
    }

    // v / |v|³ for v at scale 2^Scale, at scale 2^ResultScale
    private static BigInteger[] InverseCube(BigInteger[] v)
    {
        var squared = v[0] * v[0] + v[1] * v[1] + v[2] * v[2]; // scale 2^(2 Scale)
        var norm = SquareRoot(squared); // scale 2^Scale
        var cube = squared * norm; // scale 2^(3 Scale)
        return v.Select(c => (c << (2 * Scale + ResultScale)) / cube).ToArray();
    }

    private static BigInteger SquareRoot(BigInteger n)
    {
        var x = BigInteger.One << (int)((n.GetBitLength() + 1) / 2);
        while (true)
        {
            var y = (x + n / x) >> 1;
            if (y >= x)
            {
                return x;
            }

            x = y;
        }
    }

    private static double ToDouble(BigInteger v, int scale)
    {
        int shift = (int)System.Math.Max(0, v.GetBitLength() - 64);
        return System.Math.ScaleB((double)(v >> shift), shift - scale);
    }
}
