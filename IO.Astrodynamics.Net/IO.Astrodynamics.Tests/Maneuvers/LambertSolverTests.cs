using IO.Astrodynamics.Body;
using IO.Astrodynamics.Maneuver.Lambert;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using System;
using System.Net.Http.Headers;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers.Lambert;

class DummyBody : CelestialItem
{
    public DummyBody(double mass) : base(999, "dummy", 0, null)
    {
    }
}

public class LambertSolverTests
{
    public LambertSolverTests()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    private static Astrodynamics.OrbitalParameters.OrbitalParameters CreateStateVector(Vector3 position, Vector3 velocity, in TimeSystem.Time epoch)
    {
        var op = new StateVector(position, velocity, TestHelpers.EarthAtJ2000, epoch, Frames.Frame.ICRF);
        return op;
    }

    [Fact]
    public void Solve_ThrowsOnNullArguments()
    {
        var solver = new LambertSolver();
        var dummyBody = TestHelpers.EarthAtJ2000;
        var op = CreateStateVector(new Vector3(1, 0, 0), new Vector3(0, 1, 0), TimeSystem.Time.J2000TDB);

        Assert.Throws<ArgumentNullException>(() => solver.Solve(false, null, op, dummyBody, 0));
        Assert.Throws<ArgumentNullException>(() => solver.Solve(false, op, null, dummyBody, 0));
        Assert.Throws<ArgumentNullException>(() => solver.Solve(false, op, op, null, 0));
    }

    [Fact]
    public void Solve_ThrowsOnNonPositiveTimeOfFlight()
    {
        var solver = new LambertSolver();
        var dummyBody = TestHelpers.EarthAtJ2000;
        var now = TimeSystem.Time.J2000TDB;
        var op1 = CreateStateVector(new Vector3(1, 0, 0), new Vector3(0, 1, 0), now);
        var op2 = CreateStateVector(new Vector3(0, 1, 0), new Vector3(-1, 0, 0), now.AddSeconds(-10));

        Assert.Throws<ArgumentException>(() => solver.Solve(false, op1, op2, dummyBody, 0));
    }

    [Fact]
    public void Solve_ThrowsOnNonPositiveGM()
    {
        var solver = new LambertSolver();
        var dummyBody = new DummyBody(0); // Create a dummy body with zero mass
        var now = TimeSystem.Time.J2000TDB;
        var op1 = CreateStateVector(new Vector3(1, 0, 0), new Vector3(0, 1, 0), now);
        var op2 = CreateStateVector(new Vector3(0, 1, 0), new Vector3(-1, 0, 0), now.AddSeconds(1000));

        Assert.Throws<ArgumentException>(() => solver.Solve(false, op1, op2, dummyBody, 0));
    }

