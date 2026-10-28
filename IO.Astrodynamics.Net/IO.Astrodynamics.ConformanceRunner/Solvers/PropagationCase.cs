using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.ConformanceRunner.Models;
using IO.Astrodynamics.ConformanceRunner.Utilities;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using TLE = IO.Astrodynamics.OrbitalParameters.TLE.TLE;

namespace IO.Astrodynamics.ConformanceRunner.Solvers;

/// <summary>
/// A propagation case of the conformance suite, built from the inputs shared by the <c>propagator</c> and <c>stm</c>
/// categories: the window, the spacecraft at its initial state, the bodies (the central body first), the
/// non-conservative forces and the step of the case (<c>step_size_s</c>).
/// </summary>
/// <remarks>
/// The model is that of spec.md section 12.1: the field of the table of <see cref="GeopotentialFieldCatalog"/> with the
/// model's constants on the whole field; drag, about the Earth only, with NRLMSISE-00 at the constant space weather of
/// the case; SRP with the shadows of the case's <c>shadow_bodies</c>, which must be the bodies that occult the Sun in the
/// framework; the spacecraft of the case, required when drag or SRP is on. A case the framework cannot compute as the
/// suite specifies it is an <see cref="InvalidOperationException"/>, which the runner reports as ERROR.
/// </remarks>
public sealed record PropagationCase(Window Window, Spacecraft Spacecraft, IReadOnlyList<CelestialItem> Bodies,
    bool Drag, bool SolarRadiationPressure, TimeSpan StepSize)
{
    /// <summary>
    /// The output step of the propagation: the length of the window, so that no output falls inside it. The solvers
    /// read only the end of the window, and since #363 each RK7(8) output inside a step costs a shortened step (13
    /// force evaluations): <see cref="StepSize"/>, 1 s in the suite, made the GEO 70x70 case 41 times slower (#426).
    /// The accepted steps do not depend on the outputs, so the results are the same, bit for bit.
    /// </summary>
    public TimeSpan OutputStep => Window.Length;

    /// <summary>The atmosphere model of the suite's drag (spec.md section 12.1).</summary>
    public const string DragModel = "NRLMSISE-00";

    /// <summary>
    /// Builds the case of <paramref name="caseInput"/>, the geopotential model read from <paramref name="dataPath"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The case is not one the framework computes as spec.md section 12.1
    /// specifies it: an unknown field or the field of another body, drag about another body than the Earth or with
    /// another model, drag or SRP without the spacecraft, shadow bodies that are not the framework's occulters.</exception>
    /// <exception cref="ArgumentException">A force is turned on without its definition (<c>true</c>), or a body is
    /// unknown.</exception>
    /// <exception cref="FormatException">An epoch is not a tagged epoch of spec.md section 3.</exception>
    public static PropagationCase Build(CaseInput caseInput, string dataPath)
    {
        var inputs = InputParser.ParsePropagatorInputs(caseInput.Inputs);
        var frame = FrameMapper.Map(caseInput.Metadata.ReferenceFrame);
        var epoch = EpochParser.Parse(inputs.Epoch);
        var centralNaif = BodyResolver.ResolveNaif(inputs.CentralBody);

        // The field of the table of spec.md section 12.1, its GM and radius on the whole field, central term included,
        // as GMAT applies the header of a .cof (decision 18 of #457)
        GeopotentialModelParameters geoParams = null;
        if (!string.Equals(inputs.GeopotentialModel, GeopotentialFieldCatalog.None, StringComparison.OrdinalIgnoreCase))
        {
            var field = GeopotentialFieldCatalog.Resolve(inputs.GeopotentialModel, centralNaif.NaifId);
            geoParams = inputs.GeopotentialDegree > 0 ? field.Parameters(dataPath, inputs.GeopotentialDegree) : null;
        }

        var centralBody = new CelestialBody(centralNaif, frame, epoch, geoParams, DragAtmosphere(inputs.ForceModel.Drag, centralNaif));

        // Build initial state vector
        var sv = BuildStateVector(inputs.Orbit, centralBody, epoch, frame);

        // Propagation window: its end may carry up to 7 decimals (spec.md section 3)
        var windowStart = EpochParser.Parse(inputs.PropagationWindow.Start);
        var windowEnd = EpochParser.ParseWindowEnd(inputs.PropagationWindow.End);
        var propWindow = new Window(windowStart, windowEnd);

        // Build bodies list: central body + perturbation bodies
        var bodies = new List<CelestialItem> { centralBody };
        foreach (var bodyName in inputs.PerturbationBodies)
        {
            bodies.Add(BodyResolver.ResolveCelestialItem(bodyName));
        }

        if (inputs.ForceModel.Srp != null)
        {
            CheckShadowBodies(inputs.ForceModel.Srp.ShadowBodies, bodies);
        }

        var spacecraft = BuildSpacecraft(inputs, sv);
        return new PropagationCase(propWindow, spacecraft, bodies, inputs.ForceModel.Drag != null, inputs.ForceModel.Srp != null,
            TimeSpan.FromSeconds(inputs.StepSizeS));
    }

    /// <summary>
    /// The bodies that occult the Sun in the framework's solar radiation pressure: every <see cref="CelestialBody"/> of
    /// the bodies of the propagation but the Sun, by NAIF id (<c>CentralBodyPropagator</c> builds its
    /// <c>SolarRadiationPressure</c> with them; a barycentre is no <see cref="CelestialBody"/>).
    /// </summary>
    internal static IReadOnlySet<int> Occulters(IEnumerable<CelestialItem> bodies)
    {
        return bodies.OfType<CelestialBody>().Select(b => b.NaifId).Where(id => id != Stars.Sun.NaifId).ToHashSet();
    }

    // NRLMSISE-00 at the constant space weather of the case (spec.md section 12.1), with every switch on: switch 9 = 1
    // uses the daily Ap alone; about the Earth only
    private static IAtmosphericModel DragAtmosphere(DragInputs drag, NaifObject centralBody)
    {
        if (drag == null)
        {
            return null;
        }

        if (centralBody.NaifId != PlanetsAndMoons.EARTH.NaifId)
        {
            throw new InvalidOperationException(
                $"The case has drag about {centralBody.Name}: spec.md section 12.1 models drag about the Earth only.");
        }

        if (drag.Model != DragModel)
        {
            throw new InvalidOperationException(
                $"The drag model '{drag.Model}' is not the suite's: spec.md section 12.1 specifies {DragModel}.");
        }

        return new Nrlmsise00Model(NrlmsiseFlags.CreateStandard(),
            new SpaceWeather { F107 = drag.F107Sfu, F107A = drag.F107aSfu, Ap = drag.Ap });
    }

    // The case's shadow bodies must be the framework's occulters: the framework takes them from the bodies of the
    // propagation, and offers no other choice
    private static void CheckShadowBodies(IReadOnlyList<string> shadowBodies, IEnumerable<CelestialItem> bodies)
    {
        var shadows = shadowBodies.Select(name => BodyResolver.ResolveNaif(name).NaifId).ToHashSet();
        var occulters = Occulters(bodies);
        if (!shadows.SetEquals(occulters))
        {
            throw new InvalidOperationException(
                $"The case's shadow bodies ({string.Join(", ", shadowBodies)}) are not the bodies that occult the Sun in the framework, " +
                $"every celestial body of the propagation but the Sun (NAIF {string.Join(", ", occulters.Order())}).");
        }
    }

    // The spacecraft of the case, normative for drag and SRP (spec.md section 12.1); without either, a case may give
    // none, and the spacecraft does not matter
    private static Spacecraft BuildSpacecraft(PropagatorInputs inputs, StateVector sv)
    {
        if (inputs.Spacecraft == null && (inputs.ForceModel.Drag != null || inputs.ForceModel.Srp != null))
        {
            throw new InvalidOperationException(
                "The case has drag or SRP and no spacecraft: its mass, area and coefficients are inputs of these forces (spec.md section 12.1).");
        }

        var clock = new Clock("ConfClk", 256);
        double mass = inputs.Spacecraft?.MassKg ?? 100.0;
        double maxMass = inputs.Spacecraft?.MaxMassKg ?? mass * 10.0;
        double area = inputs.Spacecraft?.SectionalAreaM2 ?? 1.0;
        double cd = inputs.Spacecraft?.DragCoefficient ?? 2.2;
        double cr = inputs.Spacecraft?.RadiationPressureCoefficient ?? 1.0;
        return new Spacecraft(-999, "ConformanceTestSC", mass, maxMass, clock, sv,
            sectionalArea: area, dragCoeff: cd, solarRadiationCoeff: cr);
    }

    private static StateVector BuildStateVector(object orbit, CelestialBody observer, Time epoch, Frame frame)
    {
        if (orbit is KeplerianOrbit kep)
        {
            var ke = new KeplerianElements(
                UnitConversion.KmToM(kep.AKm),
                kep.E,
                UnitConversion.DegToRad(kep.IDeg),
                UnitConversion.DegToRad(kep.RaanDeg),
                UnitConversion.DegToRad(kep.ArgpDeg),
                UnitConversion.DegToRad(kep.MaDeg),
                observer,
                epoch,
                frame);
            return ke.ToStateVector();
        }

        if (orbit is StateVectorOrbit svo)
        {
            return new StateVector(
                new Vector3(UnitConversion.KmToM(svo.PositionKm[0]), UnitConversion.KmToM(svo.PositionKm[1]), UnitConversion.KmToM(svo.PositionKm[2])),
                new Vector3(UnitConversion.KmSToMS(svo.VelocityKmS[0]), UnitConversion.KmSToMS(svo.VelocityKmS[1]), UnitConversion.KmSToMS(svo.VelocityKmS[2])),
                observer,
                epoch,
                frame);
        }

        if (orbit is TleOrbit tleOrbit)
        {
            var tle = new TLE(tleOrbit.Name, tleOrbit.Line1, tleOrbit.Line2);
            var osculatingIcrf = tle.ToStateVector().ToFrame(frame).ToStateVector();
            return new StateVector(osculatingIcrf.Position, osculatingIcrf.Velocity,
                observer, osculatingIcrf.Epoch, frame);
        }

        throw new ArgumentException("Unknown orbit type");
    }
}