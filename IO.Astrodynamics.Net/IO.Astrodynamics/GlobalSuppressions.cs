// Copyright 2026. Sylvain Guillet (sylvain.guillet@tutamail.com)

// The CA naming rules are errors since lot L9 of the API ergonomics review (#668, design PR #671, D2 N8). This file holds
// every exception, each with the decision that keeps the name: the six type and namespace pairs of CA1724, which N8
// suppresses with a justification, and the 11 diagnostics of names decided or released (decision 114 of #668, decided
// by S. Guillet, 2026-11-26). A new exception needs its own decision; any other name breaking a CA naming rule fails the
// build. See docs/contributing/public-api.md.

using System.Diagnostics.CodeAnalysis;

// CA1724: a type named after the namespace of its feature. N4 of D2 changes a namespace only to fix a defect users hit,
// and these pairs collide only inside IO.Astrodynamics: plural feature namespaces (Bodies, Orbits, Maneuvers) would
// rewrite every user's usings for a collision no outside code meets (annex of #671, D2, rejected alternative).
[assembly: SuppressMessage("Naming", "CA1724:Type names should not match namespaces",
    Justification = "D2 N8 of #668: the pair Atmosphere and IO.Astrodynamics.Atmosphere is kept (N4, namespaces unchanged).",
    Scope = "type", Target = "~T:IO.Astrodynamics.Atmosphere.Atmosphere")]
[assembly: SuppressMessage("Naming", "CA1724:Type names should not match namespaces",
    Justification = "D2 N8 of #668: the pair Spacecraft and IO.Astrodynamics.Body.Spacecraft is kept (N4, namespaces unchanged).",
    Scope = "type", Target = "~T:IO.Astrodynamics.Body.Spacecraft.Spacecraft")]
[assembly: SuppressMessage("Naming", "CA1724:Type names should not match namespaces",
    Justification = "D2 N8 of #668: the pair Maneuver and IO.Astrodynamics.Maneuver is kept (N4, namespaces unchanged).",
    Scope = "type", Target = "~T:IO.Astrodynamics.Maneuver.Maneuver")]
[assembly: SuppressMessage("Naming", "CA1724:Type names should not match namespaces",
    Justification = "D2 N8 of #668: the pair Mission and IO.Astrodynamics.Mission is kept (N4, namespaces unchanged).",
    Scope = "type", Target = "~T:IO.Astrodynamics.Mission.Mission")]
[assembly: SuppressMessage("Naming", "CA1724:Type names should not match namespaces",
    Justification = "D2 N8 of #668: the pair OrbitalParameters and IO.Astrodynamics.OrbitalParameters is kept (N4, namespaces unchanged).",
    Scope = "type", Target = "~T:IO.Astrodynamics.OrbitalParameters.OrbitalParameters")]
[assembly: SuppressMessage("Naming", "CA1724:Type names should not match namespaces",
    Justification = "D2 N8 of #668: IO.Astrodynamics.Configuration is kept; System.Configuration is a .NET Framework namespace, in no implicit using of the SDK or of the package (PackageImplicitUsingsTests).",
    Scope = "type", Target = "~T:IO.Astrodynamics.Configuration")]

// CA1721: a property beside a Get method of the same name, both decided.
[assembly: SuppressMessage("Naming", "CA1721:Property names should not match get methods",
    Justification = "Decision 114 of #668 (S. Guillet, 2026-11-26): CelestialBody.Atmosphere, the atmosphere the drag reads, is exposed by the amendment of D4 (R06, decided by S. Guillet, 2026-11-19); GetAtmosphere reads its density, pressure and temperature at a context.",
    Scope = "member", Target = "~P:IO.Astrodynamics.Body.CelestialBody.Atmosphere")]
[assembly: SuppressMessage("Naming", "CA1721:Property names should not match get methods",
    Justification = "Decision 114 of #668 (S. Guillet, 2026-11-26): R22 keeps the CCSDS model's DateTime Epoch, as written in the message, and adds GetEpoch(), the Time of its TIME_SYSTEM.",
    Scope = "member", Target = "~P:IO.Astrodynamics.Ccsds.Omm.Epoch")]
