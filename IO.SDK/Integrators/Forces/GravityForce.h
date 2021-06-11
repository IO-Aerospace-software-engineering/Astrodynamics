#ifndef GRAVITY_FORCE_H
#define GRAVITY_FORCE_H



#include <Force.h>
#include <CelestialBody.h>
#include <StateVector.h>

namespace IO::SDK::Integrators::Forces
{
    class GravityForce : public IO::SDK::Integrators::Forces::Force
    {
    private:
        /* data */
    public:
        GravityForce(/* args */);
        ~GravityForce();

        IO::SDK::Math::Vector3D Apply(const IO::SDK::Body::Body &body, const IO::SDK::OrbitalParameters::StateVector &stateVector) override;
        
    };
    IO::SDK::Math::Vector3D ComputeForce(const double m1, const double m2, const double distance, const IO::SDK::Math::Vector3D &u12);

}

#endif