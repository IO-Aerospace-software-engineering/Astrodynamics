using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.ConformanceRunner.Models;

namespace IO.Astrodynamics.ConformanceRunner.Utilities;

public static class InputParser
{
    public static EclipseInputs ParseEclipseInputs(Dictionary<string, object> inputs)
    {
        var result = new EclipseInputs
        {
            Epoch = GetString(inputs, "epoch"),
            OccultingBody = GetString(inputs, "occulting_body"),
            LightSource = GetString(inputs, "light_source"),
            GmKm3S2 = OptionalDouble(inputs, "gm_km3_s2")
        };

        var orbit = inputs["orbit"] as Dictionary<object, object>;
        result.Orbit = ParseOrbit(orbit);

        var sw = inputs["search_window"] as Dictionary<object, object>;
        result.SearchWindow = new SearchWindow
        {
            Start = sw["start"]?.ToString(),
            End = sw["end"]?.ToString()
        };

        return result;
    }

    public static TriadInputs ParseTriadInputs(Dictionary<string, object> inputs)
    {
        var result = new TriadInputs
        {
            Epoch = GetString(inputs, "epoch"),
            PrimaryTarget = GetString(inputs, "primary_target"),
            SecondaryTarget = GetString(inputs, "secondary_target"),
            PrimaryBodyVector = ParseVec3(inputs["primary_body_vector"]),
            SecondaryBodyVector = ParseVec3(inputs["secondary_body_vector"])
        };

        var orbit = inputs["orbit"] as Dictionary<object, object>;
        result.Orbit = ParseOrbit(orbit);

        var fov = inputs["field_of_view"] as Dictionary<object, object>;
        result.FieldOfView = new FieldOfView
        {
            HalfAngleDeg = ToDouble(fov["half_angle_deg"]),
            AxisBody = ParseVec3(fov["axis_body"])
        };

        return result;
    }

    public static PropagatorInputs ParsePropagatorInputs(Dictionary<string, object> inputs)
    {
        var result = new PropagatorInputs
        {
            Epoch = GetString(inputs, "epoch"),
            CentralBody = GetString(inputs, "central_body"),
            GeopotentialDegree = (int)ToDouble(inputs["geopotential_degree"]),
            GeopotentialModel = GetString(inputs, "geopotential_model"),
            StepSizeS = ToDouble(inputs["step_size_s"])
        };

        var orbit = inputs["orbit"] as Dictionary<object, object>;
        result.Orbit = ParseOrbit(orbit);

        var pw = inputs["propagation_window"] as Dictionary<object, object>;
        result.PropagationWindow = new SearchWindow
        {
            Start = pw["start"]?.ToString(),
            End = pw["end"]?.ToString()
        };

        var fm = inputs["force_model"] as Dictionary<object, object>;
        result.ForceModel = new PropagatorForceModel
        {
            Drag = Force(fm["drag"], "drag", drag => new DragInputs
            {
                Model = drag["model"]?.ToString(),
                F107Sfu = ToDouble(drag["f107_sfu"]),
                F107aSfu = ToDouble(drag["f107a_sfu"]),
                Ap = ToDouble(drag["ap"])
            }),
            Srp = Force(fm["srp"], "srp", srp => new SrpInputs
            {
                ShadowBodies = (srp["shadow_bodies"] as List<object>)?.ConvertAll(b => b.ToString()) ?? new List<string>()
            })
        };

        var bodies = inputs["perturbation_bodies"] as List<object>;
        result.PerturbationBodies = bodies?.ConvertAll(b => b.ToString()) ?? new List<string>();

        if (inputs.TryGetValue("spacecraft", out var scObj) && scObj is Dictionary<object, object> sc)
        {
            result.Spacecraft = new SpacecraftInputs
            {
                MassKg = ToDouble(sc["mass_kg"]),
                MaxMassKg = sc.ContainsKey("max_mass_kg") ? ToDouble(sc["max_mass_kg"]) : null,
                SectionalAreaM2 = ToDouble(sc["sectional_area_m2"]),
                DragCoefficient = ToDouble(sc["drag_coefficient"]),
                RadiationPressureCoefficient = ToDouble(sc["radiation_pressure_coefficient"])
            };
        }

        return result;
    }

