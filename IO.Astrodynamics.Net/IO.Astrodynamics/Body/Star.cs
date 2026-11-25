﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.Propagator;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body;

/// <summary>
/// Represents a star in the celestial system.
/// </summary>
/// <remarks>
/// <para>
/// Its position at an epoch is its catalog position moved along a straight line at constant speed in ICRF, SOFA's
/// <c>iauStarpm</c> with a radial velocity of 0 (<c>external-lib/sofa-src/starpm.c</c>, Note 5, #718), over dt, the
/// Julian years of TDB from the catalog epoch (both times converted to TDB, as <c>iauStarpm</c> takes TDB dates and its
/// proper motions in radians per TDB Julian year, Notes 1 and 2, #709); its distance the length of that line; its sigmas
/// √(σ² + (dt σμ)²); the velocity of its states the derivative of that line, constant (#731). See
/// <see cref="GetEquatorialCoordinates"/>, <see cref="GetGeometricStateRelativeTo"/> and <c>docs/reference/stars.md</c>.
/// </para>
/// <para>
/// The units and conventions are those of the Hipparcos and Gaia catalogs: angles in radians, proper motions in radians
/// per Julian year of TDB, the proper motion in right ascension and its sigmas true angles (μα* = (dα/dt) cos δ, σα*,
/// σμα*), the parallax in arcseconds. A Gaia DR3 record converts by its units alone: <c>pmra</c> is μα*.
/// </para>
/// </remarks>
public class Star : CelestialBody
{
    // The states of the last propagation, in the order of their epochs; replaced as a whole by each propagation
    private StateVector[] _propagatedStates = Array.Empty<StateVector>();

    // The catalog epoch in TDB, the time scale of the catalog motion's dt (#709)
    private readonly Time _epochTdb;

    // The space velocity, the derivative of the straight line, constant, in m/s of TDB (#731)
    private readonly Vector3 _spaceVelocity;

