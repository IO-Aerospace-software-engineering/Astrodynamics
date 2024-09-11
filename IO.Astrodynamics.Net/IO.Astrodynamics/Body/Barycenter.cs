using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body
{
    /// <summary>
    /// Represents a barycenter, which is the center of mass of two or more bodies that are orbiting each other.
    /// </summary>
    public class Barycenter : CelestialItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Barycenter"/> class with the specified NAIF ID and the default epoch (J2000TDB).
        /// </summary>
        /// <param name="naifId">The NAIF ID of the barycenter.</param>
        public Barycenter(int naifId) : this(naifId, TimeSystem.Time.J2000TDB)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Barycenter"/> class with the specified NAIF ID and epoch.
        /// </summary>
        /// <param name="naifId">The NAIF ID of the barycenter.</param>
        /// <param name="epoch">The epoch time for the barycenter.</param>
        public Barycenter(int naifId, Time epoch) : base(naifId, Frame.ECLIPTIC_J2000, epoch)
        {
        }
    }
}