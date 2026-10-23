// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Forces;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

/// <summary>
/// The bodies of <see cref="GeopotentialPartialsTests"/>: Earths with EGM2008 to degree 2, 10, 70, at the guard degree
/// of the partials and above it (the coefficients missing from the file are zeros).
/// </summary>
/// <remarks>
/// Shared by the tests of the class as an xUnit class fixture: they run one after the other, so sharing the fields,
/// which are not thread-safe, is safe.
/// </remarks>
public sealed class GeopotentialPartialsBodies
{
    internal const string Model = "Data/SolarSystem/EGM2008_to70_TideFree";

    public GeopotentialPartialsBodies()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        foreach (int degree in new[] { 2, 10, 70, GeopotentialGravitationalField.MaxPartialsDegree,
                     GeopotentialGravitationalField.MaxPartialsDegree + 10 })
        {
            Earths[degree] = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, PartialsTestCases.Epoch,
                new GeopotentialModelParameters(Model, (ushort)degree));
        }
    }

    internal Dictionary<int, CelestialBody> Earths { get; } = new();
}

/// <summary>
/// B3: the analytic partials of the geopotential by the Cunningham recursion, against Ridders' derivative of the
/// acceleration, which the field computes in spherical coordinates.
/// </summary>
/// <remarks>
/// The states are the four of B7 and two near the pole, above 85° of latitude, where a naive spherical formulation of
/// the partials would be singular. The error is measured on the whole block and on the harmonic part alone (the block
/// less the point mass), whose reference is Ridders' derivative of the acceleration less the point-mass acceleration.
/// </remarks>
public class GeopotentialPartialsTests : IClassFixture<GeopotentialPartialsBodies>
{
    private const string Polar87 = "polar 87°";
    private const string Polar899 = "polar 89.9°";

    private static readonly ForceEvaluationContext Context = new(100.0, 2.2, 1.5);

    private readonly GeopotentialPartialsBodies _bodies;
    private readonly ITestOutputHelper _output;

    public GeopotentialPartialsTests(GeopotentialPartialsBodies bodies, ITestOutputHelper output)
    {
        _bodies = bodies;
        _output = output;
    }

