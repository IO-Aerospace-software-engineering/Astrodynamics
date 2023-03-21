//
// Created by s.guillet on 23/02/2023.
//

#ifndef IOSDK_SCENARIO_H
#define IOSDK_SCENARIO_H

#include <string>
#include <vector>
#include <map>
#include <optional>

#include <UTC.h>
#include <Window.h>
#include <TimeSpan.h>
#include <CelestialBody.h>
#include <Spacecraft.h>
#include <DistanceParameters.h>
#include <OccultationParameters.h>
#include <ByDayParameters.h>
#include <ByNightParameters.h>
#include <BodyVisibilityFromSiteParameters.h>
#include <InFieldOfViewParameters.h>
#include <Site.h>
#include <VVIntegrator.h>
#include <GravityForce.h>
#include <Force.h>

namespace IO::SDK::Integrators
{
    class VVIntegrator;
}

namespace IO::SDK::Constraints::Parameters
{
    class ByDayParameters;

    class ByNightParameters;

    class BodyVisibilityFromSiteParameters;

    class OccultationParameters;

    class DistanceParameters;

    class InFieldOfViewParameters;
}

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

        IO::SDK::Integrators::Forces::GravityForce m_gravityForce;
        std::vector<IO::SDK::Integrators::Forces::Force *> m_forces{&m_gravityForce};
        const IO::SDK::Integrators::VVIntegrator m_integrator;

        //Body constraints
        std::map<IO::SDK::Constraints::Parameters::DistanceParameters *, std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>>> m_distanceConstraints;

        std::map<IO::SDK::Constraints::Parameters::OccultationParameters *, std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>>> m_occultationConstraints;

        //Site constraints
        std::map<IO::SDK::Constraints::Parameters::ByDayParameters *, std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>>> m_dayConstraints;

        std::map<IO::SDK::Constraints::Parameters::ByNightParameters *, std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>>> m_nightConstraints;

        std::map<IO::SDK::Constraints::Parameters::BodyVisibilityFromSiteParameters *, std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>>> m_bodyVisibilityConstraints;

        std::map<IO::SDK::Constraints::Parameters::InFieldOfViewParameters *, std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>>> m_inFieldOfViewConstraints;

    public:
        Scenario(const std::string &name, const IO::SDK::Time::Window<IO::SDK::Time::UTC> &windows);

        /**
         * Add celestial body to the scenario
         * @param celestialBody
         */
        void AddCelestialBody(const IO::SDK::Body::CelestialBody &celestialBody);

        /**
         * Add spacecraft to the scenario
         * @param spacecraft
         */
        void AddSpacecraft(const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft);

        /**
         * Add a site to the scenario
         * @param site
         */
        void AddSite(const IO::SDK::Sites::Site &site);

        /**
         * Get the name of the scenario
         * @return
         */
        inline const std::string GetName() const
        { return m_name; }

        /**
         * Get the window of the scenario
         * @return
         */
        inline const IO::SDK::Time::Window<IO::SDK::Time::UTC> &GetWindow() const
        { return m_windows; }

        /**
         * Get celestial bodies of the scenario
         * @return
         */
        inline const std::vector<const IO::SDK::Body::CelestialBody *> &GetCelestialBodies()
        { return m_celestialBodies; }

        /**
         * Get spacecrafts of the scenario
         * @return
         */
        inline const std::vector<const IO::SDK::Body::Spacecraft::Spacecraft *> &GetSpacecrafts()
        { return m_spacecrafts; }

        /**
         * Get the sites of the scenario
         * @return
         */
        inline const std::vector<const IO::SDK::Sites::Site *> &GetSites()
        { return m_sites; }

        /**
         * Get distance constraints of the scenario
         * @return
         */
        inline const std::map<IO::SDK::Constraints::Parameters::DistanceParameters *, std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>>> &
        GetDistanceConstraints() const
        {
            return m_distanceConstraints;
        }

        /**
         * Get occultation constraints of the scenario
         * @return
         */
        inline const std::map<IO::SDK::Constraints::Parameters::OccultationParameters *, std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>>> &
        GetOccultationConstraints() const
        {
            return m_occultationConstraints;
        }

        /**
         * Get by day constraints of the scenario
         * @return
         */
        inline const std::map<IO::SDK::Constraints::Parameters::ByDayParameters *, std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>>> &
        GetByDaysConstraints() const
        {
            return m_dayConstraints;
        }

        /**
         * Get by night constraints of the scenario
         * @return
         */
        inline const std::map<IO::SDK::Constraints::Parameters::ByNightParameters *, std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>>> &
        GetByNightConstraints() const
        {
            return m_nightConstraints;
        }

        /**
         * Get body visibility constraint from site
         * @return
         */
        inline const std::map<IO::SDK::Constraints::Parameters::BodyVisibilityFromSiteParameters *, std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::UTC>>>> &
        GetBodyVisibilityFromSiteConstraints() const
        {
            return m_bodyVisibilityConstraints;
        }

        /**
         * Get in field of view constraint parameter
         * @return
         */
        inline const std::map<IO::SDK::Constraints::Parameters::InFieldOfViewParameters *, std::optional<std::vector<IO::SDK::Time::Window<IO::SDK::Time::TDB>>>> &
        GetInFieldOfViewConstraints() const
        {
            return m_inFieldOfViewConstraints;
        }

        /**
         * Add a distance constraint
         * @param distanceParameters
         */
        void AddDistanceConstraint(Constraints::Parameters::DistanceParameters *distanceParameters);

        /**
         * Add an occultation constraint
         * @param occultationParameters
         */
        void AddOccultationConstraint(IO::SDK::Constraints::Parameters::OccultationParameters *occultationParameters);

        /**
         * Add a day constraint
         * @param byDayParameters
         */
        void AddDayConstraint(IO::SDK::Constraints::Parameters::ByDayParameters *byDayParameters);

        /**
         * Add a night constraint
         * @param byNightParameters
         */
        void AddNightConstraint(IO::SDK::Constraints::Parameters::ByNightParameters *byNightParameters);

        /**
         * Add a body visibility constraint
         * @param bodyVisibilityParameters
         */
        void AddBodyVisibilityConstraint(IO::SDK::Constraints::Parameters::BodyVisibilityFromSiteParameters *bodyVisibilityParameters);

        /**
         * Add a field of view constraint
         * @param inFieldOfViewParameters
         */
        void AddInFieldOfViewConstraint(IO::SDK::Constraints::Parameters::InFieldOfViewParameters *inFieldOfViewParameters);

        void Execute();
    };

} // IO::SDK

#endif //IOSDK_SCENARIO_H
