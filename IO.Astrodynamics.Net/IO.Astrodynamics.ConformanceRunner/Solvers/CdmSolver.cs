using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.CCSDS.CDM;
using IO.Astrodynamics.ConformanceRunner.Comparison;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>cdm</c> category (spec.md section 16.3): the fields of a CDM read by the framework (<c>decode</c>), with each
/// object's covariance turned to inertial axes (<c>to_inertial</c>), and the CDM the framework writes for an encounter at
/// an exact TCA, read back (<c>export</c>).
/// </summary>
/// <remarks>
/// <para>
/// <c>decode</c> reads the CDM next to <c>inputs.yaml</c> with <see cref="Cdm.ReadFromFile"/>; the framework holds its
/// fields in SI units (m, m/s, m², the consider terms in the units of CCSDS 508.0 table 3-2), which the solver gives in
/// the suite's (km: a value in m^p times 10^(−3p)). <c>to_inertial</c> turns each RTN covariance by
/// <see cref="StateVector.RotateCovarianceFromRtn"/> on the object's state. <c>export</c> analyzes the encounter on
/// straight-line trajectories (<see cref="StraightLineEncounter"/>), writes its CDM by <c>EncounterCase.ToCdm</c> and
/// <see cref="Cdm.WriteToString"/>, reads it back with <see cref="Cdm.ReadFromString"/> and gives the fields read.
/// </para>
/// </remarks>
public class CdmSolver : ICategorySolver
{
    // The NAIF id of object 1 of an exported encounter
    private const int NaifId = -1701;

    // The consider terms of a 9x9 covariance (spec.md section 16.3), in their order, each with the power of m of its unit
    // in CCSDS 508.0 table 3-2 (m³/kg: 3); a CDM gives whole rows, CDRG (7 terms), then CSRP (15), then CTHR (24)
    private static readonly (string Name, Func<CdmCovarianceMatrix, double?> Value, int Power)[] ConsiderTerms =
    [
        ("CDRG_R", c => c.CdrgR, 3), ("CDRG_T", c => c.CdrgT, 3), ("CDRG_N", c => c.CdrgN, 3),
        ("CDRG_RDOT", c => c.CdrgRdot, 3), ("CDRG_TDOT", c => c.CdrgTdot, 3), ("CDRG_NDOT", c => c.CdrgNdot, 3),
        ("CDRG_DRG", c => c.CdrgDrg, 4),
        ("CSRP_R", c => c.CsrpR, 3), ("CSRP_T", c => c.CsrpT, 3), ("CSRP_N", c => c.CsrpN, 3),
        ("CSRP_RDOT", c => c.CsrpRdot, 3), ("CSRP_TDOT", c => c.CsrpTdot, 3), ("CSRP_NDOT", c => c.CsrpNdot, 3),
        ("CSRP_DRG", c => c.CsrpDrg, 4), ("CSRP_SRP", c => c.CsrpSrp, 4),
        ("CTHR_R", c => c.CthrR, 2), ("CTHR_T", c => c.CthrT, 2), ("CTHR_N", c => c.CthrN, 2),
        ("CTHR_RDOT", c => c.CthrRdot, 2), ("CTHR_TDOT", c => c.CthrTdot, 2), ("CTHR_NDOT", c => c.CthrNdot, 2),
        ("CTHR_DRG", c => c.CthrDrg, 3), ("CTHR_SRP", c => c.CthrSrp, 3), ("CTHR_THR", c => c.CthrThr, 2)
    ];

    public string Category => "cdm";

    /// <summary>The indexed array of the two objects of a CDM.</summary>
    public const string Objects = "objects";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParseCdmInputs(caseInput.Inputs);
        if (inputs.Operation == CdmInputs.Export)
        {
            var tca = EpochParser.Parse(inputs.Tca);
            var encounter = StraightLineEncounter.Analyze(tca, inputs.Objects, 1e6, UnitConversion.KmToM(inputs.HardBodyRadiusKm!.Value), NaifId);
            var written = encounter.ToCdm().WriteToString();
            return Fields(Cdm.ReadFromString(written), false);
        }

        if (caseInput.Directory == null)
        {
            throw new InvalidOperationException("The runner gives no directory of the case, where its CDM lies.");
        }

