// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator.Forces;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// Partials of the radiation pressures (B6): SRP analytic outside the penumbra and by the default central differences
/// inside it, thermal pressure analytic, albedo by the default central differences, ∂a/∂Cr exact, against Ridders'
/// derivative of the acceleration; in the penumbra, the term of the shadow function that the analytic SRP block leaves
/// out, measured against Ridders' derivative of the shadow function.
/// </summary>
public class RadiationPressurePartialsTests : IClassFixture<PartialsTestCases>
{
    private readonly PartialsTestCases _cases;
    private readonly ITestOutputHelper _output;

    public RadiationPressurePartialsTests(PartialsTestCases cases, ITestOutputHelper output)
    {
        _cases = cases;
        _output = output;
    }

    public static TheoryData<string, string, bool> Cases()
    {
        var data = new TheoryData<string, string, bool>();
        foreach (var state in PartialsTestCases.StateNames)
        {
            foreach (var force in new[] { "SRP", "thermal" })
            {
                data.Add(state, force, false);
                data.Add(state, force, true);
            }
        }

        return data;
    }

    public static TheoryData<string> States => PartialsTestCases.States;

    public static TheoryData<string> PenumbraOrbits => new() { PartialsTestCases.Leo400, PartialsTestCases.Geo };

    [Theory]
    [MemberData(nameof(Cases))]
    public void AnalyticPartials_MatchRiddersWithin1e6(string stateName, string forceName, bool useCache)
    {
        // Arrange
        var state = PartialsTestCases.State(stateName, _cases.Earth);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        ForceBase force = forceName == "SRP"
            ? new SolarRadiationPressure(spacecraft, new[] { _cases.Earth, _cases.Moon })
            : new ThermalRadiationPressure(spacecraft, _cases.Earth);
        if (useCache)
        {
            force.EphemerisCache = PartialsTestCases.Cache(_cases.Earth, _cases.Sun, _cases.Moon);
        }

        var dadr = new double[9];
        var dadv = Enumerable.Repeat(5.0, 9).ToArray();

        // Act
        force.AccumulateStatePartials(state, context, dadr, dadv);

        // Assert. SRP varies on the scale of the distance to the Sun, so steps on the scale of |r| leave its reference
        // with the rounding of an acceleration that hardly changes (about 5e-9 measured). The states are sunlit, so the
        // reference is the same SRP without occulting body, the same acceleration, with steps of 1e-5 of the distance
        // to the Sun, out of reach of the shadow of the Earth or the Moon.
        var referenceForce = force;
        double positionStep = 1e-3 * state.Position.Magnitude();
        if (force is SolarRadiationPressure)
        {
            referenceForce = new SolarRadiationPressure(spacecraft, Array.Empty<CelestialBody>());
            Assert.Equal(referenceForce.Apply(state, context), force.Apply(state, context));
            positionStep = 1e-5 * (state.Position - _cases.Sun.GetEphemeris(state.Epoch, _cases.Earth,
                Frames.Frame.ICRF, Aberration.None).ToStateVector().Position).Magnitude();
        }

        var reference = RiddersDerivative.StatePartials(referenceForce, state, context, positionStep, 1.0);
        double error = RiddersDerivative.RelativeFrobeniusError(dadr, reference.Dadr);
        _output.WriteLine($"{stateName}, {forceName}: relative error {error:E2}, reference {reference.DadrError:E2}");
        // The reference resolves the tolerance by two orders of magnitude.
        Assert.True(reference.DadrError < 1e-8, $"reference error {reference.DadrError:E2}");
        Assert.True(error < 1e-6, $"relative error {error:E2}");
        Assert.All(dadv, x => Assert.Equal(5.0, x));
    }

    public static TheoryData<string, bool> StatesRelativeToTheEarthAndTheSun()
    {
        var data = new TheoryData<string, bool>();
        foreach (var state in PartialsTestCases.StateNames)
        {
            data.Add(state, false);
            data.Add(state, true);
        }

        return data;
    }

    public static TheoryData<string, bool> PenumbraOrbitsRelativeToTheEarthAndTheSun => new()
    {
        { PartialsTestCases.Leo400, false },
        { PartialsTestCases.Leo400, true },
        { PartialsTestCases.Geo, false },
        { PartialsTestCases.Geo, true }
    };