    // The seconds of a Julian year of TDB, the unit of time of the proper motions
    private const double JulianYearSeconds = Time.JULIAN_YEAR * 86400.0;
    /// <summary>
    /// Initializes a new instance of the <see cref="Star"/> class.
    /// </summary>
    /// <param name="catalogNumber">The catalog number of the star.</param>
    /// <param name="name">The name of the star.</param>
    /// <param name="mass">The mass of the star.</param>
    /// <param name="spectralType">The spectral type of the star.</param>
    /// <param name="visualMagnitude">The visual magnitude of the star.</param>
    /// <param name="parallax">The parallax of the star in arcseconds, finite and positive; its distance is 1/parallax
    /// parsecs (<see cref="Constants.Parsec2Meters"/>).</param>
    /// <param name="equatorialCoordinatesAtEpoch">The right ascension and declination of the star in ICRF at the catalog
    /// epoch, in radians (its distance is not read).</param>
    /// <param name="declinationProperMotion">The proper motion in declination, dδ/dt, in radians per Julian year of TDB.</param>
    /// <param name="rightAscensionProperMotion">The proper motion in right ascension as a true angle, μα* = (dα/dt) cos δ,
    /// in radians per Julian year of TDB: the <c>pmra</c> of Gaia and the μα* of Hipparcos, converted from mas/yr (#718;
    /// before 11.0.0 it was read as dα/dt).</param>
    /// <param name="declinationSigma">The standard deviation of the declination at the catalog epoch, in radians.</param>
    /// <param name="rightAscensionSigma">The standard deviation of the right ascension at the catalog epoch as a true
    /// angle, σα* = σα cos δ, in radians (Gaia's <c>ra_error</c>).</param>
    /// <param name="declinationSigmaProperMotion">The standard deviation of the proper motion in declination, in radians
    /// per Julian year of TDB.</param>
    /// <param name="rightAscensionSigmaProperMotion">The standard deviation of the proper motion in right ascension as a
    /// true angle, σμα*, in radians per Julian year of TDB (Gaia's <c>pmra_error</c>).</param>
    /// <param name="epoch">The catalog epoch, read in its own time scale and converted to TDB (#709).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="parallax"/> is 0, negative, NaN or infinite, or so small
    /// that 1/parallax parsecs overflows a double (below about 1.7e-292″): no distance in front of the star (#732, decided
    /// by S. Guillet, 2026-11-17). Before 11.0.0 it was accepted: 0 gave an infinite distance, a negative parallax put the
    /// star in the opposite direction, NaN a NaN distance, an infinite one a distance of 0.</exception>
    public Star(int catalogNumber, string name, double mass, string spectralType, double visualMagnitude, double parallax, Equatorial equatorialCoordinatesAtEpoch,
        double declinationProperMotion, double rightAscensionProperMotion, double declinationSigma, double rightAscensionSigma, double declinationSigmaProperMotion,
        double rightAscensionSigmaProperMotion, Time epoch) : base((int)1E+09 + catalogNumber, name, mass)
    {
        // A parallax that gives no distance in front of the star (#732, decided by S. Guillet, 2026-11-17): 0, a negative
        // value, NaN or an infinity, and a positive value so small that 1/parallax parsecs overflows a double
        if (!(parallax > 0.0) || double.IsPositiveInfinity(parallax))
        {
            throw new ArgumentOutOfRangeException(nameof(parallax), parallax,
                "The parallax must be a finite positive number of arcseconds: 0, a negative value, NaN and an infinity give no distance.");
        }

        double distance = (1 / parallax) * Constants.Parsec2Meters;
        if (double.IsPositiveInfinity(distance))
        {
            throw new ArgumentOutOfRangeException(nameof(parallax), parallax,
                "The parallax is so small that its distance, 1/parallax parsecs, overflows a double.");
        }

        CatalogNumber = catalogNumber;
        SpectralType = spectralType ?? throw new ArgumentNullException(nameof(spectralType));
        VisualMagnitude = visualMagnitude;
        Epoch = epoch;
        _epochTdb = epoch.ToTDB();
        Parallax = parallax;
        EquatorialCoordinatesAtEpoch = equatorialCoordinatesAtEpoch;
        RightAscensionProperMotion = rightAscensionProperMotion;
        DeclinationProperMotion = declinationProperMotion;
        RightAscensionSigma = rightAscensionSigma;
        DeclinationSigma = declinationSigma;
        RightAscensionSigmaProperMotion = rightAscensionSigmaProperMotion;
        DeclinationSigmaProperMotion = declinationSigmaProperMotion;
        Distance = distance;
        _spaceVelocity = SpaceVelocity(equatorialCoordinatesAtEpoch, rightAscensionProperMotion, declinationProperMotion, distance);
    }

    /// <summary>
    /// The space velocity of the star, the derivative of its straight line (<see cref="GetEquatorialCoordinates"/>):
    /// r0 (μα* e_α + μδ e_δ) per Julian year of TDB, r0 the catalog distance, e_α = (−sin α0, cos α0, 0) and
    /// e_δ = (−sin δ0 cos α0, −sin δ0 sin α0, cos δ0), in m/s; constant, perpendicular to the catalog direction (#731).
    /// </summary>
    private static Vector3 SpaceVelocity(Equatorial catalog, double rightAscensionProperMotion, double declinationProperMotion, double distance)
    {
        double sinDec = System.Math.Sin(catalog.Declination);
        double cosDec = System.Math.Cos(catalog.Declination);
        double sinRa = System.Math.Sin(catalog.RightAscension);
        double cosRa = System.Math.Cos(catalog.RightAscension);
        var east = new Vector3(-sinRa, cosRa, 0.0);
        var north = new Vector3(-sinDec * cosRa, -sinDec * sinRa, cosDec);
        return (east * rightAscensionProperMotion + north * declinationProperMotion) * (distance / JulianYearSeconds);
    }

