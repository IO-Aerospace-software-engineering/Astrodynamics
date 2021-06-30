#ifndef LAUNCH_WINDOW_H
#define LAUNCH_WINDOW_H

#include<memory>

#include <Window.h>
#include <UTC.h>
#include <LaunchSite.h>

namespace IO::SDK::Maneuvers
{
    class LaunchWindow
    {
    private:
        const std::shared_ptr<IO::SDK::Sites::LaunchSite> m_launchSite;
        const IO::SDK::Time::Window<IO::SDK::Time::UTC> m_window;
        const double m_inertialAzimuth;
        const double m_nonInertialAzimuth;
        
        const double m_nonInertialInsertionVelocity;
        const double m_inertialInsertionVelocity;

    public:
        /**
         * @brief Construct a new Launch Window object
         * 
         * @param launchSite 
         * @param window 
         * @param inertialAzimuth 
         * @param nonInertialAzimuth 
         * @param inertialInsertionVelocity 
         * @param nonInertialInsertionVelocity 
         */
        LaunchWindow(const std::shared_ptr<IO::SDK::Sites::LaunchSite> &launchSite, const IO::SDK::Time::Window<IO::SDK::Time::UTC> &window, const double inertialAzimuth, const double nonInertialAzimuth, const double inertialInsertionVelocity, const double nonInertialInsertionVelocity);
        ~LaunchWindow() = default;

        LaunchWindow& operator=(const LaunchWindow& lw);

        const std::shared_ptr<IO::SDK::Sites::LaunchSite>& GetLaunchSite() const;
        const IO::SDK::Time::Window<IO::SDK::Time::UTC>& GetWindow() const;
        double GetInertialAzimuth() const;
        double GetNonInertialAzimuth() const;
        double GetInertialInsertionVelocity() const;
        double GetNonInertialInsertionVelocity() const;

    };

} // namespace IO::SDK::Maneuvers

#endif