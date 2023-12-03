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

        /**
         * @brief Computes the maneuver point for a given set of orbital parameters.
         *
         * This function computes the maneuver point for a given set of orbital parameters. The maneuver point represents the
         * position in the orbit where a maneuver is planned to be executed.
         *
         * @param orbitalParameters The orbital parameters used to compute the maneuver point.
         *
         * @note The orbital parameters should be in a consistent reference frame and units.
         *
         * @return The computed maneuver point.
         *
         * @remark The computation is performed by taking into account various factors such as the current position, velocity,
         *         gravitational and perturbation forces acting on the spacecraft.
         *
         * @see IO::Astrodynamics::OrbitalParameters::OrbitalParameters
         * @see IO::Astrodynamics::ManeuverPoint
         */
        Math::Vector3D ManeuverPointComputation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters& orbitalParameters) override;
    public:
        /**
         * @brief Construct a new Combined Maneuver object
         * 
         */
        CombinedManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, double inclination, double perigeeRadius);

        CombinedManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, double inclination, double perigeeRadius, const IO::Astrodynamics::Time::TDB &minimumEpoch);

        using IO::Astrodynamics::Maneuvers::ManeuverBase::GetDeltaV;
//        bool CanExecute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParams) override;
    };
}

#endif