    [Theory]
    [MemberData(nameof(StatesRelativeToTheEarthAndTheSun))]
    public void Albedo_DefaultPartials_MatchRiddersWithin1e4(string stateName, bool relativeToTheSun)
    {
        // Arrange
        var state = PartialsTestCases.State(stateName, _cases.Earth);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new AlbedoRadiationPressure(spacecraft, _cases.Earth);
        var dadr = new double[9];
        var dadv = Enumerable.Repeat(5.0, 9).ToArray();

        // Act
        Partials(force, state, context, relativeToTheSun, dadr, dadv);

        // Assert: the threshold of the specification for the default path. Relative to the Sun, as in SSB mode, the step
        // scaled by |r| was 450 km and left 1.6e-4 to 7.1e-3 (H3 review of #378); it is now scaled by the distance to the
        // Earth. The reference is Ridders' derivative of the acceleration relative to the Earth: a change of origin is a
        // translation, so the partials are the same.
        var reference = RiddersDerivative.StatePartials(force, state, context, 1e-3 * state.Position.Magnitude(), 1.0);
        double error = RiddersDerivative.RelativeFrobeniusError(dadr, reference.Dadr);
        _output.WriteLine($"{stateName}{(relativeToTheSun ? ", relative to the Sun" : "")}: relative error {error:E2}, " +
                          $"reference {reference.DadrError:E2}");
        Assert.True(reference.DadrError < 1e-6, $"reference error {reference.DadrError:E2}");
        Assert.True(error < 1e-4, $"relative error {error:E2}");
        Assert.All(dadv, x => Assert.Equal(5.0, x));
    }

    [Fact]
    public void AnalyticPartials_WithTheEphemerisCache_AllocateNothing()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var cache = PartialsTestCases.Cache(_cases.Earth, _cases.Sun, _cases.Moon);
        ForceBase[] forces =
        {
            new SolarRadiationPressure(spacecraft, new[] { _cases.Earth, _cases.Moon }) { EphemerisCache = cache },
            new ThermalRadiationPressure(spacecraft, _cases.Earth) { EphemerisCache = cache }
        };
        var dadr = new double[9];
        var dadv = new double[9];

