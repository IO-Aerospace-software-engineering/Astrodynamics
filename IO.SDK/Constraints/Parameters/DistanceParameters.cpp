//
// Created by spacer on 3/1/23.
//
#include <DistanceParameters.h>

IO::SDK::Constraints::Parameters::DistanceParameters::DistanceParameters(const IO::SDK::Body::Body &observer,
                                                                         const IO::SDK::Body::Body &target, const IO::SDK::Constraints::Constraint &constraint,
                                                                         const IO::SDK::AberrationsEnum aberration, const double value,
                                                                         const IO::SDK::Time::TimeSpan &initialStepSize) :
        m_observer{observer}, m_target{target},
        m_constraint{constraint},
        m_aberration{aberration}, m_value{value}, m_initialStepSize{initialStepSize}
{

}
