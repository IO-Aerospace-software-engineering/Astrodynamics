/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
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

//namespace IO::Astrodynamics::Integrators::Forces
//{
//    class Force;
//    class GravityForce;
//}

namespace IO::Astrodynamics::Integrators
{
    class VVIntegrator final : public IO::Astrodynamics::Integrators::IntegratorBase
    {
    private:
        std::vector<IO::Astrodynamics::Integrators::Forces::Force *> m_forces{};
        std::optional<IO::Astrodynamics::Math::Vector3D> m_acceleration{std::nullopt};

        [[nodiscard]] IO::Astrodynamics::Math::Vector3D ComputeAcceleration(const IO::Astrodynamics::Body::Body &body, const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) const;

    public:
        /**
         * @brief Construct a new VVIntegrator object
         * 
         * @param stepDuration 
         */
        explicit VVIntegrator(const IO::Astrodynamics::Time::TimeSpan &stepDuration);

        /**
         * @brief Construct a new VVIntegrator object
         * 
         * @param stepDuration 
         * @param forces 
         */
        VVIntegrator(const IO::Astrodynamics::Time::TimeSpan &stepDuration, std::vector<IO::Astrodynamics::Integrators::Forces::Force *>& forces);

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
         * @return IO::Astrodynamics::OrbitalParameters::StateVector
         */
        IO::Astrodynamics::OrbitalParameters::StateVector Integrate(const IO::Astrodynamics::Body::Body &body, const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) override;
    };

}

#endif