        foreach (var force in forces)
        {
            force.AccumulateStatePartials(state, context, dadr, dadv);

            // Act
            long before = GC.GetAllocatedBytesForCurrentThread();
            force.AccumulateStatePartials(state, context, dadr, dadv);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert
            Assert.Equal(0, allocated);
        }
    }

    [Theory]
    [MemberData(nameof(PenumbraOrbits))]
    public void Umbra_NoAccelerationAndNoPartials(string orbit)
    {
        // Arrange
        var state = ShadowState(orbit, 1.0);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new SolarRadiationPressure(spacecraft, new[] { _cases.Earth });
        var dadr = Enumerable.Repeat(5.0, 9).ToArray();

        // Act
        force.AccumulateStatePartials(state, context, dadr, new double[9]);

        // Assert
        Assert.Equal(Vector3.Zero, force.Apply(state, context));
        Assert.All(dadr, x => Assert.Equal(5.0, x));
    }

    [Theory]
    [MemberData(nameof(PenumbraOrbitsRelativeToTheEarthAndTheSun))]
    public void Penumbra_DefaultPartials_MatchRiddersWithin1e4(string orbit, bool relativeToTheSun)
    {
        // Arrange: half the solar disc hidden by the Earth
        var state = ShadowState(orbit, 0.5);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new SolarRadiationPressure(spacecraft, new[] { _cases.Earth });
        var dadr = new double[9];
        var dadv = Enumerable.Repeat(5.0, 9).ToArray();

        // Act
        Partials(force, state, context, relativeToTheSun, dadr, dadv);

        // Assert: the threshold of the specification for the default path. Relative to the Sun, as in SSB mode, the step
        // scaled by |r| was 450 km, wider than the penumbra, and the block was off by 66 % (GEO) to 100 % (LEO) (H3 review
        // of #378); it is now scaled by the distance to the Earth. The reference is Ridders' derivative of the acceleration
        // relative to the Earth, the same partials since a change of origin is a translation.
        var reference = RiddersDerivative.StatePartials(force, state, context, PenumbraStep(state), 1.0);
        double error = RiddersDerivative.RelativeFrobeniusError(dadr, reference.Dadr);
        _output.WriteLine($"{orbit}{(relativeToTheSun ? ", relative to the Sun" : "")}: relative error {error:E2}, " +
                          $"reference {reference.DadrError:E2}");
        Assert.True(reference.DadrError < 1e-7, $"reference error {reference.DadrError:E2}");
        Assert.True(error < 1e-4, $"relative error {error:E2}");
        Assert.All(dadv, x => Assert.Equal(5.0, x));
    }

    [Theory]
    [MemberData(nameof(PenumbraOrbits))]
    public void Penumbra_TheAnalyticBlockMissesExactlyTheShadowTerm(string orbit)
    {
        // Arrange: half the solar disc hidden by the Earth. The acceleration is a = (1 − ν) a_lit, a_lit the acceleration
        // without occulting body, so ∂a/∂r = (1 − ν) ∂a_lit/∂r − a_lit (∇ν)ᵀ: the analytic block holds ν constant and
        // leaves out the last term, which comes here from Ridders' derivative of the shadow function of the Sun. This is
        // why the penumbra goes through the default central differences.
        var state = ShadowState(orbit, 0.5);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new SolarRadiationPressure(spacecraft, new[] { _cases.Earth });
        var lit = new SolarRadiationPressure(spacecraft, Array.Empty<CelestialBody>()).Apply(state, context);
        var dadr = new double[9];

        // Act
        force.AccumulateAnalyticStatePartials(state, context, dadr);

        // Assert
        double step = PenumbraStep(state);
        var reference = RiddersDerivative.StatePartials(force, state, context, step, 1.0);
        var shadowGradient = new double[3];
        for (int j = 0; j < 3; j++)
        {
            int column = j;
            shadowGradient[j] = RiddersDerivative.Derivative(
                t => _cases.Sun.ShadowFraction(_cases.Earth, WithPosition(state, column, t), Aberration.None),
                RiddersDerivative.Component(state.Position, j), step).Derivative;
        }

        var shadowTerm = new double[9];
        var predicted = new double[9];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                shadowTerm[3 * i + j] = -RiddersDerivative.Component(lit, i) * shadowGradient[j];
                predicted[3 * i + j] = dadr[3 * i + j] + shadowTerm[3 * i + j];
            }
        }

        var pointMass = new double[9];
        new GravitationalAcceleration(_cases.Earth).AccumulateStatePartials(state, context, pointMass, new double[9]);
        double neglected = RiddersDerivative.RelativeFrobeniusError(dadr, reference.Dadr);
        double scaled = Norm(shadowTerm) / Norm(pointMass);
        double decomposition = RiddersDerivative.RelativeFrobeniusError(predicted, reference.Dadr);
        _output.WriteLine($"{orbit}: ν = {_cases.Sun.ShadowFraction(_cases.Earth, state, Aberration.None):F6}, " +
                          $"analytic block against Ridders {neglected:E2} relative, shadow term {scaled:E2} of the " +
                          $"point-mass block, with the term {decomposition:E2}, reference {reference.DadrError:E2}");
        // The reference resolves the tolerance by an order of magnitude: measured 7.9e-9 (LEO) and 1.6e-10 (GEO)
        Assert.True(reference.DadrError < 1e-7, $"reference error {reference.DadrError:E2}");
        Assert.True(decomposition < 1e-6, $"with the shadow term, relative error {decomposition:E2}");
        // The shadow term is the whole block: 1.3e-5 (LEO) and 1.7e-4 (GEO) of the point-mass block, measured
        Assert.True(neglected > 0.99, $"the shadow term is not the whole block: {neglected:E2}");
    }

    [Theory]
    [InlineData(PartialsTestCases.Leo400, true)]
    [InlineData(PartialsTestCases.Leo400, false)]
    [InlineData(PartialsTestCases.Geo, true)]
    [InlineData(PartialsTestCases.Geo, false)]
    public void ShadowBoundaries_PartialsAreContinuous(string orbit, bool umbra)
    {
        // Arrange: the states 0.01 mm on either side of the inner contact (umbra) or the outer contact (sunlight) of the
        // shadow of the Earth. Before the H3 review of #378, the partials switched there from the central differences
        // of the penumbra to zero or to the analytic block, and jumped: at the umbra, where the exact partials tend to
        // zero, the central difference straddling the boundary gives about a(r + h) / 2h; in sunlight, the analytic
        // block, 1.1e-17 /s² in LEO, against 8.5e-13 /s² for the central differences that hold the derivative of ν.
        var (inside, outside) = ContactStates(orbit, umbra, 1e-5);
        var spacecraft = PartialsTestCases.Spacecraft(inside);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new SolarRadiationPressure(spacecraft, new[] { _cases.Earth });
        var dadrInside = new double[9];
        var dadrOutside = new double[9];

        // Act
        force.AccumulateStatePartials(inside, context, dadrInside, new double[9]);
        force.AccumulateStatePartials(outside, context, dadrOutside, new double[9]);

        // Assert: both sides take the central differences, which move by the 0.02 mm only. Measured (Linux, .NET 10,
        // 2026-10-13) 7.1e-4 (LEO, sunlight) and 8.7e-4 (LEO, umbra), 1.0e-2 and 2.8e-3 over 2 mm, falling with the gap
        // as a continuous function does; before the fix, 7.5e4 (LEO, sunlight) and infinite (umbra, zero partials)
        double jump = RiddersDerivative.RelativeFrobeniusError(dadrInside, dadrOutside);
        _output.WriteLine($"{orbit}, {(umbra ? "umbra" : "sunlight")}: |A| {Norm(dadrOutside):E3} /s², relative jump " +
                          $"{jump:E2} over 0.02 mm");
        Assert.True(Norm(dadrOutside) > 0.0);
        Assert.True(jump < 1e-2, $"relative jump {jump:E2}");
    }

    [Fact]
    public void Sunlit_TheAnalyticBlockIsThePartials()
    {
        // Arrange
        var state = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new SolarRadiationPressure(spacecraft, new[] { _cases.Earth, _cases.Moon });
        var dadr = new double[9];
        var analytic = new double[9];
        var umbra = Enumerable.Repeat(5.0, 9).ToArray();

        // Act
        force.AccumulateStatePartials(state, context, dadr, new double[9]);
        force.AccumulateAnalyticStatePartials(state, context, analytic);
        force.AccumulateAnalyticStatePartials(ShadowState(PartialsTestCases.Leo400, 1.0), context, umbra);

        // Assert
        Assert.Equal(analytic, dadr);
        Assert.All(umbra, x => Assert.Equal(5.0, x));
    }

    [Fact]
    public void Thermal_AtTheCentreOfTheEmittingBody_NoAccelerationAndNoPartials()
    {
        // Arrange: within 1 m of the centre, where the acceleration is set to zero to avoid the singularity
        var leo = PartialsTestCases.State(PartialsTestCases.Leo400, _cases.Earth);
        var state = new StateVector(new Vector3(0.5, 0.0, 0.0), leo.Velocity, leo.Observer, leo.Epoch, leo.Frame);
        var spacecraft = PartialsTestCases.Spacecraft(leo);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        var force = new ThermalRadiationPressure(spacecraft, _cases.Earth);
        var dadr = Enumerable.Repeat(5.0, 9).ToArray();

        // Act
        force.AccumulateStatePartials(state, context, dadr, new double[9]);

        // Assert
        Assert.Equal(Vector3.Zero, force.Apply(state, context));
        Assert.All(dadr, x => Assert.Equal(5.0, x));
    }

    [Theory]
    [MemberData(nameof(States))]
    public void ReflectivityPartials_AddUpToRiddersOfTheSum(string stateName)
    {
        // Arrange: Cr is shared by the three radiation pressures, so its column of Ψ sums their partials
        var state = PartialsTestCases.State(stateName, _cases.Earth);
        var spacecraft = PartialsTestCases.Spacecraft(state);
        var context = ForceEvaluationContext.FromSpacecraft(spacecraft);
        ForceBase[] forces =
        {
            new SolarRadiationPressure(spacecraft, new[] { _cases.Earth, _cases.Moon }),
            new AlbedoRadiationPressure(spacecraft, _cases.Earth),
            new ThermalRadiationPressure(spacecraft, _cases.Earth)
        };
        var dadCd = new double[3];
        var dadCr = new double[3];

        // Act
        foreach (var force in forces)
        {
            force.AccumulateParameterPartials(state, context, dadCd, dadCr);
        }

        // Assert
        var expected = new double[3];
        for (int i = 0; i < 3; i++)
        {
            int row = i;
            expected[i] = RiddersDerivative.Derivative(cr => forces.Sum(f =>
                    RiddersDerivative.Component(f.Apply(state, context with { ReflectivityCoefficient = cr }), row)),
                context.ReflectivityCoefficient, 1.0).Derivative;
        }

        double error = RiddersDerivative.RelativeFrobeniusError(dadCr, expected);
        _output.WriteLine($"{stateName}: ∂a/∂Cr relative error {error:E2}");
        Assert.True(error < 1e-12, $"relative error {error:E2}");
        Assert.Equal(new double[3], dadCd);
    }

    // The state partials at the state relative to the Earth, or at the same physical state relative to the Sun with the
    // Sun as observer and the Earth read from the ephemeris cache, as in SSB mode.
    private void Partials(ForceBase force, StateVector state, ForceEvaluationContext context, bool relativeToTheSun,
        double[] dadr, double[] dadv)
    {
        if (relativeToTheSun)
        {
            force.EphemerisCache = PartialsTestCases.Cache(_cases.Sun, _cases.Earth);
            state = state.RelativeTo(_cases.Sun, Aberration.None).ToStateVector();
        }

        force.AccumulateStatePartials(state, context, dadr, dadv);
        force.EphemerisCache = null;
    }

    // Ridders' initial step in the penumbra, 1e-4 of the radius (680 m in LEO, 4.2 km at GEO), below the width of the
    // penumbra: the reference error is then 7.9e-9 (LEO) and 1.6e-10 (GEO), against 4.8e-6 and 2.0e-8 at 1e-6.
    private static double PenumbraStep(StateVector state) => 1e-4 * state.Position.Magnitude();

    /// <summary>
    /// A state of the radius of <paramref name="orbit"/> in the plane of the Sun direction and the equator, behind the
    /// Earth, where the shadow fraction of the Sun is <paramref name="shadowFraction"/>: 1 on the anti-Sun axis, or
    /// found by bisection on the angle from that axis.
    /// </summary>
    private StateVector ShadowState(string orbit, double shadowFraction)
    {
        double radius = PartialsTestCases.State(orbit, _cases.Earth).Position.Magnitude();
        var sunDirection = _cases.Sun.GetEphemeris(PartialsTestCases.Epoch, _cases.Earth, Frames.Frame.ICRF,
            Aberration.None).ToStateVector().Position.Normalize();
        var side = sunDirection.Cross(Vector3.VectorZ).Normalize();
        var velocity = Vector3.VectorZ * System.Math.Sqrt(_cases.Earth.GM / radius);

        StateVector At(double angle) => new((sunDirection * -System.Math.Cos(angle) + side * System.Math.Sin(angle)) *
                                            radius, velocity, _cases.Earth, PartialsTestCases.Epoch, Frames.Frame.ICRF);

        double Shadow(double angle) => _cases.Sun.ShadowFraction(_cases.Earth, At(angle), Aberration.None);

        if (shadowFraction >= 1.0)
        {
            return At(0.0);
        }

        double low = 0.0;
        double high = System.Math.PI / 2.0;
        for (int k = 0; k < 100 && high - low > 1e-15; k++)
        {
            double middle = 0.5 * (low + high);
            if (Shadow(middle) > shadowFraction)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return At(0.5 * (low + high));
    }

    /// <summary>
    /// Two states of the radius of <paramref name="orbit"/>, in the plane of the Sun direction and the equator, at
    /// <paramref name="distance"/> on either side of the inner contact (ν = 1 inside) or of the outer contact (ν = 0
    /// outside) of the shadow of the Earth, found by bisection on the angle from the anti-Sun axis.
    /// </summary>
    private (StateVector Inside, StateVector Outside) ContactStates(string orbit, bool umbra, double distance)
    {
        double radius = PartialsTestCases.State(orbit, _cases.Earth).Position.Magnitude();
        var sunDirection = _cases.Sun.GetEphemeris(PartialsTestCases.Epoch, _cases.Earth, Frames.Frame.ICRF,
            Aberration.None).ToStateVector().Position.Normalize();
        var side = sunDirection.Cross(Vector3.VectorZ).Normalize();
        var velocity = Vector3.VectorZ * System.Math.Sqrt(_cases.Earth.GM / radius);

        StateVector At(double angle) => new((sunDirection * -System.Math.Cos(angle) + side * System.Math.Sin(angle)) *
                                            radius, velocity, _cases.Earth, PartialsTestCases.Epoch, Frames.Frame.ICRF);

        bool Inside(double angle)
        {
            double nu = _cases.Sun.ShadowFraction(_cases.Earth, At(angle), Aberration.None);
            return umbra ? nu >= 1.0 : nu > 0.0;
        }

        double low = 0.0;
        double high = System.Math.PI / 2.0;
        for (int k = 0; k < 200 && high - low > 1e-16; k++)
        {
            double middle = 0.5 * (low + high);
            if (Inside(middle))
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        double offset = distance / radius;
        return (At(low - offset), At(high + offset));
    }

    private static StateVector WithPosition(StateVector state, int index, double value)
    {
        var position = index switch
        {
            0 => new Vector3(value, state.Position.Y, state.Position.Z),
            1 => new Vector3(state.Position.X, value, state.Position.Z),
            _ => new Vector3(state.Position.X, state.Position.Y, value)
        };
        return new StateVector(position, state.Velocity, state.Observer, state.Epoch, state.Frame);
    }

    private static double Norm(double[] matrix)
    {
        return System.Math.Sqrt(matrix.Sum(x => x * x));
    }
}