        var cdm = Cdm.ReadFromFile(Path.Combine(caseInput.Directory, inputs.CdmFile));
        return Fields(cdm, inputs.Operation == CdmInputs.ToInertial);
    }

    /// <summary>
    /// The fields of a CDM in the suite's units (spec.md section 16.3), each one the CDM holds; with
    /// <paramref name="inertial"/>, each object's covariance in inertial axes too.
    /// </summary>
    internal static Dictionary<string, object> Fields(Cdm cdm, bool inertial)
    {
        var relative = cdm.RelativeMetadataData;
        var outputs = new Dictionary<string, object>
        {
            ["tca"] = Utc(relative.TcaUtc),
            ["creation_date"] = Utc(cdm.Header.CreationDateUtc),
            ["miss_distance_km"] = UnitConversion.MToKm(relative.MissDistanceMeters)
        };
        Optional(outputs, "relative_speed_km_s", relative.RelativeSpeedMetersPerSecond, UnitConversion.MSToKmS);
        if (relative.RelativeStateVector is { } rsv)
        {
            outputs["relative_position_rtn_km"] = Km(rsv.RelativePositionRtnMeters);
            outputs["relative_velocity_rtn_km_s"] = Km(rsv.RelativeVelocityRtnMetersPerSecond);
        }

        Optional(outputs, "start_screen_period", relative.StartScreenPeriodUtc);
        Optional(outputs, "stop_screen_period", relative.StopScreenPeriodUtc);
        if (relative.ScreenVolumeXMeters is { } x && relative.ScreenVolumeYMeters is { } y && relative.ScreenVolumeZMeters is { } z)
        {
            outputs["screen_volume_km"] = new[] { UnitConversion.MToKm(x), UnitConversion.MToKm(y), UnitConversion.MToKm(z) };
        }

        Optional(outputs, "collision_probability", relative.CollisionProbability, p => p);
        if (relative.CollisionProbabilityMethod != null)
        {
            outputs["collision_probability_method"] = relative.CollisionProbabilityMethod;
        }

        outputs[Objects] = cdm.Segments.Count;
        for (int k = 0; k < cdm.Segments.Count; k++)
        {
            AddObject(outputs, k, cdm.Segments[k], relative.TcaUtc, inertial);
        }

        return outputs;
    }

    private static void AddObject(Dictionary<string, object> outputs, int k, CdmSegment segment, DateTime tca, bool inertial)
    {
        string Name(string field) => OutputComparer.IndexedOutput(Objects, k, field);
        if (segment.Metadata?.ReferenceFrame is { } frame)
        {
            outputs[Name("ref_frame")] = SolverOutput.Of(() => ReferenceFrame(frame));
        }

        var state = segment.Data?.StateVector;
        if (state != null)
        {
            outputs[Name("position_km")] = Km(state.PositionMeters);
            outputs[Name("velocity_km_s")] = Km(state.VelocityMetersPerSecond);
        }

        var covariance = segment.Data?.CovarianceMatrix;
        if (covariance != null)
        {
            outputs[Name("covariance_rtn")] = LowerTriangle.Of(covariance.StateCovarianceRtn, 1e-6);
            var consider = ConsiderTerms.Select(t => (t.Name, Value: t.Value(covariance), t.Power)).ToArray();
            if (consider.Any(t => t.Value.HasValue))
            {
                outputs[Name("covariance_consider")] = SolverOutput.Of(() => Consider(consider, k));
            }

            if (inertial)
            {
                outputs[Name("covariance_inertial")] = SolverOutput.Of(() => Inertial(state, covariance.StateCovarianceRtn, tca, k));
            }
        }

        var od = segment.Data?.OdParameters;
        Optional(outputs, Name("time_lastob_start"), od?.TimeLastObservationStartUtc);
        Optional(outputs, Name("time_lastob_end"), od?.TimeLastObservationEndUtc);
    }

    // The consider terms present, in their order, each in km: the 7, 15 or 24 terms of whole rows CDRG, then CSRP, then
    // CTHR (the 8x8 or 9x9 covariance of CCSDS 508.0, as the suite's generator writes them); a hole inside a row, or a
    // later row without an earlier one, is refused
    private static double[] Consider((string Name, double? Value, int Power)[] consider, int k)
    {
        int present = consider.TakeWhile(t => t.Value.HasValue).Count();
        if (present is 7 or 15 or 24 && consider.Skip(present).All(t => !t.Value.HasValue))
        {
            return consider.Take(present).Select(t => t.Value!.Value / System.Math.Pow(10.0, 3 * t.Power)).ToArray();
        }

        int last = Array.FindLastIndex(consider, t => t.Value.HasValue);
        int rowsEnd = last < 7 ? 7 : last < 15 ? 15 : 24;
        var missing = consider.Take(rowsEnd).Where(t => !t.Value.HasValue).Select(t => t.Name);
        throw new InvalidOperationException(
            $"The consider terms of object {k + 1} are not whole rows CDRG, then CSRP, then CTHR: no {string.Join(", ", missing)}.");
    }

    // The covariance of an object in inertial axes, km: the RTN covariance turned on its state
    private static double[] Inertial(CdmStateVector state, Matrix covarianceRtn, DateTime tca, int k)
    {
        if (state == null)
        {
            throw new InvalidOperationException($"The CDM gives object {k + 1} no state, whose RTN axes its covariance is in.");
        }

        var epoch = Utc(tca);
        var vector = new StateVector(state.PositionMeters, state.VelocityMetersPerSecond, new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch),
            epoch, Frame.ICRF);
        return LowerTriangle.Of(vector.RotateCovarianceFromRtn(covarianceRtn), 1e-6);
    }

    // The REF_FRAME keyword of a frame the framework reads (CCSDS 508.0 table 3-3)
    private static string ReferenceFrame(CdmReferenceFrame frame) => frame switch
    {
        CdmReferenceFrame.Eme2000 => "EME2000",
        CdmReferenceFrame.Gcrf => "GCRF",
        CdmReferenceFrame.Itrf => "ITRF",
        _ => throw new InvalidOperationException($"The framework reads the reference frame {frame}, which is not a REF_FRAME of CCSDS 508.0.")
    };

    private static Time Utc(DateTime instant) => new(instant, TimeFrame.UTCFrame);

    private static double[] Km(Vector3 vector) =>
        [UnitConversion.MToKm(vector.X), UnitConversion.MToKm(vector.Y), UnitConversion.MToKm(vector.Z)];

    private static void Optional(Dictionary<string, object> outputs, string name, double? value, Func<double, double> convert)
    {
        if (value is { } v)
        {
            outputs[name] = convert(v);
        }
    }

    private static void Optional(Dictionary<string, object> outputs, string name, DateTime? instant)
    {
        if (instant is { } i)
        {
            outputs[name] = Utc(i);
        }
    }
}
