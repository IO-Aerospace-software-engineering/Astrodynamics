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
/// results are those of their input (#725).
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
}
