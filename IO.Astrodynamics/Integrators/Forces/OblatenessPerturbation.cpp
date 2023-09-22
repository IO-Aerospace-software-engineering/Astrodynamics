/*
 Copyright (c) 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */

//
// Created by spacer on 9/22/23.
//

#include "OblatenessPerturbation.h"

IO::Astrodynamics::Integrators::Forces::OblatenessPerturbation::OblatenessPerturbation(/* args */)
= default;

IO::Astrodynamics::Integrators::Forces::OblatenessPerturbation::~OblatenessPerturbation() = default;

/**
 * @class Apply
 * @brief Applies the force of oblateness perturbation on a celestial item.
 *
 * This function calculates and applies the force of oblateness perturbation on a celestial item,
 * taking into account its orbital parameters and state vector.
 *
 * @param body The celestial item on which the force is to be applied.
 * @param stateVector The state vector of the celestial item.
 *
 * @return void
 */

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::Integrators::Forces::OblatenessPerturbation::Apply(const IO::Astrodynamics::Body::CelestialItem &body,
                                                                                                        const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector)
{
    double bodyMass = body.GetMass();
    IO::Astrodynamics::Math::Vector3D position{stateVector.GetPosition()};

    const std::shared_ptr<IO::Astrodynamics::Body::CelestialBody> &currentBody = stateVector.GetCenterOfMotion();

    auto fixedSv = stateVector.ToBodyFixedFrame().GetPosition();
    const double x = fixedSv.GetX() * 0.001;
    const double y = fixedSv.GetY() * 0.001;
    const double z = fixedSv.GetZ() * 0.001;

    auto x2y2 = x * x + y * y;
    auto r7 = std::pow(fixedSv.Magnitude()*0.001, 7);
    auto re = currentBody->GetRadius().GetX() * 0.001;

    //j2 according to JGM-3
    auto j2 = currentBody->GetJ2() * re * re * currentBody->GetMu() * 1E-09;

    auto z2 = z * z;

    double xFinal = j2 * (x / r7) * (6 * z2 - 1.5 * x2y2);
    double yFinal = j2 * (y / r7) * (6 * z2 - 1.5 * x2y2);
    double zFinal = j2 * (z / r7) * (3 * z2 - 4.5 * x2y2);

    IO::Astrodynamics::Math::Vector3D fixedFrameForce{xFinal, yFinal, zFinal};

    return currentBody->GetBodyFixedFrame().TransformVector(stateVector.GetFrame(), fixedFrameForce, stateVector.GetEpoch());
}
