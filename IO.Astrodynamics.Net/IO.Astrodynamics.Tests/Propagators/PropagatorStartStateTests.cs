// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using IO.Astrodynamics.Tests.OrbitalParameters;
using IO.Astrodynamics.Tests.Propagators.Variational;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Propagators;

/// <summary>
/// The start of a <see cref="CentralBodyPropagator"/>: a state vector given at the start of the window is taken as it is,
/// not through the Keplerian elements (#398); equinoctial elements of a circular or an equatorial orbit start at their
/// state (#452).
/// </summary>
public class PropagatorStartStateTests
{
    private const double Deg = IO.Astrodynamics.Constants.Deg2Rad;

    private static readonly TimeSystem.Time Epoch = new(2021, 3, 20, 12, 0, 0, frame: TimeFrame.UTCFrame);

    private readonly CelestialBody _earth;
    private readonly ITestOutputHelper _output;

    public PropagatorStartStateTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        _earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Epoch);
    }

    public static TheoryData<string, bool> Starts => new()
    {
        { "RK7(8), window in UTC", false }, { "RK7(8), window in TDB", true },
        { "Velocity-Verlet, window in UTC", false }, { "Velocity-Verlet, window in TDB", true }
    };

    [Theory]
    [MemberData(nameof(Starts))]
    public void StateVectorAtTheStartOfTheWindow_IsTheFirstStateBitForBit(string name, bool tdbWindow)
    {
        // Arrange: the R4 GEO state with z moved by 1 km, which the Keplerian round trip moved by 0.38 m before #398,
        // and still returns only to its rounding
        var orbit = new KeplerianElements(42164137.0, 0.0002, 0.05 * Deg, 0.0, 30.0 * Deg, 0.0, _earth, Epoch,
            Frames.Frame.ICRF).ToStateVector();
        var start = new StateVector(orbit.Position + new Vector3(0.0, 0.0, 1000.0), orbit.Velocity, _earth, Epoch,
            Frames.Frame.ICRF);
        var spacecraft = ReferenceCase.NewSpacecraft(-1398, start);
        var startDate = tdbWindow ? Epoch.ToTDB() : Epoch;
        var window = new Window(startDate, startDate.AddSeconds(600.0));
        var bodies = new CelestialItem[] { _earth };

        // Act
        using var propagator = name.StartsWith("RK")
            ? new CentralBodyPropagator(window, spacecraft, new RK78Integrator(1e-12, 1e-12), bodies, false, false,
                TimeSpan.FromSeconds(60.0))
            : new CentralBodyPropagator(window, spacecraft, bodies, false, false, TimeSpan.FromSeconds(10.0));
        var solution = propagator.Propagate();

        // Assert
        var first = solution.Segments[0].Steps[0];
        Assert.Equal(start.Position, first.StartPosition);
        Assert.Equal(start.Velocity, first.StartVelocity);
    }

    public static TheoryData<string, string, bool> EquinoctialStarts
    {
        get
        {
            var data = new TheoryData<string, string, bool>();
            foreach (var elements in new[]
                     {
                         "circular, i 30, node 40 (suite 155)", "equatorial, e 0.05, lop 120 (suite 156)",
                         "circular, i 30, node 0", "suite 151, generic"
                     })
            {
                foreach (var (start, tdbWindow) in Starts.Select(row => ((string)row[0], (bool)row[1])))
                {
                    data.Add(elements, start, tdbWindow);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(EquinoctialStarts))]
    public void EquinoctialElements_StartAtTheirState(string elements, string start, bool tdbWindow)
    {
        // Arrange: equinoctial elements at the start of the window, which goes through AtEpoch (K, negligible at
        // e <= 0.05). Before #452, the start of the circular case 155 was 4.79e6 m off, of the equatorial case 156
        // 1.32e7 m
        var reference = EquinoctialReference.Load();
        Assert.Equal(reference.Gm["gm_de440"], _earth.GM);
        var c = reference.Case(elements);
        var spacecraft = new Spacecraft(-1452, "START1452", 100.0, 1000.0, new Clock("start1452", 65536),
            c.Elements(_earth, Epoch, Frames.Frame.ICRF), 10.0);
        var startDate = tdbWindow ? Epoch.ToTDB() : Epoch;
        var window = new Window(startDate, startDate.AddSeconds(600.0));
        var bodies = new CelestialItem[] { _earth };

        // Act: the constructor with an integrator and its forces, and the default Velocity-Verlet one
        using var propagator = start.StartsWith("RK")
            ? new CentralBodyPropagator(window, spacecraft, new RK78Integrator(1e-12, 1e-12), bodies, false, false,
                TimeSpan.FromSeconds(60.0))
            : new CentralBodyPropagator(window, spacecraft, bodies, false, false, TimeSpan.FromSeconds(10.0));
        var solution = propagator.Propagate();

        // Assert against the 60-digit state of the elements. Twice the worst over Linux, Windows and macOS arm64 (CI
        // run 40280932636, 2026-10-26): 1.34e-16 and 1.93e-16 (suite 155), the same for both constructors and both time
        // frames. The start goes through AtEpoch (K): measured again on the Kepler solver of #453 (Linux, .NET 10,
        // 2026-10-26), unchanged, provisional until the CI of the three platforms
        var first = solution.Segments[0].Steps[0];
        var (position, velocity) = c.At(0.0).RelativeError(new StateVector(first.StartPosition, first.StartVelocity,
            _earth, Epoch, Frames.Frame.ICRF));
        _output.WriteLine($"MEASURE #452 EquinoctialElements_StartAtTheirState, {elements}, {start}: position " +
                          $"{position:E2}, velocity {velocity:E2} (relative)");
        Assert.True(position < 2.7e-16, $"position {position:E2}");
        Assert.True(velocity < 3.9e-16, $"velocity {velocity:E2}");
    }
}
