// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Tests.Propagators.Integrators.Forces.Partials;

namespace IO.Astrodynamics.Tests.Propagators.Variational.External;

/// <summary>
/// A case of the comparison with GMAT (H2): a reference case of F1 whose models GMAT can be given, the fixed step of
/// GMAT's integrator, and what is compared.
/// </summary>
/// <param name="Name">G1 to G6.</param>
/// <param name="Case">The reference case, from <see cref="ReferenceCases"/>.</param>
/// <param name="FixedStep">Fixed step of GMAT's PrinceDormand78, s: GMAT propagates the covariance at a fixed step
/// only, and resolves Φ best at a fixed step (design of #408).</param>
/// <param name="Covariance">Whether P and Q are compared (G1, G2).</param>
/// <param name="GeopotentialDegree">Degree and order of the EGM2008 field of the case, 0 for a point-mass Earth.</param>
internal sealed record GmatCase(string Name, ReferenceCase Case, double FixedStep, bool Covariance, int GeopotentialDegree)
{
    /// <summary>Spacing of the outputs compared over the arc, s.</summary>
    internal const double OutputInterval = 3600.0;

    /// <summary>
    /// The epochs compared: every <see cref="OutputInterval"/> from the start, and the end of the case, in seconds from
    /// the start.
    /// </summary>
    internal double[] Offsets
    {
        get
        {
            double duration = Case.Duration.TotalSeconds;
            var offsets = new List<double>();
            for (double t = OutputInterval; t < duration; t += OutputInterval)
            {
                offsets.Add(t);
            }

            offsets.Add(duration);
            return offsets.ToArray();
        }
    }
}

/// <summary>
/// The cases G1 to G6 of the comparison with GMAT R2026a (H2 of phase 2, feature 1; design of #408), and the file that
/// hands them to GMAT: <c>Data/GmatReference/cases.json</c>, the states and the model parameters as the library holds
/// them, read by <c>tools/gmat_reference/generate_scripts.py</c>.
/// </summary>
/// <remarks>
/// <list type="table">
/// <item><term>G1</term><description>R1: LEO 700 km, two-body, 1 day; Φ, P and Q.</description></item>
/// <item><term>G2</term><description>R2 without drag: LEO 400 km, EGM2008 10×10, Sun, Moon, 1 day; Φ, P and Q.</description></item>
/// <item><term>G3</term><description>R4 without SRP: GEO, EGM2008 70×70, Sun, Moon, the barycentres of Mercury to Neptune,
/// 1 day; Φ.</description></item>
/// <item><term>G4</term><description>R5 without drag: HEO 300 × 36 000 km, EGM2008 10×10, Sun, Moon, 2 revolutions; Φ.</description></item>
/// <item><term>G5</term><description>R3: SSO 700 km, EGM2008 10×10, Sun, Moon, SRP, eclipses, 1 day; Φ and Ψ_Cr.</description></item>
/// <item><term>G6</term><description>R2: G2 with drag (NRLMSISE-00), 1 day; Φ and Ψ_Cd.</description></item>
/// </list>
/// </remarks>
internal static class GmatCases
{
    /// <summary>The cases, in order.</summary>
    internal static readonly string[] Names = { "G1", "G2", "G3", "G4", "G5", "G6" };

    // Fixed steps of GMAT (decision 7): Φ at the step and at half of it agree well below what the comparison resolves
    // (GmatReferenceTests.Phi_AtTheFixedStepOfGmat_IsSettled), and every output epoch is on the grid of the steps
    private static readonly Dictionary<string, double> FixedSteps = new()
    {
        ["G1"] = 10.0, ["G2"] = 10.0, ["G3"] = 60.0, ["G4"] = 10.0, ["G5"] = 2.5, ["G6"] = 10.0
    };

