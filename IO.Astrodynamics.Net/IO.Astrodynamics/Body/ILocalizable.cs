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
        /// <returns>An enumerable of orbital parameters over the specified time window.</returns>
        IEnumerable<OrbitalParameters.OrbitalParameters> GetEphemeris(in Window searchWindow, ILocalizable observer, Frame frame, Aberration aberration, in TimeSpan stepSize);

        OrbitalParameters.OrbitalParameters GetGeometricStateFromICRF(in Time date);
        
        /// <summary>
        /// Gets the ephemeris data for the celestial object at a specific epoch.
        /// </summary>
        /// <param name="epoch">The epoch time for the ephemeris data.</param>
        /// <param name="observer">The observer of the celestial object.</param>
        /// <param name="frame">The reference frame for the ephemeris data.</param>
        /// <param name="aberration">The aberration correction to apply.</param>
        /// <returns>The orbital parameters at the specified epoch.</returns>
        OrbitalParameters.OrbitalParameters GetEphemeris(in Time epoch, ILocalizable observer, Frame frame, Aberration aberration);

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

        /// <summary>
        /// Finds time windows based on a distance constraint.
        /// </summary>
        /// <param name="searchWindow">The time window to search within.</param>
        /// <param name="observer">The observer of the celestial object.</param>
        /// <param name="relationalOperator">The relational operator for the distance constraint.</param>
        /// <param name="value">The distance value for the constraint.</param>
        /// <param name="aberration">The aberration correction to apply.</param>
        /// <param name="stepSize">The step size for the search.</param>
        /// <returns>An enumerable of time windows that satisfy the distance constraint.</returns>
        IEnumerable<Window> FindWindowsOnDistanceConstraint(in Window searchWindow, ILocalizable observer, RelationnalOperator relationalOperator, double value,
            Aberration aberration, in TimeSpan stepSize);

        /// <summary>
        /// Finds time windows based on an occultation constraint.
        /// </summary>
        /// <param name="searchWindow">The time window to search within.</param>
        /// <param name="targetShape">The shape of the target celestial object.</param>
        /// <param name="frontBody">The front body causing the occultation.</param>
        /// <param name="frontShape">The shape of the front body.</param>
        /// <param name="occultationType">The type of occultation.</param>
        /// <param name="aberration">The aberration correction to apply.</param>
        /// <param name="stepSize">The step size for the search.</param>
        /// <param name="observer">The observer of the celestial object.</param>
        /// <returns>An enumerable of time windows that satisfy the occultation constraint.</returns>
        IEnumerable<Window> FindWindowsOnOccultationConstraint(in Window searchWindow, ILocalizable observer, ShapeType targetShape, INaifObject frontBody,
            ShapeType frontShape, OccultationType occultationType, Aberration aberration, in TimeSpan stepSize);

        /// <summary>
        /// Finds time windows based on a coordinate constraint.
        /// </summary>
        /// <param name="searchWindow">The time window to search within.</param>
        /// <param name="frame">The reference frame for the coordinate constraint.</param>
        /// <param name="coordinateSystem">The coordinate system for the constraint.</param>
        /// <param name="coordinate">The coordinate for the constraint.</param>
        /// <param name="relationalOperator">The relational operator for the coordinate constraint.</param>
        /// <param name="value">The coordinate value for the constraint.</param>
        /// <param name="adjustValue">The adjustment value for the constraint.</param>
        /// <param name="aberration">The aberration correction to apply.</param>
        /// <param name="stepSize">The step size for the search.</param>
        /// <param name="observer">The observer of the celestial object.</param>
        /// <returns>An enumerable of time windows that satisfy the coordinate constraint.</returns>
        public IEnumerable<Window> FindWindowsOnCoordinateConstraint(in Window searchWindow, ILocalizable observer, Frame frame, CoordinateSystem coordinateSystem,
            Coordinate coordinate, RelationnalOperator relationalOperator, double value, double adjustValue, Aberration aberration, in TimeSpan stepSize);
    }
}