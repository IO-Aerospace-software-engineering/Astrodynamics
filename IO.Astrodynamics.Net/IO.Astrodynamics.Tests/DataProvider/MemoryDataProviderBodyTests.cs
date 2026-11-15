// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.DataProvider;

/// <summary>
/// The library on a <see cref="IO.Astrodynamics.DataProvider.MemoryDataProvider"/> set as the global provider (#680), in
/// the child process <c>IO.Astrodynamics.Tests.ColdStart</c> (mode <c>memory-provider</c>): the test process never swaps
/// the global provider (#631), since an object another test class builds during the swap, and a static frame first built
/// during it, keep the provider for the whole process.
/// </summary>
/// <remarks>
/// The child checks that the provider's missing data throw the type SPICE throws, which the library's handlers of missing
/// data catch: the Earth built with its info only, with its barycentre's, with the SSB's, without a state or a day before
/// its stored states, and the Moon with its own info only, have a null orbit and an infinite sphere of influence, as the
/// Earth built on SPICE at 1500-01-01, outside de440s (the reference, checked in the same child before the swap); within
/// the stored states the Earth has its orbit. The orientation cache of a propagation over a memory frame whose two entries
/// an hour apart do not reach its buffer points builds, and its orientation between two grid points is the closed form of
/// the rotation about the entries' axis. Before #680 the child threw <see cref="System.Collections.Generic.KeyNotFoundException"/>
/// ('3'), and the cache <see cref="ArgumentException"/>. An identifier the provider has no information on throws, through
/// each public path, the <see cref="ArgumentException"/> naming <c>naifId</c> of the SPICE provider (#680, decided by
/// S. Guillet, 2026-11-14), while the Earth and the Moon above, whose barycentre's information is missing, still build; a
/// Lagrange point's, through <see cref="IO.Astrodynamics.Body.CelestialItem.Create"/>, which reads its state only, throws
/// <see cref="InvalidOperationException"/> for its missing state, as SPICE for L3, whose state its kernels lack. The
/// class runs in the collection of <see cref="ColdStartTests"/>,
/// alone, for the handles a child inherits on Windows.
/// </remarks>
[Collection(nameof(ColdStartCollection))]
public class MemoryDataProviderBodyTests
{
    private readonly ITestOutputHelper _output;

    public MemoryDataProviderBodyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // One child for the class: its checks cover every case of the theory below
    private static readonly Lazy<string> ChildOutput = new(() => ColdStartTests.RunChild("(memory-provider)", "memory-provider"));

    [Fact]
    public void BodyWithoutBarycentreData_BuildsWithoutOrbit_AndTheOrientationCacheKeepsThePointsUnavailable_AsOnSpice()
    {
        string output = ChildOutput.Value;
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _output.WriteLine(line);
        }

