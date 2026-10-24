using System;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.Surface;
using Xunit;
using Xunit.Abstractions;

namespace IO.Astrodynamics.Tests.OrbitalParameters;

public class InitialOrbitDeterminationTests
{
    private readonly ITestOutputHelper _output;

    public InitialOrbitDeterminationTests(ITestOutputHelper output)
    {
        _output = output;
        SpiceAPI.Instance.LoadKernels(Constants.SolarSystemKernelPath);
    }

    [Fact]
    public void GeosynchronousObject()
    {
        for (int i = 1; i <= 5; i++)
        {
            var timespan = TimeSpan.FromMinutes(i * 2);
            Site site = new Site(80, "MyStation", TestHelpers.EarthAtJ2000, new Planetodetic(0.0, 45.0 * Constants.DEG_RAD, 0.0));

            var e2 = new TimeSystem.Time(2024, 1, 2);

            var e1 = e2 - timespan;
            var e3 = e2 + timespan;
            var referenceOrbit = new KeplerianElements(
                semiMajorAxis: 42164000.0, // m
                eccentricity: 0.0,
                inclination: 15.0 * Constants.DEG_RAD,
                rigthAscendingNode: 45.0 * Constants.DEG_RAD,
                argumentOfPeriapsis: 0.0,
                meanAnomaly: 45.0 * Constants.DEG_RAD, // Décalage de 45 degrés
                observer: TestHelpers.EarthAtJ2000,
                frame: Frames.Frame.ICRF,
                epoch: e2
            );
            var obs1 = referenceOrbit.AtEpoch(e1).RelativeTo(site, Aberration.LT);
            var obs2 = referenceOrbit.AtEpoch(e2).RelativeTo(site, Aberration.LT);
            var obs3 = referenceOrbit.AtEpoch(e3).RelativeTo(site, Aberration.LT);
            var orbitalParams =
                InitialOrbitDetermination.CreateFromObservation_Gauss(obs1.ToEquatorial(), obs2.ToEquatorial(), obs3.ToEquatorial(), site,
                    PlanetsAndMoons.EARTH_BODY, 42000000.0);
            var deltaRange = 100.0 * (orbitalParams.OrbitalParameters.ToStateVector().Position - referenceOrbit.ToStateVector().Position).Magnitude() /
                             referenceOrbit.ToStateVector().Position.Magnitude();
            var deltaVelocity = 100.0 * (orbitalParams.OrbitalParameters.ToStateVector().Velocity - referenceOrbit.ToStateVector().Velocity).Magnitude() /
                                referenceOrbit.ToStateVector().Velocity.Magnitude();
            Assert.True(deltaRange < 0.02);
            Assert.True(deltaVelocity < 0.02);
        }
    }

    [Fact]
    public void PlanetObject()
    {
            var timespan = TimeSpan.FromDays(10);
            Site site = new Site(13, "DSS-13", TestHelpers.EarthAtJ2000);

            var e2 = new TimeSystem.Time(2023, 3, 1);
            var e1 = e2 - timespan;
            var e3 = e2 + timespan;
            var target = new Barycenter(4);
            var obs1 = target.GetEphemeris(e1, site, Frames.Frame.ICRF, Aberration.LT);
            var obs2 = target.GetEphemeris(e2, site, Frames.Frame.ICRF, Aberration.LT);
            var obs3 = target.GetEphemeris(e3, site, Frames.Frame.ICRF, Aberration.LT);
            var referenceOrbit = target.GetEphemeris(e2, TestHelpers.Sun, Frames.Frame.ICRF, Aberration.LT);
            var orbitalParams =
                InitialOrbitDetermination.CreateFromObservation_Gauss(obs1.ToEquatorial(), obs2.ToEquatorial(), obs3.ToEquatorial(), site,
                    Stars.SUN_BODY, 350000000000.58063);
            
            var deltaRange = 100.0 * (orbitalParams.OrbitalParameters.ToStateVector().Position - referenceOrbit.ToStateVector().Position).Magnitude() /
                             referenceOrbit.ToStateVector().Position.Magnitude();
            var deltaVelocity = 100.0 * (orbitalParams.OrbitalParameters.ToStateVector().Velocity - referenceOrbit.ToStateVector().Velocity).Magnitude() /
                                referenceOrbit.ToStateVector().Velocity.Magnitude();
            
            Assert.True(deltaRange < 0.06);
            Assert.True(deltaVelocity < 0.15);
    }

    // The site of the COBE observations (University of Maryland) and the reference orbit of the observations that
    // SPICE builds from it (tools/geodetic_reference/iod_observations.c): the site by georec_c on the ellipsoid of
    // pck00011 and pxform_c from ITRF93 to J2000, the object by prop2b_c from its state at the middle epoch, the
    // geometric direction from one to the other by recrad_c
    private static readonly TimeSystem.Time MiddleEpoch =
        new(2000, 11, 6, 22, 34, 30, 0, 0, TimeSystem.TimeFrame.UTCFrame);

    private static readonly Equatorial MiddleObservation =
        new(0.81897935661301124, 5.5588517883210384, 893559.82174179261, MiddleEpoch);

    private static Site MarylandSite() => new(99, "Maryland university", TestHelpers.EarthAtJ2000,
        new Planetodetic(-76.95667 * Constants.DEG_RAD, 39.00167 * Constants.DEG_RAD, 53.0));

    private static StateVector ReferenceOrbit() => new(new Vector3(3520000.0, -4310000.0, 4645000.0),
        new Vector3(-4026.0, 2616.0, 5468.0), TestHelpers.EarthAtJ2000, MiddleEpoch, Frames.Frame.ICRF);

