// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
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
/// ('3'), and the cache <see cref="ArgumentException"/>. The class runs in the collection of <see cref="ColdStartTests"/>,
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

    [Fact]
    public void BodyWithoutBarycentreData_BuildsWithoutOrbit_AndTheOrientationCacheKeepsThePointsUnavailable_AsOnSpice()
    {
        string output = ColdStartTests.RunChild("(memory-provider)", "memory-provider");
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _output.WriteLine(line);
        }

        Assert.Contains("MEASURE #680 orientation cache", output);
    }
}
