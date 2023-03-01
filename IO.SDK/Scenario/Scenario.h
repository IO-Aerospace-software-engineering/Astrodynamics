//
// Created by s.guillet on 23/02/2023.
//

#ifndef IOSDK_SCENARIO_H
#define IOSDK_SCENARIO_H

#include <string>
#include <vector>
#include <map>
#include <optional>

#include "Body/Body.h"
#include "Time/UTC.h"
#include "Time/Window.h"
#include "Time/TimeSpan.h"
#include "Body/CelestialBody.h"
#include "Body/Spacecraft/Spacecraft.h"
#include <DistanceParameters.h>
#include <OccultationParameters.h>
#include <ByDayParameters.h>
#include <ByNightParameters.h>
#include <BodyVisibilityParameters.h>


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
        std::map<IO::SDK::Constraints::Parameters::DistanceParameters, std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>>> m_distanceConstraints;

        std::map<IO::SDK::Constraints::Parameters::OccultationParameters, std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>>> m_occultationConstraints;

        //Site constraints
        std::map<IO::SDK::Constraints::Parameters::ByDayParameters, std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>>> m_dayConstraints;

        std::map<IO::SDK::Constraints::Parameters::ByNightParameters, std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>>> m_nightConstraints;

        std::map<IO::SDK::Constraints::Parameters::BodyVisibilityParameters, std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>>> m_bodyVisibilityConstraints;


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

        inline const std::map<IO::SDK::Constraints::Parameters::DistanceParameters, std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>>> &
        GetDistanceConstraints() const
        {
            return m_distanceConstraints;
        }

        void AddDistanceConstraint(Constraints::Parameters::DistanceParameters &distanceParameters);

        void AddOccultationConstraint(IO::SDK::Constraints::Parameters::OccultationParameters &occultationParameters);

        void AddDayConstraint(IO::SDK::Constraints::Parameters::ByDayParameters &byDayParameters);

        void AddNightConstraint(IO::SDK::Constraints::Parameters::ByNightParameters &byNightParameters);

        void AddBodyVisibilityConstraint(IO::SDK::Constraints::Parameters::BodyVisibilityParameters &bodyVisibilityParameters);

        void Execute();
    };

} // IO::SDK

#endif //IOSDK_SCENARIO_H
