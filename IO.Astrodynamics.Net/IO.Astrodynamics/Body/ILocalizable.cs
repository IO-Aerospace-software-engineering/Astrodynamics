using System;
using System.Collections.Generic;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body
{
    /// <summary>
    /// Represents an interface for localizable celestial objects.
    /// </summary>
    public interface ILocalizable : INaifObject
    {
        /// <summary>
        /// Gets the ephemeris data for the celestial object over a specified time window.
        /// </summary>
        /// <param name="searchWindow">The time window to search within.</param>
        /// <param name="observer">The observer of the celestial object.</param>
        /// <param name="frame">The reference frame for the ephemeris data.</param>
        /// <param name="aberration">The aberration correction to apply.</param>
        /// <param name="stepSize">The step size for the ephemeris data.</param>
        /// <returns>The state vectors over the specified time window (before 11.0.0, typed as orbital parameters: R20 of
        /// #668).</returns>
        IEnumerable<OrbitalParameters.StateVector> GetEphemeris(in Window searchWindow, ILocalizable observer, Frame frame, Aberration aberration, in TimeSpan stepSize);

        /// <summary>
        /// Computes the geometric state of this object relative to a reference body in ICRF.
        /// Each type implements this efficiently to avoid SSB round-trips.
        /// </summary>
        /// <param name="epoch">The epoch time.</param>
        /// <param name="referenceBody">The reference body to compute the state relative to.</param>
        /// <returns>A StateVector relative to the reference body in ICRF.</returns>
        OrbitalParameters.StateVector GetGeometricStateRelativeTo(in Time epoch, CelestialItem referenceBody);

        /// <summary>
        /// Gets the ephemeris data for the celestial object at a specific epoch.
        /// </summary>
        /// <param name="epoch">The epoch time for the ephemeris data.</param>
        /// <param name="observer">The observer of the celestial object.</param>
        /// <param name="frame">The reference frame for the ephemeris data.</param>
        /// <param name="aberration">The aberration correction to apply.</param>
        /// <returns>The state vector at the specified epoch (before 11.0.0, typed as orbital parameters: R20 of #668).</returns>
        OrbitalParameters.StateVector GetEphemeris(in Time epoch, ILocalizable observer, Frame frame, Aberration aberration);

        /// <summary>
        /// Computes the angular separation between two target celestial objects at a specific epoch.
        /// </summary>
        /// <param name="epoch">The epoch time for the computation.</param>
        /// <param name="target1">The first target celestial object.</param>
        /// <param name="target2">The second target celestial object.</param>
        /// <param name="aberration">The aberration correction to apply.</param>
        /// <returns>The angular separation between the two targets.</returns>
        double AngularSeparation(in Time epoch, ILocalizable target1, ILocalizable target2, Aberration aberration);

        /// <summary>
        /// Gets the initial orbital parameters of the celestial object.
        /// </summary>
        OrbitalParameters.OrbitalParameters InitialOrbitalParameters { get; }

        /// <summary>
        /// Gets a value indicating whether this object's ephemeris is backed by SPICE kernels.
        /// When true, SPICE can compute ephemeris directly for this object (enabling native FindWindows, etc.).
        /// </summary>
        bool IsSpiceBacked { get; }

        /// <summary>
        /// Gets the centers of motion for the celestial object.
        /// </summary>
        /// <returns>An enumerable of localizable centers of motion.</returns>
        IEnumerable<ILocalizable> GetCentersOfMotion();

        /// <summary>
        /// Gets the standard gravitational parameter (GM) of the celestial object.
        /// </summary>
        double GM { get; }

        /// <summary>
        /// Gets the mass of the celestial object.
        /// </summary>
        double Mass { get; }
    }
}