        Assert.Contains("MEASURE #680 orientation cache", output);
    }

    /// <summary>
    /// An id neither provider has information on, through each public path that reads the global provider's body
    /// information: the memory provider throws the exception type and the parameter name of the SPICE provider, the
    /// reference, <see cref="ArgumentException"/> naming <c>naifId</c> (#680, decided by S. Guillet, 2026-11-14; #672 on
    /// SPICE). The child checks both and writes one line per id and path; it exits 1 on a difference.
    /// </summary>
    [Theory]
    [MemberData(nameof(UnknownIdPaths))]
    public void UnknownId_ThrowsAsTheSpiceProvider(int id, string path)
    {
        Assert.Contains($"OK unknown id {id}, {path}: SPICE ArgumentException (naifId)", ChildOutput.Value);
        Assert.Contains($"OK unknown id {id}, {path}: memory ArgumentException (naifId), SPICE ArgumentException (naifId)",
            ChildOutput.Value);
    }

    /// <summary>
    /// The Lagrange points L1 to L5, which SPICE does not name: <c>GetCelestialBodyInfo</c> and <c>new CelestialBody(id)</c>
    /// throw the <see cref="ArgumentException"/> naming <c>naifId</c> of the SPICE provider, while
    /// <see cref="CelestialItem.Create"/> reads no information of a Lagrange point and builds it from its state: SPICE builds
    /// the points its kernels hold and throws <see cref="InvalidOperationException"/> for L3, which they do not hold, and the
    /// memory provider without their states throws that type for all five, missing data, not a bad argument (#680 review,
    /// 2026-11-14).
    /// </summary>
    [Theory]
    [MemberData(nameof(LagrangeIdPaths))]
    public void LagrangeId_ThrowsAsTheSpiceProvider_CreateReadsTheState(int id, string path)
    {
        if (path == "CelestialItem.Create")
        {
            Assert.Contains($"OK Lagrange id {id}, {path}: SPICE {(id == 393 ? "InvalidOperationException" : "no exception")}",
                ChildOutput.Value);
            Assert.Contains($"OK Lagrange id {id}, {path}: memory without its state InvalidOperationException", ChildOutput.Value);
            return;
        }

        Assert.Contains($"OK Lagrange id {id}, {path}: SPICE ArgumentException (naifId)", ChildOutput.Value);
        Assert.Contains($"OK Lagrange id {id}, {path}: memory ArgumentException (naifId), SPICE ArgumentException (naifId)",
            ChildOutput.Value);
    }

    /// <summary>
    /// The static objects of <c>Barycenters</c>, <c>PlanetsAndMoons</c> and <c>Stars</c> on a memory provider set before any
    /// static class is read, holding the Earth's information only: <c>CelestialItem.Create(391)</c> throws the provider's
    /// <see cref="InvalidOperationException"/> for the missing state of L1, a provider-backed member of a static object the
    /// provider's <see cref="ArgumentException"/> naming <c>naifId</c> at each read; once the data are added, the members
    /// are the data given, combined by exact subtractions. Before, the first read of <c>Barycenters</c> threw a
    /// <see cref="TypeInitializationException"/> that the runtime kept for the life of the process.
    /// </summary>
    [Fact]
    public void StaticBodies_OnAMemoryProvider_ResolveAtFirstRead_AndRetryOnFailure()
    {
        string output = ColdStartTests.RunChild("(static-bodies-memory)", "static-bodies-memory");
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _output.WriteLine(line);
        }

        Assert.Contains("OK CelestialItem.Create(391) without its state: InvalidOperationException", output);
        Assert.Contains("OK SOLAR_SYSTEM_BARYCENTER.GM without its information, read 2: ArgumentException", output);
        Assert.Contains("OK L1's state is the stored state of 391 minus that of the Earth barycentre", output);
        Assert.Contains("OK SUN_BODY's orbit is its state about the solar system barycentre", output);
    }

    /// <summary>
    /// The static objects on SPICE, the kernels loaded before any read, against SPICE through <c>SpiceAPI</c> (the body
    /// information of <c>bodvrd</c>, the state of <c>spkezr</c>), exactly.
    /// </summary>
    [Fact]
    public void StaticBodies_OnSpice_AreTheKernelValues()
    {
        string output = ColdStartTests.RunChild("(static-bodies-spice)", "static-bodies-spice");
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _output.WriteLine(line);
        }

        Assert.Contains("OK EARTH_BODY's orbit is spkezr's state of 399 about 3 in ECLIPJ2000 at J2000 TDB", output);
        Assert.Contains("OK SOLAR_SYSTEM_BARYCENTER has no orbit", output);
    }

    /// <summary>
    /// The static objects first read on an empty memory provider, then on a SPICE provider set afterwards with the kernels
    /// loaded: each read retries on the provider set at that time, and the members are SPICE's (<c>bodvrd</c>, <c>spkezr</c>
    /// through <c>SpiceAPI</c>), exactly; once read, an object keeps the provider it read its data from, for its data and its
    /// ephemeris. Before, the retry went back to the provider current when the class was initialized: the objects threw
    /// the memory provider's <see cref="ArgumentException"/> for the life of the process.
    /// </summary>
    [Fact]
    public void StaticBodies_ReadFirstOnAMemoryProvider_ResolveOnTheProviderSetAtTheirNextRead()
    {
        string output = ColdStartTests.RunChild("(static-bodies-switch)", "static-bodies-switch");
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _output.WriteLine(line);
        }

        Assert.Contains("OK SUN_BODY.GM on the empty memory provider: ArgumentException", output);
        Assert.Contains("OK EARTH_BODY's orbit after the switch is spkezr's state of 399 about 3 in ECLIPJ2000 at J2000 TDB", output);
        Assert.Contains("OK EARTH_BARYCENTER's ephemeris after the switch is spkezr's state of 3 about 0 in ICRF", output);
        Assert.Contains("OK EARTH_BARYCENTER's ephemeris, once its data are read, stays SPICE's under a later provider", output);
    }

    /// <summary>
    /// The static objects read on SPICE before any kernel is loaded, then after <c>LoadKernels</c>, in a fresh process: SPICE
    /// names the bodies without kernels but gives no <c>BODYnnn_GM</c> nor <c>BODYnnn_RADII</c>, and a read of a static
    /// object whose data need them throws <see cref="InvalidOperationException"/> naming the missing variable, at each read;
    /// once the kernels are loaded, the members are SPICE's (<c>bodvrd</c>, <c>spkezr</c> through <c>SpiceAPI</c>), exactly.
    /// Before, the first read cached the zeros SPICE answered (GM 0, radii 0, J2 NaN, no orbit, the frame IAU_EARTH) for the
    /// life of the process.
    /// </summary>
    [Fact]
    public void StaticBodies_ReadBeforeTheKernels_ThrowAndCacheNothing_ThenAreTheKernelValues()
    {
        string output = ColdStartTests.RunChild("(static-bodies-before-kernels)", "static-bodies-before-kernels");
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _output.WriteLine(line);
        }

        Assert.Contains("OK EARTH_BODY.GM before the kernels, read 2: InvalidOperationException", output);
        Assert.Contains("OK EARTH_BODY's orbit after the kernels is spkezr's state of 399 about 3 in ECLIPJ2000 at J2000 TDB", output);
        Assert.Contains("OK EARTH_BODY.Frame after the kernels ITRF93, SPICE ITRF93", output);
    }

    public static IEnumerable<object[]> LagrangeIdPaths() =>
        from id in MemoryDataProviderTests.LagrangeIdValues
        from path in new[] { "GetCelestialBodyInfo", "new CelestialBody", "CelestialItem.Create" }
        select new object[] { id, path };

    public static IEnumerable<object[]> UnknownIdPaths() =>
        from id in MemoryDataProviderTests.UnknownIdValues
        from path in new[] { "GetCelestialBodyInfo", "new CelestialBody", "CelestialItem.Create" }
        select new object[] { id, path };
}
