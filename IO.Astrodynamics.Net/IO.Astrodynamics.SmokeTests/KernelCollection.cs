using Xunit;

namespace IO.Astrodynamics.SmokeTests;

/// <summary>
/// The test classes that load SPICE kernels and those that clear or unload them: run one after the other, so that a class
/// that clears the kernels never does so while another propagates.
/// </summary>
[CollectionDefinition(Name)]
public class KernelCollection
{
    public const string Name = "SPICE kernels";
}
