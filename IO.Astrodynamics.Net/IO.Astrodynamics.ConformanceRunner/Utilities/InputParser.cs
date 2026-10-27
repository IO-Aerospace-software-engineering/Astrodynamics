using System;
using System.Collections.Generic;
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
            Drag = Convert.ToBoolean(fm["drag"]),
            Srp = Convert.ToBoolean(fm["srp"])
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

    /// <summary>The value of an optional number of the inputs, or null.</summary>
    public static double? OptionalDouble(Dictionary<string, object> inputs, string key)
    {
        return inputs.TryGetValue(key, out var value) && value != null ? ToDouble(value) : null;
    }

    private static object ParseOrbit(Dictionary<object, object> orbit)
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
