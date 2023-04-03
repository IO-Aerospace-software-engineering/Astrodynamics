/**
 * @file VVIntegrator.h
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-03-17
 * 
 * @copyright Copyright (c) 2021
 * 
 */

#ifndef VVINTEGRATOR_H
#define VVINTEGRATOR_H

#include <vector>
#include <memory>

#include <Force.h>
#include <GravityForce.h>
#include <IntegratorBase.h>
#include <TimeSpan.h>
#include <StateVector.h>
#include <Vector3D.h>
#include <Body.h>
#include <CelestialBody.h>
#include <optional>

//namespace IO::SDK::Integrators::Forces
//{
//    class Force;
//    class GravityForce;
//}

namespace IO::SDK::Integrators
{
    class VVIntegrator final : public IO::SDK::Integrators::IntegratorBase
    {
    private:
        std::vector<IO::SDK::Integrators::Forces::Force *> m_forces{};
        std::optional<IO::SDK::Math::Vector3D> m_acceleration{std::nullopt};

        [[nodiscard]] IO::SDK::Math::Vector3D ComputeAcceleration(const IO::SDK::Body::Body &body, const IO::SDK::OrbitalParameters::StateVector &stateVector) const;

    public:
        /**
         * @brief Construct a new VVIntegrator object
         * 
         * @param stepDuration 
         */
        explicit VVIntegrator(const IO::SDK::Time::TimeSpan &stepDuration);

        /**
         * @brief Construct a new VVIntegrator object
         * 
         * @param stepDuration 
         * @param forces 
         */
        VVIntegrator(const IO::SDK::Time::TimeSpan &stepDuration, std::vector<IO::SDK::Integrators::Forces::Force *>& forces);

        /**
         * @brief Destroy the VVIntegrator object
         * 
         */
        ~VVIntegrator();

        /**
         * @brief Integrate forces
         * 
         * @param body 
         * @param stateVector 
         * @return IO::SDK::OrbitalParameters::StateVector 
         */
        IO::SDK::OrbitalParameters::StateVector Integrate(const IO::SDK::Body::Body &body, const IO::SDK::OrbitalParameters::StateVector &stateVector) override;
    };

}

#endif