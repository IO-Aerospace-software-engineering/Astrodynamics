/**
 * @file LaunchWindow.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <LaunchWindow.h>

IO::SDK::Maneuvers::LaunchWindow::LaunchWindow(const IO::SDK::Sites::LaunchSite &launchSite, const IO::SDK::Time::Window<IO::SDK::Time::UTC> &window, const double inertialAzimuth,
                                               const double nonInertialAzimuth, double inertialInsertionVelocity, const double nonInertialInsertionVelocity) : m_launchSite{
        launchSite}, m_window{window}, m_inertialAzimuth{inertialAzimuth}, m_nonInertialAzimuth{nonInertialAzimuth}, m_nonInertialInsertionVelocity{nonInertialInsertionVelocity},
                                                                                                                                                               m_inertialInsertionVelocity{
                                                                                                                                                                       inertialInsertionVelocity}
{
}

//IO::SDK::Maneuvers::LaunchWindow &IO::SDK::Maneuvers::LaunchWindow::operator=(const IO::SDK::Maneuvers::LaunchWindow &lw)
//{
//    // Guard self assignment
//    if (this == &lw)
//        return *this;
//
//    const_cast<IO::SDK::Sites::LaunchSite &>(m_launchSite) = lw.m_launchSite);
//    const_cast<IO::SDK::Time::Window<IO::SDK::Time::UTC> &>(m_window) = lw.m_window;
//    const_cast<double &>(m_inertialAzimuth) = lw.m_inertialAzimuth;
//    const_cast<double &>(m_nonInertialAzimuth) = lw.m_nonInertialAzimuth;
//    const_cast<double &>(m_inertialInsertionVelocity) = lw.m_inertialInsertionVelocity;
//    const_cast<double &>(m_nonInertialInsertionVelocity) = lw.m_nonInertialInsertionVelocity;
//    return *this;
//}

const IO::SDK::Sites::LaunchSite &IO::SDK::Maneuvers::LaunchWindow::GetLaunchSite() const
{
    return m_launchSite;
}

const IO::SDK::Time::Window<IO::SDK::Time::UTC> &IO::SDK::Maneuvers::LaunchWindow::GetWindow() const
{
    return m_window;
}

double IO::SDK::Maneuvers::LaunchWindow::GetInertialAzimuth() const
{
    return m_inertialAzimuth;
}

double IO::SDK::Maneuvers::LaunchWindow::GetNonInertialAzimuth() const
{
    return m_nonInertialAzimuth;
}

double IO::SDK::Maneuvers::LaunchWindow::GetInertialInsertionVelocity() const
{
    return m_inertialInsertionVelocity;
}

double IO::SDK::Maneuvers::LaunchWindow::GetNonInertialInsertionVelocity() const
{
    return m_nonInertialInsertionVelocity;
}