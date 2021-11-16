/**
 * @file PhasingManeuver.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#ifndef PHASING_MANEUVER_H
#define PHASING_MANEUVER_H

#include <memory>
#include <vector>

#include <ManeuverBase.h>
#include <OrbitalParameters.h>
#include <Engine.h>
#include <Spacecraft.h>
#include <Propagator.h>
#include <StateOrientation.h>
#include <TimeSpan.h>

namespace IO::SDK::Maneuvers
{
    class PhasingManeuver final : public IO::SDK::Maneuvers::ManeuverBase
    {
    private:
        const unsigned int m_revolutionsNumber;
        std::unique_ptr<bool> m_isApproachingPerigee{nullptr};
        IO::SDK::OrbitalParameters::OrbitalParameters *m_targetOrbit{nullptr};

        bool IsApproachingPerigee(const IO::SDK::OrbitalParameters::StateVector &stateVector) const;

        /**
         * @brief Compute true anomaly delta
         * 
         * @param orbitalParameters 
         * @return double 
         */
        double DeltaTrueAnomaly(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParameters);

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
        PhasingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const unsigned revolutionNumber, IO::SDK::OrbitalParameters::OrbitalParameters *targetOrbit);
        PhasingManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, const unsigned revolutionNumber, IO::SDK::OrbitalParameters::OrbitalParameters *targetOrbit, const IO::SDK::Time::TDB &minimumEpoch);

        /**
         * @brief Evaluate if maneuver can occurs
         * 
         * @param stateVector Evaluated to check if condition is satisfied
         * @return true 
         * @return false 
         */
        bool CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams) override;
    };

} // namespace IO::SDK::Maneuvers

#endif