/**
 * @file ApogeeHeightChangingManeuver.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef APOGEE_HEIGHT_CHANGING_MANEUVERS_H
#define APOGEE_HEIGHT_CHANGING_MANEUVERS_H

#include <memory>
#include <vector>

#include <ManeuverBase.h>
#include <OrbitalParameters.h>
#include <Engine.h>
#include <Spacecraft.h>
#include <Propagator.h>
#include <StateVector.h>
#include <StateOrientation.h>

namespace IO::SDK::Maneuvers
{
    class ApogeeHeightChangingManeuver final : public IO::SDK::Maneuvers::ManeuverBase
    {
    private:
        std::unique_ptr<bool> m_isApproachingPerigee{nullptr};
        const double m_targetHeight;

        bool IsApproachingPerigee(const IO::SDK::OrbitalParameters::StateVector &stateVector) const;

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
        /* data */
    public:
        ApogeeHeightChangingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const double targetHeight);
        ApogeeHeightChangingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const double targetHeight, const IO::SDK::Time::TDB &minimumEpoch);

        /**
         * @brief Evaluate if maneuver can occurs
         * 
         * @param stateVector Evaluated to check if condition is satisfied
         * @return true 
         * @return false 
         */
        bool CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams) override;
    };
}

#endif