    /// <summary>
    /// Gets the catalog number of the star.
    /// </summary>
    public int CatalogNumber { get; }

    /// <summary>
    /// Gets the spectral type of the star.
    /// </summary>
    public string SpectralType { get; }

    /// <summary>
    /// Gets the visual magnitude of the star.
    /// </summary>
    public double VisualMagnitude { get; }

    /// <summary>
    /// Gets the parallax of the star in arcseconds.
    /// </summary>
    public double Parallax { get; }

    /// <summary>
    /// Gets the distance of the star from the solar system barycenter at the catalog epoch, in meters: 1/<see cref="Parallax"/>
    /// parsecs. At another epoch the distance is the length of its straight line (<see cref="GetEquatorialCoordinates"/>,
    /// #718), √(1 + (μ dt)²) times this one.
    /// </summary>
    public double Distance { get; }

    /// <summary>
    /// Gets the epoch time for the star: the catalog epoch, as given. The catalog motion counts the Julian years of TDB
    /// from it, whatever its time scale (#709).
    /// </summary>
    public Time Epoch { get; }

    /// <summary>
    /// Gets the right ascension and declination of the star in ICRF at the catalog epoch, in radians, as given.
    /// </summary>
    public Equatorial EquatorialCoordinatesAtEpoch { get; }

    /// <summary>
    /// Gets the proper motion in right ascension as a true angle, μα* = (dα/dt) cos δ, in radians per Julian year of TDB,
    /// the convention of Hipparcos and Gaia (#718; before 11.0.0 it was read as dα/dt, which nothing documented).
    /// </summary>
    public double RightAscensionProperMotion { get; }

    /// <summary>
    /// Gets the proper motion in declination, dδ/dt, in radians per Julian year of TDB.
    /// </summary>
    public double DeclinationProperMotion { get; }

    /// <summary>
    /// Gets the standard deviation of the right ascension at the catalog epoch as a true angle, σα* = σα cos δ, in radians.
    /// </summary>
    public double RightAscensionSigma { get; }

    /// <summary>
    /// Gets the standard deviation of the declination at the catalog epoch, in radians.
    /// </summary>
    public double DeclinationSigma { get; }

    /// <summary>
    /// Gets the standard deviation of the proper motion in right ascension as a true angle, σμα*, in radians per Julian
    /// year of TDB.
    /// </summary>
    public double RightAscensionSigmaProperMotion { get; }

    /// <summary>
    /// Gets the standard deviation of the proper motion in declination, in radians per Julian year of TDB.
    /// </summary>
    public double DeclinationSigmaProperMotion { get; }

    /// <summary>
    /// Gets a value indicating whether the star holds propagated states: true once <see cref="Propagate"/> or
    /// <c>Scenario.Simulate</c> stored them.
    /// </summary>
    /// <remarks>
    /// Before 11.0.0 it read <c>PropagationOutput</c>, which no code set, and was always false; that property, always null,
    /// is removed in 11.0.0 (R02 of #668).
    /// </remarks>
    public bool IsPropagated => Volatile.Read(ref _propagatedStates).Length > 0;

