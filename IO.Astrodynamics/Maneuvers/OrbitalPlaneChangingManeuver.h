/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#ifndef ORBITAL_PLANE_CHANGING_MANEUVER_H
#define ORBITAL_PLANE_CHANGING_MANEUVER_H

#include <ManeuverBase.h>

namespace IO::Astrodynamics::Maneuvers
{
    class OrbitalPlaneChangingManeuver final : public IO::Astrodynamics::Maneuvers::ManeuverBase
    {
    private:
        double m_relativeInclination{};

        std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> m_targetOrbit;
        bool m_isAscendingNode{false};
    protected:
        Math::Vector3D ManeuverPointComputation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters& orbitalParameters) override;

    public:
        /**
         * @brief Construct a new Orbital Plane Changing Maneuver object
         * 
         * @param engines 
         * @param propagator 
         * @param targetOrbit 
         */
        OrbitalPlaneChangingManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> targetOrbit);
        
        /**
         * @brief Construct a new Orbital Plane Changing Maneuver object
         * 
         * @param engines 
         * @param propagator 
         * @param targetOrbit 
         * @param minimumEpoch 
         */
        OrbitalPlaneChangingManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> targetOrbit, const IO::Astrodynamics::Time::TDB &minimumEpoch);

        /**
         * @brief Compute impulsive maneuver
         * 
         * @param maneuverPoint 
         */
        void Compute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint) override;

        /**
         * @brief 
         * 
         * @param maneuverPoint 
         * @return IO::Astrodynamics::OrbitalParameters::StateOrientation
         */
        IO::Astrodynamics::OrbitalParameters::StateOrientation ComputeOrientation(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &maneuverPoint) override;

        /**
         * @brief Get the Relative Inclination
         * 
         * @return double 
         */
        [[nodiscard]] double GetRelativeInclination() const;
    };
}

#endif