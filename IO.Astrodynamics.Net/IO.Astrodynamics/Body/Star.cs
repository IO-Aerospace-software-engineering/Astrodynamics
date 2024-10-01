using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using IO.Astrodynamics.Coordinates;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.OrbitalParameters;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body;

/// <summary>
/// Represents a star in the celestial system.
/// </summary>
public class Star : CelestialBody
{
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
    /// Gets the epoch time for the star.
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
    /// Gets the directory for propagation output.
    /// </summary>
    public DirectoryInfo PropagationOutput { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the star has been propagated.
    /// </summary>
    public bool IsPropagated => PropagationOutput != null;

    /// <summary>
    /// Gets the equatorial coordinates of the star at a specific epoch.
    /// </summary>
    /// <param name="epoch">The epoch time for the coordinates.</param>
    /// <returns>The equatorial coordinates at the specified epoch.</returns>
    public Equatorial GetEquatorialCoordinates(Time epoch)
    {
        var dt = (epoch.ToJulianDate() - Epoch.ToJulianDate()) / Time.JULIAN_YEAR;
        var dec = (EquatorialCoordinatesAtEpoch.Declination + dt * DeclinationProperMotion) % Constants.PI2;

        var ra = (EquatorialCoordinatesAtEpoch.RightAscension + dt * RightAscensionProperMotion) % Constants._2PI;
        if (ra < 0.0)
        {
            ra += Constants._2PI;
        }

        return new Equatorial(dec, ra, Distance, epoch);
    }

    /// <summary>
    /// Gets the sigma value for right ascension at a specific epoch.
    /// </summary>
    /// <param name="epoch">The epoch time for the sigma value.</param>
    /// <returns>The sigma value for right ascension at the specified epoch.</returns>
    public double GetRightAscensionSigma(Time epoch)
    {
        var dt = (epoch.ToJulianDate() - Epoch.ToJulianDate()) / Time.JULIAN_YEAR;
        return System.Math.Sqrt(System.Math.Pow(RightAscensionSigma, 2) + System.Math.Pow(dt * RightAscensionSigmaProperMotion, 2)) % Constants._2PI;
    }

    /// <summary>
    /// Gets the sigma value for declination at a specific epoch.
    /// </summary>
    /// <param name="epoch">The epoch time for the sigma value.</param>
    /// <returns>The sigma value for declination at the specified epoch.</returns>
    public double GetDeclinationSigma(Time epoch)
    {
        var dt = (epoch.ToJulianDate() - Epoch.ToJulianDate()) / Time.JULIAN_YEAR;
        return System.Math.Sqrt(System.Math.Pow(DeclinationSigma, 2) + System.Math.Pow(dt * DeclinationSigmaProperMotion, 2)) % Constants.PI2;
    }

    /// <summary>
    /// Propagates the star asynchronously.
    /// </summary>
    /// <param name="timeWindow">The time window for propagation.</param>
    /// <param name="stepSize">The step size for propagation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task PropagateAsync(Window timeWindow, TimeSpan stepSize)
    {
        return Task.Run(() =>
        {
            List<StateVector> svs = new List<StateVector>();
            for (Time epoch = timeWindow.StartDate; epoch <= timeWindow.EndDate; epoch += stepSize)
            {
                var position = GetEquatorialCoordinates(epoch).ToCartesian();
                _stateVectorsRelativeToICRF.Add(epoch,new StateVector(position, Vector3.Zero, Barycenters.SOLAR_SYSTEM_BARYCENTER, epoch, Frame.ICRF));
            }
        });
    }

}