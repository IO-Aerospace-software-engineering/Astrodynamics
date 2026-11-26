// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Reflection;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Ssa;
using Xunit;

namespace IO.Astrodynamics.Tests.Propagators.Options;

/// <summary>
/// The records of R06 of #668 (lot L4): <see cref="PropagationOptions"/>, <see cref="RK78IntegratorOptions"/>,
/// <see cref="VVIntegratorOptions"/>, <see cref="CovariancePropagationOptions.None"/> and the null rule of
/// <see cref="ConjunctionAnalysisOptions.CovariancePropagation"/> (structuring choice 3, decided by S. Guillet, 2026-11-19).
/// D1: a default equals new(), a one-property check runs in init and again through <c>with</c>, and D4.7: each default
/// equals its explicit settings, those of <c>new RK78Integrator()</c> for the integrator.
/// </summary>
public class PropagationOptionsRecordTests
{
    public PropagationOptionsRecordTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    [Fact]
    public void Defaults_EqualNew()
    {
        Assert.Equal(new PropagationOptions(), PropagationOptions.Default);
        Assert.Equal(new RK78IntegratorOptions(), RK78IntegratorOptions.Default);
        Assert.Equal(new VVIntegratorOptions(), VVIntegratorOptions.Default);
    }

    [Fact]
    public void PropagationOptionsDefault_IsRk78AtItsDefaults_60sOutputs_NoForce_ModelsOfTheBody_CovarianceAuto()
    {
        var options = PropagationOptions.Default;
        Assert.Same(RK78IntegratorOptions.Default, options.Integrator);
        Assert.Equal(TimeSpan.FromSeconds(60), options.OutputStep);
        Assert.Null(options.ThirdBodies);
        Assert.False(options.AtmosphericDrag);
        Assert.False(options.SolarRadiationPressure);
        Assert.False(options.AlbedoRadiationPressure);
        Assert.False(options.ThermalRadiationPressure);
        Assert.Null(options.Geopotential);
        Assert.Null(options.Atmosphere);
        Assert.Null(options.CovariancePropagation);
    }

    [Fact]
    public void Rk78IntegratorOptionsDefault_IsTheIntegratorOfNewRk78Integrator()
    {
        // The settings of new RK78Integrator(): 1e-9, 1e-9, 60 s, 1e-6 s, 86400 s, 0.9, 0.2, 5 (RK78Integrator.cs)
        var options = RK78IntegratorOptions.Default;
        Assert.Equal(1e-9, options.AbsoluteTolerance);
        Assert.Equal(1e-9, options.RelativeTolerance);
        Assert.Equal(60.0, options.InitialStep.TotalSeconds);
        Assert.Equal(1e-6, options.MinStep.TotalSeconds);
        Assert.Equal(86400.0, options.MaxStep.TotalSeconds);
        Assert.Equal(0.9, options.SafetyFactor);
        Assert.Equal(0.2, options.MinFactor);
        Assert.Equal(5.0, options.MaxFactor);
        Assert.Null(options.FixedStep);

        // The integrator the options build has the settings of new RK78Integrator(), read back exactly
        var built = (RK78Integrator)options.CreateIntegrator();
        Assert.Equal(new RK78Integrator().Settings, built.Settings);
        Assert.Equal(options, built.Settings);
    }

    [Fact]
    public void Rk78IntegratorOptionsFixedStep_BuildsTheFixedStepIntegrator()
    {
        var options = new RK78IntegratorOptions { FixedStep = TimeSpan.FromSeconds(30) };
        var built = (RK78Integrator)options.CreateIntegrator();
        Assert.False(built.AdaptiveMode);
        Assert.Equal(new RK78Integrator(30.0).Settings, built.Settings);
    }

