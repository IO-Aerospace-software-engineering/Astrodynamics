using IO.Astrodynamics.Body;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers.Lambert;

public class LambertSolverTests
{
    private class DummyCelestialItem : CelestialItem
    {
        public DummyCelestialItem() : base(10, "Dummy", 1.9884E+30, null) { }

    }

    private static Astrodynamics.OrbitalParameters.OrbitalParameters CreateStateVector(Vector3 position, Vector3 velocity)
    {
        var op = new StateVector(position, velocity, new DummyCelestialItem(), TimeSystem.Time.J2000TDB, Frames.Frame.ICRF);
        return op;
    }

    [Fact]
    public void Solve_ThrowsOnNullArguments()
    {
        var solver = new LambertSolver();
        var dummyBody = new DummyCelestialItem(398600.4418);
        var op = CreateStateVector(new Vector3(1, 0, 0), new Vector3(0, 1, 0));

        Assert.Throws<ArgumentNullException>(() => solver.Solve(false, null, op, dummyBody, 0));
        Assert.Throws<ArgumentNullException>(() => solver.Solve(false, op, null, dummyBody, 0));
        Assert.Throws<ArgumentNullException>(() => solver.Solve(false, op, op, null, 0));
    }

    [Fact]
    public void Solve_ThrowsOnNonPositiveTimeOfFlight()
    {
        var solver = new LambertSolver();
        var dummyBody = new DummyCelestialItem(398600.4418);
        var now = DateTime.UtcNow;
        var op1 = CreateStateVector(new Vector3(1, 0, 0), new Vector3(0, 1, 0), now);
        var op2 = CreateStateVector(new Vector3(0, 1, 0), new Vector3(-1, 0, 0), now.AddSeconds(-10));

        Assert.Throws<ArgumentException>(() => solver.Solve(false, op1, op2, dummyBody, 0));
    }

    [Fact]
    public void Solve_ThrowsOnNonPositiveGM()
    {
        var solver = new LambertSolver();
        var dummyBody = new DummyCelestialItem(0);
        var now = DateTime.UtcNow;
        var op1 = CreateStateVector(new Vector3(1, 0, 0), new Vector3(0, 1, 0), now);
        var op2 = CreateStateVector(new Vector3(0, 1, 0), new Vector3(-1, 0, 0), now.AddSeconds(1000));

        Assert.Throws<ArgumentException>(() => solver.Solve(false, op1, op2, dummyBody, 0));
    }

    [Fact]
    public void Solve_ReturnsLambertResult_WithZeroRevolutionSolution()
    {
        var solver = new LambertSolver();
        var dummyBody = new DummyCelestialItem(398600.4418);
        var now = DateTime.UtcNow;
        var op1 = CreateStateVector(new Vector3(7000, 0, 0), new Vector3(0, 7.5, 0), now);
        var op2 = CreateStateVector(new Vector3(0, 7000, 0), new Vector3(-7.5, 0, 0), now.AddSeconds(3600));

        var result = solver.Solve(false, op1, op2, dummyBody, 0);

        Assert.NotNull(result);
        var zeroRev = result.GetZeroRevolutionSolution();
        Assert.NotNull(zeroRev);
        Assert.Equal(0u, zeroRev.Revolutions);
        Assert.NotNull(zeroRev.V1);
        Assert.NotNull(zeroRev.V2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Solve_HandlesRetrogradeAndPrograde(bool isRetrograde)
    {
        var solver = new LambertSolver();
        var dummyBody = new DummyCelestialItem(398600.4418);
        var now = DateTime.UtcNow;
        var op1 = CreateStateVector(new Vector3(8000, 0, 0), new Vector3(0, 7.0, 0), now);
        var op2 = CreateStateVector(new Vector3(0, 8000, 0), new Vector3(-7.0, 0, 0), now.AddSeconds(4000));

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
        var dummyBody = new DummyCelestialItem(398600.4418);
        var now = DateTime.UtcNow;
        var op1 = CreateStateVector(new Vector3(10000, 0, 0), new Vector3(0, 6.5, 0), now);
        var op2 = CreateStateVector(new Vector3(0, 10000, 0), new Vector3(-6.5, 0, 0), now.AddSeconds(20000));

        var result = solver.Solve(false, op1, op2, dummyBody, 2);

        Assert.NotNull(result);
        Assert.True(result.Solutions.Count > 1);
        foreach (var sol in result.Solutions)
        {
            Assert.NotNull(sol.V1);
            Assert.NotNull(sol.V2);
        }
    }

    [Fact]
    public void HalleySolver_ConvergesForSimpleCase()
    {
        var solver = new LambertSolver();
        // Set m_lambda to a reasonable value for the test
        typeof(LambertSolver).GetField("m_lambda", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .SetValue(solver, 0.5);
        double T = 1.0;
        double x0 = 0.1;
        int N = 0;
        double eps = 1e-5;
        int iterMax = 10;

        var (x, iters) = (ValueTuple<double, int>)typeof(LambertSolver)
            .GetMethod("HalleySolver", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(solver, new object[] { T, x0, N, eps, iterMax });

        Assert.InRange(x, -1.0, 1.0);
        Assert.InRange(iters, 1, iterMax);
    }
}