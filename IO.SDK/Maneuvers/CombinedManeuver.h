/**
 * @file CombinedManeuver.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef COMBINED_MANEUVER_H
#define COMBINED_MANEUVER_H

#include <vector>
#include <memory>
#include <map>
#include <string>

#include <Spacecraft.h>
#include <Engine.h>
#include <Propagator.h>
#include <OrbitalParameters.h>
#include <ManeuverBase.h>
#include <TDB.h>
#include <StateVector.h>
#include <StateOrientation.h>

namespace IO::SDK::Maneuvers
{
    class CombinedManeuver final : public IO::SDK::Maneuvers::ManeuverBase
    {
    private:
        double m_relativeInclination{};
        double m_inclination;
        double m_peregeeRadius;
        std::unique_ptr<bool> m_isApproachingNode{nullptr};

        IO::SDK::Math::Vector3D GetDeltaV(const IO::SDK::OrbitalParameters::StateVector& sv) const;

    protected:
        /**
         * @brief Compute impulsive maneuver
         * 
         * @param maneuverPoint 
         */
        void Compute(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint) override;

        /**
         * @brief Compute orientation
         * 
         * @param maneuverPoint 
         * @return IO::SDK::OrbitalParameters::StateOrientation 
         */
        IO::SDK::OrbitalParameters::StateOrientation ComputeOrientation(const IO::SDK::OrbitalParameters::OrbitalParameters &maneuverPoint) override;

        bool IsAscendingNode(const IO::SDK::OrbitalParameters::StateVector &stateVector) const;
        bool IsApproachingAscendingNode(const IO::SDK::OrbitalParameters::StateVector &stateVector) const;

    public:
        /**
         * @brief Construct a new Combined Maneuver object
         * 
         */
        CombinedManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const double inclination, const double perigeeRadius);

        CombinedManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const double inclination, const double perigeeRadius, const IO::SDK::Time::TDB &minimumEpoch);

        /**
         * @brief Evaluate if maneuver can occurs
         * 
         * @param stateVector Evaluated to check if condition is satisfied
         * @return true 
         * @return false 
         */
        bool CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams) override;

        using IO::SDK::Maneuvers::ManeuverBase::GetDeltaV;
    };
}

#endif