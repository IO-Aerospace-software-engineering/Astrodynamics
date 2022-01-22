/**
 * @file OrbitalPlaneChangingManeuver.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-03-08
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef ORBITAL_PLANE_CHANGING_MANEUVER_H
#define ORBITAL_PLANE_CHANGING_MANEUVER_H

#include <ManeuverBase.h>
#include <Spacecraft.h>
#include <Propagator.h>
#include <OrbitalParameters.h>
#include <memory>

namespace IO::SDK::Maneuvers
{
    class OrbitalPlaneChangingManeuver final : public IO::SDK::Maneuvers::ManeuverBase
    {
    private:
        double m_relativeInclination{};

        IO::SDK::OrbitalParameters::OrbitalParameters *m_targetOrbit{nullptr};
        bool m_isAscendingNode{false};


    public:
        /**
         * @brief Construct a new Orbital Plane Changing Maneuver object
         * 
         * @param engines 
         * @param propagator 
         * @param targetOrbit 
         */
        OrbitalPlaneChangingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, IO::SDK::OrbitalParameters::OrbitalParameters *targetOrbit);
        
        /**
         * @brief Construct a new Orbital Plane Changing Maneuver object
         * 
         * @param engines 
         * @param propagator 
         * @param targetOrbit 
         * @param minimumEpoch 
         */
        OrbitalPlaneChangingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, IO::SDK::OrbitalParameters::OrbitalParameters *targetOrbit, const IO::SDK::Time::TDB &minimumEpoch);

        /**
         * @brief Define maneuver execution condition
         * 
         * @param stateVector 
         * @return true 
         * @return false 
         */
        virtual bool CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams) override;

        /**
         * @brief Compute impulsive maneuver
         * 
         * @param maneuverPoint 
         */
        virtual void Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint) override;

        /**
         * @brief 
         * 
         * @param maneuverPoint 
         * @return IO::SDK::OrbitalParameters::StateOrientation 
         */
        virtual IO::SDK::OrbitalParameters::StateOrientation ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint) override;

        /**
         * @brief Get the Relative Inclination
         * 
         * @return double 
         */
        double GetRelativeInclination() const;
    };
}

#endif