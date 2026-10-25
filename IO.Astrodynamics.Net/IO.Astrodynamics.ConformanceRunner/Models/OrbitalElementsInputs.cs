namespace IO.Astrodynamics.ConformanceRunner.Models;

/// <summary>
/// The inputs of an <c>orbital_elements</c> case (spec.md section 11.1).
/// </summary>
public class OrbitalElementsInputs
{
    /// <summary>Tagged epoch (spec.md section 3).</summary>
    public string Epoch { get; set; }

    /// <summary>The central body, <c>Earth</c>.</summary>
    public string CentralBody { get; set; }

    /// <summary>The gravitational parameter of the case, km³/s².</summary>
    public double GmKm3S2 { get; set; }

    /// <summary>
    /// A <see cref="StateVectorOrbit"/>, whose outputs are elements, or a <see cref="KeplerianOrbit"/>,
    /// <see cref="KeplerianTrueAnomalyOrbit"/> or <see cref="ModifiedEquinoctialOrbit"/>, whose outputs are the state.
    /// </summary>
    public object Orbit { get; set; }
}

/// <summary>
/// Osculating Keplerian elements with the true anomaly; <c>a_km</c> is negative for e &gt; 1.
/// </summary>
public class KeplerianTrueAnomalyOrbit
{
    public string Type { get; set; }
    public double AKm { get; set; }
    public double E { get; set; }
    public double IDeg { get; set; }
    public double RaanDeg { get; set; }
    public double ArgpDeg { get; set; }
    public double TaDeg { get; set; }
}

/// <summary>
/// Prograde modified equinoctial elements: p, f = e cos ϖ, g = e sin ϖ, h = tan(i/2) cos Ω, k = tan(i/2) sin Ω and the
/// true longitude L.
/// </summary>
public class ModifiedEquinoctialOrbit
{
    public string Type { get; set; }
    public double PKm { get; set; }
    public double F { get; set; }
    public double G { get; set; }
    public double H { get; set; }
    public double K { get; set; }
    public double TrueLongitudeDeg { get; set; }
}