    /// <summary>The case named <paramref name="name"/>.</summary>
    internal static GmatCase Get(ReferenceCases cases, string name)
    {
        var referenceCase = name switch
        {
            "G1" => cases.R1,
            "G2" => cases.R2.Conservative,
            "G3" => cases.R4.Conservative,
            "G4" => cases.R5.Conservative,
            "G5" => cases.R3,
            "G6" => cases.R2,
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
        int degree = name switch { "G1" => 0, "G3" => 70, _ => 10 };
        return new GmatCase(name, referenceCase, FixedSteps[name], name is "G1" or "G2", degree);
    }

    /// <summary>
    /// The initial covariance of the comparison of P: that of <c>CovariancePropagationTests</c> (σ 50, 200 and 30 m, 0.2,
    /// 0.05 and 0.03 m/s in RTN, two correlations), so that the rotation to the inertial frame is exercised, which an
    /// isotropic P0 would not.
    /// </summary>
    internal static StateVector WithCovariance(StateVector state) => CovariancePropagationTests.WithCovariance(state);

    /// <summary>Tangential density of the state noise compensation compared with GMAT's, m²/s³ (q_T alone).</summary>
    internal const double TangentialNoise = 1e-11;

    /// <summary>
    /// Fixed steps of GMAT for its discrete state noise compensation, s, from the coarsest: GMAT adds the discrete noise
    /// at every integration step, so the step is the interval of the discrete form (an <c>UpdateTimeStep</c> longer
    /// than the step changes nothing, measured on G1).
    /// </summary>
    internal static readonly double[] NoiseSteps = { 20.0, 10.0, 5.0, 2.5 };

    /// <summary>
    /// The diagonal of P0 of the runs with the noise, m² and m²/s²: so small that P is almost Q, Q being then P minus
    /// the propagated P0, which resolves it far better than the difference of two covariances of the size of P0.
    /// </summary>
    internal static readonly double[] NoiseInitialDiagonal = { 1e-6, 1e-6, 1e-6, 1e-12, 1e-12, 1e-12 };

    /// <summary>
    /// The content of <c>cases.json</c>: for each case the epoch (TDB), the duration, the initial state (m, m/s, ICRF), the
    /// models with their parameters as the library holds them, the steps of the central differences, and P0 for the
    /// cases of the covariance. Numbers are written to the shortest round-trip form of their double, lines end with LF
    /// on every platform (the committed file).
    /// </summary>
    internal static string ToJson(ReferenceCases cases)
    {
        var root = new JsonObject
        {
            ["comment"] = "Written by GmatCasesFileTests from ReferenceCases and PartialsTestCases; read by " +
                          "tools/gmat_reference/generate_scripts.py and gmat_partials.py.",
            ["cases"] = new JsonArray(Names.Select(name => (JsonNode)CaseNode(Get(cases, name))).ToArray()),
            ["partials"] = PartialsNode(cases)
        };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, NewLine = "\n" }) + "\n";
    }

    private static JsonObject CaseNode(GmatCase gmatCase)
    {
        var referenceCase = gmatCase.Case;
        var state = referenceCase.InitialState;
        var earth = (CelestialBody)state.Observer;
        var sun = new CelestialBody(Stars.Sun);
        var node = new JsonObject
        {
            ["name"] = gmatCase.Name,
            ["from"] = referenceCase.Name,
            ["epoch_tdb"] = TdbEpoch(state.Epoch),
            ["duration_s"] = referenceCase.Duration.TotalSeconds,
            ["output_interval_s"] = GmatCase.OutputInterval,
            ["fixed_step_s"] = gmatCase.FixedStep,
            ["state_m_m_s"] = Numbers(state.Position.X, state.Position.Y, state.Position.Z, state.Velocity.X,
                state.Velocity.Y, state.Velocity.Z),
            ["earth"] = new JsonObject
            {
                ["gm_m3_s2"] = earth.GM,
                ["equatorial_radius_m"] = earth.EquatorialRadius,
                ["flattening"] = earth.Flattening,
                ["geopotential_degree"] = gmatCase.GeopotentialDegree
            },
            ["third_bodies"] = new JsonArray(referenceCase.Bodies.Skip(1).Select(body => (JsonNode)new JsonObject
            {
                ["name"] = body.Name,
                ["naif_id"] = body.NaifId,
                ["gm_m3_s2"] = body.GM
            }).ToArray()),
            ["spacecraft"] = new JsonObject
            {
                ["mass_kg"] = 100.0,
                ["area_m2"] = 10.0,
                ["drag_coefficient"] = ReferenceCases.DragCoefficient,
                ["reflectivity_coefficient"] = ReferenceCases.ReflectivityCoefficient
            },
            ["drag"] = referenceCase.AtmosphericDrag,
            ["srp"] = referenceCase.SolarRadiationPressure,
            ["sun_equatorial_radius_m"] = sun.EquatorialRadius,
            ["srp_flux_at_1_au_w_m2"] = IO.Astrodynamics.Constants.SolarMeanRadiativeLuminosity /
                                       (4.0 * System.Math.PI * IO.Astrodynamics.Constants.AU * IO.Astrodynamics.Constants.AU),
            ["au_m"] = IO.Astrodynamics.Constants.AU,
            ["space_weather"] = new JsonObject { ["f107"] = 150.0, ["f107a"] = 150.0, ["kp"] = 1.0 },
            ["central_differences"] = new JsonObject
            {
                ["position_step_m"] = referenceCase.PositionStep,
                ["velocity_step_m_s"] = referenceCase.VelocityStep,
                ["drag_coefficient_step"] = referenceCase.AtmosphericDrag ? referenceCase.DragCoefficientStep : 0.0,
                ["reflectivity_coefficient_step"] = referenceCase.SolarRadiationPressure
                    ? referenceCase.ReflectivityCoefficientStep
                    : 0.0
            }
        };

        if (gmatCase.Covariance)
        {
            var p0 = WithCovariance(state).Covariance!.Value;
            var values = new double[36];
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 6; j++)
                {
                    values[6 * i + j] = p0.Get(i, j);
                }
            }

            node["covariance"] = new JsonObject
            {
                ["p0_si"] = Numbers(values),
                ["tangential_noise_m2_s3"] = TangentialNoise,
                ["noise_steps_s"] = Numbers(NoiseSteps),
                ["noise_p0_diagonal_si"] = Numbers(NoiseInitialDiagonal)
            };
        }

        // The constants of the harmonics, those of the model (#410), with which egm2008_to_cof.py writes the .cof
        if (earth.Field is GeopotentialGravitationalField geopotential)
        {
            var earthNode = node["earth"]!.AsObject();
            earthNode["geopotential_gm_m3_s2"] = geopotential.GravitationalParameter ?? earth.GM;
            earthNode["geopotential_reference_radius_m"] = geopotential.ReferenceRadius ?? earth.EquatorialRadius;
        }

        return node;
    }

    /// <summary>The forces whose partials are compared at the B7 states, by the name gmat_partials.py knows them.</summary>
    internal static readonly string[] PartialForces = { "earth_point_mass", "earth_geopotential", "moon", "sun", "srp", "drag" };

    /// <summary>Degree and order of the geopotential whose partials are compared.</summary>
    internal const int PartialsGeopotentialDegree = 70;

    /// <summary>
    /// The forces compared at the state <paramref name="stateName"/>: all of <see cref="PartialForces"/>, drag
    /// excepted at GEO, where NRLMSISE-00 gives no density to speak of.
    /// </summary>
    internal static string[] ForcesAt(string stateName) =>
        PartialForces.Where(f => f != "drag" || stateName != PartialsTestCases.Geo).ToArray();

    /// <summary>The identifier of a B7 state in the names of the files.</summary>
    internal static string StateId(string stateName) => stateName switch
    {
        PartialsTestCases.Leo400 => "leo400",
        PartialsTestCases.Leo800 => "leo800",
        PartialsTestCases.Geo => "geo",
        PartialsTestCases.HeoPerigee => "heo_perigee",
        _ => throw new ArgumentOutOfRangeException(nameof(stateName))
    };

    // The four states of the force partials (B7), at the epoch of PartialsTestCases, relative to a point-mass Earth
    private static JsonObject PartialsNode(ReferenceCases cases)
    {
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, PartialsTestCases.Epoch);
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, PartialsTestCases.Epoch);
        var sun = new CelestialBody(Stars.Sun);
        return new JsonObject
        {
            ["epoch_tdb"] = TdbEpoch(PartialsTestCases.Epoch),
            ["geopotential_degree"] = PartialsGeopotentialDegree,
            ["moon_gm_m3_s2"] = moon.GM,
            ["sun_gm_m3_s2"] = sun.GM,
            ["states"] = new JsonArray(PartialsTestCases.StateNames.Select(name =>
            {
                var state = PartialsTestCases.State(name, earth);
                return (JsonNode)new JsonObject
                {
                    ["name"] = name,
                    ["id"] = StateId(name),
                    ["state_m_m_s"] = Numbers(state.Position.X, state.Position.Y, state.Position.Z, state.Velocity.X,
                        state.Velocity.Y, state.Velocity.Z),
                    ["forces"] = new JsonArray(ForcesAt(name).Select(f => (JsonNode)JsonValue.Create(f)).ToArray())
                };
            }).ToArray())
        };
    }

    // GMAT takes its Gregorian epochs to the millisecond (20 Mar 2021 12:00:00.000): an epoch off that grid would be cut
    // short without notice, so it is refused (H3 review of #408, nit 5)
    internal static string TdbEpoch(TimeSystem.Time epoch)
    {
        var dateTime = epoch.ToTDB().DateTime;
        if (dateTime.Ticks % TimeSpan.TicksPerMillisecond != 0)
        {
            throw new ArgumentException($"{dateTime:O} TDB is not on the millisecond grid of GMAT's epochs", nameof(epoch));
        }

        return dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    private static JsonArray Numbers(params double[] values) =>
        new(values.Select(v => (JsonNode)JsonValue.Create(v)).ToArray());

    /// <summary>The culture-invariant shortest round-trip form of <paramref name="value"/>.</summary>
    internal static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
