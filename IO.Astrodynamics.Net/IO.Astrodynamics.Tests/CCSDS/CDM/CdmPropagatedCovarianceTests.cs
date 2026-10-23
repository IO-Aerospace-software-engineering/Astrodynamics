// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Atmosphere.NRLMSISE_00;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.CCSDS.CDM;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.Propagator.Integrators;
using IO.Astrodynamics.Propagator.Variational;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.SSA;
using IO.Astrodynamics.Tests.SSA;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Matrix = IO.Astrodynamics.Math.Matrix;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.CCSDS.CDM;

/// <summary>
/// A conjunction 2 h after the initial states, both participants propagated in RK7(8) with EGM2008 10×10, NRLMSISE-00
/// drag, the Moon, the Sun and solar radiation pressure.
/// </summary>
public sealed class CdmPropagatedCovarianceCase
{
    internal static readonly TimeSystem.Time Start = new(2024, 3, 1, 0, 0, 0, frame: TimeFrame.TDBFrame);
    internal static readonly TimeSystem.Time Tca = Start.AddHours(2.0);

    internal static readonly CovariancePropagationOptions Options = new()
    {
        Mode = CovariancePropagationMode.Covariance,
        ProcessNoise = new RtnProcessNoise(1e-12, 1e-11, 1e-12),
        ConsiderParameters = new ConsiderParameters(dragCoefficientSigma: 0.2, reflectivityCoefficientSigma: 0.1)
    };

    public CdmPropagatedCovarianceCase()
    {
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
        var earth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start,
            new GeopotentialModelParameters("Data/SolarSystem/EGM2008_to70_TideFree", 10),
            new Nrlmsise00Model(SpaceWeather.Nominal));
        var moon = new CelestialBody(PlanetsAndMoons.MOON, Frames.Frame.ICRF, Start);
        var sun = new CelestialBody(Stars.Sun);
        Bodies = new CelestialItem[] { earth, moon, sun };

        // The same without the geopotential, whose harmonics move the two-body geometry of the encounter by about 100 km
        // in 2 h: the case of the avoidance options, which need the encounter
        var pointMassEarth = new CelestialBody(PlanetsAndMoons.EARTH, Frames.Frame.ICRF, Start, null,
            new Nrlmsise00Model(SpaceWeather.Nominal));
        PointMassBodies = new CelestialItem[] { pointMassEarth, moon, sun };
        var (protectedPointMass, secondaryPointMass) = ConjunctionCovarianceCase.Pair(pointMassEarth, Start, Tca);
        PointMassProtectedStart = ConjunctionCovarianceCase.WithRtnCovariance(protectedPointMass, new[] { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 });
        PointMassSecondaryStart = ConjunctionCovarianceCase.WithRtnCovariance(secondaryPointMass, new[] { 80.0, 300.0, 60.0, 0.3, 0.08, 0.05 });
        var (protectedOrbit, secondaryOrbit) = ConjunctionCovarianceCase.Pair(earth, Start, Tca);
        ProtectedStart = ConjunctionCovarianceCase.WithRtnCovariance(protectedOrbit, new[] { 50.0, 200.0, 30.0, 0.2, 0.05, 0.03 });
        SecondaryStart = ConjunctionCovarianceCase.WithRtnCovariance(secondaryOrbit, new[] { 80.0, 300.0, 60.0, 0.3, 0.08, 0.05 });
    }

    internal IReadOnlyList<CelestialItem> Bodies { get; }

    /// <summary>The bodies with a point-mass Earth (NRLMSISE-00 kept).</summary>
    internal IReadOnlyList<CelestialItem> PointMassBodies { get; }

    internal StateVector PointMassProtectedStart { get; }

    internal StateVector PointMassSecondaryStart { get; }

    internal StateVector ProtectedStart { get; }

    internal StateVector SecondaryStart { get; }

    internal static Window AnalysisWindow => new(Tca.AddMinutes(-30.0), Tca.AddMinutes(30.0));

    internal PropagationSolution Propagate(Spacecraft spacecraft, CovariancePropagationOptions options = null,
        bool pointMass = false, double tolerance = 1e-11)
    {
        return spacecraft.Propagate(new Window(Start, Start.AddHours(3.0)), pointMass ? PointMassBodies : Bodies,
            new RK78Integrator(tolerance, tolerance), true, true, TimeSpan.FromSeconds(60.0), options);
    }
}

/// <summary>
/// The CDM export of a propagated covariance (step 10, E5): the consider parameters in the rows CDRG and CSRP, the force
/// model actually used in the metadata, the process noise in a comment; schema validation and re-read.
/// </summary>
public class CdmPropagatedCovarianceTests : IClassFixture<CdmPropagatedCovarianceCase>
{
    private readonly CdmPropagatedCovarianceCase _case;
    private readonly ITestOutputHelper _output;

