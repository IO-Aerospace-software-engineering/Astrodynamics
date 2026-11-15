// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Text;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;
using Xunit;

namespace IO.Astrodynamics.Tests;

/// <summary>
/// The kernel paths the library hands to SPICE's writers and to FURNSH, which stores a file name of at most 255 characters
/// (#673): the native writers reject a longer target before they write, where CSPICE wrote under the name truncated to 255
/// and then failed; the managed writers still write such a path, which SPICE loads through a shorter relative name.
/// </summary>
/// <remarks>
/// The bound is checked against SPICE itself: a copy of the 255-character kernel at 256 characters fails to load
/// (<c>SPICE(FILENAMETOOLONG)</c>). The objects (site 399673, star 1000000673, spacecraft -8673) are defined by no other
/// test nor by <c>Data/SolarSystem</c>, and every kernel loaded is unloaded in <c>finally</c>.
/// </remarks>
public class KernelPathLengthTests : IDisposable
{
    private static readonly TimeSystem.Time Start = new(2024, 1, 1);
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("kernel-length-");
    private int _targets;

    public KernelPathLengthTests()
    {
        TestKernels.EnsureSolarSystem();
    }

    public void Dispose()
    {
        _root.Delete(true);
    }

    public enum NativeWriter
    {
        SiteEphemeris,
        StarEphemeris,
        SpacecraftOrientation
    }

    /// <summary>A file named <paramref name="fileName"/> at a full path of exactly <paramref name="length"/> characters, in
    /// a directory of its own under the root, created; components of at most 200 characters.</summary>
    private FileInfo Target(int length, string fileName, char pad = 'd')
    {
        var directory = Path.Combine(_root.FullName, $"t{++_targets}");
        int remaining = length - directory.Length - 1 - fileName.Length;
        while (remaining > 0)
        {
            // Each component adds its separator
            int component = System.Math.Min(200, remaining - 1);
            if (component <= 0)
            {
                // One character left: lengthen the last component instead
                directory += pad;
                remaining -= 1;
                continue;
            }

            directory = Path.Combine(directory, new string(pad, component));
            remaining -= component + 1;
        }

        Directory.CreateDirectory(directory);
        var file = new FileInfo(Path.Combine(directory, fileName));
        Assert.Equal(length, file.FullName.Length);
        return file;
    }

