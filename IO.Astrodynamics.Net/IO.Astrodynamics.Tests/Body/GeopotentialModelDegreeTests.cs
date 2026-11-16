// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Physics;
using IO.Astrodynamics.SolarSystemObjects;
using Xunit;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Body;

/// <summary>
/// The degree of a geopotential is checked against its model file (#689): a degree above the highest degree of the file
/// throws, and so does a file missing a coefficient (n, m) with 2 ≤ n ≤ degree; degrees 0 and 1 stay the point mass.
/// Before, the field padded every missing coefficient with zeros and ran silently at a lower degree, or without J2.
/// </summary>
public class GeopotentialModelDegreeTests
{
    public GeopotentialModelDegreeTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static readonly TimeSystem.Time Epoch = new(2021, 1, 1, 12, 0, 0);

    // 300 km above the reference radius of EGM2008, the radius of the issue
    private const double Radius = 6678137.0;

    private static CelestialBody Observer() =>
        new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch);

    private static StateVector State(CelestialBody observer, Vector3 position) =>
        new(position, new Vector3(0.0, 7.7e3, 0.0), observer, Epoch, Frames.Frame.ICRF);

    [Theory]
    [InlineData(71)]
    [InlineData(80)]
    [InlineData(100)]
    public void ADegreeAboveTheFile_Throws_OnEveryPath(int degree)
    {
        // Arrange: EGM2008_to70_TideFree holds the degrees 2 to 70

        // Act
        var byField = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GeopotentialGravitationalField(new StreamReader(GeopotentialTestModels.To70Path), (ushort)degree));
        var byPath = Assert.Throws<ArgumentOutOfRangeException>(() => new CelestialBody(PlanetsAndMoons.EARTH,
            Frames.Frame.ICRF, Epoch, new GeopotentialModelParameters(GeopotentialTestModels.To70Path, (ushort)degree)));
        var byStream = Assert.Throws<ArgumentOutOfRangeException>(() => new CelestialBody(PlanetsAndMoons.EARTH,
            Frames.Frame.ICRF, Epoch,
            new GeopotentialModelParameters(File.OpenRead(GeopotentialTestModels.To70Path), (ushort)degree)));

        // Assert: the requested degree and the highest degree of the file are named
        foreach (var exception in new[] { byField, byPath, byStream })
        {
            Assert.Equal("maxDegrees", exception.ParamName);
            Assert.Contains($"degree {degree}", exception.Message);
            Assert.Contains("70", exception.Message);
        }
    }

    [Fact]
    public void TheHighestDegreeOfTheFile_Builds()
    {
        // Act
        var field = new GeopotentialGravitationalField(new StreamReader(GeopotentialTestModels.To70Path), 70);

        // Assert
        Assert.Equal(70, field.MaxDegree);
    }

    [Fact]
    public void TheReader_RecordsTheHighestDegreeOfItsFile()
    {
        // Arrange: the rows of the file counted independently: Σ (n + 1) for n = 2 to 70 = 2553
        var lines = File.ReadAllLines(GeopotentialTestModels.To70Path);
        int rows = Array.FindAll(lines, l => !string.IsNullOrWhiteSpace(l)).Length;
        int expectedRows = 0;
        for (int n = 2; n <= 70; n++)
        {
            expectedRows += n + 1;
        }

        // Act
        var reader = new GeopotentialModelReader(new StreamReader(GeopotentialTestModels.To70Path));
        var empty = new GeopotentialModelReader(new StreamReader(new MemoryStream()));
        var padded = new GeopotentialModelReader(new StreamReader(GeopotentialTestModels.PaddedTo(130)));

        // Assert
        Assert.Equal(expectedRows, rows);
        Assert.Equal(2553, rows);
        Assert.Equal(70, reader.MaximumDegree);
        Assert.Equal(-1, empty.MaximumDegree);
        Assert.Equal(130, padded.MaximumDegree);
    }

    [Fact]
    public void AFileWithoutAHarmonic_Throws_NamingIt()
    {
        // Arrange: the (2, 0) row, J2, removed; before #689 the field built and was 3 J2 μ Re² / r⁴ = 2.648e-2 m/s²
        // off at the pole at 300 km

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new GeopotentialGravitationalField(new StreamReader(GeopotentialTestModels.Without((2, 0))), 10));
        var byBody = Assert.Throws<ArgumentException>(() => new CelestialBody(PlanetsAndMoons.EARTH,
            Frames.Frame.ICRF, Epoch, new GeopotentialModelParameters(GeopotentialTestModels.Without((2, 0)), 10)));

        // Assert: a bad argument, not the degree check
        Assert.IsNotType<ArgumentOutOfRangeException>(exception);
        foreach (var e in new[] { exception, byBody })
        {
            Assert.Contains("n = 2", e.Message);
            Assert.Contains("m = 0", e.Message);
            Assert.Contains("70", e.Message);
        }
    }

    [Fact]
    public void AFileWithoutAnOrder_Throws_NamingIt()
    {
        // Arrange: a sectoral row at the highest degree used
        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            new GeopotentialGravitationalField(new StreamReader(GeopotentialTestModels.Without((10, 10))), 10));

        // Assert
        Assert.Contains("n = 10", exception.Message);
        Assert.Contains("m = 10", exception.Message);

        // A degree below the missing row does not read it
        var below = new GeopotentialGravitationalField(new StreamReader(GeopotentialTestModels.Without((10, 10))), 9);
        Assert.Equal(9, below.MaxDegree);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void DegreesZeroAndOne_AreThePointMass_WhateverTheFileHolds(int degree)
    {
        // Arrange: a file without J2 at a degree that reads no harmonic
        var observer = Observer();
        var partial = new GeopotentialGravitationalField(
            new StreamReader(GeopotentialTestModels.Without((2, 0))), (ushort)degree);
        var full = new GeopotentialGravitationalField(new StreamReader(GeopotentialTestModels.To70Path), (ushort)degree);
        var position = new Vector3(0.3, -0.4, 0.866).Normalize() * Radius;

        // Act
        var a = partial.ComputeGravitationalAcceleration(State(observer, position));
        var b = full.ComputeGravitationalAcceleration(State(observer, position));

        // Assert: bit for bit the field of the full file, and the closed form GM/r² along -r to the last bits
        Assert.Equal(b, a);
        var closedForm = position * (-observer.GM / (Radius * Radius * Radius));
        Assert.True((a - closedForm).Magnitude() <= 1e-15 * closedForm.Magnitude(),
            $"{(a - closedForm).Magnitude():E3}");
    }

    [Fact]
    public void ExplicitZeroRowsAboveTheModel_AreTheModelBitForBit()
    {
        // Arrange: a complete file of degree 80 whose rows beyond 70 are zeros, against the file of degree 70
        var observer = Observer();
        var to70 = new GeopotentialGravitationalField(new StreamReader(GeopotentialTestModels.To70Path), 70);
        var to80 = new GeopotentialGravitationalField(new StreamReader(GeopotentialTestModels.PaddedTo(80)), 80);
        var random = new Random(689);

        // Act and assert: zero coefficients add exact zeros to every sum
        for (int i = 0; i < 2000; i++)
        {
            var direction = new Vector3(random.NextDouble() * 2.0 - 1.0, random.NextDouble() * 2.0 - 1.0,
                random.NextDouble() * 2.0 - 1.0);
            if (direction.Magnitude() < 1e-3)
            {
                continue;
            }

            var state = State(observer, direction.Normalize() * Radius);
            Assert.Equal(to70.ComputeGravitationalAcceleration(state), to80.ComputeGravitationalAcceleration(state));
        }
    }
}
