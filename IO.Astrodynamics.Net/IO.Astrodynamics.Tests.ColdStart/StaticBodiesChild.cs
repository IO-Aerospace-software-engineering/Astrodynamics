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
using BodyInfo = IO.Astrodynamics.DataProvider.CelestialBodyInfo;

namespace IO.Astrodynamics.Tests.ColdStart;

/// <summary>
/// The modes <c>static-bodies-memory</c>, <c>static-bodies-spice</c>, <c>static-bodies-switch</c> and
/// <c>static-bodies-before-kernels</c> of the child process: the static objects of
/// <see cref="Barycenters"/>, <see cref="PlanetsAndMoons"/> (<see cref="PlanetsAndMoons.EarthBody"/>,
/// <see cref="PlanetsAndMoons.MoonBody"/>) and <see cref="Stars"/> (<see cref="Stars.SunBody"/>) read no provider data
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
/// through <see cref="SpiceApi"/> (the body information of <c>bodvrd</c> and the state of <c>spkezr</c>), exactly.
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
    private static readonly Time J2000 = Time.J2000Tdb;

    private static readonly BodyInfo EarthInfo = new(399, "EARTH", 10, 3, new Vector3(6378137.0, 6378137.0, 6356752.0), 3.986004418e14,
        "ITRF93", 13000, 1.08e-3, 0.0, 0.0);

    private static readonly BodyInfo EarthBarycenterInfo = new(3, "EARTH BARYCENTER", 0, 0, new Vector3(0.0, 0.0, 0.0), 4.0350323550225975e14,
        "", 0, 0.0, 0.0, 0.0);

    private static readonly BodyInfo SsbInfo = new(0, "SOLAR SYSTEM BARYCENTER", 0, 0, new Vector3(0.0, 0.0, 0.0), 1.3289e20, "", 0, 0.0, 0.0,
        0.0);

    private static readonly BodyInfo SunInfo = new(10, "SUN", 0, 0, new Vector3(696000000.0, 696000000.0, 696000000.0), 1.32712440041e20,
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
                var gm = Expect<ArgumentException>($"SolarSystemBarycenter.GM without its information, read {i}",
                    () => Barycenters.SolarSystemBarycenter.GM);
                Check($"read {i}: the parameter is naifId ({gm.ParamName})", gm.ParamName == "naifId");
            }

            Expect<ArgumentException>("EARTH_BARYCENTER.GM without its information", () => Barycenters.EarthBarycenter.GM);
            Check("the identifiers need no data", Barycenters.EarthBarycenter.NaifId == 3 && Barycenters.EarthBarycenter.IsBarycenter
                                                    && PlanetsAndMoons.EarthBody.NaifId == 399 && Stars.SunBody.IsSun);

            // The data: states about the solar system barycentre at J2000 TDB, the epoch of the static objects and of L1
            provider.AddCelestialBodyInfo(SsbInfo, EarthBarycenterInfo, SunInfo);
            var ssb = Barycenters.SolarSystemBarycenter;
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

            Check("SolarSystemBarycenter.GM, once its information exists, is the given GM", ssb.GM == SsbInfo.GM);
            Check("EARTH_BARYCENTER.GM is the given GM", Barycenters.EarthBarycenter.GM == EarthBarycenterInfo.GM);
            // The orbits of the static objects are in ECLIPJ2000, their frame: the stored ICRF difference rotated by SPICE's
            // obliquity (#559; before, the ICRF numbers labelled ECLIPJ2000)
            Check("EARTH_BARYCENTER's orbit is its state about the solar system barycentre, in ECLIPJ2000",
                IsInEclipJ2000(Barycenters.EarthBarycenter.InitialOrbitalParameters.ToStateVector(), s3.Position, s3.Velocity));

            var l1 = CelestialItem.Create(391).InitialOrbitalParameters.ToStateVector();
            Check("L1's state is the stored state of 391 minus that of the Earth barycentre, in ECLIPJ2000",
                IsInEclipJ2000(l1, s391.Position - s3.Position, s391.Velocity - s3.Velocity));

            var earth = PlanetsAndMoons.EarthBody;
            Check("EarthBody.GM is the given GM", earth.GM == EarthInfo.GM);
            Check("EarthBody's radii are the given ones", earth.EquatorialRadius == EarthInfo.Radii.X && earth.PolarRadius == EarthInfo.Radii.Z);
            Check("EarthBody.J2 is the given J2", earth.J2 == EarthInfo.J2);
            Check("EarthBody's frame is the given one", earth.Frame.Name == "ITRF93");
            var earthState = earth.InitialOrbitalParameters.ToStateVector();
            Check("EarthBody's orbit is the state of 399 minus that of its barycentre, in ECLIPJ2000",
                IsInEclipJ2000(earthState, s399.Position - s3.Position, s399.Velocity - s3.Velocity));
            Check("EarthBody's sphere of influence is finite", double.IsFinite(earth.SphereOfInfluence) && earth.SphereOfInfluence > 0.0);
            Check("SunBody.GM is the given GM", Stars.SunBody.GM == SunInfo.GM);
            Check("SunBody's orbit is its state about the solar system barycentre, in ECLIPJ2000",
                IsInEclipJ2000(Stars.SunBody.InitialOrbitalParameters.ToStateVector(), s10.Position, s10.Velocity));
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
            SpiceApi.Instance.LoadKernels(new DirectoryInfo(kernels));

            var barycenterInfo = SpiceApi.Instance.GetCelestialBodyInfo(3);
            var earthInfo = SpiceApi.Instance.GetCelestialBodyInfo(399);
            var sunInfo = SpiceApi.Instance.GetCelestialBodyInfo(10);
            Check($"EARTH_BARYCENTER.GM {Barycenters.EarthBarycenter.GM:R}, SPICE {barycenterInfo.GM:R}",
                Barycenters.EarthBarycenter.GM == barycenterInfo.GM);
            Check($"EARTH_BARYCENTER.Name {Barycenters.EarthBarycenter.Name}, SPICE {barycenterInfo.Name}",
                Barycenters.EarthBarycenter.Name == barycenterInfo.Name);
            for (int id = 0; id <= 9; id++)
            {
                var info = SpiceApi.Instance.GetCelestialBodyInfo(id);
                var barycenter = BarycenterOf(id);
                Check($"barycentre {id}: name {barycenter.Name}, SPICE {info.Name}; GM {barycenter.GM:R}, SPICE {info.GM:R}",
                    barycenter.Name == info.Name && barycenter.GM == info.GM);
            }

            var earth = PlanetsAndMoons.EarthBody;
            Check($"EarthBody.GM {earth.GM:R}, SPICE {earthInfo.GM:R}", earth.GM == earthInfo.GM);
            Check($"EarthBody.EquatorialRadius {earth.EquatorialRadius:R}, SPICE {earthInfo.Radii.X:R}",
                earth.EquatorialRadius == earthInfo.Radii.X);
            Check($"EarthBody.J2 {earth.J2:R}, SPICE {earthInfo.J2:R}", earth.J2.Equals(earthInfo.J2));
            Check($"EarthBody.Name {earth.Name}, SPICE {earthInfo.Name}", earth.Name == earthInfo.Name);
            Check($"EarthBody.Frame {earth.Frame.Name}, SPICE {earthInfo.FrameName}", earth.Frame.Name == earthInfo.FrameName);
            Check($"SunBody.GM {Stars.SunBody.GM:R}, SPICE {sunInfo.GM:R}", Stars.SunBody.GM == sunInfo.GM);
            Check($"MoonBody.GM {PlanetsAndMoons.MoonBody.GM:R}, SPICE {SpiceApi.Instance.GetCelestialBodyInfo(301).GM:R}",
                PlanetsAndMoons.MoonBody.GM == SpiceApi.Instance.GetCelestialBodyInfo(301).GM);

            var spkezr = SpiceApi.Instance.ReadEphemeris(J2000, 3, 399, Frame.ECLIPJ2000, Aberration.None);
            var orbit = earth.InitialOrbitalParameters.ToStateVector();
            Check("EarthBody's orbit is spkezr's state of 399 about 3 in ECLIPJ2000 at J2000 TDB",
                orbit.Position == spkezr.Position && orbit.Velocity == spkezr.Velocity && orbit.Frame == Frame.ECLIPJ2000);
            Check("SolarSystemBarycenter has no orbit", Barycenters.SolarSystemBarycenter.InitialOrbitalParameters == null);
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
            Expect<ArgumentException>("SolarSystemBarycenter.GM on the empty memory provider",
                () => Barycenters.SolarSystemBarycenter.GM);
            Expect<ArgumentException>("EARTH_BARYCENTER.GM on the empty memory provider", () => Barycenters.EarthBarycenter.GM);
            Expect<ArgumentException>("EarthBody.GM on the empty memory provider", () => PlanetsAndMoons.EarthBody.GM);
            Expect<ArgumentException>("SunBody.GM on the empty memory provider", () => Stars.SunBody.GM);

            // The usual recovery: the SPICE provider, then the kernels
            Configuration.Instance.SetDataProvider(new SpiceDataProvider());
            SpiceApi.Instance.LoadKernels(new DirectoryInfo(kernels));

            var ssbInfo = SpiceApi.Instance.GetCelestialBodyInfo(0);
            var barycenterInfo = SpiceApi.Instance.GetCelestialBodyInfo(3);
            var earthInfo = SpiceApi.Instance.GetCelestialBodyInfo(399);
            var sunInfo = SpiceApi.Instance.GetCelestialBodyInfo(10);
            Check($"SolarSystemBarycenter.GM after the switch {Barycenters.SolarSystemBarycenter.GM:R}, SPICE {ssbInfo.GM:R}",
                Barycenters.SolarSystemBarycenter.GM == ssbInfo.GM);
            Check($"EARTH_BARYCENTER.GM after the switch {Barycenters.EarthBarycenter.GM:R}, SPICE {barycenterInfo.GM:R}",
                Barycenters.EarthBarycenter.GM == barycenterInfo.GM);
            Check($"EarthBody.GM after the switch {PlanetsAndMoons.EarthBody.GM:R}, SPICE {earthInfo.GM:R}",
                PlanetsAndMoons.EarthBody.GM == earthInfo.GM);
            Check($"SunBody.GM after the switch {Stars.SunBody.GM:R}, SPICE {sunInfo.GM:R}", Stars.SunBody.GM == sunInfo.GM);
            Check($"EarthBody.Frame after the switch {PlanetsAndMoons.EarthBody.Frame.Name}, SPICE {earthInfo.FrameName}",
                PlanetsAndMoons.EarthBody.Frame.Name == earthInfo.FrameName);

            var barycenterOrbit = SpiceApi.Instance.ReadEphemeris(J2000, 0, 3, Frame.ECLIPJ2000, Aberration.None);
            var barycenterState = Barycenters.EarthBarycenter.InitialOrbitalParameters.ToStateVector();
            Check("EARTH_BARYCENTER's orbit after the switch is spkezr's state of 3 about 0 in ECLIPJ2000 at J2000 TDB",
                barycenterState.Position == barycenterOrbit.Position && barycenterState.Velocity == barycenterOrbit.Velocity);
            var earthOrbit = SpiceApi.Instance.ReadEphemeris(J2000, 3, 399, Frame.ECLIPJ2000, Aberration.None);
            var earthState = PlanetsAndMoons.EarthBody.InitialOrbitalParameters.ToStateVector();
            Check("EarthBody's orbit after the switch is spkezr's state of 399 about 3 in ECLIPJ2000 at J2000 TDB",
                earthState.Position == earthOrbit.Position && earthState.Velocity == earthOrbit.Velocity);
            Check("EarthBody's sphere of influence after the switch is finite",
                double.IsFinite(PlanetsAndMoons.EarthBody.SphereOfInfluence) && PlanetsAndMoons.EarthBody.SphereOfInfluence > 0.0);

            var epoch = new Time(2021, 1, 20, 18, 0, 0, TimeScale.UTC);
            var spkezr = SpiceApi.Instance.ReadEphemeris(epoch, 0, 3, Frame.ICRF, Aberration.None);
            var ephemeris = Barycenters.EarthBarycenter.GetEphemeris(epoch, Barycenters.SolarSystemBarycenter, Frame.ICRF,
                Aberration.None);
            Check("EARTH_BARYCENTER's ephemeris after the switch is spkezr's state of 3 about 0 in ICRF",
                ephemeris.Position == spkezr.Position && ephemeris.Velocity == spkezr.Velocity);

            // Once read, an object keeps the provider it read its data from: another provider set later changes nothing
            Configuration.Instance.SetDataProvider(new MemoryDataProvider());
            Check("EarthBody.GM, once read, stays SPICE's under a later provider", PlanetsAndMoons.EarthBody.GM == earthInfo.GM);
            var kept = Barycenters.EarthBarycenter.GetEphemeris(epoch, Barycenters.SolarSystemBarycenter, Frame.ICRF,
                Aberration.None);
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
    /// loaded, then after <see cref="SpiceApi.LoadKernels"/>. Without kernels SPICE names the bodies (its built-in names)
    /// but gives no <c>BODYnnn_GM</c> nor <c>BODYnnn_RADII</c>: a read of a member of an object whose data need them throws
    /// <see cref="InvalidOperationException"/> naming the missing variable, at each read, and caches nothing, so that once
    /// the kernels are loaded the members are SPICE's (<c>bodvrd</c>, <c>spkezr</c> through <see cref="SpiceApi"/>),
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
                var gm = Expect<InvalidOperationException>($"EarthBody.GM before the kernels, read {i}", () => PlanetsAndMoons.EarthBody.GM);
                Check($"read {i}: the message names BODY399_GM ({gm.Message})", gm.Message.Contains("BODY399_GM"));
            }

            Expect<InvalidOperationException>("EarthBody.Frame before the kernels", () => PlanetsAndMoons.EarthBody.Frame);
            Expect<InvalidOperationException>("EarthBody.EquatorialRadius before the kernels",
                () => PlanetsAndMoons.EarthBody.EquatorialRadius);
            Expect<InvalidOperationException>("EarthBody.InitialOrbitalParameters before the kernels",
                () => PlanetsAndMoons.EarthBody.InitialOrbitalParameters);
            var barycenter = Expect<InvalidOperationException>("EARTH_BARYCENTER.GM before the kernels", () => Barycenters.EarthBarycenter.GM);
            Check($"the message names BODY3_GM ({barycenter.Message})", barycenter.Message.Contains("BODY3_GM"));
            Expect<InvalidOperationException>("SunBody.EquatorialRadius before the kernels", () => Stars.SunBody.EquatorialRadius);
            Expect<InvalidOperationException>("MoonBody.GM before the kernels", () => PlanetsAndMoons.MoonBody.GM);
            Check("the identifiers need no data", PlanetsAndMoons.EarthBody.NaifId == 399 && PlanetsAndMoons.EarthBody.Name == "EARTH"
                                                    && Barycenters.EarthBarycenter.IsBarycenter && Stars.SunBody.IsSun);
            var ssb = Expect<InvalidOperationException>("SolarSystemBarycenter.GM before the kernels",
                () => Barycenters.SolarSystemBarycenter.GM);
            Check($"the message names BODY1_GM to BODY10_GM ({ssb.Message})", ssb.Message.Contains("BODY1_GM to BODY10_GM"));
            var sun = Expect<InvalidOperationException>("SunBody.Frame before the kernels", () => Stars.SunBody.Frame);
            Check($"the message names the GM first, BODY10_GM ({sun.Message})", sun.Message.Contains("BODY10_GM"));

            // A GM kernel alone: the bodies still lack their radii, which the message names, and nothing is kept
            var gmKernel = new FileInfo(Path.Combine(kernels, "gm_de440.tpc"));
            SpiceApi.Instance.LoadKernels(gmKernel);
            for (int i = 1; i <= 2; i++)
            {
                var radii = Expect<InvalidOperationException>($"EarthBody.GM with the GM kernel alone, read {i}",
                    () => PlanetsAndMoons.EarthBody.GM);
                Check($"read {i}: the message names BODY399_RADII ({radii.Message})", radii.Message.Contains("BODY399_RADII"));
            }

            Expect<InvalidOperationException>("MoonBody.Frame with the GM kernel alone", () => PlanetsAndMoons.MoonBody.Frame);
            SpiceApi.Instance.UnloadKernels(gmKernel);

            // A partial load: the leap seconds, the GMs and the PCK, without geophysical.ker (BODY399_J2), the frame
            // associations (OBJECT_EARTH_FRAME, OBJECT_MOON_FRAME) nor an SPK. The data are complete for the check (GM and
            // radii): the members are what SPICE gives at that time, J2 NaN, the built-in frames, no orbit
            var partial = new[] { "latest_leapseconds.tls", "gm_de440.tpc", "pck00011.tpc" }
                .Select(x => new FileInfo(Path.Combine(kernels, x))).ToArray();
            foreach (var kernel in partial)
            {
                SpiceApi.Instance.LoadKernels(kernel);
            }

            var partialEarth = SpiceApi.Instance.GetCelestialBodyInfo(399);
            var partialMoon = SpiceApi.Instance.GetCelestialBodyInfo(301);
            var early = PlanetsAndMoons.EarthBody;
            Check($"EarthBody.J2 after a partial load {early.J2:R}, SPICE {partialEarth.J2:R}", early.J2.Equals(partialEarth.J2));
            Check($"EarthBody.Frame after a partial load {early.Frame.Name}, SPICE {partialEarth.FrameName}",
                early.Frame.Name == partialEarth.FrameName && early.Frame.Name == "IAU_EARTH");
            Check($"MoonBody.Frame after a partial load {PlanetsAndMoons.MoonBody.Frame.Name}, SPICE {partialMoon.FrameName}",
                PlanetsAndMoons.MoonBody.Frame.Name == partialMoon.FrameName && partialMoon.FrameName == "IAU_MOON");
            Check("EarthBody has no orbit after a partial load", early.InitialOrbitalParameters == null
                                                                 && double.IsPositiveInfinity(early.SphereOfInfluence));

            // A load that changes none of its data keeps the objects an item gave (its frame), as the orientation cache of
            // a propagation is keyed by its frame instance
            var partialFrame = early.Frame;
            SpiceApi.Instance.LoadKernels(partial[2]);
            Check("EarthBody.Frame after the reload of the PCK is the same instance", ReferenceEquals(early.Frame, partialFrame));

            // The kernels: the directory unloads the files loaded inside it first. Before, the data of the partial load were
            // kept for the life of the process (J2 NaN, IAU_EARTH, no orbit)
            SpiceApi.Instance.LoadKernels(new DirectoryInfo(kernels));

            var ssbInfo = SpiceApi.Instance.GetCelestialBodyInfo(0);
            var barycenterInfo = SpiceApi.Instance.GetCelestialBodyInfo(3);
            var earthInfo = SpiceApi.Instance.GetCelestialBodyInfo(399);
            var moonInfo = SpiceApi.Instance.GetCelestialBodyInfo(301);
            var sunInfo = SpiceApi.Instance.GetCelestialBodyInfo(10);
            var earth = PlanetsAndMoons.EarthBody;
            Check($"EarthBody.GM after the kernels {earth.GM:R}, SPICE {earthInfo.GM:R}", earth.GM == earthInfo.GM && earth.GM > 0.0);
            Check($"EarthBody's radii after the kernels {earth.EquatorialRadius:R} {earth.PolarRadius:R}, SPICE {earthInfo.Radii.X:R} {earthInfo.Radii.Z:R}",
                earth.EquatorialRadius == earthInfo.Radii.X && earth.PolarRadius == earthInfo.Radii.Z && earth.EquatorialRadius > 0.0);
            Check($"EarthBody.J2 after the kernels {earth.J2:R}, SPICE {earthInfo.J2:R}", earth.J2.Equals(earthInfo.J2) && double.IsFinite(earth.J2));
            Check($"EarthBody.Frame after the kernels {earth.Frame.Name}, SPICE {earthInfo.FrameName}",
                earth.Frame.Name == earthInfo.FrameName && earth.Frame.Name == "ITRF93");
            var earthOrbit = SpiceApi.Instance.ReadEphemeris(J2000, 3, 399, Frame.ECLIPJ2000, Aberration.None);
            var earthState = earth.InitialOrbitalParameters.ToStateVector();
            Check("EarthBody's orbit after the kernels is spkezr's state of 399 about 3 in ECLIPJ2000 at J2000 TDB",
                earthState.Position == earthOrbit.Position && earthState.Velocity == earthOrbit.Velocity);
            Check("EarthBody's sphere of influence after the kernels is finite",
                double.IsFinite(earth.SphereOfInfluence) && earth.SphereOfInfluence > 0.0);
            Check($"EARTH_BARYCENTER.GM after the kernels {Barycenters.EarthBarycenter.GM:R}, SPICE {barycenterInfo.GM:R}",
                Barycenters.EarthBarycenter.GM == barycenterInfo.GM);
            Check($"SunBody.EquatorialRadius after the kernels {Stars.SunBody.EquatorialRadius:R}, SPICE {sunInfo.Radii.X:R}",
                Stars.SunBody.EquatorialRadius == sunInfo.Radii.X && Stars.SunBody.GM == sunInfo.GM);
            Check($"MoonBody.GM after the kernels {PlanetsAndMoons.MoonBody.GM:R}, SPICE {moonInfo.GM:R}",
                PlanetsAndMoons.MoonBody.GM == moonInfo.GM);
            Check($"SolarSystemBarycenter.GM after the kernels {Barycenters.SolarSystemBarycenter.GM:R}, SPICE {ssbInfo.GM:R}",
                Barycenters.SolarSystemBarycenter.GM == ssbInfo.GM && ssbInfo.GM > 0.0);
            Check("SolarSystemBarycenter has no orbit", Barycenters.SolarSystemBarycenter.InitialOrbitalParameters == null);
            Check($"MoonBody.Frame after the kernels {PlanetsAndMoons.MoonBody.Frame.Name}, SPICE {moonInfo.FrameName}",
                PlanetsAndMoons.MoonBody.Frame.Name == moonInfo.FrameName && moonInfo.FrameName == "MOON_ME");

            // The reload of a kernel of the directory changes none of the data: the same instances
            var frame = earth.Frame;
            var orbit = earth.InitialOrbitalParameters;
            SpiceApi.Instance.LoadKernels(new FileInfo(Path.Combine(kernels, "pck00011.tpc")));
            Check("EarthBody's frame and orbit after the reload of a kernel are the same instances",
                ReferenceEquals(earth.Frame, frame) && ReferenceEquals(earth.InitialOrbitalParameters, orbit));

            // ClearKernels: the data follow the kernel pool, the next read throws naming the missing variable, at each read
            SpiceApi.Instance.ClearKernels();
            for (int i = 1; i <= 2; i++)
            {
                var cleared = Expect<InvalidOperationException>($"EarthBody.GM after ClearKernels, read {i}", () => earth.GM);
                Check($"read {i}: the message names BODY399_GM ({cleared.Message})", cleared.Message.Contains("BODY399_GM"));
            }

            SpiceApi.Instance.LoadKernels(new DirectoryInfo(kernels));
            Check($"EarthBody.GM after the kernels are loaded again {earth.GM:R}, SPICE {earthInfo.GM:R}", earth.GM == earthInfo.GM);
            Check("EarthBody.J2 and Frame after the kernels are loaded again", earth.J2.Equals(earthInfo.J2) && earth.Frame.Name == "ITRF93");
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
        0 => Barycenters.SolarSystemBarycenter,
        1 => Barycenters.MercuryBarycenter,
        2 => Barycenters.VenusBarycenter,
        3 => Barycenters.EarthBarycenter,
        4 => Barycenters.MarsBarycenter,
        5 => Barycenters.JupiterBarycenter,
        6 => Barycenters.SaturnBarycenter,
        7 => Barycenters.UranusBarycenter,
        8 => Barycenters.NeptuneBarycenter,
        _ => Barycenters.PlutoBarycenter
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
        return state.Frame == Frame.ECLIPJ2000 && Close(state.Position, icrfPosition) && Close(state.Velocity, icrfVelocity);
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
