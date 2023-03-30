/**
 * @file GravityForce.cpp
 * @author Sylvain Guillet (sylvain.guillet@live.com)
 * @brief 
 * @version 0.1
 * @date 2021-07-03
 * 
 * @copyright Copyright (c) 2021
 * 
 */
#include <GravityForce.h>
#include <Constants.h>
#include <InertialFrames.h>
#include <Aberrations.h>
#include "Helpers/Type.cpp"

IO::SDK::Integrators::Forces::GravityForce::GravityForce(/* args */)
{
}

IO::SDK::Integrators::Forces::GravityForce::~GravityForce()
{
}

IO::SDK::Math::Vector3D IO::SDK::Integrators::Forces::GravityForce::Apply(const IO::SDK::Body::Body &body, const IO::SDK::OrbitalParameters::StateVector &stateVector)
{
    //Compute force from his major body
    double bodyMass = body.GetMass();
    IO::SDK::Math::Vector3D position{stateVector.GetPosition()};
    IO::SDK::Math::Vector3D force{ComputeForce(stateVector.GetCenterOfMotion()->GetMass(), bodyMass, position.Magnitude(), position.Normalize())};

    //Each body is under sphere of influence of his major body
    //So spacecraft is influenced by his center of motion and his parents
    //Eg. Sun->Earth->Moon->Spacecraft
    std::shared_ptr<IO::SDK::Body::Body> currentBody = stateVector.GetCenterOfMotion();
    while (currentBody->GetOrbitalParametersAtEpoch())
    {
        //Compute vector state
        position = position + currentBody->ReadEphemeris(stateVector.GetFrame(), AberrationsEnum::None, stateVector.GetEpoch(),*currentBody->GetOrbitalParametersAtEpoch()->GetCenterOfMotion()).GetPosition();

        //Compute force
        force = force + ComputeForce(currentBody->GetOrbitalParametersAtEpoch()->GetCenterOfMotion()->GetMass(),bodyMass, position.Magnitude(), position.Normalize());

        //Set next parent
        currentBody = currentBody->GetOrbitalParametersAtEpoch()->GetCenterOfMotion();
    }

    //Compute force induced by others satellites with the same center of motion
    for (auto &&sat : body.GetOrbitalParametersAtEpoch()->GetCenterOfMotion()->GetSatellites())
    {
        if (*sat == body || !IO::SDK::Helpers::IsInstanceOf<IO::SDK::Body::CelestialBody>(sat))
        {
            continue;
        }
        auto sv = sat->ReadEphemeris(stateVector.GetFrame(), IO::SDK::AberrationsEnum::None, stateVector.GetEpoch());

        auto relativePosition = stateVector.GetPosition() - sv.GetPosition();

        force = force + ComputeForce(sat->GetOrbitalParametersAtEpoch()->GetCenterOfMotion()->GetMass(), bodyMass, relativePosition.Magnitude(), relativePosition.Normalize());
    }

    return force;
}

IO::SDK::Math::Vector3D IO::SDK::Integrators::Forces::ComputeForce(const double m1, const double m2, const double distance, const IO::SDK::Math::Vector3D &u12)
{
    return u12 * (-IO::SDK::Constants::G * ((m1 * m2) / (distance * distance)));
}