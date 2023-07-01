/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

#ifndef APSIDAL_ALIGNMENT_MANEUVER_H
#define APSIDAL_ALIGNMENT_MANEUVER_H

#include <memory>
#include <ManeuverBase.h>

namespace IO::Astrodynamics::Maneuvers
{
    class ApsidalAlignmentManeuver final : public IO::Astrodynamics::Maneuvers::ManeuverBase
    {
    private:
        double m_theta{};
        bool m_isIntersectP{false};
        bool m_isIntersectQ{false};

        [[nodiscard]] bool IsIntersectP(const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) const;
        [[nodiscard]] bool IsIntersectQ(const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) const;

        [[nodiscard]] std::map<std::string, double> GetCoefficients(const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) const;

        [[nodiscard]] double GetPTrueAnomaly(const IO::Astrodynamics::OrbitalParameters::StateVector &sv) const;
        [[nodiscard]] double GetQTrueAnomaly(const IO::Astrodynamics::OrbitalParameters::StateVector &sv) const;

        [[nodiscard]] double GetPTargetTrueAnomaly(const IO::Astrodynamics::OrbitalParameters::StateVector &sv) const;
        [[nodiscard]] double GetQTargetTrueAnomaly(const IO::Astrodynamics::OrbitalParameters::StateVector &sv) const;
        [[nodiscard]] IO::Astrodynamics::Math::Vector3D GetDeltaV(const IO::Astrodynamics::OrbitalParameters::StateVector &sv) const;

        std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> m_targetOrbit;

    protected:
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

    public:
        ApsidalAlignmentManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> targetOrbit);
        ApsidalAlignmentManeuver(std::vector<IO::Astrodynamics::Body::Spacecraft::Engine*> engines, IO::Astrodynamics::Propagators::Propagator &propagator, std::shared_ptr<IO::Astrodynamics::OrbitalParameters::OrbitalParameters> targetOrbit, const IO::Astrodynamics::Time::TDB &minimumEpoch);

        using IO::Astrodynamics::Maneuvers::ManeuverBase::GetDeltaV;

        /**
         * @brief Define maneuver execution condition
         * 
         * @param stateVector 
         * @return true 
         * @return false 
         */
        bool CanExecute(const IO::Astrodynamics::OrbitalParameters::OrbitalParameters &orbitalParams) override;

        /**
         * @brief Get the theta angle
         * 
         * @return double 
         */
        [[nodiscard]] double GetTheta() const;

        /**
         * @brief Get the Theta
         * 
         * @param stateVector 
         * @return double 
         */
        [[nodiscard]] double GetTheta(const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) const;
    };
}

#endif