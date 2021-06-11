#include <CentrifugalForce.h>

IO::SDK::Integrators::Forces::CentrifugalForce::CentrifugalForce(/* args */)
{
}

IO::SDK::Integrators::Forces::CentrifugalForce::~CentrifugalForce()
{
}

IO::SDK::Math::Vector3D IO::SDK::Integrators::Forces::CentrifugalForce::Apply(const IO::SDK::Body::Body &body, const IO::SDK::OrbitalParameters::StateVector &stateVector)
{
    IO::SDK::Math::Vector3D direction{stateVector.GetPosition().Normalize()};
    IO::SDK::Math::Vector3D force{direction * ((body.GetMass() * stateVector.GetVelocity().Magnitude()* stateVector.GetVelocity().Magnitude()) / stateVector.GetPosition().Magnitude())};

    return force;
}