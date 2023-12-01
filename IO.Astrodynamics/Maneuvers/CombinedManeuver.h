/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
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

namespace IO::Astrodynamics::Maneuvers
{
    class CombinedManeuver final : public IO::Astrodynamics::Maneuvers::ManeuverBase
    {
    private:
        double m_inclination;
        double m_peregeeRadius;

        [[nodiscard]] IO::Astrodynamics::Math::Vector3D GetDeltaV(const IO::Astrodynamics::OrbitalParameters::StateVector& sv) const;

    protected:
        /**
         * @brief Compute impulsive maneuver
         * 
         * @param maneuverPoint 
         */
        void Compute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint) override;

        /**
         * @brief Compute orientation
         * 
         * @param maneuverPoint 
         * @return IO::Astrodynamics::OrbitalParameters::StateOrientation
         */
        IO::Astrodynamics::OrbitalParameters::StateOrientation ComputeOrientation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint) override;
        Math::Vector3D ManeuverPointComputation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters& orbitalParameters) override;
    public:
        /**
         * @brief Construct a new Combined Maneuver object
         * 
         */
        CombinedManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, double inclination, double perigeeRadius);

        CombinedManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, double inclination, double perigeeRadius, const IO::Astrodynamics::Time::TDB &minimumEpoch);

        /**
         * @brief Evaluate if maneuver can occurs
         * 
         * @param stateVector Evaluated to check if condition is satisfied
         * @return true 
         * @return false 
         */
        bool CanExecute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParams) override;

        using IO::Astrodynamics::Maneuvers::ManeuverBase::GetDeltaV;
    };
}

#endif