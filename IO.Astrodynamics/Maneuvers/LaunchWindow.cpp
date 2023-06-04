/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <LaunchWindow.h>

IO::Astrodynamics::Maneuvers::LaunchWindow::LaunchWindow(const IO::Astrodynamics::Sites::LaunchSite &launchSite, const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &window, const double inertialAzimuth,
                                               const double nonInertialAzimuth, double inertialInsertionVelocity, const double nonInertialInsertionVelocity) : m_launchSite{
        launchSite}, m_window{window}, m_inertialAzimuth{inertialAzimuth}, m_nonInertialAzimuth{nonInertialAzimuth}, m_nonInertialInsertionVelocity{nonInertialInsertionVelocity},
                                                                                                                                                               m_inertialInsertionVelocity{
                                                                                                                                                                       inertialInsertionVelocity}
{
}

//IO::Astrodynamics::Maneuvers::LaunchWindow &IO::Astrodynamics::Maneuvers::LaunchWindow::operator=(const IO::Astrodynamics::Maneuvers::LaunchWindow &lw)
//{
//    // Guard self assignment
//    if (this == &lw)
//        return *this;
//
//    const_cast<IO::Astrodynamics::Sites::LaunchSite &>(m_launchSite) = lw.m_launchSite);
//    const_cast<IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &>(m_window) = lw.m_window;
//    const_cast<double &>(m_inertialAzimuth) = lw.m_inertialAzimuth;
//    const_cast<double &>(m_nonInertialAzimuth) = lw.m_nonInertialAzimuth;
//    const_cast<double &>(m_inertialInsertionVelocity) = lw.m_inertialInsertionVelocity;
//    const_cast<double &>(m_nonInertialInsertionVelocity) = lw.m_nonInertialInsertionVelocity;
//    return *this;
//}

const IO::Astrodynamics::Sites::LaunchSite &IO::Astrodynamics::Maneuvers::LaunchWindow::GetLaunchSite() const
{
    return m_launchSite;
}

const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &IO::Astrodynamics::Maneuvers::LaunchWindow::GetWindow() const
{
    return m_window;
}

double IO::Astrodynamics::Maneuvers::LaunchWindow::GetInertialAzimuth() const
{
    return m_inertialAzimuth;
}

double IO::Astrodynamics::Maneuvers::LaunchWindow::GetNonInertialAzimuth() const
{
    return m_nonInertialAzimuth;
}

double IO::Astrodynamics::Maneuvers::LaunchWindow::GetInertialInsertionVelocity() const
{
    return m_inertialInsertionVelocity;
}

double IO::Astrodynamics::Maneuvers::LaunchWindow::GetNonInertialInsertionVelocity() const
{
    return m_nonInertialInsertionVelocity;
}