// Copyright 2024. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Matrix = IO.Astrodynamics.Math.Matrix;

namespace IO.Astrodynamics.Tests.Time;

/// <summary>
/// A conversion of a <see cref="TimeSystem.Time"/> to its own frame is the identity (#510). Through TAI, a TDB time
/// came back a tick away at 2 of the 935 041 TT instants every 0.05 day from 1972 to 2100 (2024-05-09T12:00 and
/// 2078-02-02T18:00 TDB) and at 2 of 2 000 000 random ticks of that span: the start of a propagation moved by a tick,
/// and a covariance given at the start was rejected as given at another epoch. Since the conversions round TDB − TT to
/// the nearest tick (#625), it comes back a tick away at 1 of the 935 041 instants, 2032-12-20T20:24 TDB (found by an
/// exact replica of the two conversions, which finds the two instants above under truncation).
/// </summary>
public class ConvertToOwnFrameTests
{
    public ConvertToOwnFrameTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    public static TheoryData<long> TicksThatMovedThroughTai => new()
    {
        new DateTime(2024, 5, 9, 12, 0, 0).Ticks,
        new DateTime(2078, 2, 2, 18, 0, 0).Ticks,
        new DateTime(2032, 12, 20, 20, 24, 0).Ticks,
        650637292165637068L,
        626854470396592948L
    };

    [Theory]
    [MemberData(nameof(TicksThatMovedThroughTai))]
    public void ToTdb_OfATdbTime_IsTheTime(long ticks)
    {
        // Arrange
        var tdb = new TimeSystem.Time(new DateTime(ticks), TimeFrame.TDBFrame);

        // Act
        var converted = tdb.ToTDB();

        // Assert
        Assert.Equal(ticks, converted.DateTime.Ticks);
        Assert.Equal(tdb, converted);
    }

    [Theory]
    [MemberData(nameof(TicksThatMovedThroughTai))]
    public void ConvertTo_TheOwnFrame_IsTheTime_InEveryFrame(long ticks)
    {
        foreach (var frame in new ITimeFrame[]
                 {
                     TimeFrame.TDBFrame, TimeFrame.TDTFrame, TimeFrame.TAIFrame, TimeFrame.UTCFrame, TimeFrame.GPSFrame,
                     TimeFrame.LocalFrame
                 })
        {
            var time = new TimeSystem.Time(new DateTime(ticks), frame);
            Assert.Equal(time, time.ConvertTo(frame));
        }
    }

    [Fact]
    public void ConvertTo_AnotherFrame_StillGoesThroughTai()
    {
        // The TDB time of the instant of the rounding, to TT and back to TDB: the series at two arguments rounded on
        // either side of a half tick, a tick away
        var tdb = new TimeSystem.Time(new DateTime(2032, 12, 20, 20, 24, 0), TimeFrame.TDBFrame);
        Assert.Equal(1, tdb.ToTDT().ToTDB().DateTime.Ticks - tdb.DateTime.Ticks);
    }

    [Fact]
    public void CovariancePropagation_FromATdbEpochThatMovedThroughTai_StartsThere()
    {
        // Arrange: a LEO with its covariance at 2024-05-09T12:00 TDB, the start of the window
        var epoch = new TimeSystem.Time(new DateTime(2024, 5, 9, 12, 0, 0), TimeFrame.TDBFrame);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH);
        var state = new KeplerianElements(6778137.0, 0.0005, 51.6 * IO.Astrodynamics.Constants.Deg2Rad, 0.3, 0.2, 1.0,
            earth, epoch, Frames.Frame.ICRF).ToStateVector();
        var covariance = new Matrix(6, 6);
        for (int i = 0; i < 3; i++)
        {
            covariance.Set(i, i, 100.0);
            covariance.Set(i + 3, i + 3, 1e-4);
        }

        var spacecraft = new Spacecraft(-5100, "CONVERT5100", 100.0, 1000.0, new Clock("convert5100", 65536),
            new StateVector(state.Position, state.Velocity, earth, epoch, Frames.Frame.ICRF, covariance));

        // Act: before #510 the propagator rejected the covariance, given at the epoch it had moved by a tick
        var propagator = new CentralBodyPropagator(new Window(epoch, epoch.AddHours(1.0)), spacecraft,
            new RK78Integrator(1e-12, 1e-12), new CelestialItem[] { earth }, false, false, TimeSpan.FromSeconds(60.0),
            new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance });
        var solution = propagator.Propagate();

        // Assert: the propagation starts at the epoch of the state, from the state itself, with its covariance
        Assert.Equal(epoch, propagator.Window.StartDate);
        Assert.Equal(epoch, solution.Segments[0].BaseEpoch);
        Assert.Equal(state.Position, solution.StateVectors[0].Position);
        Assert.Equal(state.Velocity, solution.StateVectors[0].Velocity);
        Assert.Equal(100.0, solution.GetCovariance(epoch).Get(0, 0));
    }
}