    [Fact]
    public void Rk78IntegratorOptionsFixedStep_WithAnotherSetting_ThrowsAtTheStartOfTheCall()
    {
        var options = new RK78IntegratorOptions { FixedStep = TimeSpan.FromSeconds(30), AbsoluteTolerance = 1e-12 };
        var exception = Assert.Throws<ArgumentException>(() => options.CreateIntegrator());
        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void VVIntegratorOptionsDefault_IsAStepOf1s()
    {
        Assert.Equal(TimeSpan.FromSeconds(1), VVIntegratorOptions.Default.Step);
        var built = (VVIntegrator)new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10) }.CreateIntegrator();
        Assert.Equal(TimeSpan.FromSeconds(10), built.DeltaT);
    }

    public static IEnumerable<object[]> InvalidSettings()
    {
        yield return new object[] { "OutputStep", (Action)(() => _ = PropagationOptions.Default with { OutputStep = TimeSpan.Zero }) };
        yield return new object[] { "OutputStep", (Action)(() => _ = new PropagationOptions { OutputStep = TimeSpan.FromSeconds(-1) }) };
        yield return new object[] { "AbsoluteTolerance", (Action)(() => _ = RK78IntegratorOptions.Default with { AbsoluteTolerance = double.NaN }) };
        yield return new object[] { "AbsoluteTolerance", (Action)(() => _ = new RK78IntegratorOptions { AbsoluteTolerance = 0.0 }) };
        yield return new object[] { "RelativeTolerance", (Action)(() => _ = new RK78IntegratorOptions { RelativeTolerance = -1e-9 }) };
        yield return new object[] { "InitialStep", (Action)(() => _ = new RK78IntegratorOptions { InitialStep = TimeSpan.Zero }) };
        yield return new object[] { "MinStep", (Action)(() => _ = new RK78IntegratorOptions { MinStep = TimeSpan.Zero }) };
        yield return new object[] { "MaxStep", (Action)(() => _ = new RK78IntegratorOptions { MaxStep = TimeSpan.FromSeconds(-1) }) };
        yield return new object[] { "SafetyFactor", (Action)(() => _ = new RK78IntegratorOptions { SafetyFactor = double.PositiveInfinity }) };
        yield return new object[] { "MinFactor", (Action)(() => _ = new RK78IntegratorOptions { MinFactor = 0.0 }) };
        yield return new object[] { "MaxFactor", (Action)(() => _ = new RK78IntegratorOptions { MaxFactor = double.NaN }) };
        yield return new object[] { "FixedStep", (Action)(() => _ = new RK78IntegratorOptions { FixedStep = TimeSpan.Zero }) };
        yield return new object[] { "Step", (Action)(() => _ = VVIntegratorOptions.Default with { Step = TimeSpan.Zero }) };
    }

    [Theory]
    [MemberData(nameof(InvalidSettings))]
    public void AnInvalidSetting_ThrowsInInit_NamingTheProperty(string property, Action set)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(set);
        Assert.Equal(property, exception.ParamName);
    }

    [Fact]
    public void ANullIntegrator_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => PropagationOptions.Default with { Integrator = null });
        Assert.Equal("Integrator", exception.ParamName);
    }

    [Fact]
    public void ThirdBodies_AreACopyComparedByValue()
    {
        var bodies = new List<CelestialItem> { Stars.SunBody, PlanetsAndMoons.MoonBody };
        var options = PropagationOptions.Default with { ThirdBodies = bodies };
        bodies.Clear();

        Assert.Equal(2, options.ThirdBodies.Count);
        var same = PropagationOptions.Default with { ThirdBodies = new CelestialItem[] { Stars.SunBody, PlanetsAndMoons.MoonBody } };
        Assert.Equal(same, options);
        Assert.Equal(same.GetHashCode(), options.GetHashCode());
        Assert.NotEqual(PropagationOptions.Default with { ThirdBodies = new CelestialItem[] { Stars.SunBody } }, options);
    }

    [Fact]
    public void ThirdBodies_WithANullBody_Throw()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            PropagationOptions.Default with { ThirdBodies = new CelestialItem[] { Stars.SunBody, null } });
        Assert.Equal("ThirdBodies", exception.ParamName);
    }

    [Fact]
    public void IntegratorOptions_IsAClosedHierarchy()
    {
        // D1 rule 5: no constructor of IntegratorOptions is visible outside the library but the copy constructor, which
        // the compiler requires protected (CS8878), and its abstract member is internal, so no record outside the
        // library can derive from it
        foreach (var constructor in typeof(IntegratorOptions).GetConstructors(BindingFlags.Instance | BindingFlags.Public |
                                                                               BindingFlags.NonPublic))
        {
            bool copy = constructor.GetParameters() is { Length: 1 } p && p[0].ParameterType == typeof(IntegratorOptions);
            Assert.True(copy || !(constructor.IsPublic || constructor.IsFamily || constructor.IsFamilyOrAssembly));
        }

        var create = typeof(IntegratorOptions).GetMethod("CreateIntegrator", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.True(create.IsAbstract && create.IsAssembly);
    }

    [Fact]
    public void CovariancePropagationOptionsNone_IsModeNone_AndRefusesTheSettingsOfTheCovariance()
    {
        Assert.Equal(CovariancePropagationMode.None, CovariancePropagationOptions.None.Mode);
        var noise = new RtnProcessNoise(1e-9, 1e-9, 1e-9);
        Assert.Throws<ArgumentException>(() => CovariancePropagationOptions.None with { ProcessNoise = noise });
        Assert.Throws<ArgumentException>(() => new CovariancePropagationOptions
            { ProcessNoise = noise, Mode = CovariancePropagationMode.None });
        Assert.Throws<ArgumentException>(() => CovariancePropagationOptions.None with
            { ConsiderParameters = new ConsiderParameters(0.1, 0.1) });
    }

    [Fact]
    public void ConjunctionAnalysisOptionsCovariancePropagationNull_Throws_PointingToNone()
    {
        // Choice 3: before 11.0.0 null held the covariance in RTN; it throws now, and None does that
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new ConjunctionAnalysisOptions() with { CovariancePropagation = null });
        Assert.Equal("CovariancePropagation", exception.ParamName);
        Assert.Contains("CovariancePropagationOptions.None", exception.Message);

        var held = new ConjunctionAnalysisOptions() with { CovariancePropagation = CovariancePropagationOptions.None };
        Assert.Same(CovariancePropagationOptions.None, held.CovariancePropagation);
    }
}