    private static Spacecraft CreateSpacecraft()
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start);
        var orbit = new StateVector(new Vector3(6800000.0, 0.0, 0.0), new Vector3(0.0, 7656.2204182967143, 0.0), earth, Start, Frames.Frame.ICRF);
        return new Spacecraft(-8673, "KPL8673", 1000.0, 3000.0, new Clock("KPL8673_CLK", 65536), orbit);
    }

    /// <summary>Writes the kernel of <paramref name="writer"/> to <paramref name="file"/>; the clock kernel of the spacecraft,
    /// which the CK writer needs, is loaded from a short path and unloaded after.</summary>
    private void Write(NativeWriter writer, FileInfo file)
    {
        var window = new Window(Start, TimeSpan.FromHours(1.0));
        switch (writer)
        {
            case NativeWriter.SiteEphemeris:
                new Site(673, "S673", TestHelpers.EarthAtJ2000, new Planetodetic(0.1, 0.5, 100.0)).WriteEphemeris(window, file);
                break;
            case NativeWriter.StarEphemeris:
                var star = new Star(673, "star673", 1E+30, "spec", 2, 0.3792, new Equatorial(1, 1, Start), 0.1, 0.1, 0, 0, 0, 0, Start);
                star.PropagateAsync(window, TimeSpan.FromMinutes(20.0)).GetAwaiter().GetResult();
                star.WriteEphemeris(file);
                break;
            case NativeWriter.SpacecraftOrientation:
                var spacecraft = CreateSpacecraft();
                for (int i = 0; i < 3; i++)
                {
                    spacecraft.Frame.AddStateOrientationToICRF(new StateOrientation(new Quaternion(Vector3.VectorZ, 0.1 * i), Vector3.Zero,
                        Start.AddSeconds(60.0 * i), spacecraft.Frame));
                }

                var clock = new FileInfo(Path.Combine(_root.FullName, "kpl8673.tsc"));
                spacecraft.Clock.WriteAsync(clock).GetAwaiter().GetResult();
                SpiceAPI.Instance.LoadKernels(clock);
                try
                {
                    spacecraft.WriteOrientation(file);
                }
                finally
                {
                    SpiceAPI.Instance.UnloadKernels(clock);
                }

                break;
        }
    }

    [Theory]
    [InlineData(NativeWriter.SiteEphemeris)]
    [InlineData(NativeWriter.StarEphemeris)]
    [InlineData(NativeWriter.SpacecraftOrientation)]
    public void NativeWriters_AtTheFurnshLimit(NativeWriter writer)
    {
        // Arrange
        var atLimit = Target(255, "k.bsp");
        var beyond = Target(256, "k.bsp");

        // Act: at 255 the kernel is written and SPICE loads it
        Write(writer, atLimit);
        Assert.True(File.Exists(atLimit.FullName));
        SpiceAPI.Instance.LoadKernels(new FileInfo(atLimit.FullName));
        try
        {
            // SPICE's own bound: the same kernel at 256 characters cannot be loaded
            var copy = Target(256, "c.bsp");
            File.Copy(atLimit.FullName, copy.FullName);
            Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.LoadKernels(new FileInfo(copy.FullName)));
        }
        finally
        {
            SpiceAPI.Instance.UnloadKernels(new FileInfo(atLimit.FullName));
        }

        // Assert: at 256 the writer throws before it writes; before #673 CSPICE left a file under the name truncated to 255
        var exception = Assert.Throws<ArgumentException>(() => Write(writer, beyond));
        Assert.Equal("filePath", exception.ParamName);
        Assert.False(File.Exists(beyond.FullName));
        var truncated = beyond.FullName[..255];
        Assert.False(File.Exists(truncated), $"{truncated} was written");
        Assert.Empty(Directory.EnumerateFileSystemEntries(beyond.DirectoryName!));
    }

    [Fact]
    public void ManagedWriters_BeyondTheFurnshLimit_WriteAsToday()
    {
        // Arrange: a spacecraft with states, and its clock
        var spacecraft = CreateSpacecraft();
        var orbit = spacecraft.InitialOrbitalParameters;
        spacecraft.AddPropagatedStates(Enumerable.Range(0, 10).Select(i => orbit.AtEpoch(Start.AddSeconds(60.0 * i)).ToStateVector()).ToArray());
        var shortSpk = new FileInfo(Path.Combine(_root.FullName, "s.bsp"));
        var longSpk = Target(300, "s.bsp");
        var shortClock = new FileInfo(Path.Combine(_root.FullName, "s.tsc"));
        var longClock = Target(300, "s.tsc");

        // Act
        spacecraft.WriteEphemeris(shortSpk);
        spacecraft.WriteEphemeris(longSpk);
        spacecraft.Clock.WriteAsync(shortClock).GetAwaiter().GetResult();
        spacecraft.Clock.WriteAsync(longClock).GetAwaiter().GetResult();

        // Assert: the same bytes, whatever the length of the path
        Assert.Equal(File.ReadAllBytes(shortSpk.FullName), File.ReadAllBytes(longSpk.FullName));
        Assert.Equal(File.ReadAllBytes(shortClock.FullName), File.ReadAllBytes(longClock.FullName));
    }

    [Fact]
    [Trait("Category", "Validation")]
    public void Utf8Path_CountsBytes()
    {
        // The marshaller hands SPICE the UTF-8 bytes of a path on Linux; elsewhere the code page differs
        if (!OperatingSystem.IsLinux()) return;

        // Arrange: 255 characters, one of them é, so 256 bytes
        var target = Target(255, "é.bsp");
        Assert.Equal(256, Encoding.UTF8.GetByteCount(target.FullName));
        var site = new Site(673, "S673", TestHelpers.EarthAtJ2000, new Planetodetic(0.1, 0.5, 100.0));
        var window = new Window(Start, TimeSpan.FromHours(1.0));

        // Act and assert: the writer rejects the path, and SPICE would not load a kernel there
        var exception = Assert.Throws<ArgumentException>(() => site.WriteEphemeris(window, target));
        Assert.Equal("filePath", exception.ParamName);
        Assert.Empty(Directory.EnumerateFileSystemEntries(target.DirectoryName!));
        var kernel = new FileInfo(Path.Combine(_root.FullName, "s673.bsp"));
        site.WriteEphemeris(window, kernel);
        File.Copy(kernel.FullName, target.FullName);
        Assert.Throws<InvalidOperationException>(() => SpiceAPI.Instance.LoadKernels(new FileInfo(target.FullName)));
    }
}
