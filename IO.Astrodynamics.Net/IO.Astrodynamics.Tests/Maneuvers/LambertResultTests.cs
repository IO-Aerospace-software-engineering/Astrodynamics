using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Maneuver.Lambert;
using IO.Astrodynamics.Math;
using Xunit;

namespace IO.Astrodynamics.Tests.Maneuvers.Lambert
{
    public class LambertResultTests
    {
        private LambertSolution CreateLambertSolution(uint revolutions)
        {
            var v1 = new Vector3(1.0, 2.0, 3.0);
            var v2 = new Vector3(4.0, 5.0, 6.0);
            double x = 0.5;
            uint iterations = 10;
            LambertBranch? branch = revolutions == 0 ? null : LambertBranch.Left;
            return new LambertSolution(revolutions, v1, v2, x, iterations, branch);
        }

        [Fact]
        public void Constructor_InitializesProperties()
        {
            ushort maxRevolutions = 2;
            var result = new LambertResult(maxRevolutions);

            Assert.Equal(maxRevolutions, result.MaxRevolutions);
            Assert.NotNull(result.Solutions);
            Assert.Empty(result.Solutions);
        }

        [Fact]
        public void AddSolution_AddsSolutionToCollection()
        {
            var result = new LambertResult(1);
            var solution = CreateLambertSolution(0);

            result.AddSolution(solution);

            Assert.Single(result.Solutions);
            Assert.Contains(solution, result.Solutions);
        }

        [Fact]
        public void GetZeroRevolutionSolution_ReturnsCorrectSolution()
        {
            var result = new LambertResult(2);
            var sol0 = CreateLambertSolution(0);
            var sol1 = CreateLambertSolution(1);

            result.AddSolution(sol1);
            result.AddSolution(sol0);

            var found = result.GetZeroRevolutionSolution();
            Assert.Equal(sol0, found);
        }

        [Fact]
        public void GetZeroRevolutionSolution_ReturnsNullIfNotFound()
        {
            var result = new LambertResult(1);
            var sol1 = CreateLambertSolution(1);
            result.AddSolution(sol1);

            var found = result.GetZeroRevolutionSolution();
            Assert.Null(found);
        }

        [Fact]
        public void GetMultiRevolutionSolutions_ReturnsCorrectSolutions()
        {
            var result = new LambertResult(3);
            var sol0 = CreateLambertSolution(0);
            var sol1a = CreateLambertSolution(1);
            var sol1b = CreateLambertSolution(1);
            var sol2 = CreateLambertSolution(2);

            result.AddSolution(sol0);
            result.AddSolution(sol1a);
            result.AddSolution(sol1b);
            result.AddSolution(sol2);

            var found = result.GetMultiRevolutionSolutions(1).ToList();
            Assert.Equal(2, found.Count);
            Assert.All(found, s => Assert.Equal((uint)1, s.Revolutions));
        }
    }
}
