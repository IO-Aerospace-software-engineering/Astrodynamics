using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>orbital_elements</c> category (spec.md section 11.1): the elements of a state, or the state of elements given
/// with the mean anomaly, the true anomaly or as modified equinoctial elements, by the framework's two-body conversions.
/// </summary>
/// <remarks>
/// <para>
/// The case carries its gravitational parameter, and the framework takes the Earth's from the kernels: the two must be
/// the same double (398600435507022.6 m³/s² for the lot-1 cases), or the case is an ERROR.
/// </para>
/// <para>
/// The outputs come from the table <see cref="ElementOutputs"/> (a state input) or <see cref="StateOutputs"/> (an
/// elements input), each with the condition under which spec.md defines it, evaluated on the input as written: an
/// output that is not defined is not produced. An output the framework cannot compute is given as its exception
/// (<see cref="SolverOutput"/>), an ERROR only when the golden holds it. The conversion does not depend on the axes, so
/// every object is built in ICRF, and the outputs are in the axes of the input.
/// </para>
/// </remarks>
public class OrbitalElementsSolver : ICategorySolver
{
    // m³ per km³ and m² per km²
    private const double CubicMetresPerCubicKilometre = 1e9;
    private const double SquareMetresPerSquareKilometre = 1e6;

    private sealed record Output(string Name, Func<StateVector, bool> IsDefined, Func<StateVector, object> Value);

    /// <summary>The outputs of a state input (spec.md section 11.1).</summary>
    private static readonly Output[] ElementOutputs =
    {
        new("semi_major_axis_km", NotParabolic, sv => UnitConversion.MToKm(sv.SemiMajorAxis())),
        new("eccentricity", Always, sv => sv.Eccentricity()),
        new("inclination_deg", Always, sv => UnitConversion.RadToDeg(sv.Inclination())),
        new("raan_deg", NotEquatorial, sv => UnitConversion.RadToDeg(sv.AscendingNode())),
        new("argp_deg", sv => NotEquatorial(sv) && NotCircular(sv), sv => UnitConversion.RadToDeg(sv.ArgumentOfPeriapsis())),
        new("true_anomaly_deg", NotCircular, sv => UnitConversion.RadToDeg(sv.TrueAnomaly())),
        // The closed form of the true anomaly; the Newton solver of the mean anomaly is covered by the mean-anomaly input
        new("eccentric_anomaly_deg", EllipticNotCircular, sv => UnitConversion.RadToDeg(sv.EccentricAnomaly(sv.TrueAnomaly()))),
        new("mean_anomaly_deg", EllipticNotCircular, sv => UnitConversion.RadToDeg(sv.MeanAnomaly())),
        new("hyperbolic_anomaly_deg", Hyperbolic, sv => UnitConversion.RadToDeg(sv.EccentricAnomaly(sv.TrueAnomaly()))),
        new("hyperbolic_mean_anomaly_deg", Hyperbolic, sv => UnitConversion.RadToDeg(sv.MeanAnomaly())),
        new("argument_of_latitude_deg", NotEquatorial,
            sv => UnitConversion.RadToDeg(sv.ArgumentOfPeriapsis() + sv.TrueAnomaly())),
        new("longitude_of_periapsis_deg", NotCircular,
            sv => UnitConversion.RadToDeg(sv.AscendingNode() + sv.ArgumentOfPeriapsis())),
        new("true_longitude_deg", Always, sv => UnitConversion.RadToDeg(sv.TrueLongitude())),
        new("mean_longitude_deg", Elliptic, sv => UnitConversion.RadToDeg(sv.MeanLongitude())),
        new("periapsis_radius_km", NotParabolic, sv => UnitConversion.MToKm(sv.PerigeeRadius())),
        new("apoapsis_radius_km", Elliptic, sv => UnitConversion.MToKm(sv.ApogeeVector().Magnitude())),
        new("semi_latus_rectum_km", Always, sv => UnitConversion.MToKm(sv.SemiLatusRectum())),
        // Period() is a TimeSpan, in ticks of 100 ns
        new("period_s", Elliptic, sv => sv.Period().TotalSeconds),
        new("specific_energy_km2_s2", Always, sv => sv.SpecificOrbitalEnergy() / SquareMetresPerSquareKilometre),
        new("angular_momentum_km2_s", Always, sv => Components(sv.SpecificAngularMomentum(), SquareMetresPerSquareKilometre)),
        new("periapsis_speed_km_s", NotParabolic, sv => UnitConversion.MSToKmS(sv.PerigeeVelocity())),
        new("apoapsis_speed_km_s", Elliptic, sv => UnitConversion.MSToKmS(sv.ApogeeVelocity())),
        new("mean_motion_rad_s", NotParabolic, sv => sv.MeanMotion()),
        new("mod_equinoctial_f", Always, sv => sv.ToEquinoctial().F),
        new("mod_equinoctial_g", Always, sv => sv.ToEquinoctial().G),
        new("mod_equinoctial_h", NotRetrogradeEquatorial, sv => sv.ToEquinoctial().H),
        new("mod_equinoctial_k", NotRetrogradeEquatorial, sv => sv.ToEquinoctial().K),
        new("right_ascension_deg", OffPolarAxis, sv => UnitConversion.RadToDeg(sv.ToEquatorial().RightAscension)),
        new("declination_deg", Always, sv => UnitConversion.RadToDeg(sv.ToEquatorial().Declination))
    };

