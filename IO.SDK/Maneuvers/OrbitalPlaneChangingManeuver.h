/**
 * @file OrbitalPlaneChangingManeuver.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.x
 * @date 2021-03-08
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef ORBITAL_PLANE_CHANGING_MANEUVER_H
#define ORBITAL_PLANE_CHANGING_MANEUVER_H

#include <ManeuverBase.h>

namespace IO::SDK::Maneuvers
{
    class OrbitalPlaneChangingManeuver final : public IO::SDK::Maneuvers::ManeuverBase
    {
    private:
        double m_relativeInclination{};

        std::shared_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> m_targetOrbit;
        bool m_isAscendingNode{false};


    public:
        /**
         * @brief Construct a new Orbital Plane Changing Maneuver object
         * 
         * @param engines 
         * @param propagator 
         * @param targetOrbit 
         */
        OrbitalPlaneChangingManeuver(std::vector<IO::SDK::Body::Spacecraft::Engine*> engines, IO::SDK::Propagators::Propagator &propagator, std::shared_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> targetOrbit);
        
        /**
         * @brief Construct a new Orbital Plane Changing Maneuver object
         * 
         * @param engines 
         * @param propagator 
         * @param targetOrbit 
         * @param minimumEpoch 
         */
        OrbitalPlaneChangingManeuver(std::vector<IO::SDK::Body::Spacecraft::Engine*> engines, IO::SDK::Propagators::Propagator &propagator, std::shared_ptr<IO::SDK::OrbitalParameters::OrbitalParameters> targetOrbit, const IO::SDK::Time::TDB &minimumEpoch);

        /**
         * @brief Define maneuver execution condition
         * 
         * @param stateVector 
         * @return true 
         * @return false 
         */
        bool CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams) override;

        /**
         * @brief Compute impulsive maneuver
         * 
         * @param maneuverPoint 
         */
        void Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint) override;

        /**
         * @brief 
         * 
         * @param maneuverPoint 
         * @return IO::SDK::OrbitalParameters::StateOrientation 
         */
        IO::SDK::OrbitalParameters::StateOrientation ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint) override;

        /**
         * @brief Get the Relative Inclination
         * 
         * @return double 
         */
        [[nodiscard]] double GetRelativeInclination() const;
    };
}

#endif