using IO.Astrodynamics.Coordinates;

namespace IO.Astrodynamics.Tests.Coordinates;

using System;
using Xunit;

public class HorizontalTests
{
    [Fact]
    public void Constructor_ShouldInitializePropertiesCorrectly()
    {
        // Arrange
        double azimuth = 45.0;
        double elevation = 30.0;
        double range = 1000.0;
        TimeSystem.Time epoch = TimeSystem.Time.J2000UTC;

        // Act
        var horizontal = new Horizontal(azimuth, elevation, range, epoch);

        // Assert
        Assert.Equal(azimuth, horizontal.Azimuth);
        Assert.Equal(elevation, horizontal.Elevation);
        Assert.Equal(range, horizontal.Range);
        Assert.Equal(epoch, horizontal.Epoch);
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenPropertiesAreEqual()
    {
        // Arrange
        double azimuth = 45.0;
        double elevation = 30.0;
        double range = 1000.0;
        TimeSystem.Time epoch = TimeSystem.Time.J2000UTC;

        var horizontal1 = new Horizontal(azimuth, elevation, range, epoch);
        var horizontal2 = new Horizontal(azimuth, elevation, range, epoch);

        // Act & Assert
        Assert.True(horizontal1.Equals(horizontal2));
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenPropertiesDiffer()
    {
        // Arrange
        double azimuth1 = 45.0;
        double elevation1 = 30.0;
        double range1 = 1000.0;
        TimeSystem.Time epoch1 = TimeSystem.Time.J2000UTC;

        double azimuth2 = 50.0;
        double elevation2 = 35.0;
        double range2 = 1100.0;
        TimeSystem.Time epoch2 = TimeSystem.Time.J2000UTC.AddHours(1);

        var horizontal1 = new Horizontal(azimuth1, elevation1, range1, epoch1);
        var horizontal2 = new Horizontal(azimuth2, elevation2, range2, epoch2);

        // Act & Assert
        Assert.False(horizontal1.Equals(horizontal2));
    }

    [Fact]
    public void GetHashCode_ShouldReturnSameHashCode_WhenPropertiesAreEqual()
    {
        // Arrange
        double azimuth = 45.0;
        double elevation = 30.0;
        double range = 1000.0;
        TimeSystem.Time epoch = TimeSystem.Time.J2000UTC;

        var horizontal1 = new Horizontal(azimuth, elevation, range, epoch);
        var horizontal2 = new Horizontal(azimuth, elevation, range, epoch);

        // Act
        var hashCode1 = horizontal1.GetHashCode();
        var hashCode2 = horizontal2.GetHashCode();

        // Assert
        Assert.Equal(hashCode1, hashCode2);
    }

    [Fact]
    public void ToString_ShouldReturnExpectedFormat()
    {
        // Arrange
        double azimuth = 45.0;
        double elevation = 30.0;
        double range = 1000.0;
        TimeSystem.Time epoch = TimeSystem.Time.J2000UTC;

        var horizontal = new Horizontal(azimuth, elevation, range, epoch);

        // Act
        var result = horizontal.ToString();

        // Assert
        var expected = $"Horizontal {{ Azimuth = {azimuth}, Elevation = {elevation}, Range = {range}, Epoch = {epoch} }}";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Deconstruct_ShouldReturnCorrectValues()
    {
        // Arrange
        double azimuth = 45.0;
        double elevation = 30.0;
        double range = 1000.0;
        TimeSystem.Time epoch = TimeSystem.Time.J2000UTC;

        var horizontal = new Horizontal(azimuth, elevation, range, epoch);

        // Act
        var (deconstructedAzimuth, deconstructedElevation, deconstructedRange, deconstructedEpoch) = horizontal;

        // Assert
        Assert.Equal(azimuth, deconstructedAzimuth);
        Assert.Equal(elevation, deconstructedElevation);
        Assert.Equal(range, deconstructedRange);
        Assert.Equal(epoch, deconstructedEpoch);
    }
}