    /// <summary>The outputs of an elements input.</summary>
    private static readonly Output[] StateOutputs =
    {
        new("position_km", Always, sv => Components(sv.Position, 1000.0)),
        new("velocity_km_s", Always, sv => Components(sv.Velocity, 1000.0))
    };

    public string Category => "orbital_elements";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParseOrbitalElementsInputs(caseInput.Inputs);
        if (inputs.CentralBody != "Earth")
        {
            throw new ArgumentException($"The central body {inputs.CentralBody} is not one of the orbital_elements category (Earth).");
        }

        var epoch = EpochParser.Parse(inputs.Epoch);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch);
        double gm = inputs.GmKm3S2 * CubicMetresPerCubicKilometre;
        if (gm != earth.GM)
        {
            throw new InvalidOperationException(
                $"The gravitational parameter of the case, {gm:R} m3/s2, is not that of the kernels, {earth.GM:R} m3/s2.");
        }

        var (state, outputs) = inputs.Orbit is StateVectorOrbit stateInput
            ? (new StateVector(Meters(stateInput.PositionKm), MetersPerSecond(stateInput.VelocityKmS), earth, epoch, Frame.ICRF),
                ElementOutputs)
            : (Elements(inputs.Orbit, earth, epoch).ToStateVector(), StateOutputs);

        return outputs.Where(output => output.IsDefined(state))
            .ToDictionary(output => output.Name, output => SolverOutput.Of(() => output.Value(state)));
    }

    private static OrbitalParameters.OrbitalParameters Elements(object orbit, CelestialBody earth, Time epoch)
    {
        return orbit switch
        {
            KeplerianOrbit k => new KeplerianElements(UnitConversion.KmToM(k.AKm), k.E, UnitConversion.DegToRad(k.IDeg),
                UnitConversion.DegToRad(k.RaanDeg), UnitConversion.DegToRad(k.ArgpDeg), UnitConversion.DegToRad(k.MaDeg), earth,
                epoch, Frame.ICRF),
            KeplerianTrueAnomalyOrbit t => FromTrueAnomaly(t, earth, epoch),
            ModifiedEquinoctialOrbit m => new EquinoctialElements(UnitConversion.KmToM(m.PKm), m.F, m.G, m.H, m.K,
                UnitConversion.DegToRad(m.TrueLongitudeDeg), earth, epoch, Frame.ICRF),
            _ => throw new ArgumentException($"The orbit {orbit?.GetType().Name} is not an input of the orbital_elements category.")
        };
    }

    /// <summary>
    /// Keplerian elements whose true anomaly is the given one: the framework takes the mean anomaly, here M(ν) from the
    /// closed-form eccentric anomaly, and the true anomaly, which it then uses as it is.
    /// </summary>
    private static KeplerianElements FromTrueAnomaly(KeplerianTrueAnomalyOrbit orbit, CelestialBody earth, Time epoch)
    {
        double a = UnitConversion.KmToM(orbit.AKm);
        double i = UnitConversion.DegToRad(orbit.IDeg);
        double raan = UnitConversion.DegToRad(orbit.RaanDeg);
        double argp = UnitConversion.DegToRad(orbit.ArgpDeg);
        double nu = UnitConversion.DegToRad(orbit.TaDeg);
        var shape = new KeplerianElements(a, orbit.E, i, raan, argp, 0.0, earth, epoch, Frame.ICRF);
        double m = OrbitalParameters.OrbitalParameters.TrueAnomalyToMeanAnomaly(nu, orbit.E, shape.EccentricAnomaly(nu));
        return new KeplerianElements(a, orbit.E, i, raan, argp, m, earth, epoch, Frame.ICRF, trueAnomaly: nu);
    }

    private static Vector3 Meters(double[] km)
    {
        return new Vector3(UnitConversion.KmToM(km[0]), UnitConversion.KmToM(km[1]), UnitConversion.KmToM(km[2]));
    }

    private static Vector3 MetersPerSecond(double[] kmS)
    {
        return new Vector3(UnitConversion.KmSToMS(kmS[0]), UnitConversion.KmSToMS(kmS[1]), UnitConversion.KmSToMS(kmS[2]));
    }

    private static double[] Components(Vector3 vector, double scale)
    {
        return new[] { vector.X / scale, vector.Y / scale, vector.Z / scale };
    }

    // The conditions of spec.md section 11.1, on the input as written: e from the state, equatorial when h_x = h_y = 0
    private static bool Always(StateVector sv) => true;
    private static bool Elliptic(StateVector sv) => sv.Eccentricity() < 1.0;
    private static bool EllipticNotCircular(StateVector sv) => NotCircular(sv) && Elliptic(sv);
    private static bool Hyperbolic(StateVector sv) => sv.Eccentricity() > 1.0;
    private static bool NotParabolic(StateVector sv) => sv.Eccentricity() != 1.0;
    private static bool NotCircular(StateVector sv) => sv.Eccentricity() != 0.0;

    private static bool NotEquatorial(StateVector sv)
    {
        var h = sv.SpecificAngularMomentum();
        return h.X != 0.0 || h.Y != 0.0;
    }

    // i < 180°: not equatorial, or equatorial and prograde
    private static bool NotRetrogradeEquatorial(StateVector sv) => NotEquatorial(sv) || sv.SpecificAngularMomentum().Z > 0.0;

    private static bool OffPolarAxis(StateVector sv) => sv.Position.X != 0.0 || sv.Position.Y != 0.0;
}
