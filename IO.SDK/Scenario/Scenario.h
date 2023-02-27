//
// Created by s.guillet on 23/02/2023.
//

#ifndef IOSDK_SCENARIO_H
#define IOSDK_SCENARIO_H

#include <string>
#include <vector>

#include "Body/Body.h"
#include "Time/UTC.h"
#include "Time/Window.h"
#include "Time/TimeSpan.h"
#include "Body/CelestialBody.h"
#include "Body/Spacecraft/Spacecraft.h"
#include <map>
#include <optional>


namespace IO::SDK
{
    class Scenario
    {
    private:
        const std::string m_name;
        const IO::SDK::Time::Window<IO::SDK::Time::UTC> m_windows;
        std::vector<const IO::SDK::Body::CelestialBody *> m_celestialBodies;
        std::vector<const IO::SDK::Body::Spacecraft::Spacecraft *> m_spacecrafts;
        std::vector<const IO::SDK::Sites::Site *> m_sites;

        //Body constraints
        std::map<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>(*)(
                const IO::SDK::Time::Window<IO::SDK::Time::TDB> searchWindow,
                const IO::SDK::Body::Body &,
                const IO::SDK::Body::Body &,
                const IO::SDK::Constraints::Constraint &,
                const IO::SDK::AberrationsEnum,
                const double value,
                const IO::SDK::Time::TimeSpan &
        ),
                std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>>> m_distanceConstraints;


        std::map<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>(*)(const IO::SDK::Time::Window<IO::SDK::Time::TDB>,
                                                                           const IO::SDK::Body::CelestialBody &, const IO::SDK::Body::CelestialBody &,
                                                                           const IO::SDK::OccultationType &, const IO::SDK::AberrationsEnum,
                                                                           const IO::SDK::Time::TimeSpan &
        ),
                std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>>> m_occultationConstraints;

        //Site constraints
        std::map<std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>(*)(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &searchWindow, const double twilight
        ),
                std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>>> m_dayConstraints;

        std::map<std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>(*)(const IO::SDK::Time::Window<IO::SDK::Time::UTC> &searchWindow, const double twilight
        ),
                std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>>> m_nightConstraints;

        std::map<std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>(*)(const IO::SDK::Body::Body &body, const IO::SDK::Time::Window<IO::SDK::Time::UTC> &searchWindow,
                                                                           const IO::SDK::AberrationsEnum aberrationCorrection
        ),
                std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>>> m_bodyVisibilityConstraints;


    public:
        Scenario(const std::string &name, const IO::SDK::Time::Window<IO::SDK::Time::UTC> &windows);

        void AddCelestialBody(const IO::SDK::Body::CelestialBody &celestialBody);

        void AddSpacecraft(const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft);

        void AddSite(const IO::SDK::Sites::Site &site);

        inline const std::string GetName() const
        { return m_name; }

        inline const IO::SDK::Time::Window<IO::SDK::Time::UTC> &GetWindow() const
        { return m_windows; }

        inline const std::vector<const IO::SDK::Body::CelestialBody *> &GetCelestialBodies()
        { return m_celestialBodies; }

        inline const std::vector<const IO::SDK::Body::Spacecraft::Spacecraft *> &GetSpacecrafts()
        { return m_spacecrafts; }

        inline const std::vector<const IO::SDK::Sites::Site *> &GetSites()
        { return m_sites; }

        void AddDistanceConstraintWindow(std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>(*func

        )(
                const IO::SDK::Time::Window<IO::SDK::Time::TDB> searchWindow,
                const IO::SDK::Body::Body &targetBody,
                const IO::SDK::Body::Body &observer,
                const IO::SDK::Constraints::Constraint &constraint,
                const IO::SDK::AberrationsEnum aberration,
                const double value,
                const IO::SDK::Time::TimeSpan &step
        ));

        void AddOccultationConstraintWindow(std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>(*func

        )(
                const IO::SDK::Time::Window<IO::SDK::Time::TDB>,
                const IO::SDK::Body::CelestialBody &, const IO::SDK::Body::CelestialBody &,
                const IO::SDK::OccultationType &, const IO::SDK::AberrationsEnum,
                const IO::SDK::Time::TimeSpan &
        ));

        void AddDayConstraintsWindow(std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>(*func

        )(
                const IO::SDK::Time::Window<IO::SDK::Time::UTC> &, const double
        ));

        void AddNightConstraintsWindow(std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>(*func

        )(
                const IO::SDK::Time::Window<IO::SDK::Time::UTC> &, const double
        ));

        void AddBodyVisibilityConstraintsWindow(std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>(*func

        )(
                const IO::SDK::Body::Body &, const IO::SDK::Time::Window<IO::SDK::Time::UTC> &,
                const IO::SDK::AberrationsEnum
        ));

        void Execute();
    };

} // IO::SDK

#endif //IOSDK_SCENARIO_H
