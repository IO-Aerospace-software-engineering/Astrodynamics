/*
 Copyright (c) 2021-2023. Sylvain Guillet (sylvain.guillet@tutamail.com)
 */
#include <GravityForce.h>
#include <Constants.h>
#include <InertialFrames.h>
#include <Aberrations.h>
#include <Type.h>

IO::Astrodynamics::Integrators::Forces::GravityForce::GravityForce(/* args */)
= default;

IO::Astrodynamics::Integrators::Forces::GravityForce::~GravityForce() = default;

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::Integrators::Forces::GravityForce::Apply(const IO::Astrodynamics::Body::Body &body, const IO::Astrodynamics::OrbitalParameters::StateVector &stateVector) {
    //Compute force from his major body
    double bodyMass = body.GetMass();
    IO::Astrodynamics::Math::Vector3D position{stateVector.GetPosition()};
    IO::Astrodynamics::Math::Vector3D force{ComputeForce(stateVector.GetCenterOfMotion()->GetMass(), bodyMass, position.Magnitude(), position.Normalize())};

    //Each body is under sphere of influence of his major body
    //So Spacecraft is influenced by his center of motion and his parents
    //Eg. Sun->Earth->Moon->Spacecraft
    std::shared_ptr<IO::Astrodynamics::Body::Body> currentBody = stateVector.GetCenterOfMotion();
    while (currentBody->GetOrbitalParametersAtEpoch()) {
        //Compute vector state
        position=position+ currentBody->ReadEphemeris(stateVector.GetFrame(), AberrationsEnum::None, stateVector.GetEpoch(),
                                            *currentBody->GetOrbitalParametersAtEpoch()->GetCenterOfMotion()).GetPosition();
        //Compute force
        force = force + ComputeForce(currentBody->GetOrbitalParametersAtEpoch()->GetCenterOfMotion()->GetMass(), bodyMass, position.Magnitude(), position.Normalize());

        //Set next parent
        currentBody = currentBody->GetOrbitalParametersAtEpoch()->GetCenterOfMotion();
    }

    //Compute force induced by others satellites with the same center of motion
    for (auto &&sat: body.GetOrbitalParametersAtEpoch()->GetCenterOfMotion()->GetSatellites()) {
        if (*sat == body || !IO::Astrodynamics::Helpers::IsInstanceOf<IO::Astrodynamics::Body::CelestialBody>(sat)) {
            continue;
        }
        auto sv = sat->ReadEphemeris(stateVector.GetFrame(), IO::Astrodynamics::AberrationsEnum::None, stateVector.GetEpoch());

        auto relativePosition = stateVector.GetPosition() - sv.GetPosition();

        force = force + ComputeForce(sat->GetOrbitalParametersAtEpoch()->GetCenterOfMotion()->GetMass(), bodyMass, relativePosition.Magnitude(), relativePosition.Normalize());
    }

    return force;
}

IO::Astrodynamics::Math::Vector3D IO::Astrodynamics::Integrators::Forces::ComputeForce(const double m1, const double m2, const double distance, const IO::Astrodynamics::Math::Vector3D &u12) {
    return u12 * (-IO::Astrodynamics::Constants::G * ((m1 * m2) / (distance * distance)));
}