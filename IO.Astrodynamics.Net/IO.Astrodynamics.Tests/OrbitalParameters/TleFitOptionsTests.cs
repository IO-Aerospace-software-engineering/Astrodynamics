// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using System.Reflection;
using IO.Astrodynamics.OrbitalParameters.TLE;
using Xunit;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

/// <summary>
/// The shape of the TLE fit (decision 113 of #668, decided by S. Guillet, 2026-11-26): <c>ToTle(name, noradId, cosparId,
/// options = null)</c> in the order of <see cref="TLE.Create"/>, its other fields and its settings in the D1 record
/// <see cref="TleFitOptions"/>, whose defaults are those of the record <c>TLE.Configuration</c> it replaces (revolution
/// number 0, unclassified, B* 0.0001, ṅ/2 and n̈/6 0, element set 999, 15 iterations), and whose unread <c>Tolerance</c> is
/// removed (#833).
/// </summary>
public class TleFitOptionsTests
{
    private static readonly string[] IssLines =
    [
        "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
        "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703"
    ];

    public TleFitOptionsTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    [Fact]
    public void ToTle_TakesTheIdentifiersInTheOrderOfCreate_ThenOptionalOptions()
    {
        var method = typeof(Astrodynamics.OrbitalParameters.OrbitalParameters).GetMethod("ToTle");
        Assert.NotNull(method);
        var parameters = method.GetParameters();
        Assert.Equal(["name", "noradId", "cosparId", "options"], parameters.Select(p => p.Name));
        Assert.Equal([typeof(string), typeof(int), typeof(string), typeof(TleFitOptions)],
            parameters.Select(p => p.ParameterType));
        Assert.True(parameters[3].IsOptional);
        Assert.Null(parameters[3].DefaultValue);
    }

    [Fact]
    public void TleFitOptions_IsASealedRecord_WithoutTolerance_AndAnIntMaxIterations()
    {
        var type = typeof(TleFitOptions);
        Assert.True(type.IsSealed);
        Assert.NotNull(type.GetMethod("<Clone>$"));
        Assert.Null(type.GetProperty("Tolerance"));
        Assert.Equal(typeof(int), type.GetProperty(nameof(TleFitOptions.MaxIterations))!.PropertyType);
        Assert.Null(typeof(TLE).Assembly.GetType("IO.Astrodynamics.OrbitalParameters.TLE.Configuration"));
    }

    [Fact]
    public void Default_EqualsNew_WithTheDefaultsOfTheFormerConfiguration()
    {
        var options = TleFitOptions.Default;
        Assert.Equal(new TleFitOptions(), options);
        Assert.Equal(0, options.RevolutionNumberAtEpoch);
        Assert.Equal(Classification.Unclassified, options.Classification);
        Assert.Equal(0.0001, options.BStar);
        Assert.Equal(0.0, options.MeanMotionDot);
        Assert.Equal(0.0, options.MeanMotionDDot);
        Assert.Equal(TLE.DefaultElementSetNumber, options.ElementSetNumber);
        Assert.Equal(15, options.MaxIterations);
    }

    [Fact]
    public void ToTle_WithoutOptions_IsTheFitOfTheFormerDefaults_BitForBit()
    {
        var state = new TLE("ISS", IssLines[0], IssLines[1]).ToOsculating();
        var former = new TleFitOptions
        {
            RevolutionNumberAtEpoch = 0, Classification = Classification.Unclassified, BStar = 0.0001, MeanMotionDot = 0.0,
            MeanMotionDDot = 0.0, ElementSetNumber = 999, MaxIterations = 15
        };

        var withoutOptions = state.ToTle("ISS", 25544, "98067A");
        var withNull = state.ToTle("ISS", 25544, "98067A", null);
        var withFormer = state.ToTle("ISS", 25544, "98067A", former);

        Assert.Equal(withFormer.Line1, withoutOptions.Line1);
        Assert.Equal(withFormer.Line2, withoutOptions.Line2);
        Assert.Equal(withFormer.Line1, withNull.Line1);
        Assert.Equal(withFormer.Line2, withNull.Line2);
    }

    [Fact]
    public void ToTle_WritesTheFieldsOfItsOptions()
    {
        var state = new TLE("ISS", IssLines[0], IssLines[1]).ToOsculating();
        var options = new TleFitOptions
        {
            RevolutionNumberAtEpoch = 25703, Classification = Classification.Secret, BStar = 0.0001027,
            MeanMotionDot = 0.00016717, ElementSetNumber = 905
        };

        var tle = state.ToTle("ISS (ZARYA)", 25544, "98067A", options);

        Assert.Equal("ISS (ZARYA)", tle.Name);
        Assert.Equal(25544, tle.NoradCatalogId);
        Assert.Equal('S', tle.Line1[7]);
        Assert.Equal(" .00016717", tle.Line1.Substring(33, 10));
        Assert.Equal(" 10270-3", tle.Line1.Substring(53, 8));
        Assert.Equal(" 905", tle.Line1.Substring(64, 4));
        Assert.Equal("25703", tle.Line2.Substring(63, 5));
    }

    [Fact]
    public void MaxIterations_Negative_Throws()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new TleFitOptions { MaxIterations = -1 });
        Assert.Equal(nameof(TleFitOptions.MaxIterations), exception.ParamName);
        Assert.Equal(0, new TleFitOptions { MaxIterations = 0 }.MaxIterations);
    }
}
