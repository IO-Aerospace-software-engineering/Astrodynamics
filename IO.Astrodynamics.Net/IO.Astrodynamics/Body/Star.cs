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
/// Its position at an epoch is that of its catalog plus the proper motions times dt, the Julian years of TDB from the
/// catalog epoch (both times converted to TDB, as SOFA's <c>iauStarpm</c> takes TDB dates and its proper motions in radians
/// per TDB Julian year, <c>external-lib/sofa-src/starpm.c</c>, Notes 1 and 2, #709): the angles move
/// linearly, the declination reduced to [−π/2, π/2] across a pole, and the sigmas grow as √(σ² + (dt σμ)²).
/// </remarks>
public class Star : CelestialBody
{
    // The states of the last propagation, in the order of their epochs; replaced as a whole by each propagation
    private StateVector[] _propagatedStates = Array.Empty<StateVector>();

    // The catalog epoch in TDB, the time scale of the catalog motion's dt (#709)
    private readonly Time _epochTdb;
    /// <summary>
    /// Initializes a new instance of the <see cref="Star"/> class.
    /// </summary>
    /// <param name="catalogNumber">The catalog number of the star.</param>
    /// <param name="name">The name of the star.</param>
    /// <param name="mass">The mass of the star.</param>
    /// <param name="spectralType">The spectral type of the star.</param>
    /// <param name="visualMagnitude">The visual magnitude of the star.</param>
    /// <param name="parallax">The parallax of the star in arcseconds.</param>
    /// <param name="equatorialCoordinatesAtEpoch">The equatorial coordinates of the star at the given epoch.</param>
    /// <param name="declinationProperMotion">The proper motion in declination.</param>
    /// <param name="rightAscensionProperMotion">The proper motion in right ascension.</param>
    /// <param name="declinationSigma">The sigma value for declination.</param>
    /// <param name="rightAscensionSigma">The sigma value for right ascension.</param>
    /// <param name="declinationSigmaProperMotion">The sigma value for proper motion in declination.</param>
    /// <param name="rightAscensionSigmaProperMotion">The sigma value for proper motion in right ascension.</param>
    /// <param name="epoch">The epoch time for the star.</param>
    public Star(int catalogNumber, string name, double mass, string spectralType, double visualMagnitude, double parallax, Equatorial equatorialCoordinatesAtEpoch,
        double declinationProperMotion, double rightAscensionProperMotion, double declinationSigma, double rightAscensionSigma, double declinationSigmaProperMotion,
        double rightAscensionSigmaProperMotion, Time epoch) : base((int)1E+09 + catalogNumber, name, mass)
    {
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
        Distance = (1 / Parallax) * Constants.Parsec2Meters;
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
    /// Gets the distance to the star in meters.
    /// </summary>
    public double Distance { get; }

    /// <summary>
    /// Gets the epoch time for the star: the catalog epoch, as given. The catalog motion counts the Julian years of TDB
    /// from it, whatever its time frame (#709).
    /// </summary>
    public Time Epoch { get; }

    /// <summary>
    /// Gets the equatorial coordinates of the star at the given epoch.
    /// </summary>
    public Equatorial EquatorialCoordinatesAtEpoch { get; }

    /// <summary>
    /// Gets the proper motion in right ascension.
    /// </summary>
    public double RightAscensionProperMotion { get; }

    /// <summary>
    /// Gets the proper motion in declination.
    /// </summary>
    public double DeclinationProperMotion { get; }

    /// <summary>
    /// Gets the sigma value for right ascension.
    /// </summary>
    public double RightAscensionSigma { get; }

    /// <summary>
    /// Gets the sigma value for declination.
    /// </summary>
    public double DeclinationSigma { get; }

    /// <summary>
    /// Gets the sigma value for proper motion in right ascension.
    /// </summary>
    public double RightAscensionSigmaProperMotion { get; }

    /// <summary>
    /// Gets the sigma value for proper motion in declination.
    /// </summary>
    public double DeclinationSigmaProperMotion { get; }

    /// <summary>
    /// Always null: the directory of an older design that wrote the propagation to files. A star keeps its propagated
    /// states in memory and writes them only through <see cref="WriteEphemeris"/>, to the file its caller gives.
    /// Obsolete since 11.0.0: read <see cref="IsPropagated"/>, and write the states with <see cref="WriteEphemeris"/>.
    /// </summary>
    [Obsolete("Always null: a star keeps its propagated states in memory. Read IsPropagated to know whether it holds them, and write them with WriteEphemeris.")]
    public DirectoryInfo PropagationOutput { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the star holds propagated states: true once <see cref="Propagate"/>,
    /// <see cref="PropagateAsync"/> or <c>Scenario.SimulateAsync</c> stored them.
    /// </summary>
    /// <remarks>
    /// Before 11.0.0 it read <see cref="PropagationOutput"/>, which no code sets, and was always false.
    /// </remarks>
    public bool IsPropagated => Volatile.Read(ref _propagatedStates).Length > 0;

    /// <summary>
    /// The Julian years of TDB from the catalog epoch to <paramref name="epoch"/>, both converted to TDB, from the exact
    /// difference of their ticks rounded once to a double, as SOFA's <c>iauStarpm</c> and <c>iauPmsafe</c> take TDB dates
    /// (<c>external-lib/sofa-src/starpm.c</c> and <c>pmsafe.c</c>, Note 1, #709): the same instant gives the same position whatever its time frame, and a leap second between two UTC times
    /// counts. A time in TDB is used as it is, bit for bit.
    /// </summary>
    /// <remarks>
    /// Before 11.0.0 it was the difference of the two Julian dates, each rounded to a double near 2.45e6 (4.66e-10 day,
    /// about 40 µs) before the subtraction: off whole and half days from J2000 the star was that of an epoch up to about
    /// 20 µs away, half that resolution. It was also the difference of the two calendars, each in its own time frame
    /// (#709): an epoch in UTC moved a star of the catalog epoch of TDB by its proper motion over the 64 to 69 s of
    /// TDB − UTC (2.3e-7 relative at 0.1 rad/yr), and two UTC times skipped the leap seconds between them.
    /// </remarks>
    private double JulianYearsFromCatalogEpoch(Time epoch)
    {
        return (epoch.ToTDB().DateTime.Ticks - _epochTdb.DateTime.Ticks) / (double)TimeSpan.TicksPerDay / Time.JULIAN_YEAR;
    }

    /// <summary>
    /// Gets the equatorial coordinates of the star at a specific epoch: those of the catalog epoch plus the proper motions
    /// times dt, the Julian years of TDB from the catalog epoch (both times converted to TDB, whatever their frames), from
    /// the exact difference of the ticks; the declination reduced to [−π/2, π/2] and the right ascension to [0, 2π), a
    /// pole crossed.
    /// </summary>
    /// <remarks>
    /// The angles of the linear model, d = δ0 + μδ dt and a = α0 + μα dt, are reduced to the pair that points where
    /// (cos d cos a, cos d sin a, sin d) points: when |d| exceeds π/2, d is reduced to (−π, π], and a declination past a
    /// pole becomes π − d (−π − d past the south pole) with the right ascension turned by π. A declination within
    /// [−π/2, π/2] is used as it is. The right ascension is then reduced modulo 2π, a value just below 0 whose sum with 2π
    /// rounds to 2π returned as 0, the same direction. Before 11.0.0 the declination was reduced modulo π/2, so that a
    /// star whose declination reached or passed a pole jumped toward the equator, by up to π/2 in direction, and a star at
    /// a pole read the equator (#709), and such a right ascension was returned as 2π.
    /// </remarks>
    /// <param name="epoch">The epoch time for the coordinates.</param>
    /// <returns>The equatorial coordinates at the specified epoch.</returns>
    public Equatorial GetEquatorialCoordinates(Time epoch)
    {
        var dt = JulianYearsFromCatalogEpoch(epoch);
        var dec = EquatorialCoordinatesAtEpoch.Declination + dt * DeclinationProperMotion;
        var ra = EquatorialCoordinatesAtEpoch.RightAscension + dt * RightAscensionProperMotion;
        if (System.Math.Abs(dec) > Constants.PI2)
        {
            // Past a pole: the declination in (−π, π], then reflected through the pole it passed, the right ascension
            // turned by π (cos(π − d) = −cos d, sin(π − d) = sin d)
            dec = System.Math.IEEERemainder(dec, Constants._2PI);
            if (dec > Constants.PI2)
            {
                dec = Constants.PI - dec;
                ra += Constants.PI;
            }
            else if (dec < -Constants.PI2)
            {
                dec = -Constants.PI - dec;
                ra += Constants.PI;
            }
        }

        ra %= Constants._2PI;
        if (ra < 0.0)
        {
            // A right ascension just below 0 (above −ulp(2π)/2) rounds to 2π, the end [0, 2π) excludes: it is the direction
            // of 0
            ra += Constants._2PI;
            if (ra == Constants._2PI)
            {
                ra = 0.0;
            }
        }

        return new Equatorial(dec, ra, Distance, epoch);
    }

    /// <summary>
    /// Gets the sigma value for right ascension at a specific epoch, √(σα² + (dt σμα)²), dt the Julian years of TDB from
    /// the catalog epoch, from the exact difference of the ticks.
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
    /// Computes the geometric state relative to a reference body.
    /// Stars use equatorial coordinates (inherently SSB-relative), then chain to reference body.
    /// </summary>
    public override StateVector GetGeometricStateRelativeTo(in Time epoch, CelestialItem referenceBody)
    {
        var starSsb = GetEquatorialCoordinates(epoch).ToCartesian();
        if (referenceBody.NaifId == 0)
            return new StateVector(starSsb, Vector3.Zero, referenceBody, epoch, Frame.ICRF);

        var refFromSsb = referenceBody.GetGeometricStateFromICRF(epoch).ToStateVector();
        return new StateVector(
            starSsb - refFromSsb.Position, refFromSsb.Velocity.Inverse(),
            referenceBody, epoch, Frame.ICRF);
    }

    /// <summary>
    /// Get geometric state from SSB in ICRF. Computes directly from equatorial coordinates.
    /// </summary>
    internal override OrbitalParameters.OrbitalParameters GetGeometricStateFromICRF(in Time date)
    {
        var position = GetEquatorialCoordinates(date).ToCartesian();
        return new StateVector(position, Vector3.Zero, Barycenters.SOLAR_SYSTEM_BARYCENTER, date, Frame.ICRF);
    }

    /// <summary>
    /// The star seen from <paramref name="observer"/> at <paramref name="epoch"/>: a direction, its catalog position moved
    /// by its proper motion to the epoch minus the observer's position from the solar system barycenter at the epoch.
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
    public override OrbitalParameters.OrbitalParameters GetEphemeris(in Time epoch, ILocalizable observer, Frame frame, Aberration aberration)
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
    /// Write star ephemeris from propagated states: those of its last propagation, through the native writer
    /// (<see cref="SpiceAPI.WriteEphemeris"/>): a file it replaces that <see cref="SpiceAPI.LoadKernels"/> loaded is unloaded
    /// first, and the new one is left unloaded (#706).
    /// </summary>
    /// <exception cref="ArgumentException">The last propagation stored fewer than three states: the native writer writes three
    /// or more (#706: before, a C++ exception crossed P/Invoke and ended the process). See
    /// <see cref="SpiceAPI.WriteEphemeris"/> for the others.</exception>
    public override void WriteEphemeris(FileInfo outputFile)
    {
        SpiceAPI.Instance.WriteEphemeris(outputFile, NaifId, Volatile.Read(ref _propagatedStates));
    }

    /// <summary>
    /// Propagates the star asynchronously.
    /// </summary>
    /// <param name="timeWindow">The time window for propagation.</param>
    /// <param name="stepSize">The step size for propagation, positive.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>The states of the propagation replace those the star held, see <see cref="Propagate"/>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="stepSize"/> is zero or negative: thrown by the call
    /// itself, before any task is created, and nothing is stored. Before 11.0.0 the task never ended (a negative step also
    /// stored states without bound).</exception>
    public Task PropagateAsync(Window timeWindow, TimeSpan stepSize)
    {
        PropagatorBase.RequirePositiveStep(stepSize, nameof(stepSize));
        return Task.Run(() =>
        {
            Propagate(timeWindow, stepSize);
        });
    }

    /// <summary>
    /// Propagates the star.
    /// </summary>
    /// <param name="timeWindow">The time window for propagation.</param>
    /// <param name="stepSize">The step size for propagation, positive.</param>
    /// <remarks>
    /// The states of the propagation replace those the star held, as <c>Spacecraft.Propagate</c> replaces the states of a
    /// spacecraft: a state at the start of the window and every <paramref name="stepSize"/> up to its end, published at once
    /// when they are all computed. Before 11.0.0 each propagation added its states to those of the previous ones (the
    /// first state of an epoch kept), so that <see cref="WriteEphemeris"/> wrote every window ever propagated, the gaps
    /// between them interpolated.
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
            states.Add(new StateVector(position, Vector3.Zero, Barycenters.SOLAR_SYSTEM_BARYCENTER, epoch, Frame.ICRF));
        }

        Volatile.Write(ref _propagatedStates, states.ToArray());
    }
}