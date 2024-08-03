// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Physics;
using Xunit;

namespace IO.Astrodynamics.Tests.Physics;

public class GeopotentialCoefficentTests
{
    [Fact]
    public void Equality()
    {
        var geo1 = new GeopotentialCoefficient(7, 2, 3, 4, 5, 6);
        var geo2 = new GeopotentialCoefficient(7, 2, 3, 4, 5, 6);
        Assert.Equal(geo1, geo2);
        Assert.True(geo1 == geo2);
        Assert.True(geo1.Equals(geo2));
        Assert.True(geo1.Equals(geo1));
        Assert.False(geo1.Equals(null));
        Assert.True(geo1.Equals((object)geo2));
        Assert.False(geo1.Equals((object)null));
        Assert.True(geo1.Equals((object)geo1));
        Assert.False(geo1.Equals((object)"geo1"));
        Assert.False(geo1 != geo2);
        Assert.Equal(geo1.GetHashCode(), geo2.GetHashCode());
    }

    [Fact]
    public void InvalidConstructor()
    {
        Assert.Throws<ArgumentException>(() => new GeopotentialCoefficient(1, 2, 3, 4, 5, 6));
    }
}