    public static TrackingDataInputs ParseTrackingDataInputs(Dictionary<string, object> inputs)
    {
        var result = new TrackingDataInputs
        {
            Epoch = GetString(inputs, "epoch"),
            OccultingBody = GetString(inputs, "occulting_body")
        };

        var orbit = inputs["orbit"] as Dictionary<object, object>;
        result.Orbit = ParseOrbit(orbit);

        var sw = inputs["search_window"] as Dictionary<object, object>;
        result.SearchWindow = new SearchWindow
        {
            Start = sw["start"]?.ToString(),
            End = sw["end"]?.ToString()
        };
        result.StepSizeS = sw.ContainsKey("step_size_s") ? ToDouble(sw["step_size_s"]) : 60.0;

        var loc = inputs["location"] as Dictionary<object, object>;
        result.Location = new LocationInput
        {
            ReferenceFrame = loc["reference_frame"]?.ToString(),
            LatitudeDeg = ToDouble(loc["latitude_deg"]),
            LongitudeDeg = ToDouble(loc["longitude_deg"]),
            HeightKm = ToDouble(loc["height_km"])
        };
        if (loc.TryGetValue("ellipsoid", out var ellipsoidObj) && ellipsoidObj is Dictionary<object, object> ellipsoid)
        {
            result.Location.Ellipsoid = ParseEllipsoid(ellipsoid);
        }

        return result;
    }

    public static GeosynchronousInputs ParseGeosynchronousInputs(Dictionary<string, object> inputs)
    {
        return new GeosynchronousInputs
        {
            Epoch = GetString(inputs, "epoch"),
            CentralBody = GetString(inputs, "central_body"),
            LongitudeDeg = ToDouble(inputs["longitude_deg"]),
            LatitudeDeg = ToDouble(inputs["latitude_deg"]),
            GmKm3S2 = OptionalDouble(inputs, "gm_km3_s2"),
            SiderealPeriodS = OptionalDouble(inputs, "sidereal_period_s")
        };
    }

    public static OrbitalElementsInputs ParseOrbitalElementsInputs(Dictionary<string, object> inputs)
    {
        return new OrbitalElementsInputs
        {
            Epoch = GetString(inputs, "epoch"),
            CentralBody = GetString(inputs, "central_body"),
            GmKm3S2 = ToDouble(inputs["gm_km3_s2"]),
            Orbit = ParseOrbit(inputs["orbit"] as Dictionary<object, object>)
        };
    }

    public static TimeConversionInputs ParseTimeConversionInputs(Dictionary<string, object> inputs)
    {
        return new TimeConversionInputs
        {
            Epoch = GetString(inputs, "epoch")
        };
    }

    public static FrameTransformationInputs ParseFrameTransformationInputs(Dictionary<string, object> inputs)
    {
        var state = inputs["state"] as Dictionary<object, object>;
        var result = new FrameTransformationInputs
        {
            Epoch = GetString(inputs, "epoch"),
            FromFrame = GetString(inputs, "from_frame"),
            ToFrame = GetString(inputs, "to_frame"),
            FromCenter = GetString(inputs, "from_center"),
            ToCenter = GetString(inputs, "to_center"),
            PositionKm = ParseVec3(state["position_km"]),
            VelocityKmS = ParseVec3(state["velocity_km_s"]),
            Kernels = (inputs["kernels"] as List<object>)?.ConvertAll(k => k.ToString()) ?? new List<string>()
        };

        if (inputs.TryGetValue("ellipsoid", out var ellipsoidObj) && ellipsoidObj is Dictionary<object, object> ellipsoid)
        {
            result.Ellipsoid = ParseEllipsoid(ellipsoid);
        }

        return result;
    }

    /// <summary>
    /// The propulsion and the chain of a <c>maneuver</c> case (spec.md section 13.1); the propagation is read by
    /// <see cref="ParsePropagatorInputs"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The propulsion or the chain is missing, a step has no label, no type or a
    /// type that is not one of spec.md section 13.1, lacks a field of its type, or two steps share a label.</exception>
    public static ManeuverInputs ParseManeuverInputs(Dictionary<string, object> inputs)
    {
        var propulsion = inputs.TryGetValue("propulsion", out var p) && p is Dictionary<object, object> map
            ? map
            : throw new ArgumentException("A maneuver case has a propulsion block (spec.md section 13.1).");
        var steps = inputs.TryGetValue("maneuvers", out var m) && m is List<object> list && list.Count > 0
            ? list
            : throw new ArgumentException("A maneuver case has a chain of one maneuver or more (spec.md section 13.1).");

        var result = new ManeuverInputs
        {
            FuelMassKg = ToDouble(Required(propulsion, "fuel_mass_kg", "propulsion")),
            IspS = ToDouble(Required(propulsion, "isp_s", "propulsion")),
            Steps = steps.ConvertAll(step => ParseManeuverStep(step as Dictionary<object, object>
                                                              ?? throw new ArgumentException($"A maneuver step is {step}, not a mapping.")))
        };

        var duplicate = result.Steps.GroupBy(s => s.Label).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            throw new ArgumentException($"Two steps of the chain have the label '{duplicate.Key}': labels are unique in a chain (spec.md section 13.1).");
        }

