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
            GmKm3S2 = OptionalDouble(inputs, "gm_km3_s2"),
            OccultingBodies = OptionalStrings(inputs, "occulting_bodies"),
            LightTime = GetString(inputs, "light_time")
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

        result.Location = ParseLocation(inputs["location"] as Dictionary<object, object>);
        return result;
    }

    // The station of tracking_data (spec.md section 12.3) and ground_station (14.1)
    private static LocationInput ParseLocation(Dictionary<object, object> loc)
    {
        var location = new LocationInput
        {
            ReferenceFrame = loc["reference_frame"]?.ToString(),
            LatitudeDeg = ToDouble(loc["latitude_deg"]),
            LongitudeDeg = ToDouble(loc["longitude_deg"]),
            HeightKm = ToDouble(loc["height_km"])
        };
        if (loc.TryGetValue("ellipsoid", out var ellipsoidObj) && ellipsoidObj is Dictionary<object, object> ellipsoid)
        {
            location.Ellipsoid = ParseEllipsoid(ellipsoid);
        }

        return location;
    }

    /// <summary>The inputs of a <c>ground_station</c> case (spec.md section 14.1).</summary>
    /// <exception cref="ArgumentException">A field is missing.</exception>
    public static GroundStationInputs ParseGroundStationInputs(Dictionary<string, object> inputs)
    {
        var map = inputs.ToDictionary(kv => (object)kv.Key, kv => kv.Value);
        const string where = "a ground_station case";
        var window = Mapping(Required(map, "search_window", where), where);
        return new GroundStationInputs
        {
            Epoch = Required(map, "epoch", where).ToString(),
            Orbit = ParseOrbit(Mapping(Required(map, "orbit", where), where)),
            GmKm3S2 = OptionalDouble(inputs, "gm_km3_s2"),
            SearchWindow = new SearchWindow
            {
                Start = Required(window, "start", where).ToString(),
                End = Required(window, "end", where).ToString()
            },
            StepSizeS = window.TryGetValue("step_size_s", out var step) && step != null ? ToDouble(step) : DefaultStationStepS,
            Location = ParseLocation(Mapping(Required(map, "location", where), where)),
            ElevationMaskDeg = ToDouble(Required(map, "elevation_mask_deg", where)),
            Aberration = Required(map, "aberration", where).ToString(),
            OccultingBodies = OptionalStrings(inputs, "occulting_bodies") ?? new List<string>(),
            SampleEpochs = OptionalStrings(inputs, "sample_epochs") ?? new List<string>()
        };
    }

    // The search step of a station case that gives none: that of the suite's cases (spec.md section 14.1)
    private const double DefaultStationStepS = 10.0;

    /// <summary>
    /// The events of an <c>orbital_events</c> case (spec.md section 14.2); the propagation is read by
    /// <see cref="ParsePropagatorInputs"/>.
    /// </summary>
    /// <exception cref="ArgumentException">No event, an event that is not a mapping, an unknown type, an altitude event
    /// without its value or with an unknown direction, or a type listed twice.</exception>
    public static List<OrbitalEventRequest> ParseOrbitalEvents(Dictionary<string, object> inputs)
    {
        var events = inputs.TryGetValue("events", out var e) && e is List<object> list && list.Count > 0
            ? list
            : throw new ArgumentException("An orbital_events case lists one event or more (spec.md section 14.2).");
        var result = events.ConvertAll(item =>
        {
            var map = item as Dictionary<object, object> ?? throw new ArgumentException($"An event is {item}, not a mapping.");
            var type = Required(map, "type", "an event").ToString();
            switch (type)
            {
                case OrbitalEventRequest.Periapsis:
                case OrbitalEventRequest.Apoapsis:
                case OrbitalEventRequest.AscendingNode:
                case OrbitalEventRequest.DescendingNode:
                    return new OrbitalEventRequest { Type = type };
                case OrbitalEventRequest.Altitude:
                    var direction = Required(map, "direction", "the altitude event").ToString();
                    if (direction is not (OrbitalEventRequest.Up or OrbitalEventRequest.Down or OrbitalEventRequest.Any))
                    {
                        throw new ArgumentException($"The direction '{direction}' of the altitude event is not up, down or any (spec.md section 14.2).");
                    }

                    return new OrbitalEventRequest
                    {
                        Type = type, ValueKm = ToDouble(Required(map, "value_km", "the altitude event")), Direction = direction
                    };
                default:
                    throw new ArgumentException($"The event type '{type}' is not an event of spec.md section 14.2.");
            }
        });

        var duplicate = result.GroupBy(r => r.Type).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            throw new ArgumentException($"The event type '{duplicate.Key}' is listed twice: each type at most once (spec.md section 14.2).");
        }

        return result;
    }

    /// <summary>The inputs of an <c>attitude</c> case (spec.md section 14.3).</summary>
    /// <exception cref="ArgumentException">A field is missing, the law is neither two axes nor one, or a direction is
    /// unknown.</exception>
    public static AttitudeInputs ParseAttitudeInputs(Dictionary<string, object> inputs)
    {
        var map = inputs.ToDictionary(kv => (object)kv.Key, kv => kv.Value);
        const string where = "an attitude case";
        var window = Mapping(Required(map, "propagation_window", where), where);
        var law = Mapping(Required(map, "attitude", where), where);
        var result = new AttitudeInputs
        {
            Epoch = Required(map, "epoch", where).ToString(),
            Orbit = ParseOrbit(Mapping(Required(map, "orbit", where), where)),
            GmKm3S2 = ToDouble(Required(map, "gm_km3_s2", where)),
            PropagationWindow = new SearchWindow
            {
                Start = Required(window, "start", where).ToString(),
                End = Required(window, "end", where).ToString()
            },
            SampleStepS = ToDouble(Required(map, "sample_step_s", where))
        };

        if (law.ContainsKey("pointing"))
        {
            result.Pointing = ParseAttitudeAxis(law["pointing"], "pointing");
        }
        else if (law.ContainsKey("primary") && law.ContainsKey("secondary"))
        {
            result.Primary = ParseAttitudeAxis(law["primary"], "primary");
            result.Secondary = ParseAttitudeAxis(law["secondary"], "secondary");
        }
        else
        {
            throw new ArgumentException("The attitude of an attitude case is {primary, secondary} or {pointing} (spec.md section 14.3).");
        }

        return result;
    }

    private static AttitudeAxis ParseAttitudeAxis(object value, string name)
    {
        string where = $"the {name} axis";
        var map = Mapping(value, where);
        var direction = Required(map, "direction", where).ToString();
        if (direction is not (AttitudeAxis.Nadir or AttitudeAxis.Velocity or AttitudeAxis.OrbitNormal))
        {
            throw new ArgumentException($"The direction '{direction}' of {where} is not nadir, velocity or orbit_normal (spec.md section 14.3).");
        }

        return new AttitudeAxis { BodyVector = ParseVec3(Required(map, "body_vector", where)), Direction = direction };
    }

    // A list of strings of the inputs, or null when the case gives none
    private static List<string> OptionalStrings(Dictionary<string, object> inputs, string key)
    {
        return inputs.TryGetValue(key, out var value) && value is List<object> list ? list.ConvertAll(x => x.ToString()) : null;
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

    /// <summary>The inputs of an <c>sgp4</c> case (spec.md section 15.1).</summary>
    /// <exception cref="ArgumentException">A field is missing, the samples are neither <c>tsince_min</c> nor
    /// <c>utc_epochs</c>, or the output frames are not <c>[TEME]</c> or, with UTC instants, <c>[TEME, ICRF]</c>.</exception>
    public static Sgp4Inputs ParseSgp4Inputs(Dictionary<string, object> inputs)
    {
        var map = inputs.ToDictionary(kv => (object)kv.Key, kv => kv.Value);
        const string where = "an sgp4 case";
        var samples = Mapping(Required(map, "samples", where), where);
        var result = new Sgp4Inputs
        {
            Tle = ParseTle(Required(map, "tle", where), where),
            GravityModel = Required(map, "gravity_model", where).ToString(),
            Opsmode = Required(map, "opsmode", where).ToString(),
            TsinceMin = samples.TryGetValue("tsince_min", out var tsince) && tsince is List<object> minutes ? minutes.ConvertAll(ToDouble) : null,
            UtcEpochs = samples.TryGetValue("utc_epochs", out var epochs) && epochs is List<object> instants ? instants.ConvertAll(e => e.ToString()) : null,
            OutputFrames = Required(map, "output_frames", where) is List<object> frames
                ? frames.ConvertAll(f => f.ToString())
                : throw new ArgumentException($"{where} gives its output_frames as no list.")
        };

        if ((result.TsinceMin == null) == (result.UtcEpochs == null))
        {
            throw new ArgumentException($"The samples of {where} are tsince_min or utc_epochs (spec.md section 15.1).");
        }

        bool teme = result.OutputFrames.SequenceEqual(["TEME"]);
        bool icrf = result.OutputFrames.SequenceEqual(["TEME", "ICRF"]);
        if (!teme && !(icrf && result.UtcEpochs != null))
        {
            throw new ArgumentException(
                $"The output frames of {where} are [{string.Join(", ", result.OutputFrames)}]: [TEME], or [TEME, ICRF] with utc_epochs only (spec.md section 15.1).");
        }

        return result;
    }

    /// <summary>The inputs of a <c>tle_conversion</c> case (spec.md section 15.2).</summary>
    /// <exception cref="ArgumentException">The operation is neither <c>tle_to_omm</c> nor <c>omm_to_tle</c>, an item is
    /// not of the operation, or a field is missing.</exception>
    public static TleConversionInputs ParseTleConversionInputs(Dictionary<string, object> inputs)
    {
        var map = inputs.ToDictionary(kv => (object)kv.Key, kv => kv.Value);
        const string where = "a tle_conversion case";
        var operation = Required(map, "operation", where).ToString();
        var items = Required(map, "items", where) as List<object> ?? throw new ArgumentException($"{where} gives its items as no list.");
        var mappings = items.ConvertAll(item => Mapping(item, "an item"));
        return operation switch
        {
            TleConversionInputs.TleToOmm => new TleConversionInputs
            {
                Operation = operation, Tles = mappings.ConvertAll(item => ParseTle(Required(item, "tle", "a tle_to_omm item"), "a tle_to_omm item"))
            },
            TleConversionInputs.OmmToTle => new TleConversionInputs
            {
                Operation = operation, Omms = mappings.ConvertAll(item => ParseOmm(Mapping(Required(item, "omm", "an omm_to_tle item"), "an omm_to_tle item")))
            },
            _ => throw new ArgumentException($"The operation '{operation}' of {where} is not tle_to_omm or omm_to_tle (spec.md section 15.2).")
        };
    }

    /// <summary>The inputs of a <c>conjunction</c> case (spec.md section 16.1).</summary>
    /// <exception cref="ArgumentException">A field is missing or malformed, the case does not give two objects, mixes
    /// propagated and kernel objects, reports neither <c>closest</c> nor <c>all_minima</c>, or gives a covariance that is
    /// not 21 numbers.</exception>
    public static ConjunctionInputs ParseConjunctionInputs(Dictionary<string, object> inputs)
    {
        var map = inputs.ToDictionary(kv => (object)kv.Key, kv => kv.Value);
        const string where = "a conjunction case";
        var window = Mapping(Required(map, "analysis_window", where), "the analysis_window of a conjunction case");
        var result = new ConjunctionInputs
        {
            Epoch = Required(map, "epoch", where).ToString(),
            AnalysisWindow = new SearchWindow
            {
                Start = Required(window, "start", "the analysis_window").ToString(),
                End = Required(window, "end", "the analysis_window").ToString()
            },
            Report = Required(map, "report", where).ToString(),
            HardBodyRadiusKm = OptionalDouble(inputs, "hard_body_radius_km"),
            Objects = Objects(map, where).ConvertAll(ParseConjunctionObject)
        };

        if (result.Report is not (ConjunctionInputs.Closest or ConjunctionInputs.AllMinima))
        {
            throw new ArgumentException(
                $"The report '{result.Report}' of {where} is neither {ConjunctionInputs.Closest} nor {ConjunctionInputs.AllMinima} (spec.md section 16.1).");
        }

        if (result.Objects[0].FromKernel != result.Objects[1].FromKernel)
        {
            throw new ArgumentException($"{where} propagates one object and reads the other from an SPK: spec.md section 16.1 takes two of a kind.");
        }

        return result;
    }

    private static ConjunctionObject ParseConjunctionObject(Dictionary<object, object> map)
    {
        string name = Required(map, "name", "an object of a conjunction case").ToString();
        string where = $"the conjunction object {name}";
        if (map.TryGetValue("naif_id", out var naifId) && !IsNull(naifId))
        {
            return new ConjunctionObject { Name = name, NaifId = (int)ToDouble(naifId) };
        }

        return new ConjunctionObject
        {
            Name = name,
            PositionKm = ParseVec3(Required(map, "position_km", where)),
            VelocityKmS = ParseVec3(Required(map, "velocity_km_s", where)),
            Spacecraft = map.TryGetValue("spacecraft", out var spacecraft) && !IsNull(spacecraft) ? Mapping(spacecraft, where) : null,
            CovarianceRtn = map.TryGetValue("covariance_rtn", out var covariance) && !IsNull(covariance)
                ? Numbers(covariance, 21, $"the covariance_rtn of {where}")
                : null
        };
    }

    /// <summary>The inputs of a <c>collision_probability</c> case (spec.md section 16.2).</summary>
    /// <exception cref="ArgumentException">A field is missing or malformed, the case does not give two objects, a
    /// covariance is not 6 numbers, or no object has one.</exception>
    public static CollisionProbabilityInputs ParseCollisionProbabilityInputs(Dictionary<string, object> inputs)
    {
        var map = inputs.ToDictionary(kv => (object)kv.Key, kv => kv.Value);
        const string where = "a collision_probability case";
        var result = new CollisionProbabilityInputs
        {
            Tca = Required(map, "tca", where).ToString(),
            HardBodyRadiusKm = ToDouble(Required(map, "hard_body_radius_km", where)),
            Objects = Objects(map, where).ConvertAll(o => ParseEncounterObject(o, 6, where))
        };

        if (result.Objects.TrueForAll(o => o.CovarianceRtn == null))
        {
            throw new ArgumentException($"{where} gives no covariance: spec.md section 16.2 takes at least one.");
        }

        return result;
    }

    /// <summary>The inputs of a <c>cdm</c> case (spec.md section 16.3).</summary>
    /// <exception cref="ArgumentException">The operation is not one of spec.md section 16.3, or a field of the operation
    /// is missing or malformed.</exception>
    public static CdmInputs ParseCdmInputs(Dictionary<string, object> inputs)
    {
        var map = inputs.ToDictionary(kv => (object)kv.Key, kv => kv.Value);
        const string where = "a cdm case";
        var operation = Required(map, "operation", where).ToString();
        switch (operation)
        {
            case CdmInputs.Decode:
            case CdmInputs.ToInertial:
                var file = Required(map, "cdm_file", where).ToString();
                if (file != System.IO.Path.GetFileName(file) || file.StartsWith('.'))
                {
                    throw new ArgumentException($"The cdm_file '{file}' of {where} is not a file name next to inputs.yaml.");
                }

                return new CdmInputs { Operation = operation, CdmFile = file };
            case CdmInputs.Export:
                return new CdmInputs
                {
                    Operation = operation,
                    Tca = Required(map, "tca", where).ToString(),
                    HardBodyRadiusKm = ToDouble(Required(map, "hard_body_radius_km", where)),
                    Objects = Objects(map, where).ConvertAll(o => ParseEncounterObject(o, 21, where))
                };
            default:
                throw new ArgumentException(
                    $"The operation '{operation}' of {where} is not {CdmInputs.Decode}, {CdmInputs.ToInertial} or {CdmInputs.Export} (spec.md section 16.3).");
        }
    }

    private static EncounterObject ParseEncounterObject(Dictionary<object, object> map, int covarianceLength, string where)
    {
        string what = $"an object of {where}";
        if (!map.ContainsKey("covariance_rtn"))
        {
            throw new ArgumentException($"{what} has no covariance_rtn (null when absent).");
        }

        return new EncounterObject
        {
            PositionKm = ParseVec3(Required(map, "position_km", what)),
            VelocityKmS = ParseVec3(Required(map, "velocity_km_s", what)),
            CovarianceRtn = IsNull(map["covariance_rtn"]) ? null : Numbers(map["covariance_rtn"], covarianceLength, $"the covariance_rtn of {what}")
        };
    }

    // A YAML null as the deserializer gives it to an object: null, or the plain scalar null or ~
    private static bool IsNull(object value) => value is null or "null" or "~" or "";

    // The two objects of a lot-6 case, object 1 then object 2
    private static List<Dictionary<object, object>> Objects(Dictionary<object, object> map, string where)
    {
        var objects = Required(map, "objects", where) as List<object> ?? throw new ArgumentException($"{where} gives its objects as no list.");
        if (objects.Count != 2)
        {
            throw new ArgumentException($"{where} gives {objects.Count} objects: spec.md section 16 takes two, object 1 then object 2.");
        }

        return objects.ConvertAll(o => Mapping(o, $"an object of {where}"));
    }

    // A list of numbers of a given length (the lower triangle of a covariance)
    private static double[] Numbers(object value, int length, string what)
    {
        if (value is not List<object> list || list.Count != length)
        {
            throw new ArgumentException($"{what} is not {length} numbers (spec.md section 16).");
        }

        return list.Select(ToDouble).ToArray();
    }

    // The form of an OMM EPOCH (spec.md section 3): YYYY-MM-DDThh:mm:ss[.f{1,6}], no tag
    private static readonly System.Text.RegularExpressions.Regex OmmEpochPattern =
        new(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,6})?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly string[] OmmEpochFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss'.'f", "yyyy-MM-dd'T'HH:mm:ss'.'ff", "yyyy-MM-dd'T'HH:mm:ss'.'fff",
        "yyyy-MM-dd'T'HH:mm:ss'.'ffff", "yyyy-MM-dd'T'HH:mm:ss'.'fffff", "yyyy-MM-dd'T'HH:mm:ss'.'ffffff"
    ];

    /// <summary>The epoch of an OMM item (spec.md section 3), read exactly: up to 6 decimals, no tag.</summary>
    /// <exception cref="FormatException">The epoch is not of that form.</exception>
    public static DateTime ParseOmmEpoch(string epoch)
    {
        var match = epoch == null ? System.Text.RegularExpressions.Match.Empty : OmmEpochPattern.Match(epoch);
        if (!match.Success || match.Length != epoch.Length)
        {
            throw new FormatException($"The EPOCH '{epoch}' is not of the CCSDS form of spec.md section 3: YYYY-MM-DDThh:mm:ss with 0 to 6 decimals, no tag.");
        }

        return DateTime.ParseExact(epoch, OmmEpochFormats, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None);
    }

    private static OmmInput ParseOmm(Dictionary<object, object> omm)
    {
        const string where = "an OMM item";
        return new OmmInput
        {
            ObjectName = Required(omm, "OBJECT_NAME", where).ToString(),
            ObjectId = Required(omm, "OBJECT_ID", where).ToString(),
            TimeSystem = Required(omm, "TIME_SYSTEM", where).ToString(),
            Epoch = ParseOmmEpoch(Required(omm, "EPOCH", where).ToString()),
            MeanMotion = ToDouble(Required(omm, "MEAN_MOTION", where)),
            Eccentricity = ToDouble(Required(omm, "ECCENTRICITY", where)),
            Inclination = ToDouble(Required(omm, "INCLINATION", where)),
            RaOfAscNode = ToDouble(Required(omm, "RA_OF_ASC_NODE", where)),
            ArgOfPericenter = ToDouble(Required(omm, "ARG_OF_PERICENTER", where)),
            MeanAnomaly = ToDouble(Required(omm, "MEAN_ANOMALY", where)),
            EphemerisType = (int)ToDouble(Required(omm, "EPHEMERIS_TYPE", where)),
            ClassificationType = Required(omm, "CLASSIFICATION_TYPE", where).ToString(),
            NoradCatId = (int)ToDouble(Required(omm, "NORAD_CAT_ID", where)),
            ElementSetNo = (int)ToDouble(Required(omm, "ELEMENT_SET_NO", where)),
            RevAtEpoch = (int)ToDouble(Required(omm, "REV_AT_EPOCH", where)),
            BStar = ToDouble(Required(omm, "BSTAR", where)),
            MeanMotionDot = ToDouble(Required(omm, "MEAN_MOTION_DOT", where)),
            MeanMotionDdot = ToDouble(Required(omm, "MEAN_MOTION_DDOT", where))
        };
    }

    private static TleInput ParseTle(object value, string where)
    {
        var tle = Mapping(value, where);
        return new TleInput
        {
            Name = tle.TryGetValue("name", out var name) ? name?.ToString() : null,
            Line1 = Required(tle, "line1", $"the tle of {where}").ToString(),
            Line2 = Required(tle, "line2", $"the tle of {where}").ToString()
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