    public static TheoryData<string, int> StatesAndDegrees
    {
        get
        {
            var data = new TheoryData<string, int>();
            foreach (var state in new[]
                     {
                         PartialsTestCases.Leo400, PartialsTestCases.Leo800, PartialsTestCases.Geo,
                         PartialsTestCases.HeoPerigee, Polar87, Polar899
                     })
            {
                foreach (int degree in new[] { 2, 10, 70 })
                {
                    data.Add(state, degree);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(StatesAndDegrees))]
    public void AnalyticPartials_MatchRiddersWithin1e8(string stateName, int degree)
    {
        // Arrange
        var earth = _bodies.Earths[degree];
        var state = State(stateName, earth);
        var force = new GravitationalAcceleration(earth);
        var dadr = new double[9];
        var dadv = new double[9];

        // Act
        force.AccumulateStatePartials(state, Context, dadr, dadv);

        // Assert: the whole block, and the harmonics alone
        double scale = state.Position.Magnitude();
        var total = RiddersDerivative.StatePartials(force, state, Context, 1e-3 * scale, 1.0);
        double totalError = RiddersDerivative.RelativeFrobeniusError(dadr, total.Dadr);
        var harmonics = Harmonics(dadr, state, earth.GM);
        var (expectedHarmonics, harmonicsReferenceError) = HarmonicReference(force, state, earth.GM, 1e-2 * scale);
        double harmonicsError = RiddersDerivative.RelativeFrobeniusError(harmonics, expectedHarmonics);
        _output.WriteLine($"{stateName}, degree {degree}, latitude {Latitude(state, earth):F2}°: whole block " +
                          $"{totalError:E2} (reference {total.DadrError:E2}), harmonics {harmonicsError:E2} " +
                          $"(reference {harmonicsReferenceError:E2})");
        // The references resolve the tolerance: measured 1.3e-12 at most on the whole block, and on the harmonics 1.1e-10
        // at most except at GEO, 2.0e-9, where they are 1e-5 of the central term and the difference that isolates them
        // loses five digits (measured again with the constants of #410, 2026-10-23)
        Assert.True(total.DadrError < 1e-10, $"reference error {total.DadrError:E2}");
        Assert.True(harmonicsReferenceError < 5e-9, $"harmonic reference error {harmonicsReferenceError:E2}");
        Assert.True(totalError < 1e-8, $"whole block: {totalError:E2}");
        Assert.True(harmonicsError < 1e-8, $"harmonics: {harmonicsError:E2}");
        Assert.All(dadv, x => Assert.Equal(0.0, x));
    }

    [Theory]
    [MemberData(nameof(StatesAndDegrees))]
    public void AnalyticPartials_AreSymmetricAndSatisfyLaplace(string stateName, int degree)
    {
        // Arrange
        var earth = _bodies.Earths[degree];
        var state = State(stateName, earth);
        var dadr = new double[9];

        // Act
        earth.TryAccumulateGravitationalPositionPartials(state, dadr);

        // Assert: symmetric bit for bit, and the trace of the harmonics (U_xx + U_yy + U_zz) at rounding
        Assert.Equal(dadr[1], dadr[3]);
        Assert.Equal(dadr[2], dadr[6]);
        Assert.Equal(dadr[5], dadr[7]);
        var harmonics = Harmonics(dadr, state, earth.GM);
        double trace = System.Math.Abs(harmonics[0] + harmonics[4] + harmonics[8]) / Frobenius(harmonics);
        _output.WriteLine($"{stateName}, degree {degree}: |trace| / ‖H‖ = {trace:E2}");
        Assert.True(trace < 1e-12, $"trace {trace:E2}");
    }

    [Fact]
    public void ReducedDegree_IsTheFullPartialsOfTheFieldOfThatDegree_BitForBit()
    {
        // Arrange
        var state = State(PartialsTestCases.Leo400, _bodies.Earths[70]);
        var reduced = new double[9];
        var expected = new double[9];

        // Act
        _bodies.Earths[70].TryAccumulateGravitationalPositionPartials(state, reduced, 10);
        _bodies.Earths[10].TryAccumulateGravitationalPositionPartials(
            new StateVector(state.Position, state.Velocity, _bodies.Earths[10], state.Epoch, state.Frame), expected);

        // Assert
        Assert.Equal(expected, reduced);
    }

    [Fact]
    public void ReducedDegree_AboveTheFieldDegree_IsTheFieldDegree()
    {
        // Arrange
        var earth = _bodies.Earths[10];
        var state = State(PartialsTestCases.Leo400, earth);
        var above = new double[9];
        var full = new double[9];

        // Act
        earth.TryAccumulateGravitationalPositionPartials(state, above, 20);
        earth.TryAccumulateGravitationalPositionPartials(state, full);

        // Assert
        Assert.Equal(full, above);
    }

    // Departure of the reduced block from degree 70 at LEO 400 km, relative to ‖G‖, as published on the provenance page
    private static readonly System.Collections.Generic.Dictionary<int, double> LeoPublishedDepartures = new()
    {
        { 2, 4.7e-5 }, { 8, 3.1e-5 }, { 20, 3.2e-5 }
    };

    [Theory]
    [InlineData(PartialsTestCases.Leo400, false)]
    [InlineData(PartialsTestCases.Geo, true)]
    public void ReducedDegree_DepartureFromTheFullDegree(string stateName, bool decreasing)
    {
        // Arrange: the difference to the full degree-70 partials, relative to the whole block. At GEO, (R / r)^n makes
        // the harmonics fall fast with the degree; at LEO 400 km the second derivatives, (n + 1)(n + 2) times larger
        // than the potential terms, keep a contribution of every degree up to 70 (measured: 4.7e-5, 3.1e-5 and 3.2e-5
        // at degrees 2, 8 and 20), so a reduced degree is not a smaller error there; its effect on the state transition
        // matrix is measured in F1
        var earth = _bodies.Earths[70];
        var state = State(stateName, earth);
        var full = new double[9];
        earth.TryAccumulateGravitationalPositionPartials(state, full);
        double previous = double.MaxValue;

        foreach (int degree in new[] { 2, 8, 20, 70 })
        {
            // Act
            var reduced = new double[9];
            earth.TryAccumulateGravitationalPositionPartials(state, reduced, degree);

            // Assert
            double difference = RiddersDerivative.RelativeFrobeniusError(reduced, full);
            _output.WriteLine($"{stateName}, degree {degree}: ‖G_d − G_70‖ / ‖G_70‖ = {difference:E2}");
            if (degree == 70)
            {
                Assert.Equal(0.0, difference);
            }
            else if (decreasing)
            {
                Assert.True(difference < previous, $"degree {degree}: {difference:E2}");
            }
            else
            {
                // The departures published on the provenance page, within a factor of 2
                double published = LeoPublishedDepartures[degree];
                Assert.True(difference > 0.5 * published && difference < 2.0 * published,
                    $"degree {degree}: {difference:E2}, published {published:E1}");
            }

            previous = difference;
        }
    }

    [Fact]
    public void AccelerationAndPartials_WithTheOrientationCacheOfAPropagation_AllocateNothing()
    {
        // Arrange: the propagation's case, the orientation cache of the Earth-fixed frame active on this thread; the
        // rotation is shared by the acceleration and the partials (Frame.RotationTo)
        var earth = _bodies.Earths[70];
        var state = State(PartialsTestCases.Leo400, earth);
        var cache = new PropagationFrameOrientationCache(
            new TimeSystem.Window(state.Epoch.AddHours(-1.0), state.Epoch.AddHours(1.0)), earth.Frame,
            TimeSpan.FromSeconds(60.0));
        var dadr = new double[9];

        using (Frames.Frame.UseOrientationCache(earth.Frame, cache))
        {
            earth.TryAccumulateGravitationalPositionPartials(state, dadr);
            _ = earth.EvaluateGravitationalAcceleration(state);

            // Act
            long before = GC.GetAllocatedBytesForCurrentThread();
            earth.TryAccumulateGravitationalPositionPartials(state, dadr);
            long partials = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            _ = earth.EvaluateGravitationalAcceleration(state);
            long acceleration = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert
            _output.WriteLine($"partials {partials} bytes, acceleration {acceleration} bytes");
            Assert.Equal(0, partials);
            Assert.Equal(0, acceleration);
        }
    }

    [Fact]
    public void AboveTheGuardDegree_ThePartialsThrow_AndAReducedDegreeWorks()
    {
        // Arrange
        var earth = _bodies.Earths[GeopotentialGravitationalField.MaxPartialsDegree + 10];
        var state = State(PartialsTestCases.Leo400, earth);

        // Act and assert
        var exception = Assert.Throws<NotSupportedException>(() =>
            earth.TryAccumulateGravitationalPositionPartials(state, new double[9]));
        Assert.Contains("GeopotentialPartialsDegree", exception.Message);
        Assert.Throws<NotSupportedException>(() => earth.ValidateGravitationalPartialsDegree(null));
        earth.ValidateGravitationalPartialsDegree(GeopotentialGravitationalField.MaxPartialsDegree);
        var dadr = new double[9];
        Assert.True(earth.TryAccumulateGravitationalPositionPartials(state, dadr,
            GeopotentialGravitationalField.MaxPartialsDegree));
        Assert.All(dadr, x => Assert.True(double.IsFinite(x)));
    }

    [Fact]
    public void AtTheGuardDegree_TheTablesStayWithinTheirMeasuredBounds()
    {
        // Arrange: on the equator at the equatorial radius, where V_mm is largest outside the body, and a field whose
        // coefficients beyond 70 are zeros, so that every table entry is still computed
        int guard = GeopotentialGravitationalField.MaxPartialsDegree;
        var earth = _bodies.Earths[guard];
        var field = new GeopotentialGravitationalField(new StreamReader(GeopotentialPartialsBodies.Model),
            (ushort)guard);
        var state = new StateVector(new Vector3(earth.EquatorialRadius, 0.0, 0.0), new Vector3(0.0, 7.9e3, 0.0),
            earth, PartialsTestCases.Epoch, earth.Frame);
        var dadr = new double[9];

        // Act
        field.TryAccumulatePositionPartials(state, dadr);
        double largestFunction = field.LargestCunninghamFunction();
        double smallestFactor = double.MaxValue;
        for (int m = 0; m <= guard; m++)
        {
            smallestFactor = System.Math.Min(smallestFactor,
                GeopotentialGravitationalField.NormalizationFactor(guard, m));
        }

        // Assert: more than ten orders of magnitude from the range of a double on both sides
        _output.WriteLine($"degree {guard}: largest |V|, |W| {largestFunction:E2}, smallest N_nm {smallestFactor:E2}");
        Assert.True(largestFunction < 1e250, $"largest function {largestFunction:E2}");
        Assert.True(smallestFactor > 1e-250, $"smallest factor {smallestFactor:E2}");
        Assert.All(dadr, x => Assert.True(double.IsFinite(x)));
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 2)]
    [InlineData(10, 7)]
    [InlineData(40, 40)]
    [InlineData(70, 35)]
    [InlineData(80, 80)]
    public void NormalizationFactor_ConvertsTheUnnormalizedLegendreFunctionsToTheFieldNormalization(int n, int m)
    {
        // Arrange: the geodesy normalization of the field, N_nm P_nm = P̄_nm
        double colatitude = 0.7;

        // Act
        double factor = GeopotentialGravitationalField.NormalizationFactor(n, m);

        // Assert
        double expected = IO.Astrodynamics.Math.LegendreFunctions.NormalizedAssociatedLegendre(n, m, colatitude);
        double actual = factor * IO.Astrodynamics.Math.LegendreFunctions.AssociatedLegendre(n, m,
            System.Math.Cos(colatitude));
        Assert.True(System.Math.Abs(actual - expected) <= 1e-12 * System.Math.Abs(expected),
            $"N P = {actual:E17}, P̄ = {expected:E17}");
    }

    [Fact]
    public void StateInAnotherFrame_GivesTheRotatedPartials()
    {
        // Arrange: the same state in ICRF and in the Earth-fixed frame
        var earth = _bodies.Earths[10];
        var inertial = State(PartialsTestCases.Leo800, earth);
        var fixedState = inertial.ToFrame(earth.Frame).ToStateVector();
        var inertialPartials = new double[9];
        var fixedPartials = new double[9];

        // Act
        earth.TryAccumulateGravitationalPositionPartials(inertial, inertialPartials);
        earth.TryAccumulateGravitationalPositionPartials(fixedState, fixedPartials);

        // Assert: R G_inertial Rᵀ = G_fixed, R from ICRF to the Earth-fixed frame
        var rotation = inertial.Frame.ToFrame(earth.Frame, inertial.Epoch).Rotation;
        var columns = new[] { Vector3.VectorX.Rotate(rotation), Vector3.VectorY.Rotate(rotation),
            Vector3.VectorZ.Rotate(rotation) };
        var rotated = new double[9];
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                double sum = 0.0;
                for (int a = 0; a < 3; a++)
                {
                    for (int b = 0; b < 3; b++)
                    {
                        sum += RiddersDerivative.Component(columns[a], i) * inertialPartials[3 * a + b] *
                               RiddersDerivative.Component(columns[b], j);
                    }
                }

                rotated[3 * i + j] = sum;
            }
        }

