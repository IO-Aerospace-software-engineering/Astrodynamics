// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests.Body;

using Time = IO.Astrodynamics.TimeSystem.Time;

/// <summary>
/// A first spacecraft, lot L5b of #668 (R10, decided by S. Guillet, 2026-11-23): the NAIF id, the name, the dry mass and
/// the initial orbit are required, the rest optional with the values the explicit forms gave (decisions 70, 71 and 77:
/// <see cref="Spacecraft.MaxOperatingMass"/> double.MaxValue, the clock Name + "_CLK" at 65536 ticks per second, the
/// parameters named <c>maxOperatingMass</c>, <c>dragCoefficient</c> and <c>solarRadiationCoefficient</c>).
/// </summary>
/// <remarks>
/// The references are the decided rules and the explicit forms: a default equals the explicit value it stands for, bit
/// for bit (IC-20 of the lot's gap analysis).
/// </remarks>
public class SpacecraftConstructionTests
{
    public SpacecraftConstructionTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    private static readonly Time S = new(2024, 3, 1, 12, 0, 0, TimeScale.TDB);
    private static int _nextId = -181000;
    private static int NextId() => Interlocked.Decrement(ref _nextId);

    private static CelestialBody Earth() => new(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, S);

    private static KeplerianElements Orbit(CelestialBody earth) =>
        new(7000e3, 0.01, 0.5, 0.0, 0.0, 0.0, earth, S, Frames.Frame.ICRF);

    // ---------------------------------------------------------------------------------------------------------------------
    // The propagated constructor (decisions 70 and 71, R10)
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void PropagatedSpacecraft_TakesTheNaifIdTheNameTheMassAndTheOrbit()
    {
        int id = NextId();
        var spacecraft = new Spacecraft(id, "Sat" + -id, 1000.0, Orbit(Earth()));

        Assert.Equal(double.MaxValue, spacecraft.MaxOperatingMass);
        Assert.Equal("Sat" + -id + "_CLK", spacecraft.Clock.Name);
        Assert.Equal(65536u, spacecraft.Clock.Resolution);
        Assert.Same(spacecraft, spacecraft.Clock.Spacecraft);
        Assert.Equal(1.0, spacecraft.SectionalArea);
        Assert.Equal(2.2, spacecraft.DragCoefficient);
        Assert.Equal(1.0, spacecraft.SolarRadiationCoeff);
        Assert.Null(spacecraft.CosparId);
        Assert.Equal(0.0, spacecraft.HardBodyRadius);
    }

    [Fact]
    public void PropagatedSpacecraft_TakesItsOptionalValuesByTheirNames()
    {
        int id = NextId();
        var clock = new Clock("C" + -id, 256);
        var spacecraft = new Spacecraft(id, "Sat" + -id, 1000.0, Orbit(Earth()), maxOperatingMass: 3000.0, clock: clock,
            sectionalArea: 4.0, dragCoefficient: 2.0, cosparId: "2024-001A", solarRadiationCoefficient: 1.3, hardBodyRadius: 2.5);

        Assert.Equal(3000.0, spacecraft.MaxOperatingMass);
        Assert.Same(clock, spacecraft.Clock);
        Assert.Equal(4.0, spacecraft.SectionalArea);
        Assert.Equal(2.0, spacecraft.DragCoefficient);
        Assert.Equal("2024-001A", spacecraft.CosparId);
        Assert.Equal(1.3, spacecraft.SolarRadiationCoeff);
        Assert.Equal(2.5, spacecraft.HardBodyRadius);
    }