    /// <summary>
    /// The Julian years of TDB from the catalog epoch to <paramref name="epoch"/>, both converted to TDB, from the exact
    /// difference of their ticks rounded once to a double, as SOFA's <c>iauStarpm</c> and <c>iauPmsafe</c> take TDB dates
    /// (<c>external-lib/sofa-src/starpm.c</c> and <c>pmsafe.c</c>, Note 1, #709): the same instant gives the same position whatever its time scale, and a leap second between two UTC times
    /// counts. A time in TDB is used as it is, bit for bit.
    /// </summary>
    /// <remarks>
    /// Before 11.0.0 it was the difference of the two Julian dates, each rounded to a double near 2.45e6 (4.66e-10 day,
    /// about 40 µs) before the subtraction: off whole and half days from J2000 the star was that of an epoch up to about
    /// 20 µs away, half that resolution. It was also the difference of the two calendars, each in its own time scale
    /// (#709): an epoch in UTC moved a star of the catalog epoch of TDB by its proper motion over the 64 to 69 s of
    /// TDB − UTC (2.3e-7 relative at 0.1 rad/yr), and two UTC times skipped the leap seconds between them.
    /// </remarks>
    private double JulianYearsFromCatalogEpoch(Time epoch)
    {
        return (epoch.ToTDB().DateTime.Ticks - _epochTdb.DateTime.Ticks) / (double)TimeSpan.TicksPerDay / Time.JULIAN_YEAR;
    }

    /// <summary>
    /// Gets the equatorial coordinates of the star at a specific epoch: the star moved along a straight line at constant
    /// speed in ICRF, SOFA's <c>iauStarpm</c> with a radial velocity of 0 (<c>external-lib/sofa-src/starpm.c</c>, Note 5),
    /// over dt, the Julian years of TDB from the catalog epoch (both times converted to TDB, whatever their frames), from
    /// the exact difference of the ticks; the declination in [−π/2, π/2], the right ascension in [0, 2π), and the distance
    /// the length of the line, √(1 + (μ dt)²) times <see cref="Distance"/>, μ the total proper motion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With x = μα* dt and y = μδ dt (<see cref="RightAscensionProperMotion"/> and <see cref="DeclinationProperMotion"/>,
    /// true angles), the position in catalog distances is u0 + x e_α + y e_δ, u0 the catalog direction, e_α =
    /// (−sin α0, cos α0, 0) and e_δ = (−sin δ0 cos α0, −sin δ0 sin α0, cos δ0) its unit tangents: with e = cos δ0 − y sin δ0,
    /// α = α0 + atan2(x, e) (the angle of the equatorial components e (cos α0, sin α0) + x (−sin α0, cos α0)),
    /// δ = atan2(sin δ0 + y cos δ0, √(x² + e²)), and the length √(1 + x² + y²). The form holds at any
    /// catalog declination, a pole included (it divides by no cos δ0), and crosses a pole by itself; it is geometric at
    /// any speed. A star without proper motion, and every star at its catalog epoch, is its catalog (bit for bit), its
    /// right ascension reduced to [0, 2π) and a declination past a pole reflected through it, π − d (−π − d past the south
    /// pole) with the right ascension turned by π.
    /// </para>
    /// <para>
    /// Its departure from <c>iauStarpm</c> (rv = 0) is that function's light-time and relativistic terms of the observed
    /// place, which grow as dt⁴: for Barnard's star 2.4e-14 rad after 100 years and 2.4e-10 rad after 1000 (after 10
    /// years both round the right ascension to the same double), the distance 7.6e-8 relative per decade (<c>iauStarpv</c>'s inertial radial velocity −v_t²/(2c) for an observed rv of
    /// 0), measured by <c>tools/sofa_reference/starpm_ref.c</c>. No radial velocity is taken: Barnard's −110.47 km/s
    /// (Gaia DR3) would move it by 64 mas after 10 years and 6.5″ after 100 (see <c>docs/reference/stars.md</c>).
    /// </para>
    /// <para>
    /// Before 11.0.0 (#718) the angles moved linearly, α0 + μα dt and δ0 + μδ dt, μα read as dα/dt, and the distance was
    /// the catalog's: a catalog star given its μα* moved cos δ0 times too fast in right ascension (77.9 times for Polaris),
    /// and given dα/dt it left the straight line by the curvature of its angles (Barnard's star 0.33 mas after 10 years,
    /// 34 mas after 100, 9.4″ after 1000; 1.26° near a pole after 1000 years). Before 11.0.0 (#709) the declination was
    /// reduced modulo π/2 and a right ascension just below 0 returned as 2π.
    /// </para>
    /// </remarks>
    /// <param name="epoch">The epoch time for the coordinates.</param>
    /// <returns>The equatorial coordinates at the specified epoch.</returns>
    public Equatorial GetEquatorialCoordinates(Time epoch)
    {
        var dt = JulianYearsFromCatalogEpoch(epoch);
        double x = dt * RightAscensionProperMotion;
        double y = dt * DeclinationProperMotion;
        double dec = EquatorialCoordinatesAtEpoch.Declination;
        double ra = EquatorialCoordinatesAtEpoch.RightAscension;
        double distance = Distance;
        if (x != 0.0 || y != 0.0)
        {
            // The straight line u0 + x e_α + y e_δ: its components along (cos α0, sin α0, 0), e_α and the pole are e, x and
            // sin δ0 + y cos δ0. The right ascension is the angle of its equatorial components, α0 + atan2(x, e) without a
            // sum with α0, so that a catalog right ascension outside [0, 2π) is read through its own sine and cosine and
            // not reduced by the double of 2π (k (2π − fl(2π)) away)
            double sinDec = System.Math.Sin(dec);
            double cosDec = System.Math.Cos(dec);
            double sinRa = System.Math.Sin(ra);
            double cosRa = System.Math.Cos(ra);
            double e = cosDec - y * sinDec;
            ra = System.Math.Atan2(sinRa * e + cosRa * x, cosRa * e - sinRa * x);
            dec = System.Math.Atan2(sinDec + y * cosDec, System.Math.Sqrt(x * x + e * e));
            distance = Distance * System.Math.Sqrt(1.0 + x * x + y * y);
        }
        else if (System.Math.Abs(dec) > Constants.HalfPi)
        {
            // A catalog declination past a pole: in (−π, π], then reflected through the pole it passed, the right ascension
            // turned by π (cos(π − d) = −cos d, sin(π − d) = sin d)
            dec = System.Math.IEEERemainder(dec, Constants.TwoPi);
            if (dec > Constants.HalfPi)
            {
                dec = Constants.PI - dec;
                ra += Constants.PI;
            }
            else if (dec < -Constants.HalfPi)
            {
                dec = -Constants.PI - dec;
                ra += Constants.PI;
            }
        }

        ra %= Constants.TwoPi;
        if (ra < 0.0)
        {
            // A right ascension just below 0 (above −ulp(2π)/2) rounds to 2π, the end [0, 2π) excludes: it is the direction
            // of 0
            ra += Constants.TwoPi;
            if (ra == Constants.TwoPi)
            {
                ra = 0.0;
            }
        }

        return Equatorial.FromRightAscensionDeclination(ra, dec, distance, epoch);
    }

