using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace IO.Astrodynamics.ConformanceRunner.Models;

public class ToleranceConfig
{
    [YamlMember(Alias = "version")]
    public string Version { get; set; }

    [YamlMember(Alias = "defaults")]
    public Dictionary<string, TolerancePair> Defaults { get; set; }
}

public class TolerancePair
{
    [YamlMember(Alias = "abs_tol")]
    public double AbsTol { get; set; }

    [YamlMember(Alias = "rel_tol")]
    public double RelTol { get; set; }
}