    /// <summary>
    /// The maximum operating mass is informational: checked against the dry mass only, never against the fuel and payloads
    /// of the spacecraft (L5-37 of #668).
    /// </summary>
    [Fact]
    public void MaxOperatingMass_IsCheckedAgainstTheDryMassOnly()
    {
        int id = NextId();
        var spacecraft = new Spacecraft(id, "Sat" + -id, 1000.0, Orbit(Earth()), maxOperatingMass: 1000.0);
        var tank = new FuelTank("T", 500.0, 500.0, "m", "sn");
        spacecraft.AddFuelTank(tank);

        Assert.Equal(1500.0, spacecraft.GetTotalMass());
        Assert.Equal(1000.0, spacecraft.MaxOperatingMass);
        var rejected = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Spacecraft(NextId(), "Low", 1000.0, Orbit(Earth()), maxOperatingMass: 999.0));
        Assert.Equal("maxOperatingMass", rejected.ParamName);
    }

    /// <summary>
    /// The default clock writes the SCLK of the explicit clock Name + "_CLK" at 65536 ticks per second, byte for byte.
    /// </summary>
    [Fact]
    public async Task DefaultClock_WritesTheSclkOfTheExplicitClock()
    {
        int id = NextId();
        var earth = Earth();
        var byDefault = new Spacecraft(id, "Sat" + -id, 1000.0, Orbit(earth));
        var explicitClock = new Spacecraft(id, "Sat" + -id, 1000.0, Orbit(earth), clock: new Clock("Sat" + -id + "_CLK", 65536));

        var directory = Directory.CreateTempSubdirectory("l5b-clock-");
        try
        {
            var first = new FileInfo(Path.Combine(directory.FullName, "default.tsc"));
            var second = new FileInfo(Path.Combine(directory.FullName, "explicit.tsc"));
            await byDefault.Clock.WriteAsync(first);
            await explicitClock.Clock.WriteAsync(second);

            Assert.Equal(File.ReadAllBytes(second.FullName), File.ReadAllBytes(first.FullName));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    /// <summary>
    /// The defaults propagate bit for bit as the explicit values they stand for, under both integrators.
    /// </summary>
    [Theory]
    [InlineData("RK")]
    [InlineData("VV")]
    public void Defaults_PropagateAsTheExplicitValues(string integrator)
    {
        var earth = Earth();
        int id = NextId();
        var byDefault = new Spacecraft(id, "Sat" + -id, 1000.0, Orbit(earth));
        int other = NextId();
        var explicitValues = new Spacecraft(other, "Sat" + -id, 1000.0, Orbit(earth), double.MaxValue,
            new Clock("Sat" + -id + "_CLK", 65536), 1.0, 2.2, null, 1.0, null, null, null, 0.0);
        var window = new Window(S, TimeSpan.FromHours(1.0));
        var options = new PropagationOptions
        {
            Integrator = integrator == "VV" ? new VVIntegratorOptions { Step = TimeSpan.FromSeconds(10.0) } : new RK78IntegratorOptions(),
            OutputStep = TimeSpan.FromSeconds(60.0), ThirdBodies = [earth], AtmosphericDrag = true, SolarRadiationPressure = true,
            CovariancePropagation = CovariancePropagationOptions.None
        };

        var a = byDefault.Propagate(window, options).StateVectors;
        var b = explicitValues.Propagate(window, options).StateVectors;

        Assert.Equal(b.Count, a.Count);
        Assert.True(a.Zip(b).All(p => p.First.Position == p.Second.Position && p.First.Velocity == p.Second.Velocity));
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // The kernel-backed constructor: its parameter names only (decision 71)
    // ---------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void KernelBackedSpacecraft_NamesItsParametersAsThePropagatedOne()
    {
        var parameters = typeof(Spacecraft).GetConstructors()
            .Select(c => c.GetParameters().Select(p => p.Name).ToArray())
            .ToArray();

        Assert.Equal(2, parameters.Length);
        foreach (var names in parameters)
        {
            Assert.Contains("maxOperatingMass", names);
            Assert.Contains("dragCoefficient", names);
            Assert.Contains("solarRadiationCoefficient", names);
            Assert.DoesNotContain("maximumOperatingMass", names);
            Assert.DoesNotContain("dragCoeff", names);
            Assert.DoesNotContain("solarRadiationCoeff", names);
        }
    }

    [Fact]
    public void MaximumOperatingMass_IsRenamedMaxOperatingMass()
    {
        Assert.NotNull(typeof(Spacecraft).GetProperty("MaxOperatingMass"));
        Assert.Null(typeof(Spacecraft).GetProperty("MaximumOperatingMass"));
    }
}
