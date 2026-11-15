// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.DataProvider;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;
using BodyInfo = IO.Astrodynamics.DTO.CelestialBody;

namespace IO.Astrodynamics.Tests.ColdStart;

/// <summary>
/// The modes <c>static-bodies-memory</c> and <c>static-bodies-spice</c> of the child process: the static objects of
/// <see cref="Barycenters"/>, <see cref="PlanetsAndMoons"/> (<see cref="PlanetsAndMoons.EARTH_BODY"/>,
/// <see cref="PlanetsAndMoons.MOON_BODY"/>) and <see cref="Stars"/> (<see cref="Stars.SUN_BODY"/>) read no provider data
/// when their class is initialized, and resolve their data at first read, retried on failure.
/// </summary>
/// <remarks>
/// <para>
/// <c>static-bodies-memory</c>: a <see cref="MemoryDataProvider"/> holding the Earth's information only is set before any
/// static class is read. Before, the first read of <see cref="Barycenters"/> threw a
/// <see cref="TypeInitializationException"/> (its initializer read the information of the barycentres 0 to 9), which the
/// runtime keeps for the life of the process: every later use failed, even once the data existed. Now
/// <see cref="CelestialItem.Create"/> of L1 throws the provider's <see cref="InvalidOperationException"/> for its missing
/// state, a provider-backed member of a static object the provider's <see cref="ArgumentException"/> naming
/// <c>naifId</c>, at each read, and once the information and the states are added the members are the data given,
/// combined by exact subtractions.
/// </para>
/// <para>
/// <c>static-bodies-spice</c>: the kernels loaded before any read, the members of the static objects against SPICE
/// through <see cref="SpiceAPI"/> (the body information of <c>bodvrd</c> and the state of <c>spkezr</c>), exactly.
/// </para>
/// <para>Exit 0 when every check holds, 1 with the failed check or the exception on the standard error.</para>
/// </remarks>
internal static class StaticBodiesChild
{
    private static readonly Time J2000 = Time.J2000TDB;

    private static readonly BodyInfo EarthInfo = new(399, 10, 3, "EARTH", new(6378137.0, 6378137.0, 6356752.0), 3.986004418e14,
        "ITRF93", 13000, 1.08e-3, 0.0, 0.0);

    private static readonly BodyInfo EarthBarycenterInfo = new(3, 0, 0, "EARTH BARYCENTER", new(0.0, 0.0, 0.0), 4.0350323550225975e14,
        "", 0, 0.0, 0.0, 0.0);

    private static readonly BodyInfo SsbInfo = new(0, 0, 0, "SOLAR SYSTEM BARYCENTER", new(0.0, 0.0, 0.0), 1.3289e20, "", 0, 0.0, 0.0,
        0.0);

    private static readonly BodyInfo SunInfo = new(10, 0, 0, "SUN", new(696000000.0, 696000000.0, 696000000.0), 1.32712440041e20,
        "IAU_SUN", 10010, 0.0, 0.0, 0.0);

