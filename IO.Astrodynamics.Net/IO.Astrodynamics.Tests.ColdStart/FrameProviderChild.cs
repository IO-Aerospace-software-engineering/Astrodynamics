// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Tests.ColdStart;

/// <summary>
/// The mode <c>static-frames-provider</c> (structuring choice 1 and R25 of #668, decided by S. Guillet, 2026-11-19): the
/// provider a frame reads, which the global provider decides, so in a fresh process. A static SPICE frame of
/// <see cref="Frame"/> reads the provider of the process at each transform, a frame built by name the provider set at its
/// construction; a frame name a <see cref="MemoryDataProvider"/> neither holds nor defines throws
/// <see cref="ArgumentException"/>, its message pointing to <see cref="Frame.ICRF"/> for ICRF and EME2000.
/// </summary>
/// <remarks>
/// No kernel is loaded. The type <see cref="Frame"/> is first used under the default <see cref="SpiceDataProvider"/>; then
/// two memory providers hold a rotation of GALACTIC and of ITRF93 about z, 90° and 30°, whose rotation angle from ICRF is
/// that angle in closed form. Before 11.0.0 the static frames kept the provider of the type's first use: SPICE's GALACTIC
/// (its built-in rotation, not 90°) under either memory provider, and no <c>Frame.ITRF93</c>. Exit 0 when every check
/// holds, 1 with the failed check or the exception on the standard error.
/// </remarks>
internal static class FrameProviderChild
{
    private static readonly Time Epoch = new(2021, 6, 1, 12, 0, 0, TimeScale.TDB);

    internal static int Run()
    {
        try
        {
            // The type Frame first used under the default SPICE provider
            Check("Frame.ICRF before any provider is set", Frame.ICRF.Name == "J2000");

            var first = Provider(90.0);
            Configuration.Instance.SetDataProvider(first);
            var builtUnderFirst = new Frame("GALACTIC");
            Check($"Frame.GALACTIC_SYSTEM2 reads the provider set: 90° ({AngleDeg(Frame.GALACTIC_SYSTEM2)})", Near(AngleDeg(Frame.GALACTIC_SYSTEM2), 90.0));
            Check($"Frame.ITRF93 reads the provider set: 90° ({AngleDeg(Frame.ITRF93)})", Near(AngleDeg(Frame.ITRF93), 90.0));

            var second = Provider(30.0);
            Configuration.Instance.SetDataProvider(second);
            Check($"Frame.GALACTIC_SYSTEM2 follows the provider set since, at the same epoch: 30° ({AngleDeg(Frame.GALACTIC_SYSTEM2)})",
                Near(AngleDeg(Frame.GALACTIC_SYSTEM2), 30.0));
            Check($"Frame.ITRF93 follows it too: 30° ({AngleDeg(Frame.ITRF93)})", Near(AngleDeg(Frame.ITRF93), 30.0));
            Check($"a frame built by name keeps the provider of its construction: 90° ({AngleDeg(builtUnderFirst)})", Near(AngleDeg(builtUnderFirst), 90.0));
            Check("Configuration.Instance.DataProvider is the provider set", ReferenceEquals(Configuration.Instance.DataProvider, second));
            Check("Frame.ITRF93 equals new Frame(\"ITRF93\")", Frame.ITRF93 == new Frame("ITRF93"));

            // Names the memory provider neither holds nor defines
            foreach (var name in new[] { "ICRF", "EME2000", "NOT_A_FRAME" })
            {
                var e = Expect<ArgumentException>($"the memory provider's {name}", () => new Frame(name).GetStateOrientationToICRF(Epoch));
                Check($"its message names the frame ({e.Message})", e.Message.Contains(name));
                Check($"its message points to Frame.ICRF for ICRF and EME2000 only ({e.Message})",
                    e.Message.Contains("Use Frame.ICRF") == (name != "NOT_A_FRAME"));
            }

            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    // A memory provider holding GALACTIC and ITRF93 rotated by angleDeg about z from ICRF at the epoch
    private static MemoryDataProvider Provider(double angleDeg)
    {
        var provider = new MemoryDataProvider();
        var q = new Quaternion(Vector3.VectorZ, angleDeg * Constants.Deg2Rad);
        foreach (var name in new[] { "GALACTIC", "ITRF93" })
        {
            var frame = new Frame(name);
            provider.AddStateOrientationToICRF(frame, Epoch, new StateOrientation(q, Vector3.Zero, Epoch, frame));
        }

        return provider;
    }

    // The rotation angle of a frame from ICRF, in degrees (the sign of a quaternion is free)
    private static double AngleDeg(Frame frame)
    {
        var q = frame.GetStateOrientationToICRF(Epoch).Rotation;
        return 2.0 * System.Math.Acos(System.Math.Min(1.0, System.Math.Abs(q.W))) * Constants.Rad2Deg;
    }

    // The closed-form angle, to the rounding of the quaternion's components and of acos near 0.5 and 0.97
    private static bool Near(double actual, double expected) => System.Math.Abs(actual - expected) <= 1e-12;

    private static TException Expect<TException>(string label, Func<object> read) where TException : Exception
    {
        try
        {
            read();
        }
        catch (Exception e) when (e.GetType() == typeof(TException))
        {
            Console.WriteLine($"OK {label}: {e.GetType().Name}");
            return (TException)e;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Failed: {label}: {e.GetType().Name} instead of {typeof(TException).Name}", e);
        }

        throw new InvalidOperationException($"Failed: {label}: no exception");
    }

    private static void Check(string label, bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Failed: {label}");
        }

        Console.WriteLine($"OK {label}");
    }
}
