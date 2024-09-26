using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IO.Astrodynamics.Frames;
using IO.Astrodynamics.Math;
using IO.Astrodynamics.Surface;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics.Maneuver
{
    public class Launch
    {
        private double m_nonInertialAscendingAzimuthLaunch = double.NaN;
        private double m_nonInertialDescendingAzimuthLaunch = double.NaN;
        private double m_inertialAscendingAzimuthLaunch = double.NaN;
        private double m_inertialDescendingAzimuthLaunch = double.NaN;
        private double m_nonInertialInsertionVelocity = double.NaN;
        private double m_inertialInsertionVelocity = double.NaN;
        private double m_deltaL = 0.0;

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
            m_deltaL= System.Math.Asin(System.Math.Tan(LaunchSite.Planetodetic.Latitude) / System.Math.Tan(targetOrbit.Inclination()));
        }

        /// <summary>
        /// Find launch windows based on launch's constraints in the given window
        /// </summary>
        /// <param name="searchWindow"></param>
        /// <param name="outputDirectory"></param>
        /// <returns></returns>
        public IEnumerable<LaunchWindow> FindLaunchWindows(in Window searchWindow, DirectoryInfo outputDirectory)
        {
            //return API.Instance.FindLaunchWindows(this, searchWindow, outputDirectory);
            var date = searchWindow.StartDate;

            // Define initial step (half the length of the search window)
            TimeSpan step = searchWindow.Length * 0.5;

            // Define crossing plane inbound status
            bool isInboundPlaneCrossing = IsInboundPlaneCrossing(date);

            List<LaunchWindow> launchWindows = new List<LaunchWindow>();

            // Work until it reaches the end of the search window
            while (date <= searchWindow.EndDate)
            {
                date = date.Add(step);

                // Compute crossing plane inbound status
                bool isInbound = IsInboundPlaneCrossing(date);

                // If inbound status has changed, we passed through the orbital plane
                if (isInbound != isInboundPlaneCrossing)
                {
                    isInboundPlaneCrossing = isInbound;
                    // Reduce step size and reverse search direction
                    step = TimeSpan.FromTicks(-step.Ticks / 2);
                }

                // If sufficient accuracy is reached (step size less than 1 second)
                if (System.Math.Abs(step.TotalSeconds) < 1.0)
                {
                    // Fill in the first launch window

                    double inertialAzimuthLaunch = double.NaN;
                    double nonInertialAzimuthLaunch = double.NaN;

                    // Set velocities
                    double inertialInsertionVelocity = GetInertialInsertionVelocity();
                    double nonInertialInsertionVelocity = GetNonInertialInsertionVelocity();

                    // Determine if launch will be ascending or descending and set azimuths
                    bool isAscending;
                    if (IsAscending(date))
                    {
                        inertialAzimuthLaunch = GetInertialAscendingAzimuthLaunch();
                        nonInertialAzimuthLaunch = GetNonInertialAscendingAzimuthLaunch();
                        isAscending = true;
                    }
                    else
                    {
                        inertialAzimuthLaunch = GetInertialDescendingAzimuthLaunch();
                        nonInertialAzimuthLaunch = GetNonInertialDescendingAzimuthLaunch();
                        isAscending = false;
                    }

                    // Add the first launch window to the collection
                    Window window = new Window(date, date);
                    launchWindows.Add(new LaunchWindow(window,  inertialInsertionVelocity,
                        nonInertialInsertionVelocity,inertialAzimuthLaunch, nonInertialAzimuthLaunch));

                    // Compute following launch windows
                    TimeSpan remainingTime = searchWindow.EndDate - date;

                    // Sidereal rotation period
                    TimeSpan siderealRotation = LaunchSite.CelestialBody.SideralRotationPeriod(date);
                    TimeSpan halfSiderealRotation = TimeSpan.FromTicks(siderealRotation.Ticks / 2);

                    // Number of remaining launch windows
                    int nbLaunchWindows = (int)(remainingTime.TotalSeconds / halfSiderealRotation.TotalSeconds);

                    for (int i = 0; i < nbLaunchWindows; i++)
                    {
                        // If previous is ascending, next will be descending
                        isAscending = !isAscending;

                        double deltaLDuration = (m_deltaL * 2.0) / LaunchSite.CelestialBody.AngularVelocity(date);

                        if (!isAscending)
                        {
                            deltaLDuration *= -1.0;
                        }

                        // Increment next launch date
                        date = date.Add(halfSiderealRotation).Add(TimeSpan.FromSeconds(deltaLDuration));

                        // Build next window
                        Window nextWindow = new Window(date, date);

                        if (isAscending)
                        {
                            launchWindows.Add(new LaunchWindow(nextWindow,inertialInsertionVelocity,nonInertialInsertionVelocity, GetInertialAscendingAzimuthLaunch(), GetNonInertialAscendingAzimuthLaunch()));
                        }
                        else
                        {
                            launchWindows.Add(new LaunchWindow(nextWindow,inertialInsertionVelocity,nonInertialInsertionVelocity, GetInertialDescendingAzimuthLaunch(), GetNonInertialDescendingAzimuthLaunch()));
                        }
                    }

                    break; // Exit the while loop after computing all launch windows
                }
            }

            if (LaunchByDay != null)
            {
                var launchSiteDayWindows = LaunchSite.FindDayWindows(searchWindow, Twilight);
                var recoverySiteDayWindows = RecoverySite.FindDayWindows(searchWindow, Twilight);
                bool launchByDay = LaunchByDay.Value;

                foreach (var launchWindow in launchWindows.ToArray())
                {
                    bool launchSiteIntersects = launchSiteDayWindows.Any(x => x.Intersects(launchWindow.Window));
                    bool recoverySiteIntersects = recoverySiteDayWindows.Any(x => x.Intersects(launchWindow.Window));

                    bool removeWindow = launchByDay
                        ? !(launchSiteIntersects && recoverySiteIntersects)
                        : (launchSiteIntersects || recoverySiteIntersects);

                    if (removeWindow)
                    {
                        launchWindows.Remove(launchWindow);
                    }
                }
            }

            return launchWindows;
        }

        public double GetNonInertialAscendingAzimuthLaunch()
        {
            if (double.IsNaN(m_nonInertialAscendingAzimuthLaunch))
            {
                double inertialInsertionVelocity = GetInertialInsertionVelocity();
                double inertialAscendingAzimuthLaunch = GetInertialAscendingAzimuthLaunch();

                double launchSiteVelocityMagnitude = LaunchSite.GetEphemeris(Time.J2000TDB, LaunchSite.CelestialBody,Frame.ICRF, Aberration.None).ToStateVector().Velocity.Magnitude();

                double vrotx = inertialInsertionVelocity * System.Math.Sin(inertialAscendingAzimuthLaunch) -
                               launchSiteVelocityMagnitude;
                double vroty = inertialInsertionVelocity * System.Math.Cos(inertialAscendingAzimuthLaunch);

                m_nonInertialAscendingAzimuthLaunch = System.Math.Atan2(vrotx, vroty);
                if (m_nonInertialAscendingAzimuthLaunch < 0.0)
                {
                    m_nonInertialAscendingAzimuthLaunch += Constants._2PI;
                }
            }

            return m_nonInertialAscendingAzimuthLaunch;
        }

        public double GetNonInertialDescendingAzimuthLaunch()
        {
            if (double.IsNaN(m_nonInertialDescendingAzimuthLaunch))
            {
                m_nonInertialDescendingAzimuthLaunch = Constants.PI - GetNonInertialAscendingAzimuthLaunch();
                if (m_nonInertialDescendingAzimuthLaunch < 0.0)
                {
                    m_nonInertialDescendingAzimuthLaunch += Constants._2PI;
                }
            }

            return m_nonInertialDescendingAzimuthLaunch;
        }

        public double GetInertialAscendingAzimuthLaunch()
        {
            if (double.IsNaN(m_inertialAscendingAzimuthLaunch))
            {
                double angle = TargetOrbit.Inclination();

                double latitude = LaunchSite.Planetodetic.Latitude; // In radians

                m_inertialAscendingAzimuthLaunch = System.Math.Asin(System.Math.Cos(angle) / System.Math.Cos(latitude));

                if (m_inertialAscendingAzimuthLaunch < 0.0)
                {
                    m_inertialAscendingAzimuthLaunch += Constants._2PI;
                }
            }

            return m_inertialAscendingAzimuthLaunch;
        }

        public double GetInertialDescendingAzimuthLaunch()
        {
            if (double.IsNaN(m_inertialDescendingAzimuthLaunch))
            {
                m_inertialDescendingAzimuthLaunch = Constants.PI - GetInertialAscendingAzimuthLaunch();
                if (m_inertialDescendingAzimuthLaunch < 0.0)
                {
                    m_inertialDescendingAzimuthLaunch += Constants._2PI;
                }
            }

            return m_inertialDescendingAzimuthLaunch;
        }

        public double GetNonInertialInsertionVelocity()
        {
            if (double.IsNaN(m_nonInertialInsertionVelocity))
            {
                double inertialInsertionVelocity = GetInertialInsertionVelocity();
                double inertialAscendingAzimuthLaunch = GetInertialAscendingAzimuthLaunch();

                double launchSiteVelocityMagnitude = LaunchSite.GetEphemeris(Time.J2000TDB, LaunchSite.CelestialBody,Frame.ICRF, Aberration.None).ToStateVector().Velocity.Magnitude();

                double vrotx = inertialInsertionVelocity * System.Math.Sin(inertialAscendingAzimuthLaunch) -
                               launchSiteVelocityMagnitude;
                double vroty = inertialInsertionVelocity * System.Math.Cos(inertialAscendingAzimuthLaunch);

                m_nonInertialInsertionVelocity = System.Math.Sqrt(vrotx * vrotx + vroty * vroty);
            }

            return m_nonInertialInsertionVelocity;
        }
        
        public double GetInertialInsertionVelocity()
        {
            return TargetOrbit.PerigeeVelocity();
        }

        private bool IsInboundPlaneCrossing(in Time date)
        {
            // Compute the dot product between the specific angular momentum vector of the target orbit
            // and the position vector of the launch site at the given date
            Vector3 h = TargetOrbit.ToStateVector().ToFrame(Frame.ICRF).SpecificAngularMomentum();
            Vector3 r = LaunchSite.GetEphemeris(date, LaunchSite.CelestialBody, Frame.ICRF, Aberration.None).ToStateVector().Position;
            return h * r > 0.0;
        }

        private bool IsAscending(in Time date)
        {
            // Determine if the launch will be ascending based on the dot product with the ascending node vector
            Vector3 launchSitePosition = LaunchSite.GetEphemeris(date, LaunchSite.CelestialBody, Frame.ICRF, Aberration.None).ToStateVector().Position;
            Vector3 ascendingNodeVector = TargetOrbit.ToStateVector().ToFrame(Frame.ICRF).AscendingNodeVector();
            return (launchSitePosition * ascendingNodeVector) > 0.0;
        }
    }
}