    internal static int RunMemory()
    {
        try
        {
            // Before any static class of the library is read
            var provider = new MemoryDataProvider();
            provider.AddCelestialBodyInfo(EarthInfo);
            Configuration.Instance.SetDataProvider(provider);

            // L1 reads its state about the Earth barycentre: the provider has none
            var create = Expect<InvalidOperationException>("CelestialItem.Create(391) without its state", () => CelestialItem.Create(391));
            Check($"the missing state names the target 391 ({create.Message})", create.Message.Contains("391"));

            // A provider-backed member throws the provider's exception at each read, and nothing is cached
            for (int i = 1; i <= 2; i++)
            {
                var gm = Expect<ArgumentException>($"SOLAR_SYSTEM_BARYCENTER.GM without its information, read {i}",
                    () => Barycenters.SOLAR_SYSTEM_BARYCENTER.GM);
                Check($"read {i}: the parameter is naifId ({gm.ParamName})", gm.ParamName == "naifId");
            }

            Expect<ArgumentException>("EARTH_BARYCENTER.GM without its information", () => Barycenters.EARTH_BARYCENTER.GM);
            Check("the identifiers need no data", Barycenters.EARTH_BARYCENTER.NaifId == 3 && Barycenters.EARTH_BARYCENTER.IsBarycenter
                                                    && PlanetsAndMoons.EARTH_BODY.NaifId == 399 && Stars.SUN_BODY.IsSun);

            // The data: states about the solar system barycentre at J2000 TDB, the epoch of the static objects and of L1
            provider.AddCelestialBodyInfo(SsbInfo, EarthBarycenterInfo, SunInfo);
            var ssb = Barycenters.SOLAR_SYSTEM_BARYCENTER;
            var s3 = new StateVector(new Vector3(-2.7e10, 1.33e11, 5.77e10), new Vector3(-29780.3, -5017.7, -2175.4), ssb, J2000, Frame.ICRF);
            var s391 = new StateVector(new Vector3(-2.68e10, 1.318e11, 5.72e10), new Vector3(-29483.1, -4967.2, -2153.8), ssb, J2000,
                Frame.ICRF);
            var s399 = new StateVector(new Vector3(-2.700001e10, 1.3300003e11, 5.7700011e10), new Vector3(-29790.1, -5011.3, -2172.9), ssb,
                J2000, Frame.ICRF);
            var s10 = new StateVector(new Vector3(-1.07e9, -4.0e8, -1.4e8), new Vector3(9.3, -12.8, -5.6), ssb, J2000, Frame.ICRF);
            provider.AddStateVector(3, J2000, s3);
            provider.AddStateVector(391, J2000, s391);
            provider.AddStateVector(399, J2000, s399);
            provider.AddStateVector(10, J2000, s10);

            Check("SOLAR_SYSTEM_BARYCENTER.GM, once its information exists, is the given GM", ssb.GM == SsbInfo.GM);
            Check("EARTH_BARYCENTER.GM is the given GM", Barycenters.EARTH_BARYCENTER.GM == EarthBarycenterInfo.GM);
            Check("EARTH_BARYCENTER's orbit is its state about the solar system barycentre",
                Barycenters.EARTH_BARYCENTER.InitialOrbitalParameters.ToStateVector().Position == s3.Position);

            var l1 = CelestialItem.Create(391).InitialOrbitalParameters.ToStateVector();
            Check("L1's state is the stored state of 391 minus that of the Earth barycentre",
                l1.Position == s391.Position - s3.Position && l1.Velocity == s391.Velocity - s3.Velocity);

            var earth = PlanetsAndMoons.EARTH_BODY;
            Check("EARTH_BODY.GM is the given GM", earth.GM == EarthInfo.GM);
            Check("EARTH_BODY's radii are the given ones", earth.EquatorialRadius == EarthInfo.Radii.X && earth.PolarRadius == EarthInfo.Radii.Z);
            Check("EARTH_BODY.J2 is the given J2", earth.J2 == EarthInfo.J2);
            Check("EARTH_BODY's frame is the given one", earth.Frame.Name == "ITRF93");
            var earthState = earth.InitialOrbitalParameters.ToStateVector();
            Check("EARTH_BODY's orbit is the state of 399 minus that of its barycentre",
                earthState.Position == s399.Position - s3.Position && earthState.Velocity == s399.Velocity - s3.Velocity);
            Check("EARTH_BODY's sphere of influence is finite", double.IsFinite(earth.SphereOfInfluence) && earth.SphereOfInfluence > 0.0);
            Check("SUN_BODY.GM is the given GM", Stars.SUN_BODY.GM == SunInfo.GM);
            Check("SUN_BODY's orbit is its state about the solar system barycentre",
                Stars.SUN_BODY.InitialOrbitalParameters.ToStateVector().Position == s10.Position);
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"static-bodies-memory threw: {e}");
            return 1;
        }
    }

    internal static int RunSpice(string kernels)
    {
        try
        {
            SpiceAPI.Instance.LoadKernels(new DirectoryInfo(kernels));

            var barycenterInfo = SpiceAPI.Instance.GetCelestialBodyInfo(3);
            var earthInfo = SpiceAPI.Instance.GetCelestialBodyInfo(399);
            var sunInfo = SpiceAPI.Instance.GetCelestialBodyInfo(10);
            Check($"EARTH_BARYCENTER.GM {Barycenters.EARTH_BARYCENTER.GM:R}, SPICE {barycenterInfo.GM:R}",
                Barycenters.EARTH_BARYCENTER.GM == barycenterInfo.GM);
            Check($"EARTH_BARYCENTER.Name {Barycenters.EARTH_BARYCENTER.Name}, SPICE {barycenterInfo.Name}",
                Barycenters.EARTH_BARYCENTER.Name == barycenterInfo.Name);
            for (int id = 0; id <= 9; id++)
            {
                var info = SpiceAPI.Instance.GetCelestialBodyInfo(id);
                var barycenter = BarycenterOf(id);
                Check($"barycentre {id}: name {barycenter.Name}, SPICE {info.Name}; GM {barycenter.GM:R}, SPICE {info.GM:R}",
                    barycenter.Name == info.Name && barycenter.GM == info.GM);
            }

            var earth = PlanetsAndMoons.EARTH_BODY;
            Check($"EARTH_BODY.GM {earth.GM:R}, SPICE {earthInfo.GM:R}", earth.GM == earthInfo.GM);
            Check($"EARTH_BODY.EquatorialRadius {earth.EquatorialRadius:R}, SPICE {earthInfo.Radii.X:R}",
                earth.EquatorialRadius == earthInfo.Radii.X);
            Check($"EARTH_BODY.J2 {earth.J2:R}, SPICE {earthInfo.J2:R}", earth.J2.Equals(earthInfo.J2));
            Check($"EARTH_BODY.Name {earth.Name}, SPICE {earthInfo.Name}", earth.Name == earthInfo.Name);
            Check($"EARTH_BODY.Frame {earth.Frame.Name}, SPICE {earthInfo.FrameName}", earth.Frame.Name == earthInfo.FrameName);
            Check($"SUN_BODY.GM {Stars.SUN_BODY.GM:R}, SPICE {sunInfo.GM:R}", Stars.SUN_BODY.GM == sunInfo.GM);
            Check($"MOON_BODY.GM {PlanetsAndMoons.MOON_BODY.GM:R}, SPICE {SpiceAPI.Instance.GetCelestialBodyInfo(301).GM:R}",
                PlanetsAndMoons.MOON_BODY.GM == SpiceAPI.Instance.GetCelestialBodyInfo(301).GM);

            var spkezr = SpiceAPI.Instance.ReadEphemeris(J2000, 3, 399, Frame.ECLIPTIC_J2000, Aberration.None).ToStateVector();
            var orbit = earth.InitialOrbitalParameters.ToStateVector();
            Check("EARTH_BODY's orbit is spkezr's state of 399 about 3 in ECLIPJ2000 at J2000 TDB",
                orbit.Position == spkezr.Position && orbit.Velocity == spkezr.Velocity && orbit.Frame == Frame.ECLIPTIC_J2000);
            Check("SOLAR_SYSTEM_BARYCENTER has no orbit", Barycenters.SOLAR_SYSTEM_BARYCENTER.InitialOrbitalParameters == null);
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"static-bodies-spice threw: {e}");
            return 1;
        }
    }

    private static Barycenter BarycenterOf(int id) => id switch
    {
        0 => Barycenters.SOLAR_SYSTEM_BARYCENTER,
        1 => Barycenters.MERCURY_BARYCENTER,
        2 => Barycenters.VENUS_BARYCENTER,
        3 => Barycenters.EARTH_BARYCENTER,
        4 => Barycenters.MARS_BARYCENTER,
        5 => Barycenters.JUPITER_BARYCENTER,
        6 => Barycenters.SATURN_BARYCENTER,
        7 => Barycenters.URANUS_BARYCENTER,
        8 => Barycenters.NEPTUNE_BARYCENTER,
        _ => Barycenters.PLUTO_BARYCENTER
    };

    private static TException Expect<TException>(string label, Func<object> read) where TException : Exception
    {
        try
        {
            read();
        }
        catch (Exception e) when (e.GetType() == typeof(TException))
        {
            Console.WriteLine($"OK {label}: {e.GetType().Name}");
            return (TException)e;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Failed: {label}: {e.GetType().Name} instead of {typeof(TException).Name}", e);
        }

        throw new InvalidOperationException($"Failed: {label}: no exception");
    }

    private static void Check(string label, bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Failed: {label}");
        }

        Console.WriteLine($"OK {label}");
    }
}
