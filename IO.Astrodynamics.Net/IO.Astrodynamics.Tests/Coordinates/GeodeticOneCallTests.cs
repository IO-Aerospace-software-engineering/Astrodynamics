// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using IO.Astrodynamics.Atmosphere;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Body.Spacecraft;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using Xunit;
using Xunit.Abstractions;
using Vector3 = IO.Astrodynamics.Math.Vector3;

namespace IO.Astrodynamics.Tests.Coordinates;

/// <summary>
/// The coordinates of R20 (#668, lot L3): <see cref="Astrodynamics.OrbitalParameters.OrbitalParameters.ToPlanetodetic()"/> in one call
/// on the ellipsoid of the observer's body, the conversions of the coordinate records taking the
/// <see cref="CelestialBody"/> (the flattening and radius forms, whose two orders a swap compiled, are not public),
/// <see cref="Equatorial.FromRightAscensionDeclination(double, double, TimeSystem.Time)"/> in the order of its name, and
/// <see cref="AtmosphericContext.FromPlanetodetic(Planetodetic, TimeSystem.Time)"/>. Expected values: CSPICE <c>recgeo_c</c> and
/// <c>georec_c</c> on the pck00011 Earth (<c>Data/GeodeticReference/geodetic_reference.json</c>, written by
/// <c>tools/geodetic_reference/geodetic_ref.c</c>: its points at latitude ±π/2 lie 3.9e-10 m off the polar axis, those
/// of <c>georec_c</c>), and the closed forms, on the exact polar axis and the equator among them; the one-call paths
/// equal, bit for bit, the two calls they replace. The centre of the body and the axis with a negative-zero coordinate,
/// where the two calls differ from <c>reclat_c</c> and <c>recgeo_c</c> (NaN, ±π), are a known limitation of 11.0.0.
/// </summary>
public class GeodeticOneCallTests
{
    private const string ReferenceFile = "Data/GeodeticReference/geodetic_reference.json";
    private static readonly TimeSystem.Time Epoch = new(2024, 6, 21, 12, 0, 0, TimeScale.TDB);
    private readonly ITestOutputHelper _output;

    public GeodeticOneCallTests(ITestOutputHelper output)
    {
        _output = output;
        TestKernels.EnsureSolarSystem();
    }

    private sealed record EarthPoint(double A, double F, double Longitude, double Latitude, double Altitude, Vector3 SpiceXyz,
        double[] SpiceGeodetic);

