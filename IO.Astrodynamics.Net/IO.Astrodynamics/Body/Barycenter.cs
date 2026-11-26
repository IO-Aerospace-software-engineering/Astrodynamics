using System;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.SolarSystemObjects;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Body
{
    /// <summary>
    /// Represents a barycenter, which is the center of mass of two or more bodies that are orbiting each other.
    /// </summary>
    public class Barycenter : CelestialItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Barycenter"/> class with the specified NAIF ID and the default epoch (J2000Tdb).
        /// </summary>
        /// <param name="naifId">The NAIF ID of the barycenter.</param>
        public Barycenter(int naifId) : this(naifId, TimeSystem.Time.J2000Tdb)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Barycenter"/> class with the specified NAIF ID and epoch.
        /// </summary>
        /// <param name="naifId">The NAIF ID of the barycenter.</param>
        /// <param name="epoch">The epoch time for the barycenter.</param>
        public Barycenter(int naifId, Time epoch) : base(naifId, Frame.ECLIPJ2000, epoch)
        {
            RegisterWithObserver();
        }

        /// <summary>
        /// A static object of <see cref="SolarSystemObjects.Barycenters"/>: its identifiers and name without any read of
        /// the data provider, at J2000 TDB in ECLIPJ2000; its provider data (information, GM, orbit about the solar system
        /// barycenter) are read at the first read of a member that needs them, retried on failure (see
        /// <see cref="CelestialItem.Mass"/>).
        /// </summary>
        internal static Barycenter CreateDeferred(int naifId, string name) => new(new NaifObject(naifId, name), true);

        // The constructor of CreateDeferred: the parameter deferred tells it apart from the public constructors
        private Barycenter(NaifObject knownObject, bool deferred) : base(knownObject, Frame.ECLIPJ2000, TimeSystem.Time.J2000Tdb)
        {
            _ = deferred;
        }
    }
}