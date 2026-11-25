// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using Xunit;
using Quaternion = IO.Astrodynamics.Math.Quaternion;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// #661: <see cref="SpiceAPI.WriteEphemeris"/> and <see cref="SpiceAPI.WriteOrientation"/> enumerate the caller's
/// sequence once, before they take the SPICE lock, so that nothing the caller supplies runs under it.
/// </summary>
/// <remarks>
/// Before the fix both enumerated it inside <c>lock (lockObject)</c>, and <see cref="SpiceAPI.WriteEphemeris"/> twice
/// (measured on 016237bd, the issue: 2 and 1 enumerations), so that a sequence that read <c>PlanetsAndMoons</c>,
/// <c>Stars</c> or <c>Barycenters</c> for the first time could wait for a thread waiting for the lock (#623), and a lazy
/// sequence ran its work twice. The expected values are the definition of the fix: one enumeration, the lock not held by
/// the enumerating thread, and another thread able to take it while the sequence is enumerated.
/// </remarks>
public class SpiceApiWriterEnumerationTests
{
    // A thread that takes the lock while the sequence is enumerated: under the lock (before the fix) it waits until the
    // timeout, with the fix it takes it at once (a few ms, unless another test class holds the lock for its call)
    private static readonly TimeSpan OtherThreadTimeout = TimeSpan.FromSeconds(60);

    public SpiceApiWriterEnumerationTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    /// <summary>A sequence that counts its enumerations and records, at each item, whether the enumerating thread holds
    /// the SPICE lock and whether another thread could take it.</summary>
    private sealed class ProbedSequence<T>(IReadOnlyList<T> items) : IEnumerable<T>
    {
        internal int Enumerations { get; private set; }
        internal int ItemsUnderTheLock { get; private set; }
        internal bool OtherThreadTookTheLock { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            Enumerations++;
            for (int i = 0; i < items.Count; i++)
            {
                if (Monitor.IsEntered(SpiceAPI.SyncRoot))
                {
                    ItemsUnderTheLock++;
                }

                if (i == 0 && Enumerations == 1)
                {
                    bool taken = false;
                    var other = new Thread(() =>
                    {
                        if (Monitor.TryEnter(SpiceAPI.SyncRoot, OtherThreadTimeout))
                        {
                            taken = true;
                            Monitor.Exit(SpiceAPI.SyncRoot);
                        }
                    });
                    other.Start();
                    other.Join();
                    OtherThreadTookTheLock = taken;
                }

                yield return items[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void WriteEphemeris_EnumeratesTheStatesOnce_BeforeTheLock()
    {
        // Arrange: 10 states of a circular orbit about the Earth, as KernelBackedSpacecraftTests writes them
        var start = TimeSystem.Time.J2000TDB;
        var states = Enumerable.Range(0, 10).Select(i => new StateVector(
            new Vector3(6800000.0 * System.Math.Cos(i * 0.001), 6800000.0 * System.Math.Sin(i * 0.001), 0.0),
            new Vector3(-7656.0 * System.Math.Sin(i * 0.001), 7656.0 * System.Math.Cos(i * 0.001), 0.0),
            TestHelpers.EarthAtJ2000, start.AddSeconds(i), Frames.Frame.ICRF)).ToList();
        var sequence = new ProbedSequence<StateVector>(states);
        var file = new FileInfo(Path.Combine(Constants.OutputPath.CreateSubdirectory("Issue661").FullName, "issue661.bsp"));

        try
        {
            // Act
            Assert.True(SpiceAPI.Instance.WriteEphemeris(file, -661001, sequence));

            // Assert
            Assert.Equal(1, sequence.Enumerations);
            Assert.Equal(0, sequence.ItemsUnderTheLock);
            Assert.True(sequence.OtherThreadTookTheLock);
        }
        finally
        {
            file.Delete();
        }
    }

    [Fact]
    public void WriteOrientation_EnumeratesTheOrientationsOnce_BeforeTheLock()
    {
        // Arrange: a spacecraft's clock kernel, which the CK writer encodes the epochs with, and 10 orientations in J2000
        var directory = Constants.OutputPath.CreateSubdirectory("Issue661");
        var spacecraft = new Spacecraft(-661002, "ISSUE661", 1000.0, new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.0, 0.0), TestHelpers.EarthAtJ2000,
                TimeSystem.Time.J2000TDB, Frames.Frame.ICRF), 2000.0,
            new Clock("ISSUE661_CLK", 65536));
        var clock = new FileInfo(Path.Combine(directory.FullName, "issue661.tsc"));
        var ck = new FileInfo(Path.Combine(directory.FullName, "issue661.bc"));
        spacecraft.Clock.Write(clock);
        var orientations = Enumerable.Range(0, 10).Select(i => new StateOrientation(
            new Quaternion(Vector3.VectorZ, i * 0.01), Vector3.Zero, TimeSystem.Time.J2000TDB.AddSeconds(i * 10.0),
            Frames.Frame.ICRF)).ToList();
        var sequence = new ProbedSequence<StateOrientation>(orientations);

        SpiceAPI.Instance.LoadKernels(clock);
        try
        {
            // Act
            Assert.True(SpiceAPI.Instance.WriteOrientation(ck, spacecraft.NaifId, sequence));

            // Assert
            Assert.Equal(1, sequence.Enumerations);
            Assert.Equal(0, sequence.ItemsUnderTheLock);
            Assert.True(sequence.OtherThreadTookTheLock);
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(clock);
            ck.Delete();
            clock.Delete();
        }
    }
}
