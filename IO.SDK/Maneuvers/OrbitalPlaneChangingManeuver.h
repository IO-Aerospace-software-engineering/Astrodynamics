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
    class OrbitalPlaneChangingManeuver : public IO::SDK::Maneuvers::ManeuverBase
    {
    private:
        double m_relativeInclination{};
        std::unique_ptr<bool> m_isApproachingNode{nullptr};

        IO::SDK::OrbitalParameters::OrbitalParameters *m_targetOrbit{nullptr};


    public:
        OrbitalPlaneChangingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft, IO::SDK::Propagators::Propagator *propagator, IO::SDK::OrbitalParameters::OrbitalParameters *targetOrbit);
        OrbitalPlaneChangingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, const IO::SDK::Time::TDB &minimumEpoch, const IO::SDK::Body::Spacecraft::Spacecraft &spacecraft, IO::SDK::Propagators::Propagator *propagator, IO::SDK::OrbitalParameters::OrbitalParameters *targetOrbit);
        ~OrbitalPlaneChangingManeuver() = default;

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

        bool IsAscendingNode(const IO::SDK::OrbitalParameters::StateVector &stateVector) const;
        bool IsApproachingAscendingNode(const IO::SDK::OrbitalParameters::StateVector &stateVector) const;
        double GetRelativeInclination() const;
    };
}

#endif