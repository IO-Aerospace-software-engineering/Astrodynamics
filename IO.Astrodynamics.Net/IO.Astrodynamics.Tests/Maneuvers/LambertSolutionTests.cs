using Xunit;
using IO.Astrodynamics.Maneuver.Lambert;
using IO.Astrodynamics.Math;

namespace IO.Astrodynamics.Tests.Maneuvers.Lambert;

public class LambertSolutionTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsPropertiesCorrectly()
    {
        // Arrange
        uint revolutions = 2;
        var v1 = new Vector3(1.0, 2.0, 3.0);
        var v2 = new Vector3(4.0, 5.0, 6.0);
        double x = 42.0;
        uint iterations = 7;
        LambertBranch branch = LambertBranch.Right;

        // Act
        var solution = new LambertSolution(revolutions, v1, v2, x, iterations, v1, v2, branch);

        // Assert
        Assert.Equal(revolutions, solution.Revolutions);
        Assert.Equal(branch, solution.Branch);
        Assert.Equal(v1, solution.V1);
        Assert.Equal(v1, solution.DeltaV1);
        Assert.Equal(v2, solution.V2);
        Assert.Equal(v2, solution.DeltaV2);
        Assert.Equal(x, solution.X);
        Assert.Equal(iterations, solution.Iterations);
    }

    [Fact]
    public void Constructor_DirectTransfer_SetsRevolutionsZeroAndBranchNull()
    {
        // Arrange
        var v1 = new Vector3(1.0, 0.0, 0.0);
        var v2 = new Vector3(0.0, 1.0, 0.0);
        double x = 3.14;
        uint iterations = 2;

        // Act
        var solution = new LambertSolution(v1, v2, x, iterations, v1, v2);

        // Assert
        Assert.Equal(0u, solution.Revolutions);
        Assert.Null(solution.Branch);
        Assert.Equal(v1, solution.V1);
        Assert.Equal(v1, solution.DeltaV1);
        Assert.Equal(v2, solution.V2);
        Assert.Equal(v2, solution.DeltaV2);
        Assert.Equal(x, solution.X);
        Assert.Equal(iterations, solution.Iterations);
    }

    [Fact]
    public void LambertBranch_Enum_HasLeftAndRightValues()
    {
        Assert.Equal(0, (int)LambertBranch.Left);
        Assert.Equal(1, (int)LambertBranch.Right);
    }
    
    [Fact]
    public void LambertSolution_Constructor_SetsDeltaVProperties()
    {
        // Arrange
        var v1 = new Vector3(1.0, 2.0, 3.0);
        var v2 = new Vector3(4.0, 5.0, 6.0);
        double x = 42.0;
        uint iterations = 7;

        // Act
        var solution = new LambertSolution(0, v1, v2, x, iterations, v1, v2);

        // Assert
        Assert.Equal(v1, solution.DeltaV1);
        Assert.Equal(v2, solution.DeltaV2);
        Assert.Equal(v1.Magnitude() + v2.Magnitude(), solution.DeltaV);
    }
}