    /// <summary>
    /// Gets the sigma value for right ascension at a specific epoch, √(σα*² + (dt σμα*)²), a true angle in radians (#718),
    /// dt the Julian years of TDB from the catalog epoch, from the exact difference of the ticks.
    /// </summary>
    /// <remarks>
    /// A standard deviation, not an angle: it is not reduced. Before 11.0.0 it was reduced modulo 2π (#709).
    /// </remarks>
    /// <param name="epoch">The epoch time for the sigma value.</param>
    /// <returns>The sigma value for right ascension at the specified epoch.</returns>
    public double GetRightAscensionSigma(Time epoch)
    {
        var dt = JulianYearsFromCatalogEpoch(epoch);
        return System.Math.Sqrt(System.Math.Pow(RightAscensionSigma, 2) + System.Math.Pow(dt * RightAscensionSigmaProperMotion, 2));
    }

    /// <summary>
    /// Gets the sigma value for declination at a specific epoch, √(σδ² + (dt σμδ)²), dt the Julian years of TDB from the
    /// catalog epoch, from the exact difference of the ticks.
    /// </summary>
    /// <remarks>
    /// A standard deviation, not an angle: it is not reduced. Before 11.0.0 it was reduced modulo π/2 (#709).
    /// </remarks>
    /// <param name="epoch">The epoch time for the sigma value.</param>
    /// <returns>The sigma value for declination at the specified epoch.</returns>
    public double GetDeclinationSigma(Time epoch)
    {
        var dt = JulianYearsFromCatalogEpoch(epoch);
        return System.Math.Sqrt(System.Math.Pow(DeclinationSigma, 2) + System.Math.Pow(dt * DeclinationSigmaProperMotion, 2));
    }