    [Fact]
    public void Solve_ReturnsLambertResult_WithZeroRevolutionSolution()
    {
        var solver = new LambertSolver();
        var dummyBody = TestHelpers.EarthAtJ2000;
        var now = TimeSystem.Time.J2000TDB;
        var op1 = CreateStateVector(new Vector3(7000, 0, 0), new Vector3(0, 7.5, 0), now);
        var op2 = CreateStateVector(new Vector3(0, 7000, 0), new Vector3(-7.5, 0, 0), now.AddSeconds(3600));

        var result = solver.Solve(false, op1, op2, dummyBody, 0);

        Assert.NotNull(result);
        var zeroRev = result.GetZeroRevolutionSolution();
        Assert.NotNull(zeroRev);
        Assert.Equal(0u, zeroRev.Revolutions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Solve_HandlesRetrogradeAndPrograde(bool isRetrograde)
    {
        var solver = new LambertSolver();
        var dummyBody = TestHelpers.EarthAtJ2000;
        var now = TimeSystem.Time.J2000TDB;
        var op1 = CreateStateVector(new Vector3(8000000.0, 0, 0), new Vector3(0, 7000, 0), now);
        var op2 = CreateStateVector(new Vector3(0, 8000000.0, 0), new Vector3(-7000, 0, 0), now.AddSeconds(4000));

        var result = solver.Solve(isRetrograde, op1, op2, dummyBody, 0);

        Assert.NotNull(result);
        var zeroRev = result.GetZeroRevolutionSolution();
        Assert.NotNull(zeroRev);
        Assert.Equal(0u, zeroRev.Revolutions);
    }

    [Fact]
    public void Solve_ReturnsMultiRevolutionSolutions()
    {
        var solver = new LambertSolver();
        var dummyBody = TestHelpers.EarthAtJ2000;
        var now = TimeSystem.Time.J2000TDB;
        var op1 = CreateStateVector(new Vector3(6800000.0, 0, 0), new Vector3(0, 8000.0, 0), now);
        var op2 = CreateStateVector(new Vector3(0, 6800000.0, 0), new Vector3(8000.0, 0, 0), now.AddSeconds(20000));

        var result = solver.Solve(false, op1, op2, dummyBody, 2);

        Assert.NotNull(result);
        Assert.True(result.Solutions.Count > 1);
        foreach (var sol in result.Solutions)
        {
            Assert.True(sol.Revolutions >= 0);
            Assert.True(sol.V1.Magnitude() > 0);
            Assert.True(sol.V2.Magnitude() > 0);
        }
    }

    [Fact]
    public void HalleySolver_ConvergesForSimpleCase()
    {
        var solver = new LambertSolver();
        // Set m_lambda to a reasonable value for the test
        double T = 1.0;
        double x0 = 0.1;
        int N = 0;
        double eps = 1e-5;
        int iterMax = 10;
        double lambda = 0.5; // Example value for m_lambda

        var (x, iters) = (ValueTuple<double, int>)typeof(LambertSolver)
            .GetMethod("HalleySolver", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(solver, new object[] { T, x0, N, eps, iterMax, lambda });

        Assert.InRange(x, -1.0, 1.0);
        Assert.InRange(iters, 1, iterMax);
    }

    [Fact]
    public void Solve_EarthToMoon_ReturnsValidSolution()
    {
        var solver = new LambertSolver();
        var earth = TestHelpers.EarthAtJ2000;
        var arrivalTime = TimeSystem.Time.J2000UTC.AddDays(3);
        var op1 = new StateVector(new Vector3(7000000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var op2 = TestHelpers.MoonAtJ2000.GetEphemeris(arrivalTime, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        var result = solver.Solve(false, op1, op2, earth, 0);
        Assert.NotNull(result);
        Assert.True(result.Solutions.Count > 0);
        var zeroRev = result.GetZeroRevolutionSolution();
        Assert.NotNull(zeroRev);
        Assert.Equal(0u, zeroRev.Revolutions);
        Assert.True(zeroRev.V1.Magnitude() > 0);
        Assert.True(zeroRev.V2.Magnitude() > 0);
        Assert.Equal(10612.452251221264, zeroRev.V1.Magnitude(), 0.1);
        Assert.Equal(836.11866771492487, zeroRev.V2.Magnitude(), 0.1);

        var spcSV1 = new StateVector(new Vector3(7000000.0, 0.0, 0.0), zeroRev.V1, earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spcSVAtAfter3Days = spcSV1.AtEpoch(arrivalTime).ToStateVector();
        Assert.Equal(op2.Position, spcSVAtAfter3Days.Position, TestHelpers.VectorComparer);
    }

    [Fact]
    public void Solve_RetrogradeEarthToMoon_ReturnsValidSolution()
    {
        var solver = new LambertSolver();
        var earth = TestHelpers.EarthAtJ2000;
        var moon = TestHelpers.MoonAtJ2000;
        var arrivalTime = TimeSystem.Time.J2000UTC.AddDays(3);
        var op1 = new StateVector(new Vector3(7000000.0, 0.0, 0.0), new Vector3(0.0, 8000.0, 0.0), earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var op2 = TestHelpers.MoonAtJ2000.GetEphemeris(arrivalTime, earth, Frames.Frame.ICRF, Aberration.None).ToStateVector();
        var result = solver.Solve(true, op1, op2, earth, 0);
        Assert.NotNull(result);
        Assert.True(result.Solutions.Count > 0);
        var zeroRev = result.GetZeroRevolutionSolution();
        Assert.NotNull(zeroRev);
        Assert.Equal(0u, zeroRev.Revolutions);
        Assert.True(zeroRev.V1.Magnitude() > 0);
        Assert.True(zeroRev.V2.Magnitude() > 0);
        Assert.Equal(10612.331907535654, zeroRev.V1.Magnitude(), 0.1);
        Assert.Equal(834.59136063948517, zeroRev.V2.Magnitude(), 0.1);

        var spcSV1 = new StateVector(new Vector3(7000000.0, 0.0, 0.0), zeroRev.V1, earth, TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        var spcSVAtAfter3Days = spcSV1.AtEpoch(arrivalTime).ToStateVector();
        // Assert that the position at arrival time matches the expected position
        Assert.Equal(op2.Position, spcSVAtAfter3Days.Position, TestHelpers.VectorComparer);
        
        // Assert that the velocity at arrival time matches the expected velocity
        Assert.Equal(op2.Velocity, spcSVAtAfter3Days.Velocity + zeroRev.DeltaV2, TestHelpers.VectorComparer);
        
        // Assert that the velocity at departure time matches the expected velocity
        Assert.Equal(zeroRev.DeltaV1, zeroRev.V1 - op1.Velocity, TestHelpers.VectorComparer);
    }
}