        return result;
    }

    private static ManeuverStep ParseManeuverStep(Dictionary<object, object> step)
    {
        string label = Required(step, "label", "a maneuver step").ToString();
        string where = $"the maneuver step '{label}'";
        var result = new ManeuverStep
        {
            Label = label,
            Type = Required(step, "type", where).ToString(),
            EarliestAfterPreviousS = ToDouble(Required(step, "earliest_after_previous_s", where))
        };

        switch (result.Type)
        {
            case ManeuverStepType.ApoapsisRadius:
                result.TargetApoapsisRadiusKm = ToDouble(Required(step, "target_apoapsis_radius_km", where));
                break;
            case ManeuverStepType.PeriapsisRadius:
                result.TargetPeriapsisRadiusKm = ToDouble(Required(step, "target_periapsis_radius_km", where));
                break;
            case ManeuverStepType.PlaneChange:
                var plane = Mapping(Required(step, "target_plane", where), where);
                result.TargetPlane = new TargetPlane
                {
                    InclinationDeg = ToDouble(Required(plane, "inclination_deg", where)),
                    RaanDeg = ToDouble(Required(plane, "raan_deg", where))
                };
                break;
            case ManeuverStepType.ApsidalAlignment:
                var orbit = Mapping(Required(step, "target_orbit", where), where);
                result.TargetOrbit = new TargetOrbitInPlane
                {
                    AKm = ToDouble(Required(orbit, "a_km", where)),
                    E = ToDouble(Required(orbit, "e", where)),
                    ArgpDeg = ToDouble(Required(orbit, "argp_deg", where))
                };
                break;
            case ManeuverStepType.Phasing:
                result.Revolutions = (int)ToDouble(Required(step, "revolutions", where));
                result.Target = ParseOrbit(Mapping(Required(step, "target", where), where));
                break;
            case ManeuverStepType.Combined:
                result.TargetPeriapsisRadiusKm = ToDouble(Required(step, "target_periapsis_radius_km", where));
                result.TargetInclinationDeg = ToDouble(Required(step, "target_inclination_deg", where));
                break;
            default:
                throw new ArgumentException($"The type '{result.Type}' of {where} is not a maneuver type of spec.md section 13.1.");
        }

        return result;
    }

    /// <summary>The inputs of a <c>lambert</c> case (spec.md section 13.2).</summary>
    /// <exception cref="ArgumentException">A field is missing.</exception>
    public static LambertInputs ParseLambertInputs(Dictionary<string, object> inputs)
    {
        var map = inputs.ToDictionary(kv => (object)kv.Key, kv => kv.Value);
        const string where = "a lambert case";
        var branch = map.TryGetValue("branch", out var b) ? b?.ToString() : null;
        return new LambertInputs
        {
            CentralBody = Required(map, "central_body", where).ToString(),
            DepartureEpoch = Required(map, "departure_epoch", where).ToString(),
            R1Km = ParseVec3(Required(map, "r1_km", where)),
            R2Km = ParseVec3(Required(map, "r2_km", where)),
            TimeOfFlightS = ToDouble(Required(map, "time_of_flight_s", where)),
            Revolutions = (int)ToDouble(Required(map, "revolutions", where)),
            Branch = string.IsNullOrEmpty(branch) || branch is "null" or "~" ? null : branch,
            Direction = Required(map, "direction", where).ToString()
        };
    }

    private static object Required(Dictionary<object, object> map, string key, string where)
    {
        return map.TryGetValue(key, out var value) && value != null
            ? value
            : throw new ArgumentException($"{where} has no {key}.");
    }

    private static Dictionary<object, object> Mapping(object value, string where)
    {
        return value as Dictionary<object, object> ?? throw new ArgumentException($"{where} gives {value} where a mapping is expected.");
    }

    private static EllipsoidInputs ParseEllipsoid(Dictionary<object, object> ellipsoid)
    {
        return new EllipsoidInputs
        {
            EquatorialRadiusKm = ToDouble(ellipsoid["equatorial_radius_km"]),
            Flattening = ToDouble(ellipsoid["flattening"])
        };
    }

    /// <summary>
    /// The files of the suite's <c>data/</c> a case lists in <c>inputs.kernels</c> (spec.md sections 2.1 and 10); none
    /// when the case lists none, as the cases of the earlier categories before conformance-tests#25.
    /// </summary>
    public static List<string> ParseKernels(Dictionary<string, object> inputs)
    {
        return inputs.TryGetValue("kernels", out var kernels) && kernels is List<object> list
            ? list.ConvertAll(k => k.ToString())
            : new List<string>();
    }

    /// <summary>
    /// A force of <c>force_model</c> (spec.md section 12.1): null when it is <c>false</c>, the definition read from its
    /// object otherwise.
    /// </summary>
    /// <exception cref="ArgumentException">The force is neither false nor an object (<c>true</c>, null, another value):
    /// the suite defines every force it turns on, and turns off none by omission.</exception>
    private static T Force<T>(object value, string name, Func<Dictionary<object, object>, T> read) where T : class
    {
        if (value is Dictionary<object, object> definition)
        {
            return read(definition);
        }

        // false as YAML gives it (a string) or as a caller builds it (a bool)
        bool off = value is false || value is string text && bool.TryParse(text, out bool on) && !on;
        return off
            ? null
            : throw new ArgumentException(
                $"force_model.{name} is '{value ?? "null"}': it is false or the object that defines the force (spec.md section 12.1).");
    }

    /// <summary>The value of an optional number of the inputs, or null.</summary>
    public static double? OptionalDouble(Dictionary<string, object> inputs, string key)
    {
        return inputs.TryGetValue(key, out var value) && value != null ? ToDouble(value) : null;
    }

    /// <summary>An orbit of the inputs (<c>orbit</c>, a phasing <c>target</c>) by its <c>type</c>.</summary>
    /// <exception cref="ArgumentException">The type is not one of the suite's.</exception>
    public static object ParseOrbit(Dictionary<object, object> orbit)
    {
        var type = orbit["type"]?.ToString();
        if (type == "keplerian")
        {
            return new KeplerianOrbit
            {
                Type = type,
                AKm = ToDouble(orbit["a_km"]),
                E = ToDouble(orbit["e"]),
                IDeg = ToDouble(orbit["i_deg"]),
                RaanDeg = ToDouble(orbit["raan_deg"]),
                ArgpDeg = ToDouble(orbit["argp_deg"]),
                MaDeg = ToDouble(orbit["ma_deg"])
            };
        }

        if (type == "keplerian_true_anomaly")
        {
            return new KeplerianTrueAnomalyOrbit
            {
                Type = type,
                AKm = ToDouble(orbit["a_km"]),
                E = ToDouble(orbit["e"]),
                IDeg = ToDouble(orbit["i_deg"]),
                RaanDeg = ToDouble(orbit["raan_deg"]),
                ArgpDeg = ToDouble(orbit["argp_deg"]),
                TaDeg = ToDouble(orbit["ta_deg"])
            };
        }

        if (type == "modified_equinoctial")
        {
            return new ModifiedEquinoctialOrbit
            {
                Type = type,
                PKm = ToDouble(orbit["p_km"]),
                F = ToDouble(orbit["f"]),
                G = ToDouble(orbit["g"]),
                H = ToDouble(orbit["h"]),
                K = ToDouble(orbit["k"]),
                TrueLongitudeDeg = ToDouble(orbit["true_longitude_deg"])
            };
        }

        if (type == "state_vector")
        {
            return new StateVectorOrbit
            {
                Type = type,
                PositionKm = ParseVec3(orbit["position_km"]),
                VelocityKmS = ParseVec3(orbit["velocity_km_s"])
            };
        }

        if (type == "tle")
        {
            return new TleOrbit
            {
                Type = type,
                Name = orbit["name"]?.ToString(),
                Line1 = orbit["line1"]?.ToString(),
                Line2 = orbit["line2"]?.ToString()
            };
        }

        if (type == "bsp_source")
        {
            return new BspSourceOrbit
            {
                Type = type,
                NaifId = (int)ToDouble(orbit["naif_id"]),
                Filename = orbit["filename"]?.ToString(),
                Aberration = orbit.ContainsKey("aberration") ? orbit["aberration"]?.ToString() : null
            };
        }

        throw new ArgumentException($"Unknown orbit type: {type}");
    }

    public static double[] ParseVec3(object obj)
    {
        if (obj is List<object> list)
        {
            if (list.Count != 3) throw new ArgumentException($"Expected 3-element vector, got {list.Count}");
            return new[] { ToDouble(list[0]), ToDouble(list[1]), ToDouble(list[2]) };
        }

        throw new ArgumentException($"Cannot parse vec3 from {obj?.GetType().Name}");
    }

    private static string GetString(Dictionary<string, object> dict, string key)
    {
        return dict.TryGetValue(key, out var val) ? val?.ToString() : null;
    }

    public static double ToDouble(object obj)
    {
        return obj switch
        {
            double d => d,
            int i => i,
            long l => l,
            float f => f,
            string s => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
            _ => Convert.ToDouble(obj)
        };
    }
}