    // The departures of the determined state from the reference orbit, m and m/s
    private static (double Position, double Velocity) Departure(Equatorial first, Equatorial last)
    {
        var determined = InitialOrbitDetermination.CreateFromObservation_Gauss(first, MiddleObservation, last,
            MarylandSite(), PlanetsAndMoons.EARTH_BODY, 7_250_000.0).OrbitalParameters.ToStateVector();
        var reference = ReferenceOrbit();
        return ((determined.Position - reference.Position).Magnitude(),
            (determined.Velocity - reference.Velocity).Magnitude());
    }

    [Fact]
    public void FromTheExactSite_SpiceObservations()
    {
        // Arrange: the epochs of the COBE observations, 181 s before and 180 s after the middle one
        var ut1 = new TimeSystem.Time(2000, 11, 6, 22, 31, 29, 0, 0, TimeSystem.TimeFrame.UTCFrame);
        var ut3 = new TimeSystem.Time(2000, 11, 6, 22, 37, 30, 0, 0, TimeSystem.TimeFrame.UTCFrame);
        var spiceSites = new[]
        {
            new Vector3(3011128.5143432664, -3945694.6188169341, 3992301.9416880687),
            new Vector3(3062942.1778810532, -3905611.5695034186, 3992299.9926829929),
            new Vector3(3113940.4649475398, -3865075.2252871748, 3992298.0890825163)
        };
        var obs1 = new Equatorial(-0.28566294527432967, 5.7075255380194969, 1457727.8257919517, ut1);
        var obs3 = new Equatorial(1.327008296156168, 2.8898779558667966, 1600130.4050160523, ut3);
        var site = MarylandSite();

        // Act
        double siteError = new[] { ut1, MiddleEpoch, ut3 }.Select((epoch, i) => (site
            .GetEphemeris(epoch, TestHelpers.EarthAtJ2000, Frames.Frame.ICRF, Aberration.None).ToStateVector()
            .Position - spiceSites[i]).Magnitude()).Max();
        var (position, velocity) = Departure(obs1, obs3);

        // Assert
        _output.WriteLine($"Site against SPICE {siteError:E2} m; determined state against the reference orbit: " +
                          $"position {position:E3} m, velocity {velocity:E3} m/s");
        // Measured 2026-10-20: the site within 2.8e-4 m of SPICE's. The determined state departs from the reference
        // orbit by 9.58e3 m and 77.5 m/s: the error of the method, whose f and g series are cut after their τ² and τ³
        // terms in the polynomial of the range (H. D. Curtis, Orbital Mechanics for Engineering Students, algorithm
        // 5.5, confirmed by S. Guillet, 2026-10-24), of second order in the spacing
        // (FromTheExactSite_TheErrorIsOfSecondOrderInTheSpacing); held to twice that
        Assert.True(siteError < 1e-3, $"site {siteError:E2} m");
        Assert.True(position < 2.0e4, $"position {position:E3} m");
        Assert.True(velocity < 1.6e2, $"velocity {velocity:E3} m/s");
    }

    [Fact]
    public void FromTheExactSite_TheErrorIsOfSecondOrderInTheSpacing()
    {
        // Arrange: the outer observations at 90, 45 and 22.5 s from the middle epoch, by the same program
        var observations = new[]
        {
            (Spacing: 90.0, First: (Dec: 0.13732836580158847, Ra: 5.6561060687200362, Range: 1034350.1034743417),
                Last: (Dec: 1.4122823191623839, Ra: 5.0219114714334889, Range: 1138724.3067752656)),
            (Spacing: 45.0, First: (Dec: 0.45442628206408836, Ra: 5.6182633748413764, Range: 915384.72748733335),
                Last: (Dec: 1.1586312962991618, Ra: 5.4384125042834448, Range: 975439.55641085748)),
            (Spacing: 22.5, First: (Dec: 0.63479073404434205, Ra: 5.5925658479260294, Range: 891133.43961372843),
                Last: (Dec: 0.99654640814984918, Ra: 5.5115073053936658, Range: 922449.84352911904))
        };

        // Act
        var departures = observations.Select(o => Departure(
            new Equatorial(o.First.Dec, o.First.Ra, o.First.Range, MiddleEpoch - TimeSpan.FromSeconds(o.Spacing)),
            new Equatorial(o.Last.Dec, o.Last.Ra, o.Last.Range, MiddleEpoch + TimeSpan.FromSeconds(o.Spacing))))
            .ToArray();

        // Assert: halving the spacing divides the departure by 4, down to 149 m at 22.5 s; a defect of the site, of
        // its frame or of the epochs would not shrink with the spacing
        for (int k = 1; k < departures.Length; k++)
        {
            double positionRatio = departures[k - 1].Position / departures[k].Position;
            double velocityRatio = departures[k - 1].Velocity / departures[k].Velocity;
            _output.WriteLine($"{observations[k - 1].Spacing} s to {observations[k].Spacing} s: position " +
                              $"{departures[k - 1].Position:E3} m to {departures[k].Position:E3} m (ratio " +
                              $"{positionRatio:F4}), velocity ratio {velocityRatio:F4}");
            // Measured 2026-10-20: 4.002 and 4.001 in position, 3.988 and 3.997 in velocity
            Assert.InRange(positionRatio, 3.9, 4.1);
            Assert.InRange(velocityRatio, 3.9, 4.1);
        }
    }
}
