// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using System;
using System.Collections.Generic;

namespace IO.Astrodynamics.CCSDS.Common;

/// <summary>
/// The NAIF id of a CCSDS <c>CENTER_NAME</c>: the names the library's writers emit, which are the SPICE names of the
/// barycenters 0 to 9, the Sun and the planets and moons of <see cref="SolarSystemObjects.PlanetsAndMoons"/>, and NAIF's
/// built-in synonyms of those ids (NAIF IDs Required Reading, "Barycenters": SSB, EMB, EARTH MOON BARYCENTER, ...).
/// </summary>
/// <remarks>
/// The lookup trims the name and ignores its letter case. The ids are literals: the lookup reads no kernel and runs no type
/// initializer that reads SPICE (#623). A ground station, a site, a spacecraft or a body that only a kernel names is not in
/// the table: its observer is the caller's.
/// </remarks>
internal static class CcsdsCenterNames
{
    /// <summary>
    /// The SPICE name of each id of the table (<c>bodc2n</c>), the name a writer emits for that centre.
    /// </summary>
    internal static readonly IReadOnlyList<(string Name, int NaifId)> CanonicalNames =
    [
        ("SOLAR SYSTEM BARYCENTER", 0),
        ("MERCURY BARYCENTER", 1),
        ("VENUS BARYCENTER", 2),
        ("EARTH BARYCENTER", 3),
        ("MARS BARYCENTER", 4),
        ("JUPITER BARYCENTER", 5),
        ("SATURN BARYCENTER", 6),
        ("URANUS BARYCENTER", 7),
        ("NEPTUNE BARYCENTER", 8),
        ("PLUTO BARYCENTER", 9),
        ("SUN", 10),
        ("MERCURY", 199),
        ("VENUS", 299),
        ("EARTH", 399),
        ("MOON", 301),
        ("MARS", 499),
        ("PHOBOS", 401),
        ("DEIMOS", 402),
        ("JUPITER", 599),
        ("IO", 501),
        ("EUROPA", 502),
        ("GANYMEDE", 503),
        ("CALLISTO", 504),
        ("AMALTHEA", 505),
        ("HIMALIA", 506),
        ("ELARA", 507),
        ("PASIPHAE", 508),
        ("SINOPE", 509),
        ("LYSITHEA", 510),
        ("CARME", 511),
        ("ANANKE", 512),
        ("LEDA", 513),
        ("THEBE", 514),
        ("ADRASTEA", 515),
        ("METIS", 516),
        ("CALLIRRHOE", 517),
        ("THEMISTO", 518),
        ("MEGACLITE", 519),
        ("TAYGETE", 520),
        ("CHALDENE", 521),
        ("HARPALYKE", 522),
        ("KALYKE", 523),
        ("IOCASTE", 524),
        ("ERINOME", 525),
        ("ISONOE", 526),
        ("PRAXIDIKE", 527),
        ("AUTONOE", 528),
        ("THYONE", 529),
        ("HERMIPPE", 530),
        ("AITNE", 531),
        ("EURYDOME", 532),
        ("EUANTHE", 533),
        ("EUPORIE", 534),
        ("ORTHOSIE", 535),
        ("SPONDE", 536),
        ("KALE", 537),
        ("PASITHEE", 538),
        ("HEGEMONE", 539),
        ("MNEME", 540),
        ("AOEDE", 541),
        ("THELXINOE", 542),
        ("ARCHE", 543),
        ("KALLICHORE", 544),
        ("HELIKE", 545),
        ("CARPO", 546),
        ("EUKELADE", 547),
        ("CYLLENE", 548),
        ("KORE", 549),
        ("HERSE", 550),
        ("DIA", 553),
        ("SATURN", 699),
        ("MIMAS", 601),
        ("ENCELADUS", 602),
        ("TETHYS", 603),
        ("DIONE", 604),
        ("RHEA", 605),
        ("TITAN", 606),
        ("HYPERION", 607),
        ("IAPETUS", 608),
        ("PHOEBE", 609),
        ("JANUS", 610),
        ("EPIMETHEUS", 611),
        ("HELENE", 612),
        ("TELESTO", 613),
        ("CALYPSO", 614),
        ("ATLAS", 615),
        ("PROMETHEUS", 616),
        ("PANDORA", 617),
        ("PAN", 618),
        ("YMIR", 619),
        ("PAALIAQ", 620),
        ("TARVOS", 621),
        ("IJIRAQ", 622),
        ("SUTTUNGR", 623),
        ("KIVIUQ", 624),
        ("MUNDILFARI", 625),
        ("ALBIORIX", 626),
        ("SKATHI", 627),
        ("ERRIAPUS", 628),
        ("SIARNAQ", 629),
        ("THRYMR", 630),
        ("NARVI", 631),
        ("METHONE", 632),
        ("PALLENE", 633),
        ("POLYDEUCES", 634),
        ("DAPHNIS", 635),
        ("AEGIR", 636),
        ("BEBHIONN", 637),
        ("BERGELMIR", 638),
        ("BESTLA", 639),
        ("FARBAUTI", 640),
        ("FENRIR", 641),
        ("FORNJOT", 642),
        ("HATI", 643),
        ("HYRROKKIN", 644),
        ("KARI", 645),
        ("LOGE", 646),
        ("SKOLL", 647),
        ("SURTUR", 648),
        ("ANTHE", 649),
        ("JARNSAXA", 650),
        ("GREIP", 651),
        ("TARQEQ", 652),
        ("AEGAEON", 653),
        ("URANUS", 799),
        ("ARIEL", 701),
        ("UMBRIEL", 702),
        ("TITANIA", 703),
        ("OBERON", 704),
        ("MIRANDA", 705),
        ("CORDELIA", 706),
        ("OPHELIA", 707),
        ("BIANCA", 708),
        ("CRESSIDA", 709),
        ("DESDEMONA", 710),
        ("JULIET", 711),
        ("PORTIA", 712),
        ("ROSALIND", 713),
        ("BELINDA", 714),
        ("PUCK", 715),
        ("CALIBAN", 716),
        ("SYCORAX", 717),
        ("PROSPERO", 718),
        ("SETEBOS", 719),
        ("STEPHANO", 720),
        ("TRINCULO", 721),
        ("FRANCISCO", 722),
        ("MARGARET", 723),
        ("FERDINAND", 724),
        ("PERDITA", 725),
        ("MAB", 726),
        ("CUPID", 727),
        ("NEPTUNE", 899),
        ("TRITON", 801),
        ("NEREID", 802),
        ("NAIAD", 803),
        ("THALASSA", 804),
        ("DESPINA", 805),
        ("GALATEA", 806),
        ("LARISSA", 807),
        ("PROTEUS", 808),
        ("HALIMEDE", 809),
        ("PSAMATHE", 810),
        ("SAO", 811),
        ("LAOMEDEIA", 812),
        ("NESO", 813),
        ("PLUTO", 999),
        ("CHARON", 901),
        ("NIX", 902),
        ("HYDRA", 903),
        ("KERBEROS", 904),
        ("STYX", 905),
    ];