        double error = RiddersDerivative.RelativeFrobeniusError(rotated, fixedPartials);
        _output.WriteLine($"‖R G Rᵀ − G_fixed‖ / ‖G_fixed‖ = {error:E2}");
        Assert.True(error < 1e-14, $"{error:E2}");
    }

    /// <summary>
    /// The named state of B7, or a LEO 400 km state at 87° or 89.9° of declination, relative to
    /// <paramref name="earth"/>.
    /// </summary>
    internal static StateVector State(string name, CelestialBody earth)
    {
        const double deg = IO.Astrodynamics.Constants.Deg2Rad;
        double declination = name switch
        {
            Polar87 => 87.0,
            Polar899 => 89.9,
            _ => double.NaN
        };
        if (double.IsNaN(declination))
        {
            return PartialsTestCases.State(name, earth);
        }

        // A polar orbit at its argument of latitude, at perigee
        return new KeplerianElements(6778137.0, 0.0005, 90.0 * deg, 20.0 * deg, declination * deg, 0.0, earth,
            PartialsTestCases.Epoch, Frames.Frame.ICRF).ToStateVector();
    }

    private static double Latitude(StateVector state, CelestialBody earth)
    {
        var position = state.ToFrame(earth.Frame).ToStateVector().Position;
        return System.Math.Asin(position.Z / position.Magnitude()) * IO.Astrodynamics.Constants.Rad2Deg;
    }

    /// <summary>
    /// The block less the point-mass partials.
    /// </summary>
    private static double[] Harmonics(double[] dadr, StateVector state, double gm)
    {
        var pointMass = new double[9];
        new GravitationalField().TryAccumulatePositionPartials(state, pointMass);
        var harmonics = new double[9];
        for (int k = 0; k < 9; k++)
        {
            harmonics[k] = dadr[k] - pointMass[k];
        }

        return harmonics;
    }

    /// <summary>
    /// Ridders' derivative of the acceleration less the point-mass acceleration −GM r / |r|³, with its relative error
    /// estimate.
    /// </summary>
    /// <remarks>
    /// The initial step, 1e-2 |r|, is ten times that of the whole block: the difference of the two accelerations loses
    /// the digits of the central term, so the extrapolation needs larger steps to keep its rounding error low; at GEO
    /// the estimate falls from 1.2e-8 to 2.0e-9 with it.
    /// </remarks>
    private static (double[] Dadr, double Error) HarmonicReference(ForceBase force, StateVector state, double gm,
        double step)
    {
        var dadr = new double[9];
        var errors = new double[9];
        for (int j = 0; j < 3; j++)
        {
            int column = j;
            for (int i = 0; i < 3; i++)
            {
                int row = i;
                (dadr[3 * i + j], errors[3 * i + j]) = RiddersDerivative.Derivative(t =>
                {
                    var position = With(state.Position, column, t);
                    var perturbed = new StateVector(position, state.Velocity, state.Observer, state.Epoch,
                        state.Frame);
                    var pointMass = position * (-gm / System.Math.Pow(position.Magnitude(), 3.0));
                    return RiddersDerivative.Component(force.Apply(perturbed, Context) - pointMass, row);
                }, RiddersDerivative.Component(state.Position, column), step);
            }
        }

        return (dadr, Frobenius(errors) / Frobenius(dadr));
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

    private static double Frobenius(double[] matrix)
    {
        double sum = 0.0;
        foreach (double x in matrix)
        {
            sum += x * x;
        }

        return System.Math.Sqrt(sum);
    }
}