    public CdmPropagatedCovarianceTests(CdmPropagatedCovarianceCase @case, ITestOutputHelper output)
    {
        _case = @case;
        _output = output;
    }

    [Fact]
    public void ToCdm_PropagatedCovarianceWithConsiderParameters_ValidatesAndReadsBack()
    {
        // Arrange: both participants propagated without the covariance; the analysis replays them with the process noise
        // and Cd and Cr as consider parameters. The protected one is also propagated with the covariance, the reference.
        var protectedCraft = ReferenceSpacecraft(-6301, _case.ProtectedStart);
        var secondaryCraft = ReferenceSpacecraft(-6302, _case.SecondaryStart);
        _case.Propagate(protectedCraft);
        _case.Propagate(secondaryCraft);
        var reference = _case.Propagate(ReferenceSpacecraft(-6303, _case.ProtectedStart), CdmPropagatedCovarianceCase.Options);
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondaryCraft,
            CdmPropagatedCovarianceCase.AnalysisWindow,
            new ConjunctionAnalysisOptions { CovariancePropagation = CdmPropagatedCovarianceCase.Options });

        // Act
        var cdm = encounter.ToCdm(new CdmExportOptions { CreationDateUtc = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc) });
        var xml = cdm.WriteToString();
        var schema = Cdm.ValidateSchemaFromXml(xml);
        var content = cdm.Validate();
        var reread = Cdm.ReadFromString(xml);

        // Assert: valid; read back to the digit (G17)
        Assert.True(schema.IsValid, string.Join("; ", schema.Issues.Select(i => i.Message)));
        Assert.True(content.IsValid, string.Join("; ", content.Issues.Select(i => i.Message)));
        for (int s = 0; s < 2; s++)
        {
            var written = cdm.Segments[s].Data.CovarianceMatrix;
            var read = reread.Segments[s].Data.CovarianceMatrix;
            // The CDM carries the lower triangle
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    Assert.Equal(written.StateCovarianceRtn.Get(i, j), read.StateCovarianceRtn.Get(i, j));
                }
            }

            Assert.Equal(written.CdrgR, read.CdrgR);
            Assert.Equal(written.CdrgNdot, read.CdrgNdot);
            Assert.Equal(written.CdrgDrg, read.CdrgDrg);
            Assert.Equal(written.CsrpT, read.CsrpT);
            Assert.Equal(written.CsrpDrg, read.CsrpDrg);
            Assert.Equal(written.CsrpSrp, read.CsrpSrp);
            Assert.Equal(cdm.Segments[s].Metadata.GravityModel, reread.Segments[s].Metadata.GravityModel);
            Assert.Equal(cdm.Segments[s].Metadata.NBodyPerturbations, reread.Segments[s].Metadata.NBodyPerturbations);
        }

        // The rows CDRG and CSRP: the extended covariance of the propagation with the covariance at the TCA, the cross terms
        // rotated to RTN as the state block and scaled by A/m, the variances by (A/m)² (CCSDS 508.0-B-1)
        var tca = encounter.EncounterState.Epoch;
        var extended = reference.GetExtendedCovariance(tca);
        Assert.Equal(new[] { ForceParameter.DragCoefficient, ForceParameter.ReflectivityCoefficient },
            reference.ExtendedCovarianceParameters);
        double areaOverMass = protectedCraft.SectionalArea / protectedCraft.GetTotalMass();
        var state = encounter.ProtectedState;
        var protectedMatrix = cdm.Segments[0].Data.CovarianceMatrix;
        var expectedDrag = Row(state, extended, 6, areaOverMass);
        var expectedSrp = Row(state, extended, 7, areaOverMass);
        var actualDrag = new[]
        {
            protectedMatrix.CdrgR.Value, protectedMatrix.CdrgT.Value, protectedMatrix.CdrgN.Value,
            protectedMatrix.CdrgRdot.Value, protectedMatrix.CdrgTdot.Value, protectedMatrix.CdrgNdot.Value
        };
        var actualSrp = new[]
        {
            protectedMatrix.CsrpR.Value, protectedMatrix.CsrpT.Value, protectedMatrix.CsrpN.Value,
            protectedMatrix.CsrpRdot.Value, protectedMatrix.CsrpTdot.Value, protectedMatrix.CsrpNdot.Value
        };
        double dragError = RelativeError(expectedDrag, actualDrag);
        double srpError = RelativeError(expectedSrp, actualSrp);
        _output.WriteLine($"CDRG against the extended covariance: {dragError:E2}; CSRP: {srpError:E2}");
        Assert.True(dragError < 1e-12, $"{dragError:E2}");
        Assert.True(srpError < 1e-12, $"{srpError:E2}");
        Assert.Equal(0.2 * 0.2 * areaOverMass * areaOverMass, protectedMatrix.CdrgDrg.Value, 1e-15);
        Assert.Equal(0.1 * 0.1 * areaOverMass * areaOverMass, protectedMatrix.CsrpSrp.Value, 1e-15);
        Assert.Equal(0.0, protectedMatrix.CsrpDrg);

        // The force model actually used and the process noise
        var metadata = cdm.Segments[0].Metadata;
        Assert.Equal(CdmCovarianceMethod.Calculated, metadata.CovarianceMethod);
        Assert.Equal("EGM2008: 10D 10O", metadata.GravityModel);
        Assert.Equal("NRLMSISE-00", metadata.AtmosphericModel);
        Assert.Equal("MOON, SUN", metadata.NBodyPerturbations);
        Assert.True(metadata.SolarRadPressure);
        var comments = cdm.Segments[0].Data.Comments;
        Assert.Contains(comments, c => c.StartsWith("Covariance propagated to TCA"));
        Assert.Contains(comments, c => c.Contains("q_R = 1.000E-012, q_T = 1.000E-011, q_N = 1.000E-012 m**2/s**3"));
        Assert.Contains(comments, c => c.Contains("Cd sigma 0.2, Cr sigma 0.1"));
        Assert.Contains("Process noise applied", xml);
        Assert.Contains(reread.Segments[0].Data.Comments, c => c.StartsWith("Process noise applied"));
    }

    [Fact]
    public void ToCdm_PropagatedCovarianceWithoutNoiseOrConsiderParameters_SaysSoAndHasNoParameterRows()
    {
        // Arrange: the defaults of the analysis
        var protectedCraft = ReferenceSpacecraft(-6311, _case.ProtectedStart);
        var secondaryCraft = ReferenceSpacecraft(-6312, _case.SecondaryStart);
        _case.Propagate(protectedCraft);
        _case.Propagate(secondaryCraft);
        var encounter = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondaryCraft,
            CdmPropagatedCovarianceCase.AnalysisWindow);

        // Act
        var cdm = encounter.ToCdm();

        // Assert: the covariance at TCA, never stale, so exported without the opt-in; no CDRG or CSRP row
        Assert.True(Cdm.ValidateSchemaFromXml(cdm.WriteToString()).IsValid);
        var matrix = cdm.Segments[1].Data.CovarianceMatrix;
        Assert.Null(matrix.CdrgR);
        Assert.Null(matrix.CsrpSrp);
        Assert.Contains(cdm.Segments[1].Data.Comments, c => c.StartsWith("No process noise applied"));
        Assert.Equal(encounter.SecondaryState.RotateCovarianceToRtn(encounter.SecondaryState.Covariance.Value).ToArray(),
            matrix.StateCovarianceRtn.ToArray());
        Assert.Equal(CovarianceSource.Propagated, encounter.SecondaryCovariance.Source);

        // The former behaviour still refuses a stale covariance by default
        var held = ConjunctionAssessment.Analyze(new ProtectedSpacecraftProfile(protectedCraft), secondaryCraft,
            CdmPropagatedCovarianceCase.AnalysisWindow, new ConjunctionAnalysisOptions { CovariancePropagation = null });
        Assert.Throws<InvalidOperationException>(() => held.ToCdm());
        Assert.Null(held.ToCdm(new CdmExportOptions { AllowStaleCovariance = true }).Segments[0].Metadata.GravityModel);
    }

    private static Spacecraft ReferenceSpacecraft(int naifId, StateVector start) =>
        IO.Astrodynamics.Tests.Propagators.Variational.ReferenceCase.NewSpacecraft(naifId, start);

    // Row k of the extended covariance, cov(x, C), rotated to the RTN frame of the state (a pure rotation of the position
    // and velocity parts) and scaled by A/m
    private static double[] Row(StateVector state, Matrix extended, int k, double areaOverMass)
    {
        var position = state.ToRtn(new Vector3(extended.Get(k, 0), extended.Get(k, 1), extended.Get(k, 2)));
        var velocity = state.ToRtn(new Vector3(extended.Get(k, 3), extended.Get(k, 4), extended.Get(k, 5)));
        return new[] { position.X, position.Y, position.Z, velocity.X, velocity.Y, velocity.Z }
            .Select(v => v * areaOverMass).ToArray();
    }

    private static double RelativeError(double[] expected, double[] actual)
    {
        double difference = 0.0;
        double norm = 0.0;
        for (int i = 0; i < expected.Length; i++)
        {
            difference += (actual[i] - expected[i]) * (actual[i] - expected[i]);
            norm += expected[i] * expected[i];
        }

        return System.Math.Sqrt(difference / norm);
    }
}