    /// <summary>
    /// NAIF's built-in synonyms of the barycenters (<c>bodn2c</c> of CSPICE N0067 gives these ids).
    /// </summary>
    internal static readonly IReadOnlyList<(string Name, int NaifId)> Synonyms =
    [
        ("SSB", 0),
        ("SOLAR_SYSTEM_BARYCENTER", 0),
        ("MERCURY_BARYCENTER", 1),
        ("VENUS_BARYCENTER", 2),
        ("EARTH_BARYCENTER", 3),
        ("MARS_BARYCENTER", 4),
        ("JUPITER_BARYCENTER", 5),
        ("SATURN_BARYCENTER", 6),
        ("URANUS_BARYCENTER", 7),
        ("NEPTUNE_BARYCENTER", 8),
        ("PLUTO_BARYCENTER", 9),
        ("EMB", 3),
        ("EARTH MOON BARYCENTER", 3),
        ("EARTH-MOON BARYCENTER", 3),
    ];

    private static readonly Dictionary<string, int> Ids = Build();

    /// <summary>
    /// Gets the NAIF id of a <c>CENTER_NAME</c>, trimmed and in any letter case.
    /// </summary>
    /// <param name="centerName">The <c>CENTER_NAME</c> of a message.</param>
    /// <param name="naifId">The NAIF id of the centre, when the table holds the name.</param>
    /// <returns>True when the table holds the name.</returns>
    internal static bool TryGetNaifId(string centerName, out int naifId)
    {
        naifId = 0;
        return centerName != null && Ids.TryGetValue(centerName.Trim(), out naifId);
    }

    private static Dictionary<string, int> Build()
    {
        var ids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, naifId) in CanonicalNames)
            ids.Add(name, naifId);
        foreach (var (name, naifId) in Synonyms)
            ids.Add(name, naifId);
        return ids;
    }
}