[assembly: SuppressMessage("Naming", "CA1721:Property names should not match get methods",
    Justification = "Decision 114 of #668 (S. Guillet, 2026-11-26): R22 keeps the CCSDS model's DateTime Epoch, as written in the message, and adds GetEpoch(), the Time of its TIME_SYSTEM.",
    Scope = "member", Target = "~P:IO.Astrodynamics.Ccsds.Opm.Epoch")]
[assembly: SuppressMessage("Naming", "CA1721:Property names should not match get methods",
    Justification = "Decision 114 of #668 (S. Guillet, 2026-11-26): the axis a single-vector attitude points (decision 69) beside the protected Maneuver.GetBodyAxis(BodyAxis) a user law reads any axis through (decisions 48 and 93).",
    Scope = "member", Target = "~P:IO.Astrodynamics.Maneuver.AntiNormalAttitude.BodyAxis")]
[assembly: SuppressMessage("Naming", "CA1721:Property names should not match get methods",
    Justification = "Decision 114 of #668 (S. Guillet, 2026-11-26): the axis a single-vector attitude points (decision 69) beside the protected Maneuver.GetBodyAxis(BodyAxis) a user law reads any axis through (decisions 48 and 93).",
    Scope = "member", Target = "~P:IO.Astrodynamics.Maneuver.NadirAttitude.BodyAxis")]
[assembly: SuppressMessage("Naming", "CA1721:Property names should not match get methods",
    Justification = "Decision 114 of #668 (S. Guillet, 2026-11-26): the axis a single-vector attitude points (decision 69) beside the protected Maneuver.GetBodyAxis(BodyAxis) a user law reads any axis through (decisions 48 and 93).",
    Scope = "member", Target = "~P:IO.Astrodynamics.Maneuver.NormalAttitude.BodyAxis")]
[assembly: SuppressMessage("Naming", "CA1721:Property names should not match get methods",
    Justification = "Decision 114 of #668 (S. Guillet, 2026-11-26): the axis a single-vector attitude points (decision 69) beside the protected Maneuver.GetBodyAxis(BodyAxis) a user law reads any axis through (decisions 48 and 93).",
    Scope = "member", Target = "~P:IO.Astrodynamics.Maneuver.ProgradeAttitude.BodyAxis")]
[assembly: SuppressMessage("Naming", "CA1721:Property names should not match get methods",
    Justification = "Decision 114 of #668 (S. Guillet, 2026-11-26): the axis a single-vector attitude points (decision 69) beside the protected Maneuver.GetBodyAxis(BodyAxis) a user law reads any axis through (decisions 48 and 93).",
    Scope = "member", Target = "~P:IO.Astrodynamics.Maneuver.RetrogradeAttitude.BodyAxis")]
[assembly: SuppressMessage("Naming", "CA1721:Property names should not match get methods",
    Justification = "Decision 114 of #668 (S. Guillet, 2026-11-26): the axis a single-vector attitude points (decision 69) beside the protected Maneuver.GetBodyAxis(BodyAxis) a user law reads any axis through (decisions 48 and 93).",
    Scope = "member", Target = "~P:IO.Astrodynamics.Maneuver.ZenithAttitude.BodyAxis")]

// CA1711 and CA1720: names released in 10.1.0.
[assembly: SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Decision 114 of #668 (S. Guillet, 2026-11-26): EncounterQualityFlags, a [Flags] enum, released in 10.1.0 and mapped to no rename.",
    Scope = "type", Target = "~T:IO.Astrodynamics.Ssa.EncounterQualityFlags")]
[assembly: SuppressMessage("Naming", "CA1720:Identifiers should not contain type names",
    Justification = "Decision 114 of #668 (S. Guillet, 2026-11-26): the CCSDS 508.0-B-1 keyword OBJECT of the CDM metadata, released in 10.1.0.",
    Scope = "member", Target = "~P:IO.Astrodynamics.Ccsds.CdmParticipantMetadata.Object")]
