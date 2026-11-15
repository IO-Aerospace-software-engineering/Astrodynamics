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

    public static IEnumerable<object[]> LagrangeIdPaths() =>
        from id in MemoryDataProviderTests.LagrangeIdValues
        from path in new[] { "GetCelestialBodyInfo", "new CelestialBody", "CelestialItem.Create" }
        select new object[] { id, path };

    public static IEnumerable<object[]> UnknownIdPaths() =>
        from id in MemoryDataProviderTests.UnknownIdValues
        from path in new[] { "GetCelestialBodyInfo", "new CelestialBody", "CelestialItem.Create" }
        select new object[] { id, path };
}
