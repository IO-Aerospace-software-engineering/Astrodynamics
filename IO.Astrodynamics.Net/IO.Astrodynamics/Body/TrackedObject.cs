// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using IO.Astrodynamics.CCSDS.Common.Enums;
using IO.Astrodynamics.CCSDS.OMM;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.OrbitalParameters.TLE;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body;

/// <summary>
/// A catalogue object (debris, a rocket body, a payload the operator does not fly) for the conjunction analysis and the CDM
/// export: its orbit, its hard-body radius, its NORAD and COSPAR identifiers and its object type, without a clock, an
/// engine or a mass (R19 of #668, lot L7; before 11.0.0 a <see cref="Spacecraft.Spacecraft"/> wrapped such an object, and
/// the CDM export wrote it as a payload under its NAIF identifier).
/// </summary>
/// <remarks>
/// <para>
/// Built from a TLE (<see cref="FromTle"/>), a TLE-compatible OMM (<see cref="FromOmm"/>) or an osculating state
/// (<see cref="FromState"/>). Its motion is that of a <see cref="Spacecraft.Spacecraft"/> of the same initial orbit that no
/// propagation stored states for, bit for bit: SGP4 for a TLE, the two-body orbit of the state otherwise, in ICRF relative
/// to the observer of the orbit; no data provider is read for it.
/// </para>
/// <para>
/// Its NAIF identifier is negative, so it is no SPICE object (<see cref="CelestialItem.IsSpiceBacked"/> false). By default
/// it is −100000 − NORAD (mkspk's convention, confirmed by S. Guillet, 2026-11-13): −100000 to −439999 for the TLE range
/// (Alpha-5 up to 339999). That range overlaps <see cref="Propagator.MonteCarlo.MonteCarloConfiguration.BaseNaifId"/>
/// (−100000 and below), and the conjunction screening skips a candidate of the protected spacecraft's identifier: give
/// <c>naifId</c> where identifiers could meet.
/// </para>
/// </remarks>
public sealed class TrackedObject : CelestialItem
{
    // mkspk's convention for an object of the NORAD catalogue: −100000 − NORAD (confirmed by S. Guillet, 2026-11-13)
    private const int CatalogueNaifIdBase = -100000;

    private TrackedObject(int naifId, string name, OrbitalParameters.OrbitalParameters initialOrbitalParameters,
        double hardBodyRadius, int? noradCatalogId, string cosparId, ObjectType objectType)
        : base(CheckedNaifId(naifId), name, 0.0, initialOrbitalParameters)
    {
        Spacecraft.Spacecraft.ThrowIfInvalidHardBodyRadius(hardBodyRadius, "hardBodyRadiusMeters");
        HardBodyRadius = hardBodyRadius;
        NoradCatalogId = noradCatalogId;
        CosparId = cosparId;
        ObjectType = objectType;
        RegisterWithObserver();
    }

    /// <summary>
    /// The hard-body radius in meters, 0 when none is given: the radius of the object as a secondary of the conjunction
    /// analysis, as <see cref="Spacecraft.Spacecraft.HardBodyRadius"/> (an option of the analysis that differs from a radius
    /// above 0 throws).
    /// </summary>
    public double HardBodyRadius { get; }

    /// <summary>The NORAD catalogue number, null when none is given (<see cref="FromState"/>).</summary>
    public int? NoradCatalogId { get; }

    /// <summary>
    /// The COSPAR international designator in the form YYYY-NNNP (<c>1998-067A</c>), as
    /// <see cref="Spacecraft.Spacecraft.CosparId"/>; null when unknown (blank columns 10-17 of a TLE).
    /// </summary>
    public string CosparId { get; }

    /// <summary>The CCSDS object type, written as the <c>OBJECT_TYPE</c> of the CDM export.</summary>
    public ObjectType ObjectType { get; }

    /// <summary>
    /// The object of a TLE: its SGP4 motion, its NORAD number and its international designator.
    /// </summary>
    /// <param name="tle">The TLE of the object.</param>
    /// <param name="hardBodyRadiusMeters">The hard-body radius in meters, finite and not negative; 0 for none.</param>
    /// <param name="naifId">The NAIF identifier, negative; −100000 − NORAD by default.</param>
    /// <param name="objectType">The CCSDS object type; <see cref="ObjectType.Unknown"/> by default.</param>
    /// <param name="name">The name; the name of the TLE by default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tle"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="hardBodyRadiusMeters"/> is not finite or is negative, or
    /// <paramref name="naifId"/> is not negative.</exception>
    public static TrackedObject FromTle(Tle tle, double hardBodyRadiusMeters = 0.0, int? naifId = null,
        ObjectType objectType = ObjectType.Unknown, string name = null)
    {
        ArgumentNullException.ThrowIfNull(tle);
        return new TrackedObject(naifId ?? CatalogueNaifIdBase - tle.NoradCatalogId,
            string.IsNullOrWhiteSpace(name) ? tle.Name : name, tle, hardBodyRadiusMeters, tle.NoradCatalogId,
            tle.CosparId == null ? null : Tle.ConvertCosparIdToObjectId(tle.CosparId), objectType);
    }

