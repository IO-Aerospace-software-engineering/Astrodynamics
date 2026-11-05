using System.Collections.Generic;

namespace IO.Astrodynamics.ConformanceRunner.Models;

/// <summary>
/// The inputs of a <c>maneuver</c> case beyond those of a propagation (spec.md section 13.1): the propulsion and the
/// chain of impulsive maneuvers. The propagation itself (state, window, force model, kernels) is read as a
/// <c>propagator</c> case's, <c>spacecraft.mass_kg</c> being the dry mass.
/// </summary>
public class ManeuverInputs
{
    /// <summary>The fuel at the start, kg.</summary>
    public double FuelMassKg { get; set; }

    /// <summary>The specific impulse of every burn, s.</summary>
    public double IspS { get; set; }

    /// <summary>The steps of the chain, in their order.</summary>
    public List<ManeuverStep> Steps { get; set; }
}

/// <summary>
/// A step of a maneuver chain (spec.md section 13.1): its label, its type, its earliest epoch after the previous burn
/// (after the start for the first step) and the fields of its type, null for the others.
/// </summary>
public class ManeuverStep
{
    /// <summary>The label of the step, unique in the chain; its outputs are named <c>burns[&lt;label&gt;].&lt;field&gt;</c>.</summary>
    public string Label { get; set; }

    /// <summary>The type of the step (<see cref="ManeuverStepType"/>).</summary>
    public string Type { get; set; }

    /// <summary>The earliest epoch, TT seconds after the previous burn, or after the start for the first step.</summary>
    public double EarliestAfterPreviousS { get; set; }

    /// <summary>The osculating apoapsis radius after the burn, km (<c>apoapsis_radius</c>).</summary>
    public double? TargetApoapsisRadiusKm { get; set; }

    /// <summary>
    /// The osculating periapsis radius after the burn, km (<c>periapsis_radius</c>, <c>combined</c>).
    /// </summary>
    public double? TargetPeriapsisRadiusKm { get; set; }

    /// <summary>The inclination after the burn, deg in ICRF (<c>combined</c>).</summary>
    public double? TargetInclinationDeg { get; set; }

    /// <summary>The target plane, deg in ICRF (<c>plane_change</c>).</summary>
    public TargetPlane TargetPlane { get; set; }

    /// <summary>The target orbit in the current plane (<c>apsidal_alignment</c>).</summary>
    public TargetOrbitInPlane TargetOrbit { get; set; }

    /// <summary>The number of phasing revolutions N (<c>phasing</c>).</summary>
    public int? Revolutions { get; set; }

    /// <summary>The state of the target at the start (<c>phasing</c>), a <see cref="StateVectorOrbit"/> or another orbit.</summary>
    public object Target { get; set; }
}

/// <summary>The types of the steps of a maneuver chain (spec.md section 13.1).</summary>
public static class ManeuverStepType
{
    public const string ApoapsisRadius = "apoapsis_radius";
    public const string PeriapsisRadius = "periapsis_radius";
    public const string PlaneChange = "plane_change";
    public const string ApsidalAlignment = "apsidal_alignment";
    public const string Phasing = "phasing";
    public const string Combined = "combined";
}

/// <summary>A plane fixed in ICRF by its inclination and the right ascension of its ascending node, deg.</summary>
public class TargetPlane
{
    public double InclinationDeg { get; set; }
    public double RaanDeg { get; set; }
}

/// <summary>
/// An orbit in the current plane by its semi-major axis (km), eccentricity and argument of periapsis (deg, from the
/// current plane's ascending node).
/// </summary>
public class TargetOrbitInPlane
{
    public double AKm { get; set; }
    public double E { get; set; }
    public double ArgpDeg { get; set; }
}

/// <summary>
/// The inputs of a <c>lambert</c> case (spec.md section 13.2).
/// </summary>
public class LambertInputs
{
    /// <summary>The central body, <c>Earth</c>.</summary>
    public string CentralBody { get; set; }

    /// <summary>The departure epoch, informative: it only dates the two states.</summary>
    public string DepartureEpoch { get; set; }

    /// <summary>The departure position, km in ICRF.</summary>
    public double[] R1Km { get; set; }

    /// <summary>The arrival position, km in ICRF.</summary>
    public double[] R2Km { get; set; }

    /// <summary>The time of flight, s of the dynamical time (TT about the Earth).</summary>
    public double TimeOfFlightS { get; set; }

    /// <summary>The number of complete revolutions N.</summary>
    public int Revolutions { get; set; }

    /// <summary><c>lower_a</c> or <c>higher_a</c> for N ≥ 1, null for N = 0.</summary>
    public string Branch { get; set; }

    /// <summary><c>prograde</c> or <c>retrograde</c>, the sign of the z component of r1 × v1 in ICRF.</summary>
    public string Direction { get; set; }
}
