// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.IO;
using System.Linq;
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
/// The modes <c>static-bodies-memory</c>, <c>static-bodies-spice</c>, <c>static-bodies-switch</c> and
/// <c>static-bodies-before-kernels</c> of the child process: the static objects of
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
/// combined by exact subtractions, the orbits rotated into ECLIPJ2000, the frame of the static objects (#559).
/// </para>
/// <para>
/// <c>static-bodies-spice</c>: the kernels loaded before any read, the members of the static objects against SPICE
/// through <see cref="SpiceAPI"/> (the body information of <c>bodvrd</c> and the state of <c>spkezr</c>), exactly.
/// </para>
/// <para>
/// <c>static-bodies-switch</c>: the static objects first read on an empty <see cref="MemoryDataProvider"/>, then on a
/// <see cref="SpiceDataProvider"/> set afterwards with the kernels loaded, against SPICE, exactly; once read, an object
/// keeps the provider it read its data from, see <see cref="RunSwitch"/>.
/// </para>
/// <para>
/// <c>static-bodies-before-kernels</c>: the static objects read on SPICE before any kernel is loaded, then after, see
/// <see cref="RunBeforeKernels"/>.
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
            // The orbits of the static objects are in ECLIPJ2000, their frame: the stored ICRF difference rotated by SPICE's
            // obliquity (#559; before, the ICRF numbers labelled ECLIPJ2000)
            Check("EARTH_BARYCENTER's orbit is its state about the solar system barycentre, in ECLIPJ2000",
                IsInEclipJ2000(Barycenters.EARTH_BARYCENTER.InitialOrbitalParameters.ToStateVector(), s3.Position, s3.Velocity));

            var l1 = CelestialItem.Create(391).InitialOrbitalParameters.ToStateVector();
            Check("L1's state is the stored state of 391 minus that of the Earth barycentre, in ECLIPJ2000",
                IsInEclipJ2000(l1, s391.Position - s3.Position, s391.Velocity - s3.Velocity));

            var earth = PlanetsAndMoons.EARTH_BODY;
            Check("EARTH_BODY.GM is the given GM", earth.GM == EarthInfo.GM);
            Check("EARTH_BODY's radii are the given ones", earth.EquatorialRadius == EarthInfo.Radii.X && earth.PolarRadius == EarthInfo.Radii.Z);
            Check("EARTH_BODY.J2 is the given J2", earth.J2 == EarthInfo.J2);
            Check("EARTH_BODY's frame is the given one", earth.Frame.Name == "ITRF93");
            var earthState = earth.InitialOrbitalParameters.ToStateVector();
            Check("EARTH_BODY's orbit is the state of 399 minus that of its barycentre, in ECLIPJ2000",
                IsInEclipJ2000(earthState, s399.Position - s3.Position, s399.Velocity - s3.Velocity));
            Check("EARTH_BODY's sphere of influence is finite", double.IsFinite(earth.SphereOfInfluence) && earth.SphereOfInfluence > 0.0);
            Check("SUN_BODY.GM is the given GM", Stars.SUN_BODY.GM == SunInfo.GM);
            Check("SUN_BODY's orbit is its state about the solar system barycentre, in ECLIPJ2000",
                IsInEclipJ2000(Stars.SUN_BODY.InitialOrbitalParameters.ToStateVector(), s10.Position, s10.Velocity));
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

    /// <summary>
    /// The mode <c>static-bodies-switch</c>: the static objects first read on an empty <see cref="MemoryDataProvider"/>,
    /// then on a <see cref="SpiceDataProvider"/> set afterwards with the kernels loaded. A failed read is retried on the
    /// provider set at the time of the read: before, the retry went back to the provider of the type initializer, so that
    /// the objects stayed unusable for the process. Once read, an object keeps the provider it read its data from, for its
    /// data and its ephemeris.
    /// </summary>
    internal static int RunSwitch(string kernels)
    {
        try
        {
            // Before any static class of the library is read: a provider without any data
            Configuration.Instance.SetDataProvider(new MemoryDataProvider());
            Expect<ArgumentException>("SOLAR_SYSTEM_BARYCENTER.GM on the empty memory provider",
                () => Barycenters.SOLAR_SYSTEM_BARYCENTER.GM);
            Expect<ArgumentException>("EARTH_BARYCENTER.GM on the empty memory provider", () => Barycenters.EARTH_BARYCENTER.GM);
            Expect<ArgumentException>("EARTH_BODY.GM on the empty memory provider", () => PlanetsAndMoons.EARTH_BODY.GM);
            Expect<ArgumentException>("SUN_BODY.GM on the empty memory provider", () => Stars.SUN_BODY.GM);

            // The usual recovery: the SPICE provider, then the kernels
            Configuration.Instance.SetDataProvider(new SpiceDataProvider());
            SpiceAPI.Instance.LoadKernels(new DirectoryInfo(kernels));

            var ssbInfo = SpiceAPI.Instance.GetCelestialBodyInfo(0);
            var barycenterInfo = SpiceAPI.Instance.GetCelestialBodyInfo(3);
            var earthInfo = SpiceAPI.Instance.GetCelestialBodyInfo(399);
            var sunInfo = SpiceAPI.Instance.GetCelestialBodyInfo(10);
            Check($"SOLAR_SYSTEM_BARYCENTER.GM after the switch {Barycenters.SOLAR_SYSTEM_BARYCENTER.GM:R}, SPICE {ssbInfo.GM:R}",
                Barycenters.SOLAR_SYSTEM_BARYCENTER.GM == ssbInfo.GM);
            Check($"EARTH_BARYCENTER.GM after the switch {Barycenters.EARTH_BARYCENTER.GM:R}, SPICE {barycenterInfo.GM:R}",
                Barycenters.EARTH_BARYCENTER.GM == barycenterInfo.GM);
            Check($"EARTH_BODY.GM after the switch {PlanetsAndMoons.EARTH_BODY.GM:R}, SPICE {earthInfo.GM:R}",
                PlanetsAndMoons.EARTH_BODY.GM == earthInfo.GM);
            Check($"SUN_BODY.GM after the switch {Stars.SUN_BODY.GM:R}, SPICE {sunInfo.GM:R}", Stars.SUN_BODY.GM == sunInfo.GM);
            Check($"EARTH_BODY.Frame after the switch {PlanetsAndMoons.EARTH_BODY.Frame.Name}, SPICE {earthInfo.FrameName}",
                PlanetsAndMoons.EARTH_BODY.Frame.Name == earthInfo.FrameName);

            var barycenterOrbit = SpiceAPI.Instance.ReadEphemeris(J2000, 0, 3, Frame.ECLIPTIC_J2000, Aberration.None).ToStateVector();
            var barycenterState = Barycenters.EARTH_BARYCENTER.InitialOrbitalParameters.ToStateVector();
            Check("EARTH_BARYCENTER's orbit after the switch is spkezr's state of 3 about 0 in ECLIPJ2000 at J2000 TDB",
                barycenterState.Position == barycenterOrbit.Position && barycenterState.Velocity == barycenterOrbit.Velocity);
            var earthOrbit = SpiceAPI.Instance.ReadEphemeris(J2000, 3, 399, Frame.ECLIPTIC_J2000, Aberration.None).ToStateVector();
            var earthState = PlanetsAndMoons.EARTH_BODY.InitialOrbitalParameters.ToStateVector();
            Check("EARTH_BODY's orbit after the switch is spkezr's state of 399 about 3 in ECLIPJ2000 at J2000 TDB",
                earthState.Position == earthOrbit.Position && earthState.Velocity == earthOrbit.Velocity);
            Check("EARTH_BODY's sphere of influence after the switch is finite",
                double.IsFinite(PlanetsAndMoons.EARTH_BODY.SphereOfInfluence) && PlanetsAndMoons.EARTH_BODY.SphereOfInfluence > 0.0);

            var epoch = new Time(2021, 1, 20, 18, 0, 0, frame: TimeFrame.UTCFrame);
            var spkezr = SpiceAPI.Instance.ReadEphemeris(epoch, 0, 3, Frame.ICRF, Aberration.None).ToStateVector();
            var ephemeris = Barycenters.EARTH_BARYCENTER.GetEphemeris(epoch, Barycenters.SOLAR_SYSTEM_BARYCENTER, Frame.ICRF,
                Aberration.None).ToStateVector();
            Check("EARTH_BARYCENTER's ephemeris after the switch is spkezr's state of 3 about 0 in ICRF",
                ephemeris.Position == spkezr.Position && ephemeris.Velocity == spkezr.Velocity);

            // Once read, an object keeps the provider it read its data from: another provider set later changes nothing
            Configuration.Instance.SetDataProvider(new MemoryDataProvider());
            Check("EARTH_BODY.GM, once read, stays SPICE's under a later provider", PlanetsAndMoons.EARTH_BODY.GM == earthInfo.GM);
            var kept = Barycenters.EARTH_BARYCENTER.GetEphemeris(epoch, Barycenters.SOLAR_SYSTEM_BARYCENTER, Frame.ICRF,
                Aberration.None).ToStateVector();
            Check("EARTH_BARYCENTER's ephemeris, once its data are read, stays SPICE's under a later provider",
                kept.Position == spkezr.Position && kept.Velocity == spkezr.Velocity);
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"static-bodies-switch threw: {e}");
            return 1;
        }
    }

    /// <summary>
    /// The mode <c>static-bodies-before-kernels</c>: the static objects read on the SPICE provider before any kernel is
    /// loaded, then after <see cref="SpiceAPI.LoadKernels"/>. Without kernels SPICE names the bodies (its built-in names)
    /// but gives no <c>BODYnnn_GM</c> nor <c>BODYnnn_RADII</c>: a read of a member of an object whose data need them throws
    /// <see cref="InvalidOperationException"/> naming the missing variable, at each read, and caches nothing, so that once
    /// the kernels are loaded the members are SPICE's (<c>bodvrd</c>, <c>spkezr</c> through <see cref="SpiceAPI"/>),
    /// exactly; with a GM kernel alone, the read of a body throws naming its missing <c>BODYnnn_RADII</c>. Before, the
    /// first read cached the zeros SPICE answered (GM 0, radii 0, no orbit, the built-in frame <c>IAU_EARTH</c>) for the
    /// life of the process; the solar system barycentre, whose GM is the sum of <c>BODY1_GM</c> to <c>BODY10_GM</c>, kept
    /// a GM of 0. After a partial load (the leap seconds, the GMs and the PCK) the members are what SPICE gives then (J2
    /// NaN, <c>IAU_EARTH</c>, <c>IAU_MOON</c>, no orbit), and they follow each later change of the kernel pool: SPICE's
    /// once the kernels are loaded (before, the data of the partial load were kept for the process), the same instances
    /// after a reload that changes none of them, an exception naming <c>BODY399_GM</c> after <c>ClearKernels</c>.
    /// </summary>
    internal static int RunBeforeKernels(string kernels)
    {
        try
        {
            // Before any kernel is loaded, on the default SPICE provider
            for (int i = 1; i <= 2; i++)
            {
                var gm = Expect<InvalidOperationException>($"EARTH_BODY.GM before the kernels, read {i}", () => PlanetsAndMoons.EARTH_BODY.GM);
                Check($"read {i}: the message names BODY399_GM ({gm.Message})", gm.Message.Contains("BODY399_GM"));
            }

            Expect<InvalidOperationException>("EARTH_BODY.Frame before the kernels", () => PlanetsAndMoons.EARTH_BODY.Frame);
            Expect<InvalidOperationException>("EARTH_BODY.EquatorialRadius before the kernels",
                () => PlanetsAndMoons.EARTH_BODY.EquatorialRadius);
            Expect<InvalidOperationException>("EARTH_BODY.InitialOrbitalParameters before the kernels",
                () => PlanetsAndMoons.EARTH_BODY.InitialOrbitalParameters);
            var barycenter = Expect<InvalidOperationException>("EARTH_BARYCENTER.GM before the kernels", () => Barycenters.EARTH_BARYCENTER.GM);
            Check($"the message names BODY3_GM ({barycenter.Message})", barycenter.Message.Contains("BODY3_GM"));
            Expect<InvalidOperationException>("SUN_BODY.EquatorialRadius before the kernels", () => Stars.SUN_BODY.EquatorialRadius);
            Expect<InvalidOperationException>("MOON_BODY.GM before the kernels", () => PlanetsAndMoons.MOON_BODY.GM);
            Check("the identifiers need no data", PlanetsAndMoons.EARTH_BODY.NaifId == 399 && PlanetsAndMoons.EARTH_BODY.Name == "EARTH"
                                                    && Barycenters.EARTH_BARYCENTER.IsBarycenter && Stars.SUN_BODY.IsSun);
            var ssb = Expect<InvalidOperationException>("SOLAR_SYSTEM_BARYCENTER.GM before the kernels",
                () => Barycenters.SOLAR_SYSTEM_BARYCENTER.GM);
            Check($"the message names BODY1_GM to BODY10_GM ({ssb.Message})", ssb.Message.Contains("BODY1_GM to BODY10_GM"));
            var sun = Expect<InvalidOperationException>("SUN_BODY.Frame before the kernels", () => Stars.SUN_BODY.Frame);
            Check($"the message names the GM first, BODY10_GM ({sun.Message})", sun.Message.Contains("BODY10_GM"));

            // A GM kernel alone: the bodies still lack their radii, which the message names, and nothing is kept
            var gmKernel = new FileInfo(Path.Combine(kernels, "gm_de440.tpc"));
            SpiceAPI.Instance.LoadKernels(gmKernel);
            for (int i = 1; i <= 2; i++)
            {
                var radii = Expect<InvalidOperationException>($"EARTH_BODY.GM with the GM kernel alone, read {i}",
                    () => PlanetsAndMoons.EARTH_BODY.GM);
                Check($"read {i}: the message names BODY399_RADII ({radii.Message})", radii.Message.Contains("BODY399_RADII"));
            }

            Expect<InvalidOperationException>("MOON_BODY.Frame with the GM kernel alone", () => PlanetsAndMoons.MOON_BODY.Frame);
            SpiceAPI.Instance.UnloadKernels(gmKernel);

            // A partial load: the leap seconds, the GMs and the PCK, without geophysical.ker (BODY399_J2), the frame
            // associations (OBJECT_EARTH_FRAME, OBJECT_MOON_FRAME) nor an SPK. The data are complete for the check (GM and
            // radii): the members are what SPICE gives at that time, J2 NaN, the built-in frames, no orbit
            var partial = new[] { "latest_leapseconds.tls", "gm_de440.tpc", "pck00011.tpc" }
                .Select(x => new FileInfo(Path.Combine(kernels, x))).ToArray();
            foreach (var kernel in partial)
            {
                SpiceAPI.Instance.LoadKernels(kernel);
            }

            var partialEarth = SpiceAPI.Instance.GetCelestialBodyInfo(399);
            var partialMoon = SpiceAPI.Instance.GetCelestialBodyInfo(301);
            var early = PlanetsAndMoons.EARTH_BODY;
            Check($"EARTH_BODY.J2 after a partial load {early.J2:R}, SPICE {partialEarth.J2:R}", early.J2.Equals(partialEarth.J2));
            Check($"EARTH_BODY.Frame after a partial load {early.Frame.Name}, SPICE {partialEarth.FrameName}",
                early.Frame.Name == partialEarth.FrameName && early.Frame.Name == "IAU_EARTH");
            Check($"MOON_BODY.Frame after a partial load {PlanetsAndMoons.MOON_BODY.Frame.Name}, SPICE {partialMoon.FrameName}",
                PlanetsAndMoons.MOON_BODY.Frame.Name == partialMoon.FrameName && partialMoon.FrameName == "IAU_MOON");
            Check("EARTH_BODY has no orbit after a partial load", early.InitialOrbitalParameters == null
                                                                 && double.IsPositiveInfinity(early.SphereOfInfluence));

            // A load that changes none of its data keeps the objects an item gave (its frame), as the orientation cache of
            // a propagation is keyed by its frame instance
            var partialFrame = early.Frame;
            SpiceAPI.Instance.LoadKernels(partial[2]);
            Check("EARTH_BODY.Frame after the reload of the PCK is the same instance", ReferenceEquals(early.Frame, partialFrame));

            // The kernels: the directory unloads the files loaded inside it first. Before, the data of the partial load were
            // kept for the life of the process (J2 NaN, IAU_EARTH, no orbit)
            SpiceAPI.Instance.LoadKernels(new DirectoryInfo(kernels));

            var ssbInfo = SpiceAPI.Instance.GetCelestialBodyInfo(0);
            var barycenterInfo = SpiceAPI.Instance.GetCelestialBodyInfo(3);
            var earthInfo = SpiceAPI.Instance.GetCelestialBodyInfo(399);
            var moonInfo = SpiceAPI.Instance.GetCelestialBodyInfo(301);
            var sunInfo = SpiceAPI.Instance.GetCelestialBodyInfo(10);
            var earth = PlanetsAndMoons.EARTH_BODY;
            Check($"EARTH_BODY.GM after the kernels {earth.GM:R}, SPICE {earthInfo.GM:R}", earth.GM == earthInfo.GM && earth.GM > 0.0);
            Check($"EARTH_BODY's radii after the kernels {earth.EquatorialRadius:R} {earth.PolarRadius:R}, SPICE {earthInfo.Radii.X:R} {earthInfo.Radii.Z:R}",
                earth.EquatorialRadius == earthInfo.Radii.X && earth.PolarRadius == earthInfo.Radii.Z && earth.EquatorialRadius > 0.0);
            Check($"EARTH_BODY.J2 after the kernels {earth.J2:R}, SPICE {earthInfo.J2:R}", earth.J2.Equals(earthInfo.J2) && double.IsFinite(earth.J2));
            Check($"EARTH_BODY.Frame after the kernels {earth.Frame.Name}, SPICE {earthInfo.FrameName}",
                earth.Frame.Name == earthInfo.FrameName && earth.Frame.Name == "ITRF93");
            var earthOrbit = SpiceAPI.Instance.ReadEphemeris(J2000, 3, 399, Frame.ECLIPTIC_J2000, Aberration.None).ToStateVector();
            var earthState = earth.InitialOrbitalParameters.ToStateVector();
            Check("EARTH_BODY's orbit after the kernels is spkezr's state of 399 about 3 in ECLIPJ2000 at J2000 TDB",
                earthState.Position == earthOrbit.Position && earthState.Velocity == earthOrbit.Velocity);
            Check("EARTH_BODY's sphere of influence after the kernels is finite",
                double.IsFinite(earth.SphereOfInfluence) && earth.SphereOfInfluence > 0.0);
            Check($"EARTH_BARYCENTER.GM after the kernels {Barycenters.EARTH_BARYCENTER.GM:R}, SPICE {barycenterInfo.GM:R}",
                Barycenters.EARTH_BARYCENTER.GM == barycenterInfo.GM);
            Check($"SUN_BODY.EquatorialRadius after the kernels {Stars.SUN_BODY.EquatorialRadius:R}, SPICE {sunInfo.Radii.X:R}",
                Stars.SUN_BODY.EquatorialRadius == sunInfo.Radii.X && Stars.SUN_BODY.GM == sunInfo.GM);
            Check($"MOON_BODY.GM after the kernels {PlanetsAndMoons.MOON_BODY.GM:R}, SPICE {moonInfo.GM:R}",
                PlanetsAndMoons.MOON_BODY.GM == moonInfo.GM);
            Check($"SOLAR_SYSTEM_BARYCENTER.GM after the kernels {Barycenters.SOLAR_SYSTEM_BARYCENTER.GM:R}, SPICE {ssbInfo.GM:R}",
                Barycenters.SOLAR_SYSTEM_BARYCENTER.GM == ssbInfo.GM && ssbInfo.GM > 0.0);
            Check("SOLAR_SYSTEM_BARYCENTER has no orbit", Barycenters.SOLAR_SYSTEM_BARYCENTER.InitialOrbitalParameters == null);
            Check($"MOON_BODY.Frame after the kernels {PlanetsAndMoons.MOON_BODY.Frame.Name}, SPICE {moonInfo.FrameName}",
                PlanetsAndMoons.MOON_BODY.Frame.Name == moonInfo.FrameName && moonInfo.FrameName == "MOON_ME");

            // The reload of a kernel of the directory changes none of the data: the same instances
            var frame = earth.Frame;
            var orbit = earth.InitialOrbitalParameters;
            SpiceAPI.Instance.LoadKernels(new FileInfo(Path.Combine(kernels, "pck00011.tpc")));
            Check("EARTH_BODY's frame and orbit after the reload of a kernel are the same instances",
                ReferenceEquals(earth.Frame, frame) && ReferenceEquals(earth.InitialOrbitalParameters, orbit));

            // ClearKernels: the data follow the kernel pool, the next read throws naming the missing variable, at each read
            SpiceAPI.Instance.ClearKernels();
            for (int i = 1; i <= 2; i++)
            {
                var cleared = Expect<InvalidOperationException>($"EARTH_BODY.GM after ClearKernels, read {i}", () => earth.GM);
                Check($"read {i}: the message names BODY399_GM ({cleared.Message})", cleared.Message.Contains("BODY399_GM"));
            }

            SpiceAPI.Instance.LoadKernels(new DirectoryInfo(kernels));
            Check($"EARTH_BODY.GM after the kernels are loaded again {earth.GM:R}, SPICE {earthInfo.GM:R}", earth.GM == earthInfo.GM);
            Check("EARTH_BODY.J2 and Frame after the kernels are loaded again", earth.J2.Equals(earthInfo.J2) && earth.Frame.Name == "ITRF93");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"static-bodies-before-kernels threw: {e}");
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

    // A state of the memory provider in ECLIPJ2000 against the ICRF vectors rotated by the closed form of SPICE's ECLIPJ2000,
    // ROTATE(84381.448", 1) (CSPICE chgirf.c): y' = c y + s z, z' = -s y + c z, within 4.4e-16 relative, the bound of
    // MemoryDataProviderFrameTests (#559)
    private static bool IsInEclipJ2000(StateVector state, Vector3 icrfPosition, Vector3 icrfVelocity)
    {
        double eps = 84381.448 / 3600.0 * System.Math.PI / 180.0;
        double c = System.Math.Cos(eps), s = System.Math.Sin(eps);
        Vector3 Rotate(Vector3 v) => new(v.X, c * v.Y + s * v.Z, -s * v.Y + c * v.Z);
        bool Close(Vector3 actual, Vector3 icrf) => (actual - Rotate(icrf)).Magnitude() <= 4.4e-16 * icrf.Magnitude();
        return state.Frame == Frame.ECLIPTIC_J2000 && Close(state.Position, icrfPosition) && Close(state.Velocity, icrfVelocity);
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