    /// <summary>
    /// The object of a TLE-compatible OMM: <see cref="FromTle"/> of <see cref="Omm.ToTle"/>.
    /// </summary>
    /// <param name="omm">The OMM of the object, TLE-compatible (<see cref="Omm.IsTleCompatible"/>).</param>
    /// <param name="hardBodyRadiusMeters">The hard-body radius in meters, finite and not negative; 0 for none.</param>
    /// <param name="naifId">The NAIF identifier, negative; −100000 − NORAD by default.</param>
    /// <param name="objectType">The CCSDS object type; <see cref="ObjectType.Unknown"/> by default.</param>
    /// <param name="name">The name; the <c>OBJECT_NAME</c> of the OMM by default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="omm"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The OMM is not TLE-compatible, as <see cref="Omm.ToTle"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="hardBodyRadiusMeters"/> is not finite or is negative, or
    /// <paramref name="naifId"/> is not negative.</exception>
    public static TrackedObject FromOmm(Omm omm, double hardBodyRadiusMeters = 0.0, int? naifId = null,
        ObjectType objectType = ObjectType.Unknown, string name = null)
    {
        ArgumentNullException.ThrowIfNull(omm);
        return FromTle(omm.ToTle(), hardBodyRadiusMeters, naifId, objectType, name);
    }

    /// <summary>
    /// The object of an osculating state: its two-body motion (<see cref="OrbitalParameters.OrbitalParameters.ToStateVector(Time)"/>),
    /// the covariance of the state, when it holds one, its covariance in the conjunction analysis.
    /// </summary>
    /// <param name="state">The state of the object.</param>
    /// <param name="name">The name of the object.</param>
    /// <param name="hardBodyRadiusMeters">The hard-body radius in meters, finite and not negative; 0 for none.</param>
    /// <param name="naifId">The NAIF identifier, negative; −100000 − <paramref name="noradCatalogId"/> by default, required
    /// without a NORAD number.</param>
    /// <param name="noradCatalogId">The NORAD catalogue number, not negative; none by default.</param>
    /// <param name="cosparId">The COSPAR international designator (YYYY-NNNP); none by default.</param>
    /// <param name="objectType">The CCSDS object type; <see cref="ObjectType.Unknown"/> by default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null or empty, or neither <paramref name="naifId"/> nor
    /// <paramref name="noradCatalogId"/> is given.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="hardBodyRadiusMeters"/> is not finite or is negative,
    /// <paramref name="naifId"/> is not negative, or <paramref name="noradCatalogId"/> is negative.</exception>
    public static TrackedObject FromState(StateVector state, string name, double hardBodyRadiusMeters = 0.0,
        int? naifId = null, int? noradCatalogId = null, string cosparId = null, ObjectType objectType = ObjectType.Unknown)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (noradCatalogId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(noradCatalogId), noradCatalogId,
                "The NORAD catalogue number must not be negative.");
        }

        int id = naifId ?? (noradCatalogId is { } norad
            ? CatalogueNaifIdBase - norad
            : throw new ArgumentException("A state gives no NORAD number: give the NAIF identifier.", nameof(naifId)));
        return new TrackedObject(id, name, state, hardBodyRadiusMeters, noradCatalogId, cosparId, objectType);
    }

    // A catalogue object is no SPICE object: a negative identifier, as a spacecraft's
    private static int CheckedNaifId(int naifId) => naifId < 0
        ? naifId
        : throw new ArgumentOutOfRangeException(nameof(naifId), naifId, "The NAIF identifier of a tracked object must be negative.");

    /// <summary>
    /// The geometric state relative to <paramref name="referenceBody"/> in ICRF: the state of the initial orbit at
    /// <paramref name="epoch"/> (SGP4 for a TLE, the two-body orbit otherwise), as a spacecraft without stored states.
    /// </summary>
    public override StateVector GetGeometricStateRelativeTo(in Time epoch, CelestialItem referenceBody)
    {
        return InitialOrbitalParameters.ToStateVector(epoch)
            .ToFrame(Frame.ICRF)
            .RelativeTo(referenceBody, Aberration.None).ToStateVector();
    }

    /// <summary>
    /// The state seen from <paramref name="observer"/>, on the reference-body path from the observer of the initial orbit,
    /// as a spacecraft without stored states.
    /// </summary>
    public override StateVector GetEphemeris(in Time epoch, ILocalizable observer, Frame frame, Aberration aberration)
    {
        return ApparentStateFromReferenceBody(this, epoch, observer, frame, aberration, ReferenceBody);
    }

    internal override OrbitalParameters.OrbitalParameters GetGeometricStateFromICRF(in Time date)
    {
        return GetGeometricStateRelativeTo(date, Barycenters.SOLAR_SYSTEM_BARYCENTER);
    }

    // The observer of the initial orbit, the reference body of the reads of the object and of an observer at it
    internal CelestialItem ReferenceBody =>
        InitialOrbitalParameters.Observer as CelestialItem ?? Barycenters.SOLAR_SYSTEM_BARYCENTER;
}
