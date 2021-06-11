#ifndef CENTRIFUGAL_FORCE_H
#define CENTRIFUGAL_FORCE_H

#include <Force.h>

namespace IO::SDK::Integrators::Forces
{
    class CentrifugalForce : public IO::SDK::Integrators::Forces::Force
    {
    private:
        /* data */
    public:
        CentrifugalForce(/* args */);
        ~CentrifugalForce();

        IO::SDK::Math::Vector3D Apply(const IO::SDK::Body::Body &body, const IO::SDK::OrbitalParameters::StateVector &stateVector) override;
    };

}

#endif