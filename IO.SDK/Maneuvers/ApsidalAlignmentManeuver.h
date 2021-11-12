/**
 * @file ApsidalAlignmentManeuver.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-04-26
 * 
 * @copyright Copyright (c) 2021
 * 
 */

#ifndef APSIDAL_ALIGNMENT_MANEUVER_H
#define APSIDAL_ALIGNMENT_MANEUVER_H

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
    class ApsidalAlignmentManeuver final : public IO::SDK::Maneuvers::ManeuverBase
    {
    private:
        double m_theta{};
        std::unique_ptr<bool> m_isApproachingP{nullptr};
        std::unique_ptr<bool> m_isApproachingQ{nullptr}; 
        bool m_isIntersectP;       
        bool m_isIntersectQ;       

        bool IsIntersectP(const IO::SDK::OrbitalParameters::StateVector &stateVector) const;
        bool IsIntersectQ(const IO::SDK::OrbitalParameters::StateVector &stateVector) const;

        bool IsApproachingIntersectPointP(const IO::SDK::OrbitalParameters::StateVector &stateVector) const;
        bool IsApproachingIntersectPointQ(const IO::SDK::OrbitalParameters::StateVector &stateVector) const;

        std::map<std::string, double> GetCoefficients(const IO::SDK::OrbitalParameters::StateVector &stateVector) const;

        double GetPTrueAnomaly(const IO::SDK::OrbitalParameters::StateVector &sv) const;
        double GetQTrueAnomaly(const IO::SDK::OrbitalParameters::StateVector &sv) const;

        double GetPTargetTrueAnomaly(const IO::SDK::OrbitalParameters::StateVector &sv) const;
        double GetQTargetTrueAnomaly(const IO::SDK::OrbitalParameters::StateVector &sv) const;
        IO::SDK::Math::Vector3D GetDeltaV(const IO::SDK::OrbitalParameters::StateVector &sv) const;

        IO::SDK::OrbitalParameters::OrbitalParameters *m_targetOrbit{nullptr};

    protected:
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

    public:
        ApsidalAlignmentManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, IO::SDK::OrbitalParameters::OrbitalParameters *targetOrbit);
        ApsidalAlignmentManeuver(const std::vector<IO::SDK::Body::Spacecraft::Engine> &engines, IO::SDK::Propagators::Propagator &propagator, IO::SDK::OrbitalParameters::OrbitalParameters *targetOrbit, const IO::SDK::Time::TDB &minimumEpoch );

        using IO::SDK::Maneuvers::ManeuverBase::GetDeltaV;

        /**
         * @brief Define maneuver execution condition
         * 
         * @param stateVector 
         * @return true 
         * @return false 
         */
        virtual bool CanExecute(const IO::SDK::OrbitalParameters::OrbitalParameters &orbitalParams) override;

        /**
         * @brief Get the theta angle
         * 
         * @return double 
         */
        double GetTheta() const;

        /**
         * @brief Get the Theta
         * 
         * @param stateVector 
         * @return double 
         */
        double GetTheta(const IO::SDK::OrbitalParameters::StateVector &stateVector) const;
    };
}

#endif