    /// <summary>
    /// Computes the geometric state relative to a reference body, in ICRF: the star's position from the solar system
    /// barycenter (<see cref="GetEquatorialCoordinates"/>) and its space velocity, the derivative of its straight line,
    /// r0 (μα* e_α + μδ e_δ) per Julian year of TDB, constant (#731), minus the reference body's state from the barycenter.
    /// </summary>
    /// <remarks>
    /// Before 11.0.0 the velocity of a star was zero, its position moving (#731): from the barycenter a zero velocity, from
    /// another body the opposite of that body's velocity.
    /// </remarks>
    public override StateVector GetGeometricStateRelativeTo(in Time epoch, CelestialItem referenceBody)
    {
        var starSsb = GetEquatorialCoordinates(epoch).ToCartesian();
        if (referenceBody.NaifId == 0)
            return new StateVector(starSsb, _spaceVelocity, referenceBody, epoch, Frame.ICRF);

        var refFromSsb = referenceBody.GetGeometricStateFromICRF(epoch).ToStateVector();
        return new StateVector(
            starSsb - refFromSsb.Position, _spaceVelocity - refFromSsb.Velocity,
            referenceBody, epoch, Frame.ICRF);
    }

    /// <summary>
    /// Get geometric state from SSB in ICRF: the position of <see cref="GetEquatorialCoordinates"/> and the space velocity
    /// (#731).
    /// </summary>
    internal override OrbitalParameters.OrbitalParameters GetGeometricStateFromICRF(in Time date)
    {
        var position = GetEquatorialCoordinates(date).ToCartesian();
        return new StateVector(position, _spaceVelocity, Barycenters.SOLAR_SYSTEM_BARYCENTER, date, Frame.ICRF);
    }

    /// <summary>
    /// The star seen from <paramref name="observer"/> at <paramref name="epoch"/>: a direction, its catalog position moved
    /// by its proper motion to the epoch minus the observer's position from the solar system barycenter at the epoch; its
    /// velocity the star's space velocity minus the observer's (before 11.0.0 the opposite of the observer's alone, #731).
    /// </summary>
    /// <remarks>
    /// A catalog position at an epoch is the direction of the light that reaches the barycenter then, as SOFA's
    /// <c>iauPmsafe</c> reads a catalog (confirmed by S. Guillet, 2026-11-11), so no light time is applied, whatever the
    /// correction: <see cref="Aberration.LT"/>, <see cref="Aberration.CN"/>, <see cref="Aberration.XLT"/> and
    /// <see cref="Aberration.XCN"/> read as <see cref="Aberration.None"/>, their "+S" forms as the stellar aberration
    /// alone (<c>stelab</c>, <c>stlabx</c> for transmission), as the field-of-view search (#517) and the occultation search
    /// read a star (#533). Before 11.0.0 the star was read at t − lt, its light time, 65 years at 20 pc: its proper
    /// motion over that time moved its direction (6.2″ for 0.1″/yr at 20 pc).
    /// </remarks>
    public override StateVector GetEphemeris(in Time epoch, ILocalizable observer, Frame frame, Aberration aberration)
    {
        var ssb = Barycenters.SOLAR_SYSTEM_BARYCENTER;
        var starSsb = GetGeometricStateRelativeTo(epoch, ssb);
        var observerSsb = observer.GetGeometricStateRelativeTo(epoch, ssb);
        var relative = new StateVector(
            starSsb.Position - observerSsb.Position,
            starSsb.Velocity - observerSsb.Velocity,
            observer, epoch, Frame.ICRF);

        if (aberration is Aberration.LTS or Aberration.CNS or Aberration.XLTS or Aberration.XCNS)
        {
            var (position, velocity) = ApplyStellarAberration(relative.Position, relative.Velocity, observerSsb.Velocity, observer, epoch, ssb,
                aberration is Aberration.XLTS or Aberration.XCNS);
            relative = new StateVector(position, velocity, observer, epoch, Frame.ICRF);
        }

        return relative.ToFrame(frame);
    }

