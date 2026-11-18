// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Tests.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using SpiceFrame = IO.Astrodynamics.Frames.Frame;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The element conversions and the two-body propagation of <see cref="SpiceAPI"/>: the observer and the frame of their
/// results are those of their input (#725), and <see cref="SpiceAPI.ConvertEquinoctialElementsToStateVector"/> gives
/// EQNCPV the mean longitude of the elements (#450).
/// </summary>
/// <remarks>
/// The child process of <see cref="ColdStartTests"/> checks, on a probe data provider, that none of these calls reads the
/// provider under the SpiceAPI lock (#664, mode <c>conversions-lock</c>).
/// </remarks>
public class SpiceApiElementConversionTests
{
    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0, frame: TimeFrame.TDBFrame);
    private readonly ITestOutputHelper _output;

    public SpiceApiElementConversionTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    // A body from custom parameters, of an identifier SPICE does not name: no kernel read
    private sealed class CustomBody() : CelestialBody(2999725, "IOA_725_BODY", 5.972e24, 6356752.0, 6378137.0);

    // The observers of #725: the Earth with a geopotential and an atmospheric model, a barycentre and a body from custom
    // parameters
    public static IEnumerable<object[]> Observers()
    {
        yield return ["earth with models"];
        yield return ["barycentre"];
        yield return ["custom body"];
    }

    private static CelestialItem Observer(string name) => name switch
    {
        "earth with models" => new CelestialBody(PlanetsAndMoons.EARTH, SpiceFrame.ICRF, Epoch,
            new IO.Astrodynamics.Body.GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10), new EarthStandardAtmosphere()),
        "barycentre" => new Barycenter(3, Epoch),
        "custom body" => new CustomBody(),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    /// <summary>
    /// #725: the results carried a new <see cref="CelestialBody"/> built from the NAIF id of the centre and a frame rebuilt
    /// from its name, so that the observer's models (geopotential, atmosphere), a barycentre or a body from custom
    /// parameters were lost, and an observer SPICE does not name made the explicit-GM calls throw. Each call now returns the
    /// observer and the frame instances of its input.
    /// </summary>
    [Theory]
    [MemberData(nameof(Observers))]
    public void TwoBodyCalls_ReturnTheObserverAndTheFrameOfTheirInput(string observerName)
    {
        // Arrange
        var observer = Observer(observerName);
        var frame = SpiceFrame.ECLIPTIC_J2000;
        double gm = observer.GM;
        var state = new StateVector(new Vector3(6800000.0, 1000.0, 2000.0), new Vector3(10.0, 7500.0, 1000.0), observer, Epoch, frame);
        var ellipse = new KeplerianElements(7.0e6, 0.1, 0.5, 1.0, 2.0, 0.3, observer, Epoch, frame);
        var hyperbola = new KeplerianElements(-7.0e6, 1.5, 0.5, 1.0, 2.0, 0.3, observer, Epoch, frame);
        var results = new List<(string Call, IO.Astrodynamics.OrbitalParameters.OrbitalParameters Result)>
        {
            ("ConvertStateVectorToConicOrbitalElement(sv, gm)", SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state, gm)),
            ("ConvertStateVectorToConicOrbitalElement(sv)", SpiceAPI.Instance.ConvertStateVectorToConicOrbitalElement(state)),
            ("ConvertConicElementsToStateVector(ke, epoch, gm)", SpiceAPI.Instance.ConvertConicElementsToStateVector(ellipse, Epoch.AddSeconds(60.0), gm)),
            ("ConvertConicElementsToStateVector(ke, epoch)", SpiceAPI.Instance.ConvertConicElementsToStateVector(ellipse, Epoch.AddSeconds(60.0))),
            ("ConvertConicElementsToStateVector(hyperbola)", SpiceAPI.Instance.ConvertConicElementsToStateVector(hyperbola)),
            ("Propagate2Bodies(sv, gm, dt)", SpiceAPI.Instance.Propagate2Bodies(state, gm, TimeSpan.FromSeconds(60.0))),
            ("Propagate2Bodies(sv, dt)", SpiceAPI.Instance.Propagate2Bodies(state, TimeSpan.FromSeconds(60.0))),
            ("Propagate2Bodies(sv, epoch)", SpiceAPI.Instance.Propagate2Bodies(state, Epoch.AddSeconds(60.0)))
        };

        // The native ellipse and EQNCPV read the centre from the kernels: a celestial body SPICE names only (#711)
        if (observer is not CustomBody)
        {
            var equinoctial = new EquinoctialElements(7.0e6, 0.01, 0.02, 0.1, 0.2, 0.5, observer, Epoch, frame);
            results.Add(("ConvertConicElementsToStateVector(ellipse)", SpiceAPI.Instance.ConvertConicElementsToStateVector(ellipse)));
            results.Add(("ConvertEquinoctialElementsToStateVector", SpiceAPI.Instance.ConvertEquinoctialElementsToStateVector(equinoctial)));
        }

        // Assert
        foreach (var (call, result) in results)
        {
            Assert.True(ReferenceEquals(observer, result.Observer), $"{call}: the observer is {result.Observer.GetType().Name} {result.Observer.Name}");
            Assert.True(ReferenceEquals(frame, result.Frame), $"{call}: the frame is a new {result.Frame.Name}");
        }

        if (observer is CelestialBody { NaifId: 399 } earth)
        {
            // The models of the caller's Earth, which a propagation from these results runs with (#674)
            Assert.All(results, x => Assert.Same(earth.Field, ((CelestialBody)x.Result.Observer).Field));
            Assert.All(results, x => Assert.True(((CelestialBody)x.Result.Observer).HasAtmosphericModel));
        }
    }

    // Twice the worst, rounded up at the second significant digit, of EquinoctialToState_MatchesTheReference on Linux x64
    // (2026-11-17: position 1.007e-15, velocity 1.020e-15 relative, both at "keplerian e 0.05, i 0, node 180, aop 300
    // (h = -0.0)"); provisional, to be set again from the CI logs of Windows x64 and macOS arm64
    private const double EqncpvPosition = 2.1e-15;
    private const double EqncpvVelocity = 2.1e-15;

    /// <summary>
    /// #450: the state EQNCPV gives the equinoctial elements, against the 60-digit reference of
    /// <see cref="EquinoctialReference"/> (the closed form of the modified equinoctial elements at their true longitude,
    /// <c>tools/equinoctial_elements/equinoctial_reference.py</c>), for the cases of #450 (a LEO, e = 0.89, an equatorial
    /// ellipse, a circular orbit of f = −0.0, <see cref="EquinoctialReference.Eqncpv"/>) and the reference cases within the domain of EQNCPV
    /// (e below 0.9, |h| and |k| below √(double.MaxValue)) and within a turn of their true longitude (the larger ones
    /// measure the reduction of ν = L − atan2(g, f), shared by every path, #450's comment), and the Keplerian cases through
    /// <see cref="IO.Astrodynamics.OrbitalParameters.OrbitalParameters.ToEquinoctial"/>, which gives every sign pattern of the zeros. Before,
    /// the true longitude L0 went to the slot of the mean longitude: the state of another point of the orbit, 7.6 |r| off
    /// at e = 0.89 and 0.18 |r| for the LEO (measured on a85098d); now 1.0e-15 |r| at worst.
    /// </summary>
    [Fact]
    public void EquinoctialToState_MatchesTheReference()
    {
        // Arrange
        var reference = EquinoctialReference.Load();
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, SpiceFrame.ICRF, Epoch);
        Assert.Equal(reference.Gm["gm_de440"], earth.GM);
        var cases = reference.Eqncpv.Concat(reference.Equinoctial)
            .Where(c => c.Group is not "large L" && System.Math.Sqrt(c.F * c.F + c.G * c.G) < 0.9 && c.P > 0.0 &&
                        System.Math.Max(System.Math.Abs(c.H), System.Math.Abs(c.K)) < 1.3e154)
            .Select(c => (Name: c.Name, Elements: c.Elements(earth, Epoch, SpiceFrame.ICRF), State: c.At(0.0)))
            .Concat(reference.Keplerian.Where(c => c.E < 0.9 && c.A > 0.0)
                .Select(c => (Name: "keplerian " + c.Name, Elements: c.Elements(earth, Epoch).ToEquinoctial(), State: c.At(0.0))))
            .ToList();

        // Act
        var errors = cases.Select(c => (c.Name, Error: c.State.RelativeError(SpiceAPI.Instance.ConvertEquinoctialElementsToStateVector(c.Elements))))
            .ToList();

        // Assert
        foreach (var (name, (position, velocity)) in errors)
        {
            _output.WriteLine($"MEASURE #450 EQNCPV, {name}: position {position:E3}, velocity {velocity:E3} (relative)");
        }

        var worstPosition = errors.MaxBy(e => double.IsNaN(e.Error.Position) ? double.PositiveInfinity : e.Error.Position);
        var worstVelocity = errors.MaxBy(e => double.IsNaN(e.Error.Velocity) ? double.PositiveInfinity : e.Error.Velocity);
        _output.WriteLine($"MEASURE #450 EQNCPV, {errors.Count} cases, worst: position {worstPosition.Error.Position:E3} ({worstPosition.Name}), " +
                          $"velocity {worstVelocity.Error.Velocity:E3} ({worstVelocity.Name}) (relative)");
        Assert.True(worstPosition.Error.Position <= EqncpvPosition, $"position {worstPosition.Error.Position:E3} ({worstPosition.Name})");
        Assert.True(worstVelocity.Error.Velocity <= EqncpvVelocity, $"velocity {worstVelocity.Error.Velocity:E3} ({worstVelocity.Name})");
    }
}
