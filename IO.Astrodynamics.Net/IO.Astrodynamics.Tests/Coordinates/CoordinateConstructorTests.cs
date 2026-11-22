// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Reflection;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Coordinates;

/// <summary>
/// The constructors of the coordinate records of R20 (#668, lot L3, PR R20-b): camelCase parameters (D2 N1), so
/// <c>new Planetodetic(longitude: lon, latitude: lat, altitude: 1e3)</c>, the design's example, compiles; the properties
/// keep their names, their init accessors (<c>with</c>) and their Deconstruct.
/// </summary>
public class CoordinateConstructorTests
{
    [Theory]
    [InlineData(typeof(Planetodetic), new[] { "longitude", "latitude", "altitude" })]
    [InlineData(typeof(Planetocentric), new[] { "longitude", "latitude", "radius" })]
    [InlineData(typeof(Horizontal), new[] { "azimuth", "elevation", "range", "epoch" })]
    public void PublicConstructor_TakesCamelCaseParameters_AndDeconstructGivesThemBack(Type type, string[] names)
    {
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(c => c.GetParameters().Length > 0).ToArray();
        // The constructor of the components, and no other (the record struct's copy constructor is private)
        var constructor = Assert.Single(constructors);
        Assert.Equal(names, constructor.GetParameters().Select(p => p.Name));

        var deconstruct = Assert.Single(type.GetMethods(BindingFlags.Public | BindingFlags.Instance), m => m.Name == "Deconstruct");
        Assert.Equal(names, deconstruct.GetParameters().Select(p => p.Name));

        // Each property is named after its parameter in PascalCase and keeps its init accessor
        foreach (var name in names)
        {
            var property = type.GetProperty(char.ToUpperInvariant(name[0]) + name[1..], BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property);
            Assert.NotNull(property!.SetMethod);
            Assert.Contains(property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers(),
                m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");
        }
    }

    [Fact]
    public void NamedArguments_InitAndDeconstruct_KeepTheValues()
    {
        var site = new Planetodetic(longitude: 0.1, latitude: -0.2, altitude: 1e3);
        Assert.Equal(new Planetodetic(0.1, -0.2, 1e3), site);
        var (longitude, latitude, altitude) = site;
        Assert.Equal((0.1, -0.2, 1e3), (longitude, latitude, altitude));
        Assert.Equal(2e3, (site with { Altitude = 2e3 }).Altitude);
        Assert.Equal("Planetodetic { Longitude = 0.1, Latitude = -0.2, Altitude = 1000 }", site.ToString());

        var planetocentric = new Planetocentric(longitude: 0.3, latitude: 0.4, radius: 7e6);
        var (lon, lat, radius) = planetocentric;
        Assert.Equal((0.3, 0.4, 7e6), (lon, lat, radius));
        Assert.Equal(0.5, (planetocentric with { Latitude = 0.5 }).Latitude);

        var epoch = new TimeSystem.Time(2024, 6, 21, 12, 0, 0, TimeScale.TDB);
        var horizontal = new Horizontal(azimuth: 1.0, elevation: 0.5, range: 2e6, epoch: epoch);
        var (azimuth, elevation, range, at) = horizontal;
        Assert.Equal((1.0, 0.5, 2e6), (azimuth, elevation, range));
        Assert.True(at.EqualsExact(epoch));
        Assert.Equal(0.25, (horizontal with { Elevation = 0.25 }).Elevation);
        Assert.Equal(default(Planetodetic), new Planetodetic());
    }
}