    /// <summary>
    /// Write star ephemeris from propagated states: those of its last propagation, their positions and the star's space
    /// velocity (before 11.0.0 a zero velocity, #731), through the native writer (<see cref="SpiceAPI.WriteEphemeris"/>): a file it replaces that <see cref="SpiceAPI.LoadKernels(FileSystemInfo)"/> loaded is unloaded
    /// first, and the new one is left unloaded (#706).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="outputFile"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The star stores fewer than three states (never propagated, or a
    /// propagation of fewer than three steps): the native writer writes three or more, and nothing is written (decision 81
    /// of #668, decided by S. Guillet, 2026-11-23; #799(e): before 11.0.0, <see cref="ArgumentException"/>, and before #706
    /// a C++ exception that crossed P/Invoke and ended the process). See <see cref="SpiceAPI.WriteEphemeris"/> for the
    /// others.</exception>
    public override void WriteEphemeris(FileInfo outputFile)
    {
        ArgumentNullException.ThrowIfNull(outputFile);
        var states = Volatile.Read(ref _propagatedStates);
        if (states.Length < 3)
            throw new InvalidOperationException(
                $"Star {Name} stores {states.Length} states: an SPK of the native writer needs three or more; propagate it over three steps or more.");
        SpiceAPI.Instance.WriteEphemeris(outputFile, NaifId, states);
    }

    /// <summary>
    /// Propagates the star.
    /// </summary>
    /// <param name="timeWindow">The time window for propagation.</param>
    /// <param name="stepSize">The step size for propagation, positive.</param>
    /// <remarks>
    /// The states of the propagation replace those the star held, as <c>Spacecraft.Propagate</c> replaces the states of a
    /// spacecraft: a state at the start of the window and every <paramref name="stepSize"/> up to its end, published at once
    /// when they are all computed, each the position of <see cref="GetEquatorialCoordinates"/> and the space velocity of
    /// <see cref="GetGeometricStateRelativeTo"/> (before 11.0.0 a zero velocity, #731). Before 11.0.0 each propagation added its states to those of the previous ones (the
    /// first state of an epoch kept), so that <see cref="WriteEphemeris"/> wrote every window ever propagated, the gaps
    /// between them interpolated. Synchronous: <c>PropagateAsync</c>, a bare <c>Task.Run</c> of this method, is removed
    /// in 11.0.0 (R28 of #668).
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stepSize"/> is zero or negative, the rule of every
    /// step of the library (#674); nothing is stored. Before 11.0.0 the call never returned (a negative step also stored
    /// states without bound).</exception>
    public void Propagate(Window timeWindow, TimeSpan stepSize)
    {
        PropagatorBase.RequirePositiveStep(stepSize, nameof(stepSize));
        var states = new List<StateVector>();
        for (Time epoch = timeWindow.StartDate; epoch <= timeWindow.EndDate; epoch += stepSize)
        {
            var position = GetEquatorialCoordinates(epoch).ToCartesian();
            states.Add(new StateVector(position, _spaceVelocity, Barycenters.SOLAR_SYSTEM_BARYCENTER, epoch, Frame.ICRF));
        }

        Volatile.Write(ref _propagatedStates, states.ToArray());
    }
}