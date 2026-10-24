using System;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CCSDS.CDM;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Maneuver;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.SmokeTests;

/// <summary>
/// The code of the covariance propagation tutorial (docs/tutorials/covariance-propagation.md), section by section,
/// against the packed package. Keep both in step: a change to one is a change to the other.
/// </summary>
[Collection(KernelCollection.Name)]
public class CovariancePropagationTutorialSmokeTests
{
    private static readonly DirectoryInfo SolarSystemKernelPath = new("Data/SolarSystem");
    private readonly ITestOutputHelper _output;

    public CovariancePropagationTutorialSmokeTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(SolarSystemKernelPath);
    }

    // Section "The case": a LEO 400 km with EGM2008 10×10, NRLMSISE-00 drag, SRP, the Moon and the Sun
    private static (CelestialItem[] Bodies, CelestialBody Earth, Time Epoch) Environment()
    {
        var epoch = new Time(2021, 3, 20, 12, 0, 0);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, epoch,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
            new Nrlmsise00Model(SpaceWeather.Nominal));
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frame.ICRF, epoch);
        var sun = new CelestialBody(Stars.Sun);
        return (new CelestialItem[] { earth, moon, sun }, earth, epoch);
    }

    // Section "A state with its covariance": σ in RTN, rotated to the inertial frame of the state
    private static StateVector InitialState(CelestialBody earth, Time epoch)
    {
        var orbit = new KeplerianElements(6778137.0, 0.0005, 51.6 * Constants.Deg2Rad, 20.0 * Constants.Deg2Rad,
            15.0 * Constants.Deg2Rad, 0.0, earth, epoch, Frame.ICRF).ToStateVector();

        double[] sigma = { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 };   // R, T, N (m), then their rates (m/s)
        var rtn = new Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            rtn.Set(i, i, sigma[i] * sigma[i]);
        }

        rtn.Set(1, 3, -0.9 * sigma[1] * sigma[3]);   // along-track position and radial velocity
        rtn.Set(3, 1, -0.9 * sigma[1] * sigma[3]);

        return new StateVector(orbit.Position, orbit.Velocity, earth, epoch, Frame.ICRF,
            orbit.RotateCovarianceFromRtn(rtn));
    }

    [Fact]
    public void Tutorial_StateTransitionMatrix_Covariance_Maneuver_Remediation()
    {
        var (bodies, earth, epoch) = Environment();
        var initialState = InitialState(earth, epoch);
        var spacecraft = new Spacecraft(-1001, "SAT", 100.0, 1000.0, new Clock("SAT_CLK", 65536), initialState,
            sectionalArea: 10.0, dragCoeff: 2.2, solarRadiationCoeff: 1.5);
        var window = new Window(epoch, epoch.AddHours(6.0));
        var t = epoch.AddHours(6.0);

        // Section "The state transition matrix only"
        var stm = spacecraft.Propagate(window, bodies, new RK78Integrator(1e-11, 1e-11), true, true,
            TimeSpan.FromSeconds(600.0),
            new CovariancePropagationOptions { Mode = CovariancePropagationMode.StateTransitionOnly });

        Matrix phi = stm.GetStateTransitionMatrix(t);        // Φ(t, t0), 6×6
        Matrix psi = stm.GetParameterSensitivity(t);         // Ψ(t), 6×2: ∂x/∂Cd, ∂x/∂Cr
        Console.WriteLine(string.Join(", ", stm.SensitivityParameters));   // DragCoefficient, ReflectivityCoefficient
        Console.WriteLine($"∂r/∂r0 (xx): {phi.Get(0, 0):F3}, ∂x/∂Cd: {psi.Get(0, 0):F1} m");

        Assert.Equal(6, phi.Rows);
        Assert.Equal(2, psi.Columns);
        Assert.Equal(new[] { ForceParameter.DragCoefficient, ForceParameter.ReflectivityCoefficient },
            stm.SensitivityParameters);

        // Section "The covariance"
        var options = new CovariancePropagationOptions
        {
            Mode = CovariancePropagationMode.Covariance,
            ProcessNoise = new RtnProcessNoise(radial: 1e-12, transverse: 1e-11, normal: 1e-12),   // m²/s³
            ConsiderParameters = new ConsiderParameters(dragCoefficientSigma: 0.1, reflectivityCoefficientSigma: 0.05),
            IncludeInStepControl = true   // Ψ_Cr through the shadow crossings
        };
        var solution = spacecraft.Propagate(window, bodies, new RK78Integrator(1e-11, 1e-11), true, true,
            TimeSpan.FromSeconds(600.0), options);

        Matrix p = solution.GetCovariance(t);                 // 6×6, m², m²/s, m²/s²
        StateVector state = solution.GetStateVector(t);       // the state, with P in state.Covariance
        Matrix extended = solution.GetExtendedCovariance(t);  // 8×8: x, y, z, vx, vy, vz, Cd, Cr
        Console.WriteLine($"σ_x: {System.Math.Sqrt(p.Get(0, 0)):F1} m, " +
                          $"consider parameters: {string.Join(", ", solution.ExtendedCovarianceParameters)}");
        Console.WriteLine($"Output states with P: {solution.StateVectors.Count(s => s.Covariance.HasValue)}");

        Assert.Equal(8, extended.Rows);
        Assert.Equal(p.Get(0, 0), state.Covariance.Value.Get(0, 0));
        Assert.All(solution.StateVectors, s => Assert.True(s.Covariance.HasValue));

        // Section "An impulsive maneuver with its execution error"
        var tank = new FuelTank("TANK", "model", "sn1", 50.0, 50.0);
        var engine = new Engine("ENGINE", "model", "sn1", 300.0, 1.0, tank);
        var maneuvering = new Spacecraft(-1002, "SAT2", 100.0, 1000.0, new Clock("SAT2_CLK", 65536), initialState,
            sectionalArea: 10.0, dragCoeff: 2.2, solarRadiationCoeff: 1.5);
        maneuvering.AddFuelTank(tank);
        maneuvering.AddEngine(engine);
        var raise = new ApogeeHeightManeuver(earth, epoch.AddHours(1.0), TimeSpan.Zero, 6878137.0, engine)
        {
            ExecutionError = new ManeuverExecutionError
            {
                ResolutionSigma = 0.005,   // m/s, along ΔV
                ShutoffSigma = 0.001,      // proportional to |ΔV|
                AutopilotSigma = 0.002,    // m/s, across ΔV
                PointingSigma = 0.001      // rad, proportional to |ΔV|
            }
        };
        maneuvering.SetStandbyManeuver(raise);

        var withBurn = maneuvering.Propagate(window, bodies, new RK78Integrator(1e-11, 1e-11), true, true,
            TimeSpan.FromSeconds(600.0), new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance });

        Time burn = raise.ThrustWindow.Value.StartDate;
        Matrix before = withBurn.GetCovariance(burn.AddSeconds(-1.0));
        Matrix after = withBurn.GetCovariance(burn);   // the value after the burn, its execution error included
        Console.WriteLine($"Burn at {burn}, ΔV {raise.DeltaV.Magnitude():F2} m/s; " +
                          $"σ_vx {System.Math.Sqrt(before.Get(3, 3)):E3} → {System.Math.Sqrt(after.Get(3, 3)):E3} m/s");

        Assert.True(after.Get(3, 3) + after.Get(4, 4) + after.Get(5, 5) > before.Get(3, 3) + before.Get(4, 4) + before.Get(5, 5));

        // Section "A covariance indefinite from rounding": a correlation of −1 written as −1.0001
        var rounded = new Matrix(6, 6);
        for (int i = 0; i < 3; i++)
        {
            rounded.Set(i, i, 100.0 * 100.0);
            rounded.Set(i + 3, i + 3, 0.1 * 0.1);
        }

        rounded.Set(1, 3, -1.0001 * 100.0 * 0.1);
        rounded.Set(3, 1, -1.0001 * 100.0 * 0.1);
        var indefinite = new StateVector(initialState.Position, initialState.Velocity, earth, epoch, Frame.ICRF, rounded);

        var remediation = indefinite.WithRemediatedCovariance();
        if (remediation.Remediated)
            Console.WriteLine($"Smallest eigenvalue of the correlation matrix: {remediation.SmallestCorrelationEigenvalue:E3}");
        var remediated = new Spacecraft(-1003, "SAT3", 100.0, 1000.0, new Clock("SAT3_CLK", 65536), remediation.State,
            sectionalArea: 10.0, dragCoeff: 2.2, solarRadiationCoeff: 1.5);
        var fromRemediated = remediated.Propagate(window, bodies, new RK78Integrator(1e-11, 1e-11), true, true,
            TimeSpan.FromSeconds(600.0), new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance });

        Assert.True(remediation.Remediated);
        Assert.True(remediation.SmallestCorrelationEigenvalue < 0.0);
        Assert.Equal(6, fromRemediated.GetCovariance(t).Rows);
        var rejected = new Spacecraft(-1004, "SAT4", 100.0, 1000.0, new Clock("SAT4_CLK", 65536), indefinite);
        Assert.Throws<InvalidOperationException>(() => rejected.Propagate(window, bodies, new RK78Integrator(1e-11, 1e-11),
            false, false, TimeSpan.FromSeconds(600.0),
            new CovariancePropagationOptions { Mode = CovariancePropagationMode.Covariance }));
    }

    [Fact]
    public void Tutorial_ConjunctionWithThePropagatedCovariance()
    {
        // Section "To a collision probability": two-body, a protected LEO at 700 km and a secondary object that passes
        // 200 m from it 24 h after the start (120 m radial, 160 m normal), its velocity turned by 70° about the radial
        var start = new Time(2024, 3, 1, 0, 0, 0, frame: TimeFrame.TDBFrame);
        var tca = start.AddHours(24.0);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frame.ICRF, start);

        var protectedOrbit = new KeplerianElements(7078137.0, 0.001, 98.2 * Constants.Deg2Rad, 0.4, 1.1, 0.3, earth, start,
            Frame.ICRF).ToStateVector();
        var protectedAtTca = protectedOrbit.ToStateVector(tca);
        var radial = protectedAtTca.Position.Normalize();
        var normal = protectedAtTca.Position.Cross(protectedAtTca.Velocity).Normalize();
        double angle = 70.0 * Constants.Deg2Rad;
        var v = protectedAtTca.Velocity;
        var secondaryAtTca = new StateVector(protectedAtTca.Position + radial * 120.0 + normal * 160.0,
            v * System.Math.Cos(angle) + radial.Cross(v) * System.Math.Sin(angle) +
            radial * (radial * v) * (1.0 - System.Math.Cos(angle)), earth, tca, Frame.ICRF);
        var secondaryOrbit = secondaryAtTca.ToStateVector(start);

        var protectedCraft = new Spacecraft(-2001, "PROTECTED", 100.0, 1000.0, new Clock("P_CLK", 65536),
            WithRtnCovariance(protectedOrbit, new[] { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 }), sectionalArea: 10.0);
        var secondaryCraft = new Spacecraft(-2002, "SECONDARY", 100.0, 1000.0, new Clock("S_CLK", 65536),
            WithRtnCovariance(secondaryOrbit, new[] { 80.0, 300.0, 60.0, 0.3, 0.08, 0.05 }), sectionalArea: 10.0);

        // Both propagated in RK7(8), without the variational equations: the analysis replays them up to the TCA
        var propagation = new Window(start, start.AddHours(26.0));
        foreach (var craft in new[] { protectedCraft, secondaryCraft })
        {
            craft.Propagate(propagation, new CelestialItem[] { earth }, new RK78Integrator(1e-12, 1e-12), false, false,
                TimeSpan.FromSeconds(60.0));
        }

        var profile = new ProtectedSpacecraftProfile(protectedCraft);
        var window = new Window(tca.AddHours(-1.0), tca.AddHours(1.0));
        var encounter = ConjunctionAssessment.Analyze(profile, secondaryCraft, window);
        var held = ConjunctionAssessment.Analyze(profile, secondaryCraft, window,
            new ConjunctionAnalysisOptions { CovariancePropagation = null });

        var source = encounter.EncounterState.ProtectedCovariance.Source;   // CovarianceSource.Propagated
        Console.WriteLine($"Miss {encounter.EncounterState.MissDistanceMeters:F1} m, source {source}, " +
                          $"Pc {encounter.CollisionRisk.ProbabilityOfCollision:E2}; " +
                          $"held in RTN: {held.CollisionRisk.ProbabilityOfCollision:E2}");

        // Section "The CDM"
        var cdm = encounter.ToCdm(new CdmExportOptions { Originator = "MyOpsCenter", MessageId = "CDM-001" });
        string xml = cdm.WriteToString();

        _output.WriteLine($"Pc propagated {encounter.CollisionRisk.ProbabilityOfCollision:E4}, held in RTN " +
                          $"{held.CollisionRisk.ProbabilityOfCollision:E4}");
        Assert.Equal(CovarianceSource.Propagated, source);
        Assert.Equal(CovarianceSource.Propagated, encounter.EncounterState.SecondaryCovariance.Source);
        Assert.Equal(CovarianceSource.InitialStateHeldInRtn, held.EncounterState.ProtectedCovariance.Source);
        Assert.True(held.EncounterState.QualityFlags.HasFlag(EncounterQualityFlags.StaleCovarianceUsed));
        Assert.NotEqual(held.CollisionRisk.ProbabilityOfCollision, encounter.CollisionRisk.ProbabilityOfCollision);
        Assert.True(Cdm.ValidateSchemaFromXml(xml).IsValid);
        Assert.Contains("CALCULATED", xml);
    }

    // Section "To a collision probability": σ in RTN with a correlation of −0.9 between the along-track position and the
    // radial velocity, rotated to the inertial frame of the state
    private static StateVector WithRtnCovariance(StateVector state, double[] sigma)
    {
        var rtn = new Matrix(6, 6);
        for (int i = 0; i < 6; i++)
        {
            rtn.Set(i, i, sigma[i] * sigma[i]);
        }

        rtn.Set(1, 3, -0.9 * sigma[1] * sigma[3]);
        rtn.Set(3, 1, -0.9 * sigma[1] * sigma[3]);
        return new StateVector(state.Position, state.Velocity, state.Observer, state.Epoch, state.Frame,
            state.RotateCovarianceFromRtn(rtn));
    }
}
