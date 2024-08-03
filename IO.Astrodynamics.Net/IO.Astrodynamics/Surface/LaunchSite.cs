using System.Collections.Generic;
using System.Linq;
using IO.Astrodynamics.Body;
using IO.Astrodynamics.Coordinates;

namespace IO.Astrodynamics.Surface
{
    public class LaunchSite : Site
    {
        public IReadOnlyCollection<AzimuthRange> AzimuthRanges => _azimuthRanges;
        private readonly List<AzimuthRange> _azimuthRanges;

        /// <summary>
        /// Create launch site
        /// </summary>
        /// <param name="id"></param>
        /// <param name="name">Site name</param>
        /// <param name="celestialItem">Celestial celestialItem</param>
        /// <param name="planetodetic">Planetodetic coordinates</param>
        /// <param name="launchAzimuths">Allowed launch azimuths</param>
        public LaunchSite(int id, string name, CelestialBody celestialItem, in Planetodetic planetodetic, params AzimuthRange[] launchAzimuths) : base(id, name,
            celestialItem, planetodetic)
        {
            _azimuthRanges = new List<AzimuthRange>(launchAzimuths);
        }

        /// <summary>
        /// Know if the azimuth is allowed
        /// </summary>
        /// <param name="azimuth"></param>
        /// <returns></returns>
        public bool IsAzimuthAllowed(double azimuth)
        {
            return _azimuthRanges.Any(x => x.IsInRange(azimuth));
        }
    }
}