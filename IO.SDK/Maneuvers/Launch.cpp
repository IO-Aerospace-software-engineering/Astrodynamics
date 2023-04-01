/**
 * @file Launch.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <Launch.h>
#include <Constants.h>

IO::SDK::Maneuvers::Launch::Launch(const IO::SDK::Sites::LaunchSite &launchSite, const IO::SDK::Sites::Site &recoverySite, bool launchByDay,
                                   const IO::SDK::OrbitalParameters::OrbitalParameters &targetOrbit) : m_launchSite{launchSite}, m_recoverySite{recoverySite},
                                                                                                       m_launchByDay{launchByDay}, m_targetOrbit{targetOrbit}
{
    const_cast<double &>(m_inclination) = m_targetOrbit.GetStateVector().ToFrame(IO::SDK::Frames::InertialFrames::GetICRF()).GetSpecificAngularMomentum().GetAngle(
            m_launchSite.GetBody()->GetBodyFixedFrame().TransformVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Math::Vector3D::VectorZ,
                                                                        IO::SDK::Time::TDB(std::chrono::duration<double>(0.0))));

    const_cast<double &>(m_deltaL) = std::asin(std::tan(m_launchSite.GetCoordinates().GetLatitude()) / std::tan(m_inclination));
}

double IO::SDK::Maneuvers::Launch::GetNonInertialAscendingAzimuthLaunch()
{
    if (std::isnan(m_nonInertialAscendingAzimuthLaunch))
    {
        double vrotx = GetInertialInsertionVelocity() * std::sin(GetInertialAscendingAzimuthLaunch()) -
                       m_launchSite.GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB(std::chrono::duration<double>(0.0))).GetVelocity().Magnitude();
        double vroty = GetInertialInsertionVelocity() * std::cos(GetInertialAscendingAzimuthLaunch());
        m_nonInertialAscendingAzimuthLaunch = std::atan(vrotx / vroty);
        if (m_nonInertialAscendingAzimuthLaunch < 0.0)
        {
            m_nonInertialAscendingAzimuthLaunch += IO::SDK::Constants::_2PI;
        }
    }
    return m_nonInertialAscendingAzimuthLaunch;
}

double IO::SDK::Maneuvers::Launch::GetNonInertialDescendingAzimuthLaunch()
{
    if (std::isnan(m_nonInertialDescendingAzimuthLaunch))
    {
        m_nonInertialDescendingAzimuthLaunch = IO::SDK::Constants::PI - GetNonInertialAscendingAzimuthLaunch();
        if (m_nonInertialDescendingAzimuthLaunch < 0.0)
        {
            m_nonInertialDescendingAzimuthLaunch += IO::SDK::Constants::_2PI;
        }
    }
    return m_nonInertialDescendingAzimuthLaunch;
}

double IO::SDK::Maneuvers::Launch::GetInertialAscendingAzimuthLaunch()
{
    if (std::isnan(m_inertialAscendingAzimuthLaunch))
    {
        m_inertialAscendingAzimuthLaunch = std::asin(std::cos(
                m_targetOrbit.GetStateVector().ToFrame(IO::SDK::Frames::InertialFrames::GetICRF()).GetSpecificAngularMomentum().GetAngle(
                        m_launchSite.GetBody()->GetBodyFixedFrame().TransformVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Math::Vector3D::VectorZ,
                                                                                    IO::SDK::Time::TDB(std::chrono::duration<double>(0.0))))) /
                                                     std::cos(m_launchSite.GetCoordinates().GetLatitude()));
        if (m_inertialAscendingAzimuthLaunch < 0.0)
        {
            m_inertialAscendingAzimuthLaunch += IO::SDK::Constants::_2PI;
        }
    }
    return m_inertialAscendingAzimuthLaunch;
}

double IO::SDK::Maneuvers::Launch::GetInertialDescendingAzimuthLaunch()
{
    if (std::isnan(m_inertialDescendingAzimuthLaunch))
    {
        m_inertialDescendingAzimuthLaunch = IO::SDK::Constants::PI - GetInertialAscendingAzimuthLaunch();
        if (m_inertialDescendingAzimuthLaunch < 0.0)
        {
            m_inertialDescendingAzimuthLaunch += IO::SDK::Constants::_2PI;
        }
    }
    return m_inertialDescendingAzimuthLaunch;
}

double IO::SDK::Maneuvers::Launch::GetNonInertialInsertionVelocity()
{
    if (std::isnan(m_nonInertialInsertionVelocity))
    {
        double vrotx = GetInertialInsertionVelocity() * std::sin(GetInertialAscendingAzimuthLaunch()) -
                       m_launchSite.GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), IO::SDK::Time::TDB(std::chrono::duration<double>(0.0))).GetVelocity().Magnitude();
        double vroty = GetInertialInsertionVelocity() * std::cos(GetInertialAscendingAzimuthLaunch());
        m_nonInertialInsertionVelocity = std::sqrt(vrotx * vrotx + vroty * vroty);
    }
    return m_nonInertialInsertionVelocity;
}

double IO::SDK::Maneuvers::Launch::GetInertialInsertionVelocity()
{
    return m_targetOrbit.GetVelocityAtPerigee();
}

std::vector<IO::SDK::Maneuvers::LaunchWindow> IO::SDK::Maneuvers::Launch::GetLaunchWindows(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &searchWindow)
{
    std::vector<IO::SDK::Maneuvers::LaunchWindow> launchWindows;
    if (m_launchByDay)
    {
        //Find sun light windows on launch site
        auto launchSiteDayWindows = m_launchSite.FindDayWindows(searchWindow, IO::SDK::Constants::OfficialTwilight);
        if (launchSiteDayWindows.empty())
        {
            throw IO::SDK::Exception::SDKException(
                    "No sunlight at launch site on this search window day : " + searchWindow.GetStartDate().ToString() + " - " + searchWindow.GetEndDate().ToString());
        }

        //Find sun light windows on recovery site
        auto recoverySiteDayWindows = m_recoverySite.FindDayWindows(searchWindow, IO::SDK::Constants::OfficialTwilight);
        if (recoverySiteDayWindows.empty())
        {
            throw IO::SDK::Exception::SDKException(
                    "No sunlight at recovery site on this launch day : " + searchWindow.GetStartDate().ToString() + " - " + searchWindow.GetEndDate().ToString());
        }

        //Find sun light windows on both site at same time
        std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>> sunLightWindowsOnBothSites;
        for (auto &&launchSiteWindow: launchSiteDayWindows)
        {
            for (auto &&recoverySiteWindow: recoverySiteDayWindows)
            {
                if (launchSiteWindow.Intersects(recoverySiteWindow))
                {
                    auto intersection = launchSiteWindow.GetIntersection(recoverySiteWindow);
                    sunLightWindowsOnBothSites.emplace_back(intersection.GetStartDate(), intersection.GetEndDate());
                }
            }
        }

        if (sunLightWindowsOnBothSites.empty())
        {
            throw IO::SDK::Exception::SDKException("No sun light at same time on both sites");
        }

        //Search an orbital plane alignement with launch site during sun light window on both sites
        for (auto &&sunlightWindow: sunLightWindowsOnBothSites)
        {
            auto res = FindLaunchWindows(sunlightWindow);
            for (auto &lw: res)
            {
                launchWindows.push_back(lw);
            }
        }
    } else
    {
        auto res = FindLaunchWindows(searchWindow);
        for (auto &lw: res)
        {
            launchWindows.push_back(lw);
        }
    }

    return launchWindows;
}

std::vector<IO::SDK::Maneuvers::LaunchWindow> IO::SDK::Maneuvers::Launch::FindLaunchWindows(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &windowToSearch)
{
    //Initialize start date
    auto date = windowToSearch.GetStartDate();

    //Define initial step
    auto step = windowToSearch.GetLength() * 0.5;

    //Define crossing plane inbound status
    bool isInboundPlaneCrossing = m_targetOrbit.GetStateVector().ToFrame(IO::SDK::Frames::InertialFrames::GetICRF()).GetSpecificAngularMomentum().DotProduct(
            m_launchSite.GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), date.ToTDB()).GetPosition()) > 0.0;

    std::vector<LaunchWindow> launchWindows;

    //Works until it reaches the end of search window
    while (date <= windowToSearch.GetEndDate())
    {
        date = date + step;

        //Compute crossing plane inbound status
        bool isInbound = m_targetOrbit.GetStateVector().ToFrame(IO::SDK::Frames::InertialFrames::GetICRF()).GetSpecificAngularMomentum().DotProduct(
                m_launchSite.GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), date.ToTDB()).GetPosition()) > 0.0;

        //If inbound status has changed, we passed through orbital plane
        if (isInbound != isInboundPlaneCrossing)
        {
            isInboundPlaneCrossing = isInbound;
            //We reduce step size and reverse search direction
            step = step * -0.5;
        }

        //If a sufficent accuracy is reached
        if (std::abs(step.GetSeconds().count()) < 1.0)
        {
            //Fill in the first launch window

            double inertialAzimuthLaunch{std::numeric_limits<double>::quiet_NaN()};
            double nonInertialAzimuthLaunch{std::numeric_limits<double>::quiet_NaN()};

            //Set velocities
            double inertialInsertionVelocity = GetInertialInsertionVelocity();
            double nonInertialInsertionVelocity{GetNonInertialInsertionVelocity()};

            //Define if launch will be northly or southerly and set azimuths
            bool isAscending{};
            if (m_launchSite.GetStateVector(IO::SDK::Frames::InertialFrames::GetICRF(), date.ToTDB()).GetPosition().DotProduct(
                    m_targetOrbit.GetStateVector().ToFrame(IO::SDK::Frames::InertialFrames::GetICRF()).GetAscendingNodeVector()) > 0.0)
            {
                inertialAzimuthLaunch = GetInertialAscendingAzimuthLaunch();
                nonInertialAzimuthLaunch = GetNonInertialAscendingAzimuthLaunch();
                isAscending = true;
            } else
            {
                inertialAzimuthLaunch = GetInertialDescendingAzimuthLaunch();
                nonInertialAzimuthLaunch = GetNonInertialDescendingAzimuthLaunch();
            }

            //We add the first launch window in the collection
            auto window = IO::SDK::Time::Window<IO::SDK::Time::UTC>(date, date);
            launchWindows.emplace_back(m_launchSite, window, inertialAzimuthLaunch, nonInertialAzimuthLaunch, inertialInsertionVelocity,
                                                                     nonInertialInsertionVelocity);

            //We compute following launch windows
            auto remainingTime = windowToSearch.GetEndDate() - date;

            //sideral rotation period
            auto sideralRotation = m_launchSite.GetBody()->GetSideralRotationPeriod(date.ToTDB());
            auto halfSideralRotation = sideralRotation * 0.5;

            //nb remaining launch windows
            int nbLaunchWindows = remainingTime.GetSeconds().count() / halfSideralRotation.GetSeconds().count();

            for (int i = 0; i < nbLaunchWindows; i++)
            {
                //If previous is ascending next will be descending
                isAscending = !isAscending;

                auto deltalDuration = m_deltaL * 2.0 / m_launchSite.GetBody()->GetAngularVelocity(date.ToTDB());

                if (!isAscending)
                {
                    deltalDuration *= -1.0;
                }



                //We increment next launch date
                date = date + halfSideralRotation + IO::SDK::Time::TimeSpan(std::chrono::duration<double>(deltalDuration));

                //build next window
                auto nextWindow = IO::SDK::Time::Window<IO::SDK::Time::UTC>(date, date);

                if (isAscending)
                {
                    launchWindows.emplace_back(m_launchSite, nextWindow, GetInertialAscendingAzimuthLaunch(), GetNonInertialAscendingAzimuthLaunch(),
                                                                             inertialInsertionVelocity, nonInertialInsertionVelocity);
                } else
                {
                    launchWindows.emplace_back(m_launchSite, nextWindow, GetInertialDescendingAzimuthLaunch(), GetNonInertialDescendingAzimuthLaunch(),
                                                             inertialInsertionVelocity, nonInertialInsertionVelocity);
                }
            }
            break;
        }
    }

    return launchWindows;
}