    private static EarthPoint[] EarthPoints()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ReferenceFile));
        return document.RootElement.GetProperty("points").EnumerateArray()
            .Where(p => p.GetProperty("body").GetString() == "EARTH")
            .Select(p =>
            {
                var xyz = p.GetProperty("spice_xyz").EnumerateArray().Select(e => e.GetDouble()).ToArray();
                return new EarthPoint(p.GetProperty("equatorial_radius").GetDouble(), p.GetProperty("flattening").GetDouble(),
                    p.GetProperty("longitude").GetDouble(), p.GetProperty("latitude").GetDouble(), p.GetProperty("altitude").GetDouble(),
                    new Vector3(xyz[0], xyz[1], xyz[2]), p.GetProperty("spice_geodetic").EnumerateArray().Select(e => e.GetDouble()).ToArray());
            }).ToArray();
    }

    private static void AssertSameBits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));

    private static void AssertSameBits(Planetodetic expected, Planetodetic actual)
    {
        AssertSameBits(expected.Longitude, actual.Longitude);
        AssertSameBits(expected.Latitude, actual.Latitude);
        AssertSameBits(expected.Altitude, actual.Altitude);
    }

    private static void AssertSameBits(Planetocentric expected, Planetocentric actual)
    {
        AssertSameBits(expected.Longitude, actual.Longitude);
        AssertSameBits(expected.Latitude, actual.Latitude);
        AssertSameBits(expected.Radius, actual.Radius);
    }

    [Fact]
    public void ReferenceEllipsoid_IsTheEarthOfTheKernels()
    {
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var point = EarthPoints()[0];
        Assert.Equal(point.A, earth.EquatorialRadius);
        Assert.Equal(point.F, earth.Flattening, 15);
    }

    [Fact]
    public void ToPlanetodetic_OfABodyFixedState_IsTheTwoCallsBitForBit_AndRecgeo()
    {
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        double longitude = 0.0, latitude = 0.0, latitudeOffPoles = 0.0, altitude = 0.0;
        foreach (var p in EarthPoints())
        {
            var state = new StateVector(p.SpiceXyz, Vector3.Zero, earth, Epoch, earth.Frame);

            // One call against the two it replaces (11.0.0 before R20: ToPlanetocentric(aberration).ToPlanetodetic(f, R))
            var oneCall = state.ToPlanetodetic();
            var planetocentric = state.ToPlanetocentric();
            AssertSameBits(planetocentric.ToPlanetodetic(earth), oneCall);
            AssertSameBits(planetocentric.ToPlanetodetic(earth.Flattening, earth.EquatorialRadius), oneCall);

            // The planetocentric coordinates are the closed form of the position
            AssertSameBits(System.Math.Atan2(p.SpiceXyz.Y, p.SpiceXyz.X), planetocentric.Longitude);
            AssertSameBits(System.Math.Asin(p.SpiceXyz.Z / p.SpiceXyz.Magnitude()), planetocentric.Latitude);
            AssertSameBits(p.SpiceXyz.Magnitude(), planetocentric.Radius);

            // Against recgeo_c of the same position
            if (System.Math.Abs(p.Latitude) < System.Math.PI / 2.0)
            {
                longitude = System.Math.Max(longitude, System.Math.Abs(System.Math.IEEERemainder(oneCall.Longitude - p.SpiceGeodetic[0], 2.0 * System.Math.PI)));
            }

            latitude = System.Math.Max(latitude, System.Math.Abs(oneCall.Latitude - p.SpiceGeodetic[1]));
            if (System.Math.Abs(p.Latitude) < 1.5)
            {
                latitudeOffPoles = System.Math.Max(latitudeOffPoles, System.Math.Abs(oneCall.Latitude - p.SpiceGeodetic[1]));
            }
            altitude = System.Math.Max(altitude, System.Math.Abs(oneCall.Altitude - p.SpiceGeodetic[2]) / System.Math.Max(System.Math.Abs(p.Altitude), p.A));
        }

        _output.WriteLine($"ToPlanetodetic() against recgeo_c, {EarthPoints().Length} Earth points: longitude {longitude:E2} rad, " +
                          $"latitude {latitude:E2} rad ({latitudeOffPoles:E2} below 1.5 rad), altitude {altitude:E2} of max(|h|, a)");
        // Measured 2026-11-21 (Linux): 0 rad; 7.6e-12 rad in latitude at 1.75e-5 rad from the poles (112 m from the axis),
        // 5.6e-16 rad below 1.5 rad; 6.5e-16. The latitude is that of the two calls the one call equals bit for bit (R20's
        // design): the planetocentric latitude asin(z / r) loses digits where z / r is within ~1e-10 of 1, while the
        // direct conversion of GeodeticCoordinatesTests.FromBodyFixed_AgainstSpice holds 1e-15 there; reported as a
        // defect outside lot L3, not fixed here (the geodetic coordinate searches read the same path)
        Assert.True(longitude < 1e-15, $"longitude {longitude:E2}");
        Assert.True(latitude < 2e-11, $"latitude {latitude:E2}");
        Assert.True(latitudeOffPoles < 2e-15, $"latitude below 1.5 rad {latitudeOffPoles:E2}");
        Assert.True(altitude < 2e-15, $"altitude {altitude:E2}");
    }

    [Fact]
    public void ToPlanetodetic_OnTheExactPolarAxisAndTheEquator_IsTheClosedForm()
    {
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        double a = earth.EquatorialRadius, b = a * (1.0 - earth.Flattening);
        double worst = 0.0;
        foreach (double h in new[] { -5e3, 0.0, 400e3, 35786e3 })
        {
            foreach (double sign in new[] { 1.0, -1.0 })
            {
                // On the axis, +0 in x and y: longitude 0 and latitude ±π/2 exactly (reclat_c, recgeo_c), altitude |z| − b
                double z = sign * (b + h);
                var onAxis = new StateVector(new Vector3(0.0, 0.0, z), Vector3.Zero, earth, Epoch, earth.Frame);
                var geodetic = onAxis.ToPlanetodetic();
                var planetocentric = onAxis.ToPlanetocentric();
                AssertSameBits(0.0, geodetic.Longitude);
                AssertSameBits(sign * System.Math.PI / 2.0, geodetic.Latitude);
                AssertSameBits(0.0, planetocentric.Longitude);
                AssertSameBits(sign * System.Math.PI / 2.0, planetocentric.Latitude);
                AssertSameBits(System.Math.Abs(z), planetocentric.Radius);
                worst = System.Math.Max(worst, System.Math.Abs(geodetic.Altitude - (System.Math.Abs(z) - b)));
            }

            foreach (double longitude in new[] { 0.0, 1.0, -2.5 })
            {
                // On the equator: latitude 0 exactly, the longitude atan2(y, x), altitude r − a
                double r = a + h;
                var position = new Vector3(r * System.Math.Cos(longitude), r * System.Math.Sin(longitude), 0.0);
                var onEquator = new StateVector(position, Vector3.Zero, earth, Epoch, earth.Frame);
                var geodetic = onEquator.ToPlanetodetic();
                AssertSameBits(System.Math.Atan2(position.Y, position.X), geodetic.Longitude);
                AssertSameBits(0.0, geodetic.Latitude);
                worst = System.Math.Max(worst, System.Math.Abs(geodetic.Altitude - (position.Magnitude() - a)));
            }
        }

        _output.WriteLine($"ToPlanetodetic() on the exact polar axis and the equator: altitude {worst:E2} m from the closed form");
        // Measured 2026-11-21 (Linux): 7.45e-9 m, one ulp of |z| = 4.2e7 m at 35786 km (the rounding of b + h itself);
        // twice that, to be confirmed on Windows and macOS arm64 by CI
        Assert.True(worst < 1.5e-8, $"{worst:E2} m");
    }

    [Fact]
    public void ToPlanetodetic_OfAnIcrfState_RotatesToTheBodyFrameAtItsEpoch()
    {
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var state = new StateVector(new Vector3(4000e3, -3000e3, 5000e3), new Vector3(1000.0, 6000.0, 2000.0), earth, Epoch, Frames.Frame.ICRF);
        StateVector bodyFixed = state.ToFrame(earth.Frame);
        AssertSameBits(bodyFixed.ToPlanetodetic(), state.ToPlanetodetic());
        AssertSameBits(bodyFixed.ToPlanetocentric(), state.ToPlanetocentric());
    }

    [Fact]
    public void ToPlanetodetic_OnTheMoon_TakesTheEllipsoidOfTheMoon()
    {
        var moon = new CelestialBody(PlanetsAndMoons.Moon);
        var state = new StateVector(new Vector3(1000e3, 1200e3, -900e3), Vector3.Zero, moon, Epoch, moon.Frame);
        var geodetic = state.ToPlanetodetic();
        AssertSameBits(state.ToPlanetocentric().ToPlanetodetic(moon.Flattening, moon.EquatorialRadius), geodetic);
        // Back to the position by georec_c's closed form on the Moon's ellipsoid
        var back = geodetic.ToCartesianCoordinates(moon);
        Assert.True((back - state.Position).Magnitude() < 1e-8, $"{(back - state.Position).Magnitude():E2} m");
    }

    [Fact]
    public void ObserverThatIsNoBody_Throws()
    {
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var spacecraft = new Spacecraft(-2002, "R20Observer", 100.0, new StateVector(new Vector3(6800e3, 0.0, 0.0), new Vector3(0.0, 7600.0, 0.0), earth, Epoch, Frames.Frame.ICRF), 1000.0,
            new Clock("R20ObserverClk", 65536));
        var aboutSpacecraft = new StateVector(new Vector3(100.0, 0.0, 0.0), Vector3.Zero, spacecraft, Epoch, Frames.Frame.ICRF);
        var aboutBarycenter = new StateVector(new Vector3(1e11, 0.0, 0.0), Vector3.Zero, new Barycenter(0), Epoch, Frames.Frame.ICRF);
        Assert.Throws<InvalidOperationException>(() => aboutSpacecraft.ToPlanetodetic());
        Assert.Throws<InvalidOperationException>(() => aboutSpacecraft.ToPlanetocentric());
        Assert.Throws<InvalidOperationException>(() => aboutBarycenter.ToPlanetodetic());
    }

    [Fact]
    public void GeodeticSearch_FromAnObserverThatIsNoBody_ThrowsInvalidOperationException()
    {
        // The managed coordinate search reads ToPlanetodetic(): an observer without an ellipsoid throws its
        // InvalidOperationException (before 11.0.0 an InvalidCastException, the observer cast to CelestialBody)
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var spacecraft = new Spacecraft(-2003, "R20Search", 100.0, new StateVector(new Vector3(6800e3, 0.0, 0.0), new Vector3(0.0, 7600.0, 0.0), earth, Epoch, Frames.Frame.ICRF), 1000.0,
            new Clock("R20SearchClk", 65536));
        var moon = new CelestialBody(PlanetsAndMoons.Moon);
        Assert.Throws<InvalidOperationException>(() => spacecraft.FindCoordinateWindows(new Window(Epoch, Epoch.AddSeconds(600.0)), moon, Frames.Frame.ICRF, CoordinateSystem.Geodetic, Coordinate.Latitude, RelationalOperator.GreaterThan, 0.0, new WindowSearchOptions { Step = TimeSpan.FromSeconds(60.0) }).ToArray());
    }

    [Fact]
    public void ToCartesianCoordinates_OfTheBody_IsGeorec()
    {
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        double worst = 0.0;
        foreach (var p in EarthPoints())
        {
            var geodetic = new Planetodetic(p.Longitude, p.Latitude, p.Altitude);
            var position = geodetic.ToCartesianCoordinates(earth);
            // The two calls it replaces, bit for bit
            var twoCalls = geodetic.ToPlanetocentric(earth.Flattening, earth.EquatorialRadius).ToCartesianCoordinates();
            AssertSameBits(twoCalls.X, position.X);
            AssertSameBits(twoCalls.Y, position.Y);
            AssertSameBits(twoCalls.Z, position.Z);
            AssertSameBits(geodetic.ToPlanetocentric(earth), geodetic.ToPlanetocentric(earth.Flattening, earth.EquatorialRadius));
            worst = System.Math.Max(worst, (position - p.SpiceXyz).Magnitude() / System.Math.Max(p.SpiceXyz.Magnitude(), p.A));
        }

        _output.WriteLine($"ToCartesianCoordinates(earth) against georec_c: {worst:E2} of max(|r|, a)");
        // Measured 2026-11-21 (Linux): 4.0e-16; through the planetocentric coordinates, one rounding more than
        // GeodeticCoordinatesTests.ToBodyFixed_AgainstSpiceAndSofa
        Assert.True(worst < 1e-15, $"{worst:E2}");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.4)]
    [InlineData(-1.2)]
    [InlineData(System.Math.PI / 2.0)]
    public void RadiusFromPlanetocentricLatitude_OfTheBody_IsTheClosedForm(double latitude)
    {
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var planetocentric = new Planetocentric(0.3, latitude, 7000e3);
        double radius = planetocentric.RadiusFromPlanetocentricLatitude(earth);
        AssertSameBits(earth.RadiusFromPlanetocentricLatitude(latitude), radius);
        // r = a b / √(b² cos² φc + a² sin² φc), b = a (1 − f)
        double a = earth.EquatorialRadius, b = a * (1.0 - earth.Flattening);
        double expected = a * b / System.Math.Sqrt(b * b * System.Math.Cos(latitude) * System.Math.Cos(latitude) +
                                                   a * a * System.Math.Sin(latitude) * System.Math.Sin(latitude));
        Assert.Equal(expected, radius, 6);
    }

    [Fact]
    public void FlatteningAndRadiusForms_AreNotPublic()
    {
        static bool HasPublic(Type type, string name, params Type[] parameters) =>
            type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static, null, parameters, null) != null;

        Assert.False(HasPublic(typeof(Planetocentric), nameof(Planetocentric.ToPlanetodetic), typeof(double), typeof(double)));
        Assert.False(HasPublic(typeof(Planetocentric), nameof(Planetocentric.RadiusFromPlanetocentricLatitude), typeof(double), typeof(double)));
        Assert.False(HasPublic(typeof(Planetocentric), nameof(Planetocentric.RadiusFromPlanetocentricLatitude), typeof(double), typeof(double), typeof(double)));
        Assert.False(HasPublic(typeof(Planetodetic), nameof(Planetodetic.ToPlanetocentric), typeof(double), typeof(double)));
        Assert.False(HasPublic(typeof(Astrodynamics.OrbitalParameters.OrbitalParameters), nameof(Astrodynamics.OrbitalParameters.OrbitalParameters.ToPlanetocentric), typeof(Aberration)));
    }

    [Fact]
    public void Equatorial_FromRightAscensionDeclination_InTheOrderOfItsName()
    {
        var withoutDistance = Equatorial.FromRightAscensionDeclination(0.853, 0.150, Epoch);
        Assert.Equal(0.853, withoutDistance.RightAscension);
        Assert.Equal(0.150, withoutDistance.Declination);
        Assert.True(double.IsNaN(withoutDistance.Distance));
        Assert.True(withoutDistance.Epoch.EqualsExact(Epoch));

        var withDistance = Equatorial.FromRightAscensionDeclination(0.853, 0.150, 2.0, Epoch);
        Assert.Equal(2.0, withDistance.Distance);
        // The direction (cos δ cos α, cos δ sin α, sin δ)
        var direction = withDistance.ToDirection();
        Assert.Equal(System.Math.Cos(0.150) * System.Math.Cos(0.853), direction.X, 15);
        Assert.Equal(System.Math.Cos(0.150) * System.Math.Sin(0.853), direction.Y, 15);
        Assert.Equal(System.Math.Sin(0.150), direction.Z, 15);
    }

    [Fact]
    public void Equatorial_HasNoPublicConstructorOfTwoAngles()
    {
        var constructors = typeof(Equatorial).GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(c => c.GetParameters().Length >= 2 && c.GetParameters().Take(2).All(p => p.ParameterType == typeof(double)))
            .Select(c => c.ToString()).ToArray();
        Assert.True(constructors.Length == 0, "public constructors: " + string.Join(", ", constructors));
    }

    [Fact]
    public void AtmosphericContext_FromPlanetodetic_TakesTheCoordinates()
    {
        var site = new Planetodetic(-75.0 * Astrodynamics.Constants.Deg2Rad, 45.0 * Astrodynamics.Constants.Deg2Rad, 400e3);
        var context = AtmosphericContext.FromPlanetodetic(site, Epoch);
        Assert.Equal(site.Altitude, context.Altitude);
        Assert.Equal(site.Latitude, context.GeodeticLatitude);
        Assert.Equal(site.Longitude, context.GeodeticLongitude);
        Assert.True(context.Epoch.Value.EqualsExact(Epoch));

        var old = typeof(AtmosphericContext).GetMethod(nameof(AtmosphericContext.FromPlanetodetic),
            [typeof(double), typeof(double), typeof(double), typeof(TimeSystem.Time)]);
        Assert.Null(old);
    }

    [Fact]
    public void ToFrame_OfAStateVector_IsAStateVector()
    {
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var state = new StateVector(new Vector3(6800e3, 0.0, 0.0), new Vector3(0.0, 7600.0, 1000.0), earth, Epoch, Frames.Frame.ICRF);
        StateVector ecliptic = state.ToFrame(Frames.Frame.ECLIPJ2000);
        var throughBase = (StateVector)((Astrodynamics.OrbitalParameters.OrbitalParameters)state).ToFrame(Frames.Frame.ECLIPJ2000);
        AssertSameBits(throughBase.Position.X, ecliptic.Position.X);
        AssertSameBits(throughBase.Position.Y, ecliptic.Position.Y);
        AssertSameBits(throughBase.Velocity.Z, ecliptic.Velocity.Z);
        Assert.Same(state, state.ToFrame(Frames.Frame.ICRF));

        var toFrame = typeof(StateVector).GetMethod(nameof(StateVector.ToFrame), [typeof(Frames.Frame)]);
        Assert.Equal(typeof(StateVector), toFrame!.ReturnType);
        Assert.True(typeof(Astrodynamics.OrbitalParameters.OrbitalParameters).GetMethod(nameof(StateVector.ToFrame), [typeof(Frames.Frame)])!.IsVirtual);
    }

    [Fact]
    public void RelativeTo_OfAStateVector_IsAStateVector()
    {
        var earth = new CelestialBody(PlanetsAndMoons.Earth);
        var moon = new CelestialBody(PlanetsAndMoons.Moon);
        var state = new StateVector(new Vector3(6800e3, 0.0, 0.0), new Vector3(0.0, 7600.0, 1000.0), earth, Epoch, Frames.Frame.ICRF);
        StateVector fromMoon = state.RelativeTo(moon, Aberration.None);
        StateVector moonFromEarth = moon.GetEphemeris(Epoch, earth);
        Assert.Equal(state.Position - moonFromEarth.Position, fromMoon.Position);
        Assert.Equal(state.Velocity - moonFromEarth.Velocity, fromMoon.Velocity);
        Assert.Same(state, state.RelativeTo(earth, Aberration.None));

        var relativeTo = typeof(StateVector).GetMethod(nameof(StateVector.RelativeTo), [typeof(ILocalizable), typeof(Aberration)]);
        Assert.Equal(typeof(StateVector), relativeTo!.ReturnType);
    }

    [Fact]
    public void ToFrame_OfATle_InItsFrame_IsTheTle()
    {
        var tle = new Astrodynamics.OrbitalParameters.TLE.Tle("ISS", "1 25544U 98067A   21020.53488036  .00016717  00000-0  10270-3 0  9054",
            "2 25544  51.6423 353.0312 0000493 320.8755  39.2360 15.49309423 25703");
        Assert.Same(tle, tle.ToFrame(Frames.Frame.TEME));
        Assert.IsType<StateVector>(tle.ToFrame(Frames.Frame.ICRF));
    }
}
