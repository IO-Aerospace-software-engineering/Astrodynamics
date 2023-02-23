//
// Created by s.guillet on 23/02/2023.
//

#ifndef IOSDK_SCENARIO_H
#define IOSDK_SCENARIO_H

#include <string>
#include <vector>

#include <UTC.h>
#include <Window.h>
#include <CelestialBody.h>
#include <Spacecraft.h>


namespace IO::SDK
{
    class Scenario
    {
    private:
        const std::string m_name;
        const IO::SDK::Time::Window<IO::SDK::Time::UTC> m_windows;
        const std::vector<IO::SDK::Body::CelestialBody> m_celestialBodies;
        const std::vector<IO::SDK::Body::Spacecraft::Spacecraft> m_spacecrafts;
        const std::vector<IO::SDK::Sites::Site> m_sites;

    public:
        Scenario(const std::string &name, const IO::SDK::Time::Window<IO::SDK::Time::UTC> &windows);

        void AddCelestialBody(const IO::SDK::Body::CelestialBody &celestialBody) const;

        void AddSpacecraft(const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft) const;

        void AddSite(const IO::SDK::Sites::Site &site) const;

        inline std::string GetName() const
        { return m_name; }

        inline const IO::SDK::Time::Window<IO::SDK::Time::UTC> &GetWindow() const
        { return m_windows; }

        inline const std::vector<IO::SDK::Body::CelestialBody> &GetCelestialBodies() const
        { return m_celestialBodies; }

        inline const std::vector<IO::SDK::Body::Spacecraft::Spacecraft> &GetSpacecrafts() const
        { return m_spacecrafts; }

        inline const std::vector<IO::SDK::Sites::Site>& GetSites() const
        { return m_sites; }

        void Execute() const;
    };

} // IO::SDK

#endif //IOSDK_SCENARIO_H
