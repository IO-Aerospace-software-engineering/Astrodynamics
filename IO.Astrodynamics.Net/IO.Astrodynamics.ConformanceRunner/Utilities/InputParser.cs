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
            LightSource = GetString(inputs, "light_source")
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
