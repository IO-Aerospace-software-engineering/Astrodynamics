using System;
using System.Collections.Generic;
using System.IO;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.Time;

namespace IO.Astrodynamics.Maneuver
{
    public class Launch
    {
        public LaunchSite LaunchSite { get; }
        public Site RecoverySite { get; }
        public OrbitalParameters.OrbitalParameters TargetOrbit { get; }
        public Body.CelestialItem TargetCelestialItem { get; }
        public bool? LaunchByDay { get; }
        public double Twilight { get; }
        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="launchSite">Launch from</param>
        /// <param name="recoverySite">Recovery site</param>
        /// <param name="targetCelestialItem">CelestialItem in orbit to reach</param>
        /// <param name="twilight">Twilight definition</param>
        /// <param name="launchByDay">Define if launch should occur by day. If undefined launch can occur everytime</param>
        /// <exception cref="ArgumentNullException"></exception>
        public Launch(LaunchSite launchSite, Site recoverySite, Body.CelestialItem targetCelestialItem, double twilight,
            bool? launchByDay)
        {
            LaunchSite = launchSite ?? throw new ArgumentNullException(nameof(launchSite));
            RecoverySite = recoverySite ?? throw new ArgumentNullException(nameof(recoverySite));
            TargetCelestialItem = targetCelestialItem ?? throw new ArgumentNullException(nameof(targetCelestialItem));
            LaunchByDay = launchByDay;
            Twilight = twilight;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="launchSite">Launch from</param>
        /// <param name="recoverySite">Recovery site</param>
        /// <param name="targetOrbit">Orbit to reach</param>
        /// <param name="twilight"></param>
        /// <param name="launchByDay">Define if launch should occur by day. If undefined launch can occur everytime</param>
        /// <exception cref="ArgumentNullException"></exception>
        public Launch(LaunchSite launchSite, Site recoverySite, OrbitalParameters.OrbitalParameters targetOrbit,
            double twilight, bool? launchByDay)
        {
            LaunchSite = launchSite ?? throw new ArgumentNullException(nameof(launchSite));
            RecoverySite = recoverySite ?? throw new ArgumentNullException(nameof(recoverySite));
            TargetOrbit = targetOrbit ?? throw new ArgumentNullException(nameof(targetOrbit));
            LaunchByDay = launchByDay;
            Twilight = twilight;
        }

        /// <summary>
        /// Find launch windows based on launch's constraints in the given window
        /// </summary>
        /// <param name="searchWindow"></param>
        /// <param name="outputDirectory"></param>
        /// <returns></returns>
        public IEnumerable<LaunchWindow> FindLaunchWindows(in Window searchWindow, DirectoryInfo outputDirectory)
        {
            return API.Instance.FindLaunchWindows(this, searchWindow, outputDirectory);
        }
    }
}