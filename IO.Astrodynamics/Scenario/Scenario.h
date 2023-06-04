/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by s.guillet on 23/02/2023.
//

#ifndef IOSDK_SCENARIO_H
#define IOSDK_SCENARIO_H

#include <map>

#include <VVIntegrator.h>
#include <LaunchWindow.h>
#include "Propagator.h"

namespace IO::Astrodynamics::Integrators
{
    class VVIntegrator;
}

namespace IO::Astrodynamics::Constraints::Parameters
{
    class ByDayParameters;

    class ByNightParameters;

    class BodyVisibilityFromSiteParameters;

    class OccultationParameters;

    class DistanceParameters;

    class InFieldOfViewParameters;

    class LaunchParameters;
}

namespace IO::Astrodynamics
{
    class Scenario
    {
    private:
        const std::string m_name;
        const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> m_windows;
        std::vector<const IO::Astrodynamics::Body::CelestialBody *> m_celestialBodies;
        std::vector<const IO::Astrodynamics::Sites::Site *> m_sites;

        IO::Astrodynamics::Integrators::Forces::GravityForce m_gravityForce;
        std::vector<IO::Astrodynamics::Integrators::Forces::Force *> m_forces{&m_gravityForce};
        const IO::Astrodynamics::Integrators::VVIntegrator m_integrator;

        std::unique_ptr<Propagators::Propagator> m_propagator;
        const Body::Spacecraft::Spacecraft *m_spacecraft{nullptr};

    public:
        Scenario(std::string name, const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &windows);

        /**
         * Add celestial body to the scenario
         * @param celestialBody
         */
        void AddCelestialBody(const IO::Astrodynamics::Body::CelestialBody &celestialBody);

        /**
         * Add Spacecraft to the scenario
         * @param spacecraft
         */
        void AttachSpacecraft(const IO::Astrodynamics::Body::Spacecraft::Spacecraft &spacecraft);

        /**
         * Add a site to the scenario
         * @param site
         */
        void AddSite(const IO::Astrodynamics::Sites::Site &site);

        /**
         * Get the name of the scenario
         * @return
         */
        [[nodiscard]] inline std::string GetName() const
        { return m_name; }

        /**
         * Get the window of the scenario
         * @return
         */
        [[nodiscard]] inline const IO::Astrodynamics::Time::Window<IO::Astrodynamics::Time::UTC> &GetWindow() const
        { return m_windows; }

        /**
         * Get celestial bodies of the scenario
         * @return
         */
        inline const std::vector<const IO::Astrodynamics::Body::CelestialBody *> &GetCelestialBodies()
        { return m_celestialBodies; }

        /**
         * Get spacecrafts of the scenario
         * @return
         */
        inline const IO::Astrodynamics::Body::Spacecraft::Spacecraft *GetSpacecraft()
        { return m_spacecraft; }

        /**
         * Get the Sites of the scenario
         * @return
         */
        inline const std::vector<const IO::Astrodynamics::Sites::Site *> &GetSites()
        { return m_sites; }

        /**
         * Return the propagator used by this scenario
         * @return
         */
        inline Propagators::Propagator &GetPropagator()
        { return *m_propagator; }

        /**
         * Execute scenario
         */
        void Execute();
    };

} // IO::Astrodynamics

#endif //IOSDK_SCENARIO_H
