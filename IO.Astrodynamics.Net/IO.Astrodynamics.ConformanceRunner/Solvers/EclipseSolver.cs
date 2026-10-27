using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// The <c>eclipse</c> category (spec.md section 12.2): the first eclipse of the search window of a spacecraft on the
/// two-body orbit of the case's elements, with the case's GM (<c>gm_km3_s2</c>, the kernel GM of the occulting body when
/// the case gives none), the occulting body the ellipsoid of its radii oriented by its IAU frame, the light source its
/// own shape, geometric. The search is the library's (managed: the spacecraft is propagated), its transitions located
/// to 1 µs.
/// </summary>
/// <remarks>
/// The case computes with the files of the suite's <c>data/</c> it lists (<see cref="CaseKernels"/>). The kernels give
/// the Earth the GM of DE440; a case GM that differs from it is applied as the GM of the whole field of a geopotential of
/// degree 0 (<see cref="GeopotentialCentralTerm.ModelGravitationalParameter"/>), which is the point mass of that GM, and
/// to the state of the elements, whose velocity scales as √GM at a given position.
/// </remarks>
public class EclipseSolver : ICategorySolver
{
    // m³ per km³
    private const double CubicMetresPerCubicKilometre = 1e9;

    // The search step: the managed search finds every eclipse of a satellite with a step of at most a quarter of its
    // period, and here the windows of 2083 s and 2064 s, and the partial phases of 9.3 s between them
    private static readonly TimeSpan SearchStep = TimeSpan.FromSeconds(60.0);

    // The output step of the propagation, from which the search interpolates the states (Lagrange, 8 points)
    private static readonly TimeSpan OutputStep = TimeSpan.FromSeconds(10.0);

    private readonly string _conformanceTestsPath;

    /// <param name="conformanceTestsPath">The root of the suite, whose <c>data/</c> holds the kernels of the cases and
    /// the field file that carries a case GM.</param>
    public EclipseSolver(string conformanceTestsPath)
    {
        _conformanceTestsPath = conformanceTestsPath;
    }

    public string Category => "eclipse";

    public Dictionary<string, object> Solve(CaseInput caseInput)
    {
        var inputs = InputParser.ParseEclipseInputs(caseInput.Inputs);
        using var kernels = new CaseKernels(_conformanceTestsPath, InputParser.ParseKernels(caseInput.Inputs));
        var epoch = EpochParser.Parse(inputs.Epoch);
        var searchWindow = new Window(EpochParser.Parse(inputs.SearchWindow.Start), EpochParser.Parse(inputs.SearchWindow.End));

        // After the kernels of the case, so that the bodies take their radii
        var occultingBody = BodyResolver.Resolve(inputs.OccultingBody);
        var lightSource = BodyResolver.Resolve(inputs.LightSource);
        double gm = inputs.GmKm3S2.HasValue ? inputs.GmKm3S2.Value * CubicMetresPerCubicKilometre : occultingBody.GM;
        var centralBody = CentralBody(occultingBody, gm, epoch);

        var spacecraft = new Spacecraft(-999, "ConformanceTestSC", 100.0, 1000.0, new Clock("ConfClk", 65536),
            InitialState(inputs.Orbit, occultingBody, centralBody, gm, epoch));
        spacecraft.Propagate(searchWindow, [centralBody],
            new RK78Integrator(absoluteTolerance: 1e-12, relativeTolerance: 1e-12, initialStepSize: 60.0, minStepSize: 1e-3, maxStepSize: 300.0),
            false, false, OutputStep);

        var options = new OccultationSearchOptions { FrontFrame = new Frame($"IAU_{occultingBody.Name.ToUpperInvariant()}") };
        var penumbra = lightSource.FindWindowsOnOccultationConstraint(searchWindow, spacecraft, ShapeType.Ellipsoid, occultingBody,
                ShapeType.Ellipsoid, OccultationType.Any, Aberration.None, SearchStep, options)
            .FirstOrDefault();
        var umbra = penumbra == default
            ? default
            : lightSource.FindWindowsOnOccultationConstraint(penumbra, spacecraft, ShapeType.Ellipsoid, occultingBody,
                    ShapeType.Ellipsoid, OccultationType.Full, Aberration.None, SearchStep, options)
                .FirstOrDefault();

        return new Dictionary<string, object>
        {
            ["penumbra_entry"] = Output(penumbra, () => penumbra.StartDate),
            ["umbra_entry"] = Output(umbra, () => umbra.StartDate),
            ["umbra_exit"] = Output(umbra, () => umbra.EndDate),
            ["penumbra_exit"] = Output(penumbra, () => penumbra.EndDate),
            ["penumbra_duration_s"] = Output(umbra, () => penumbra.Length.TotalSeconds - umbra.Length.TotalSeconds),
            ["umbra_duration_s"] = Output(umbra, () => umbra.Length.TotalSeconds)
        };
    }

    // An output of a window, or the exception that says it was not found
    private static object Output(Window window, Func<object> value)
    {
        return window == default
            ? new InvalidOperationException("No eclipse of that type in the search window.")
            : value();
    }

    /// <summary>
    /// The body about which the trajectory is integrated: the occulting body, a point mass of its kernel GM, or of
    /// <paramref name="gm"/> (m³/s²) when it differs.
    /// </summary>
    private CelestialBody CentralBody(CelestialBody occultingBody, double gm, Time epoch)
    {
        if (gm == occultingBody.GM)
        {
            return new CelestialBody(occultingBody.NaifId, Frame.ICRF, epoch);
        }

        // A field of degree 0 with its GM in the central term is the point mass of that GM; the file gives no coefficient
        var fieldPath = Path.Combine(_conformanceTestsPath, "data", "EGM2008_to70_TideFree");
        SuiteDataFile.EnsureContent(fieldPath);
        return new CelestialBody(occultingBody.NaifId, Frame.ICRF, epoch,
            new GeopotentialModelParameters(fieldPath, 0, gm, occultingBody.EquatorialRadius)
            {
                CentralTerm = GeopotentialCentralTerm.ModelGravitationalParameter
            });
    }

    /// <summary>
    /// The state of the elements about <paramref name="centralBody"/> with <paramref name="gm"/> (m³/s²): the library
    /// converts them with the kernel GM of <paramref name="occultingBody"/>; at the position of the elements, which does
    /// not depend on GM, the velocity scales as √GM.
    /// </summary>
    private static StateVector InitialState(object orbit, CelestialBody occultingBody, CelestialBody centralBody, double gm, Time epoch)
    {
        if (orbit is not KeplerianOrbit kep)
        {
            throw new ArgumentException("The eclipse category takes Keplerian elements.");
        }

        var state = new KeplerianElements(UnitConversion.KmToM(kep.AKm), kep.E, UnitConversion.DegToRad(kep.IDeg),
            UnitConversion.DegToRad(kep.RaanDeg), UnitConversion.DegToRad(kep.ArgpDeg), UnitConversion.DegToRad(kep.MaDeg),
            occultingBody, epoch, Frame.ICRF).ToStateVector();
        return new StateVector(state.Position, state.Velocity * System.Math.Sqrt(gm / occultingBody.GM), centralBody, epoch, Frame.ICRF);
    }
}
